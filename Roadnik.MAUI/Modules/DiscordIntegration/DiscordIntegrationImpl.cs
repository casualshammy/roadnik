using Android.Gms.Auth.Api.SignIn.Internal;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data.Discord;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Modules.DiscordIntegration;

internal class DiscordIntegrationImpl : IDiscordIntegration, IAppModule<IDiscordIntegration>
{
  private record PresenceData(
    double Lat,
    double Lng,
    string RoomId);

  /// <summary>Thrown when Discord rejects our token (close 4004). Loop must NOT reconnect.</summary>
  private sealed class DiscordAuthFailedException(int _closeCode, string? _reason)
    : Exception($"Discord gateway authentication failed (close code: {_closeCode}, reason: {_reason ?? "none"})")
  { }

  public static IDiscordIntegration ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      ILog _log,
      IPreferencesStorage _storage,
      IHttpClientProvider _httpClientProvider)
      => new DiscordIntegrationImpl(_lifetime, _log["discord-integration"], _storage, _httpClientProvider));
  }

  private const string DISCORD_API_BASE = "https://discord.com/api/v10";
  private const string DISCORD_GATEWAY_URL = "wss://gateway.discord.gg/?v=10&encoding=json";
  private const int GATEWAY_OP_DISPATCH = 0;
  private const int GATEWAY_OP_HEARTBEAT = 1;
  private const int GATEWAY_OP_IDENTIFY = 2;
  private const int GATEWAY_OP_PRESENCE_UPDATE = 3;
  private const int GATEWAY_OP_HELLO = 10;
  private const int GATEWAY_OP_HEARTBEAT_ACK = 11;

  private readonly ReplaySubject<bool> p_isAuthenticatedFlow = new(1);
  private readonly ReplaySubject<bool> p_isEnabledFlow = new(1);
  private readonly ReplaySubject<string?> p_usernameFlow = new(1);
  private readonly BehaviorSubject<PresenceData?> p_presenceDataSubj = new(null);
  private readonly BehaviorSubject<bool> p_isBroadcastingSubj = new(false);
  private readonly ILog p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;
  private string? p_lastLocationName;
  private (double Lat, double Lng) p_lastGeocodedPoint;

  private DiscordIntegrationImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider)
  {
    p_log = _log;
    p_storage = _storage;
    p_httpClientProvider = _httpClientProvider;

    p_storage.PreferencesChanged
      .Select(_ =>
      {
        var savedUsername = _storage.GetValueOrDefault<string>(PREF_DISCORD_USERNAME);
        var isEnabled = _storage.GetValueOrDefault<bool>(PREF_DISCORD_ENABLED);
        var encToken = _storage.GetValueOrDefault<string>(PREF_DISCORD_TOKEN);
        var appId = _storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
        return (UserName: savedUsername, Enabled: isEnabled, EncToken: encToken, AppId: appId);
      })
      .DistinctUntilChanged(_ => HashCode.Combine(_.UserName, _.Enabled, _.EncToken))
      .Subscribe(_ =>
      {
        var isAuthenticated = !_.EncToken.IsNullOrWhiteSpace();
        var tokenData = TryLoadTokenData();

        p_isAuthenticatedFlow.OnNext(isAuthenticated);
        p_usernameFlow.OnNext(isAuthenticated ? (_.UserName ?? tokenData?.Username) : null);
        p_isEnabledFlow.OnNext(isAuthenticated && _.Enabled);
      }, _lifetime);

    // Maintain the Discord Gateway connection only while enabled + authenticated + broadcasting
    p_isEnabledFlow
      .CombineLatest(
        p_isAuthenticatedFlow, 
        p_isBroadcastingSubj, 
        (_enabled, _auth, _broadcasting) => _enabled && _auth && _broadcasting)
      .DistinctUntilChanged()
      .HotAlive(_lifetime, null, (_active, _life) =>
      {
        if (!_active)
          return;

        _ = Task.Run(() => RunGatewayLoopAsync(_life), _life.Token);
      });
  }

  public void RevokeAuth()
  {
    try
    {
      p_storage.RemoveValue(PREF_DISCORD_TOKEN);
      p_storage.RemoveValue(PREF_DISCORD_USERNAME);
      p_storage.SetValue(PREF_DISCORD_ENABLED, false);
      p_log.Info($"Discord auth revoked");
    }
    catch (Exception ex)
    {
      p_log.Error($"Error revoking Discord auth", ex);
    }
  }

  public void UpdatePresence(double _lat, double _lng, string _roomId)
  {
    p_isBroadcastingSubj.OnNext(true);
    p_presenceDataSubj.OnNext(new PresenceData(_lat, _lng, _roomId));
  }

  public void ClearPresence()
  {
    p_presenceDataSubj.OnNext(null);
    p_isBroadcastingSubj.OnNext(false);
  }

  public async Task<string?> FetchUsernameAsync(string _token, CancellationToken _ct)
  {
    using var req = new HttpRequestMessage(HttpMethod.Get, $"{DISCORD_API_BASE}/users/@me");
    // Discord user session tokens are passed as a raw header value (no "Bearer" prefix)
    req.Headers.TryAddWithoutValidation("Authorization", _token);

    using var res = await p_httpClientProvider.Value.SendAsync(req, _ct);
    if (!res.IsSuccessStatusCode)
      return null;

    var json = await res.Content.ReadAsStringAsync(_ct);
    var node = JsonNode.Parse(json);
    return node?["username"]?.GetValue<string>();
  }

  private async Task RunGatewayLoopAsync(IReadOnlyLifetime _life)
  {
    while (!_life.Token.IsCancellationRequested)
    {
      try
      {
        await RunGatewaySessionAsync(_life);
      }
      catch (DiscordAuthFailedException ex)
      {
        p_log.Error($"Discord gateway: authentication rejected — invalidating token. {ex.Message}");
        RevokeAuth();
        break;
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        p_log.Error($"Discord gateway session error, reconnecting in 5s...", ex);

        try
        {
          await Task.Delay(TimeSpan.FromSeconds(5), _life.Token);
        }
        catch (OperationCanceledException)
        {
          break;
        }
      }
    }

    p_log.Info($"Discord gateway loop stopped");
  }

  private async Task RunGatewaySessionAsync(IReadOnlyLifetime _life)
  {
    var tokenData = TryLoadTokenData();
    if (tokenData == null)
    {
      p_log.Warn($"Discord gateway: no valid token, aborting session");
      p_isAuthenticatedFlow.OnNext(false);
      p_isEnabledFlow.OnNext(false);
      return;
    }

    using var ws = new ClientWebSocket();
    // ws.Options.SetRequestHeader("User-Agent", "Roadnik/1.0 (roadnik.app)");

    p_log.Info($"Connecting to Discord gateway...");
    await ws.ConnectAsync(new Uri(DISCORD_GATEWAY_URL), _life.Token);
    p_log.Info($"Discord gateway connected");

    // outgoing message channel to serialise sends
    var sendChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);

    var sendTask = Task.Run(async () =>
    {
      await foreach (var msg in sendChannel.Reader.ReadAllAsync(sendCts.Token))
      {
        var bytes = Encoding.UTF8.GetBytes(msg);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, sendCts.Token);
      }
    }, sendCts.Token);

    void EnqueueSend(object _payload)
    {
      var json = JsonSerializer.Serialize(_payload);
      sendChannel.Writer.TryWrite(json);
    }

    // Receive HELLO
    var helloMsg = await ReceiveGatewayMessageAsync(ws, _life.Token);
    if (helloMsg == null || helloMsg.Value.Op != GATEWAY_OP_HELLO)
      throw new InvalidOperationException($"Expected HELLO, got op={helloMsg?.Op}");

    var heartbeatInterval = helloMsg.Value.D!["heartbeat_interval"]!.GetValue<int>();
    p_log.Info($"Discord gateway: heartbeat interval = {heartbeatInterval}ms");

    var lastSeq = (int?)null;
    var heartbeatAckReceived = true;

    // Start heartbeat
    _ = Task.Run(async () =>
    {
      // initial jitter
      await Task.Delay((int)(heartbeatInterval * Random.Shared.NextDouble()), _life.Token);
      while (!_life.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
      {
        if (!heartbeatAckReceived)
        {
          p_log.Warn($"Discord gateway: heartbeat ACK not received, closing connection");
          ws.Abort();
          return;
        }

        heartbeatAckReceived = false;
        EnqueueSend(new { op = GATEWAY_OP_HEARTBEAT, d = lastSeq });
        await Task.Delay(heartbeatInterval, _life.Token);
      }
    }, _life.Token);

    p_log.Info($"Discord gateway: sending IDENTIFY (token length: {tokenData.Token.Length})");
    // Send IDENTIFY — masquerade as Discord desktop client (required for user session tokens)
    EnqueueSend(new
    {
      op = GATEWAY_OP_IDENTIFY,
      d = new
      {
        tokenData.Token,
        capabilities = 65,
        compress = false,
        properties = new { os = "Windows", browser = "Discord Client", device = "ktor" },
      }
    });

    // Wait for READY — log every received message for diagnostics
    var ready = false;
    while (!ready && !_life.Token.IsCancellationRequested)
    {
      var msg = await ReceiveGatewayMessageAsync(ws, _life.Token);
      if (msg == null)
      {
        // WebSocket was closed — check Discord close code
        var closeCode = (int?)ws.CloseStatus;
        var closeDesc = ws.CloseStatusDescription;
        p_log.Warn($"Discord gateway: connection closed while waiting for READY (code: {closeCode}, reason: {closeDesc ?? "none"})");

        if (closeCode == 4004)
          throw new DiscordAuthFailedException(4004, closeDesc);

        break;
      }

      p_log.Info($"Discord gateway: received op={msg.Value.Op} t={msg.Value.T ?? "-"} s={msg.Value.S?.ToString() ?? "-"}");

      if (msg.Value.S != null) lastSeq = msg.Value.S;

      if (msg.Value.Op == GATEWAY_OP_DISPATCH && msg.Value.T == "READY")
      {
        ready = true;
        p_log.Info($"Discord gateway: READY received");
      }
      else if (msg.Value.Op == GATEWAY_OP_HEARTBEAT_ACK)
      {
        heartbeatAckReceived = true;
      }
    }

    if (!ready)
      throw new InvalidOperationException("Discord gateway: did not receive READY");

    // Send current presence immediately on connect
    await SendPresenceUpdateAsync(p_presenceDataSubj.Value, EnqueueSend, _life.Token);

    // Subscribe to future presence changes
    using var presenceSub = p_presenceDataSubj
      .Skip(1)
      .DistinctUntilChanged()
      .Subscribe(data =>
      {
        _ = SendPresenceUpdateAsync(data, EnqueueSend, _life.Token);
      });

    // Main receive loop
    while (!_life.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
    {
      var msg = await ReceiveGatewayMessageAsync(ws, _life.Token);
      if (msg == null) break;

      if (msg.Value.S != null) lastSeq = msg.Value.S;

      if (msg.Value.Op == GATEWAY_OP_HEARTBEAT_ACK)
        heartbeatAckReceived = true;
      else if (msg.Value.Op == GATEWAY_OP_HEARTBEAT)
        EnqueueSend(new { op = GATEWAY_OP_HEARTBEAT, d = lastSeq }); // server-requested heartbeat
    }

    sendChannel.Writer.TryComplete();
    sendCts.Cancel();

    try { await sendTask; }
    catch (OperationCanceledException) { }

    if (ws.State == WebSocketState.Open)
    {
      try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None); }
      catch { }
    }

    p_log.Info($"Discord gateway session ended (ws state: {ws.State})");
  }

  private async Task SendPresenceUpdateAsync(
    PresenceData? _data,
    Action<object> _enqueue,
    CancellationToken _ct)
  {
    if (_data == null)
    {
      _enqueue(new
      {
        op = GATEWAY_OP_PRESENCE_UPDATE,
        d = new { since = (long?)null, activities = Array.Empty<object>(), status = "online", afk = false }
      });
      return;
    }

    var (lat, lng, roomId) = (_data.Lat, _data.Lng, _data.RoomId);
    var locationName = await GetApproximateLocationNameAsync(lat, lng, _ct);
    var trackingUrl = $"{ROADNIK_APP_ADDRESS}/r/?id={roomId}";
    var startTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var customStatus = p_storage.GetValueOrDefault<string>(PREF_DISCORD_STATUS);

    var stateParts = new List<string>();

    if (locationName != null)
      stateParts.Add($"📍 {locationName}");
    else
      stateParts.Add("📍 Sharing location");

    if (!customStatus.IsNullOrWhiteSpace())
      stateParts.Add(customStatus);

    _enqueue(new
    {
      op = GATEWAY_OP_PRESENCE_UPDATE,
      d = new
      {
        since = (long?)null,
        activities = new[]
        {
          new
          {
            name = "Roadnik",
            type = 5, // Watching
            state = string.Join(" · ", stateParts),
            details = trackingUrl,
            timestamps = new { start = startTs },
          }
        },
        status = "online",
        afk = false,
      }
    });
  }

  private async Task<(int Op, JsonNode? D, int? S, string? T)?> ReceiveGatewayMessageAsync(
    ClientWebSocket _ws,
    CancellationToken _ct)
  {
    var buffer = new ArraySegment<byte>(new byte[65536]);
    using var ms = new MemoryStream();

    WebSocketReceiveResult result;
    do
    {
      result = await _ws.ReceiveAsync(buffer, _ct);
      if (result.MessageType == WebSocketMessageType.Close)
      {
        p_log.Warn($"Discord gateway: server closed connection (code: {result.CloseStatus}, reason: {result.CloseStatusDescription})");
        return null;
      }

      ms.Write(buffer.Array!, buffer.Offset, result.Count);
    }
    while (!result.EndOfMessage);

    ms.Seek(0, SeekOrigin.Begin);
    var node = JsonNode.Parse(ms);
    if (node == null) return null;

    var op = node["op"]?.GetValue<int>() ?? -1;
    var s = node["s"]?.GetValue<int?>();
    var t = node["t"]?.GetValue<string?>();
    var d = node["d"];

    return (op, d, s, t);
  }

  private DiscordTokenData? TryLoadTokenData()
  {
    var appId = p_storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
    var encToken = p_storage.GetValueOrDefault<string>(PREF_DISCORD_TOKEN);
    
    if (encToken.IsNullOrEmpty())
      return null;

    try
    {
      using var aes = new Ax.Fw.Crypto.AesWithGcm(appId.ToByteArray());
      var tokenDataSpan = aes.Decrypt(Convert.FromBase64String(encToken));
      var tokenData = JsonSerializer.Deserialize(tokenDataSpan, DiscordJsonCtx.Default.DiscordTokenData);
      return tokenData;
    }
    catch (Exception ex)
    {
      p_log.Warn($"Error decrypting Discord token from preferences: {ex}");
      return null;
    }
  }

  private async Task<string?> GetApproximateLocationNameAsync(
    double _lat,
    double _lng,
    CancellationToken _ct)
  {
    // Only re-geocode if moved more than ~1 km
    if (p_lastLocationName != null)
    {
      var dlat = (_lat - p_lastGeocodedPoint.Lat) * 111_000;
      var dlng = (_lng - p_lastGeocodedPoint.Lng) * 111_000 * Math.Cos(_lat * Math.PI / 180);
      if (Math.Sqrt(dlat * dlat + dlng * dlng) < 1000)
        return p_lastLocationName;
    }

    try
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
      cts.CancelAfter(TimeSpan.FromSeconds(5));

      var placemarks = await Geocoding.Default.GetPlacemarksAsync(_lat, _lng).WaitAsync(cts.Token);
      var pm = placemarks?.FirstOrDefault();
      if (pm == null) return null;

      var name = pm.Locality
        ?? pm.SubAdminArea
        ?? pm.AdminArea
        ?? pm.CountryName;

      p_lastLocationName = name;
      p_lastGeocodedPoint = (_lat, _lng);

      return name;
    }
    catch (Exception ex)
    {
      p_log.Warn($"Reverse geocoding failed: {ex.Message}");
      return p_lastLocationName;
    }
  }

}
