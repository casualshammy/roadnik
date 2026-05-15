using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data.Discord;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Toolkit;
using System.Buffers;
using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Modules.DiscordIntegration;

internal class DiscordIntegrationImpl : IDiscordIntegration, IAppModule<IDiscordIntegration>
{
  private sealed record PresenceData(
    int SessionId,
    double Lat,
    double Lng,
    string RoomId,
    float? Speed,
    int? Hrm);

  private sealed record TimestampedPresenceData(
    PresenceData? Data,
    DateTimeOffset Timestamp)
  {
    public static TimestampedPresenceData Default { get; } = new(null, default);
  }

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
  private const int DISCORD_IDENTIFY_CAPABILITIES = 65;
  private const int DISCORD_CLOSE_CODE_AUTH_FAILED = 4004;

  private readonly BehaviorSubject<PresenceData?> p_presenceDataSubj = new(null);
  // private readonly BehaviorSubject<bool> p_isBroadcastingSubj = new(false);
  private readonly ILog p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;
  // private long? p_broadcastStartTs;

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
        var isEnabled = _storage.GetValueOrDefault<bool>(PREF_DISCORD_ENABLED);
        var encToken = _storage.GetValueOrDefault<string>(PREF_DISCORD_TOKEN);
        var appId = _storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
        return (Enabled: isEnabled, EncToken: encToken, AppId: appId);
      })
      .CombineLatest(p_presenceDataSubj)
      .DistinctUntilChanged(_ =>
      {
        var (data, presenceData) = _;
        return HashCode.Combine(data.Enabled, data.EncToken, presenceData?.SessionId);
      })
      .Throttle(TimeSpan.FromSeconds(1))
      .HotAlive(_lifetime, null, (_e, _life) =>
      {
        var (data, presenceData) = _e;
        var token = TryLoadToken();
        var isEnabled = data.Enabled;
        var active = isEnabled && token != null && presenceData != null;

        if (!active)
          return;

        _ = Task.Run(() => RunGatewayLoopAsync(_life, token!), _life.Token);
      });
  }

  public void RevokeAuth()
  {
    try
    {
      p_storage.RemoveValue(PREF_DISCORD_TOKEN);
      p_storage.SetValue(PREF_DISCORD_ENABLED, false, PrefsStorageJsonCtx.Default.Boolean);
      p_log.Info($"Discord auth revoked");
    }
    catch (Exception ex)
    {
      p_log.Error($"Error revoking Discord auth", ex);
    }
  }

  public void UpdatePresence(
    int _sessionId,
    double _lat,
    double _lng,
    string _roomId,
    float? _speed,
    int? _hrm)
  {
    // p_broadcastStartTs ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    // p_isBroadcastingSubj.OnNext(true);
    p_presenceDataSubj.OnNext(new PresenceData(_sessionId, _lat, _lng, _roomId, _speed, _hrm));
  }

  public void ClearPresence()
  {
    p_presenceDataSubj.OnNext(null);
    // p_isBroadcastingSubj.OnNext(false);
    // p_broadcastStartTs = null;
  }

  private async Task RunGatewayLoopAsync(IReadOnlyLifetime _life, string _token)
  {
    while (!_life.Token.IsCancellationRequested)
    {
      try
      {
        await RunGatewaySessionAsync(_life, _token);
      }
      catch (AccessViolationException ex)
      {
        p_log.Error($"Discord gateway: authentication rejected — invalidating token. {ex.Message}");
        RevokeAuth();
        break;
      }
      catch (OperationCanceledException) when (_life.IsCancellationRequested)
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

  private async Task RunGatewaySessionAsync(IReadOnlyLifetime _life, string _token)
  {
    using var ws = new ClientWebSocket();

    p_log.Info($"Connecting to Discord gateway...");
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
      cts.CancelAfter(TimeSpan.FromSeconds(10));
      await ws.ConnectAsync(new Uri(DISCORD_GATEWAY_URL), cts.Token);
    }
    p_log.Info($"Discord gateway connected");

    // outgoing message channel to serialise sends
    var sendChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    void EnqueueJson(string _json) => sendChannel.Writer.TryWrite(_json);
    void Enqueue<T>(T _msg, JsonTypeInfo<T> _typeInfo) => EnqueueJson(JsonSerializer.Serialize(_msg, _typeInfo));

    using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);

    var sendTask = Task.Run(async () =>
    {
      try
      {
        await foreach (var msg in sendChannel.Reader.ReadAllAsync(sendCts.Token))
        {
          var bytes = Encoding.UTF8.GetBytes(msg);
          await ws.SendAsync(bytes, WebSocketMessageType.Text, true, sendCts.Token);
        }
      }
      catch (OperationCanceledException) { }
      catch (Exception ex)
      {
        p_log.Error($"Discord gateway send task failed: {ex.Message}");
        ws.Abort();
      }
    }, sendCts.Token);

    // Receive HELLO
    var helloMsg = await ReceiveGatewayMessageAsync(ws, _life.Token);
    if (helloMsg == null || helloMsg.Op != DiscordGatewayOpCode.Hello || helloMsg.D == null)
      throw new InvalidOperationException($"Expected HELLO, got op={helloMsg?.Op}");

    var helloData = JsonSerializer.Deserialize(helloMsg.D.Value, DiscordJsonCtx.Default.DiscordHelloData)
      ?? throw new InvalidOperationException("Missing HELLO payload");

    var heartbeatInterval = helloData.HeartbeatInterval;
    p_log.Info($"Discord gateway: heartbeat interval = {heartbeatInterval}ms");

    var lastSeq = (int?)null;
    var heartbeatAckReceived = 1;

    // Start heartbeat
    _ = Task.Run(async () =>
    {
      // initial jitter
      await Task.Delay((int)(heartbeatInterval * Random.Shared.NextDouble()), _life.Token);

      while (!_life.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
      {
        if (Interlocked.Exchange(ref heartbeatAckReceived, 0) == 0)
        {
          p_log.Warn($"Discord gateway: heartbeat ACK not received, closing connection");
          ws.Abort();
          return;
        }

        Enqueue(new DiscordHeartbeatMsg(DiscordGatewayOpCode.Heartbeat, lastSeq), DiscordJsonCtx.Default.DiscordHeartbeatMsg);
        await Task.Delay(heartbeatInterval, _life.Token);
      }
    }, _life.Token);

    // Send IDENTIFY
    p_log.Info($"Discord gateway: sending IDENTIFY (token length: {_token.Length})");
    Enqueue(
      new DiscordIdentifyMsg(
        DiscordGatewayOpCode.Identify,
        new DiscordIdentifyData(
          _token,
          Capabilities: DISCORD_IDENTIFY_CAPABILITIES,
          Compress: false,
          new DiscordIdentifyProperties("Windows", "Discord Client", "ktor"))),
      DiscordJsonCtx.Default.DiscordIdentifyMsg);

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

        if (closeCode == DISCORD_CLOSE_CODE_AUTH_FAILED)
          throw new AccessViolationException($"Discord gateway authentication failed (close code: {closeCode}, reason: {closeDesc ?? "none"})");

        break;
      }

      p_log.Info($"Discord gateway: received op={msg.Op} t={msg.T ?? "-"} s={msg.S?.ToString() ?? "-"}");

      if (msg.S != null)
        lastSeq = msg.S;

      if (msg.Op == DiscordGatewayOpCode.Dispatch && msg.T == "READY")
      {
        ready = true;
        p_log.Info($"Discord gateway: READY received");
      }
      else if (msg.Op == DiscordGatewayOpCode.HeartbeatAck)
      {
        Interlocked.Exchange(ref heartbeatAckReceived, 1);
      }
    }

    if (!ready)
      throw new InvalidOperationException("Discord gateway: did not receive READY");

    // Subscribe to presence changes
    p_presenceDataSubj
      .DistinctUntilChanged()
      .Scan(TimestampedPresenceData.Default, (_acc, _presence) =>
      {
        if (_presence?.SessionId != _acc.Data?.SessionId)
          return new TimestampedPresenceData(_presence, DateTimeOffset.UtcNow);
        else
          return _acc with { Data = _presence };
      })
      .SelectAsync(async (_d, _ct) =>
      {
        try
        {
          using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_ct, timeoutCts.Token);
          await SendPresenceUpdateAsync(_d.Data, _d.Timestamp, EnqueueJson, linkedCts.Token);
        }
        catch (OperationCanceledException) when (_ct.IsCancellationRequested)
        { }
        catch (Exception ex)
        {
          p_log.Error($"Error sending presence update to Discord: {ex}");
        }
        return Unit.Default;
      })
      .Subscribe(_life);

    // Main receive loop
    while (!_life.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
    {
      var msg = await ReceiveGatewayMessageAsync(ws, _life.Token);
      if (msg == null)
        break;

      if (msg.S != null)
        lastSeq = msg.S;

      if (msg.Op == DiscordGatewayOpCode.HeartbeatAck)
        Interlocked.Exchange(ref heartbeatAckReceived, 1);
      else if (msg.Op == DiscordGatewayOpCode.Heartbeat)
        Enqueue(new DiscordHeartbeatMsg(DiscordGatewayOpCode.Heartbeat, lastSeq), DiscordJsonCtx.Default.DiscordHeartbeatMsg);
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
    DateTimeOffset _sessionStart,
    Action<string> _enqueue,
    CancellationToken _ct)
  {
    if (_data == null)
    {
      _enqueue(JsonSerializer.Serialize(
        new DiscordPresenceUpdateMsg(
          DiscordGatewayOpCode.PresenceUpdate,
          new DiscordPresenceUpdateData(null, [], "online", false)),
        DiscordJsonCtx.Default.DiscordPresenceUpdateMsg));

      return;
    }

    var (lat, lng, roomId, speed, hrm) = (_data.Lat, _data.Lng, _data.RoomId, _data.Speed, _data.Hrm);
    var locationName = await LocationToolkit.TryGetApproximateLocationNameAsync(p_httpClientProvider, lat, lng, _ct);
    var trackingUrl = $"{ROADNIK_APP_ADDRESS}/r/?id={roomId}";
    var customStatus = p_storage.GetValueOrDefault<string>(PREF_DISCORD_STATUS);

    var detailsParts = new List<string>();
    if (locationName != null)
      detailsParts.Add($"📍 {locationName}");
    else
      detailsParts.Add("📍 Sharing location");

    if (!customStatus.IsNullOrWhiteSpace())
      detailsParts.Add(customStatus!);

    var stateParts = new List<string>();
    if (speed != null && speed >= 0.5f)
      stateParts.Add($"🚀 {(int)Math.Round(speed.Value * 3.6f)} km/h");
    if (hrm != null && hrm > 0)
      stateParts.Add($"❤️ {hrm} bpm");

    _enqueue(JsonSerializer.Serialize(
      new DiscordPresenceUpdateMsg(
        DiscordGatewayOpCode.PresenceUpdate,
        new DiscordPresenceUpdateData(
          null,
          [new DiscordActivity(
            "Roadnik",
            Type: DiscordActivityType.Competing,
            Details: string.Join(" · ", detailsParts),
            State: stateParts.Count > 0 ? string.Join(" · ", stateParts) : null,
            Timestamps: new DiscordActivityTimestamps(_sessionStart.ToUnixTimeMilliseconds()),
            ApplicationId: DISCORD_APPLICATION_ID,
            Buttons: ["Open Roadnik"],
            Metadata: new DiscordActivityMetadata([trackingUrl]))],
          "online",
          false)),
      DiscordJsonCtx.Default.DiscordPresenceUpdateMsg));
  }

  private async Task<DiscordGatewayMessage?> ReceiveGatewayMessageAsync(
    ClientWebSocket _ws,
    CancellationToken _ct)
  {
    var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
    try
    {
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

        ms.Write(buffer, 0, result.Count);
      }
      while (!result.EndOfMessage);

      ms.Position = 0;

      var data = await JsonSerializer.DeserializeAsync(ms, DiscordJsonCtx.Default.DiscordGatewayMessage, _ct);
      return data;
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  private string? TryLoadToken()
  {
    var appId = p_storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
    var encToken = p_storage.GetValueOrDefault<string>(PREF_DISCORD_TOKEN);

    if (encToken.IsNullOrEmpty())
      return null;

    try
    {
      using var aes = new Ax.Fw.Crypto.AesWithGcm(appId.ToByteArray());
      var tokenDataSpan = aes.Decrypt(Convert.FromBase64String(encToken));
      var tokenData = JsonSerializer.Deserialize(tokenDataSpan, DiscordJsonCtx.Default.String);
      return tokenData;
    }
    catch (Exception ex)
    {
      p_log.Warn($"Error decrypting Discord token from preferences: {ex}");
      return null;
    }
  }

}
