using Android.App;
using Android.Content;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data.Discord;
using Roadnik.MAUI.Data.DiscordIntegration;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Toolkit;
using System.Buffers;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using L = Roadnik.MAUI.Resources.Strings.AppResources;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Modules.DiscordIntegration.Parts;

internal sealed class DiscordWebsocketClient
{
  private const string DISCORD_GATEWAY_URL = "wss://gateway.discord.gg/?v=10&encoding=json";
  private const int DISCORD_IDENTIFY_CAPABILITIES = 65;
  private const int DISCORD_CLOSE_CODE_AUTH_FAILED = 4004;

  private readonly IReadOnlyLifetime p_lifetime;
  private readonly ILog p_log;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly IPreferencesStorage p_preferencesStorage;
  private readonly IObservable<PresenceData?> p_presenceFlow;
  private readonly string p_token;

  public DiscordWebsocketClient(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IHttpClientProvider _httpClientProvider,
    IPreferencesStorage _preferencesStorage,
    IObservable<PresenceData?> _presenceFlow,
    string _token)
  {
    p_lifetime = _lifetime;
    p_log = _log;
    p_httpClientProvider = _httpClientProvider;
    p_preferencesStorage = _preferencesStorage;
    p_presenceFlow = _presenceFlow;
    p_token = _token;
  }

  public void StartLoop(Action _revokeAuth)
  {
    _ = Task.Run(async () =>
    {
      p_log.Info($"Starting Discord gateway loop");

      while (!p_lifetime.IsCancellationRequested)
      {
        try
        {
          using var life = p_lifetime.GetChildLifetime()
            ?? throw new OperationCanceledException("Failed to create child lifetime");

          await RunGatewaySessionAsync(life, p_token);
        }
        catch (AccessViolationException ex)
        {
          p_log.Error($"Discord gateway: authentication rejected — invalidating token. {ex.Message}");
          _revokeAuth();
          ShowAuthRevocationNotification();
          break;
        }
        catch (OperationCanceledException) when (p_lifetime.IsCancellationRequested)
        {
          break;
        }
        catch (Exception ex)
        {
          p_log.Error($"Discord gateway session error, reconnecting in 5s...", ex);

          try
          {
            await Task.Delay(TimeSpan.FromSeconds(5), p_lifetime.Token);
          }
          catch (OperationCanceledException)
          {
            break;
          }
        }
      }

      p_log.Info($"Discord gateway loop ended");
    });
  }

  private async Task RunGatewaySessionAsync(
    ILifetime _life,
    string _token)
  {
    using var ws = new ClientWebSocket();

    p_log.Info($"Connecting to Discord gateway...");
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
      cts.CancelAfter(TimeSpan.FromSeconds(10));
      await ws.ConnectAsync(new Uri(DISCORD_GATEWAY_URL), cts.Token);
    }
    p_log.Info($"Discord gateway connected");

    // setup send queue
    var sendSubj = _life.ToDisposeOnEnded(new Subject<string>());
    sendSubj
      .SelectAsync(async (_jsonMsg, _ct) =>
      {
        try
        {
          var bytes = Encoding.UTF8.GetBytes(_jsonMsg);
          await ws.SendAsync(bytes, WebSocketMessageType.Text, true, _ct);
        }
        catch (Exception ex)
        {
          p_log.Error($"Discord ws msg send task failed: {ex.Message}");
          ws.Abort();
          _life.End();
        }
      })
      .Subscribe(_life);

    void EnqueueJson(string _json) => sendSubj.OnNext(_json);
    void Enqueue<T>(T _msg, JsonTypeInfo<T> _typeInfo) => EnqueueJson(JsonSerializer.Serialize(_msg, _typeInfo));

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

    // heartbeat
    Observable
      .Timer(TimeSpan.FromMilliseconds((int)(heartbeatInterval * Random.Shared.NextDouble())))
      .Concat(Observable.Interval(TimeSpan.FromMilliseconds(heartbeatInterval)))
      .Subscribe(_ =>
      {
        if (ws.State == WebSocketState.Open)
        {
          if (Interlocked.Exchange(ref heartbeatAckReceived, 0) == 0)
          {
            p_log.Warn($"Discord gateway: heartbeat ACK not received, closing connection");
            ws.Abort();
            _life.End();
            return;
          }

          Enqueue(new DiscordHeartbeatMsg(DiscordGatewayOpCode.Heartbeat, lastSeq), DiscordJsonCtx.Default.DiscordHeartbeatMsg);
        }
      }, _life);

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
    p_presenceFlow
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
          using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
          cts.CancelAfter(TimeSpan.FromSeconds(5));
          await SendPresenceUpdateAsync(_d.Data, _d.Timestamp, EnqueueJson, cts.Token);
        }
        catch (OperationCanceledException) when (_ct.IsCancellationRequested)
        { }
        catch (Exception ex)
        {
          p_log.Error($"Error sending presence update to Discord: {ex}");
        }
      })
      .Subscribe(_life);

    // Main receive loop
    while (!_life.IsCancellationRequested && ws.State == WebSocketState.Open)
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

    if (ws.State == WebSocketState.Open)
    {
      try
      {
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
      }
      catch { }
    }

    p_log.Info($"Discord gateway session ended (ws state: {ws.State})");
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

  private async Task SendPresenceUpdateAsync(
    PresenceData? _data,
    DateTimeOffset _sessionStart,
    Action<string> _enqueue,
    CancellationToken _ct)
  {
    if (_data == null)
    {
      p_log.Info($"No location data is available, resetting presence...");

      _enqueue(JsonSerializer.Serialize(
        new DiscordPresenceUpdateMsg(
          DiscordGatewayOpCode.PresenceUpdate,
          new DiscordPresenceUpdateData(null, [], "online", false)),
        DiscordJsonCtx.Default.DiscordPresenceUpdateMsg));

      return;
    }

    p_log.Info($"Location data is available, getting approximate location...");

    var (lat, lng, roomId, speed, hrm) = (_data.Lat, _data.Lng, _data.RoomId, _data.Speed, _data.Hrm);
    var locationName = await LocationToolkit.TryGetApproximateLocationNameAsync(p_httpClientProvider, lat, lng, _ct);

    p_log.Info($"Approximate location: '{locationName}'");

    var trackingUrl = $"{ROADNIK_APP_ADDRESS}/r/?id={roomId}";
    var customStatus = p_preferencesStorage.GetValueOrDefault(PREF_DISCORD_STATUS, PrefsStorageJsonCtx.Default.String);

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

  private void ShowAuthRevocationNotification()
  {
    var context = global::Android.App.Application.Context;
    var notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;

    var channel = new NotificationChannel(NOTIFICATION_CHANNEL_INTEGRATIONS, "Integrations", NotificationImportance.Default);
    notificationManager.CreateNotificationChannel(channel);

    var notification = new Notification.Builder(context, NOTIFICATION_CHANNEL_INTEGRATIONS)
      .SetSmallIcon(Resource.Drawable.letter_r_blue)
      .SetContentTitle(L.discord_auth_revoked_notification_title)
      .SetContentText(L.discord_auth_revoked_notification_body)
      .SetAutoCancel(true)
      .Build();

    notificationManager.Notify(NOTIFICATION_ID_DISCORD_AUTH_REVOKED, notification);
  }

}