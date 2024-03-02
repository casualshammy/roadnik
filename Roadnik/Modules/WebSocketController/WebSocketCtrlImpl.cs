using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Roadnik.Interfaces;
using Roadnik.Modules.WebSocketController.Parts;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ILogger = Ax.Fw.SharedTypes.Interfaces.ILogger;

namespace Roadnik.Modules.WebSocketController;

public class WebSocketCtrlImpl : IWebSocketCtrl, IAppModule<IWebSocketCtrl>
{
  public static IWebSocketCtrl ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILogger _log,
      ISettingsController _settingsController,
      IReadOnlyLifetime _lifetime) => new WebSocketCtrlImpl(_log, _settingsController, _lifetime));
  }

  private readonly ILogger p_log;
  private readonly ISettingsController p_settingsCtrl;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly ConcurrentDictionary<int, WebSocketSession> p_sessions = new();
  private readonly Subject<object> p_incomingMsgs = new();
  private readonly Subject<WebSocketSession> p_clientConnectedFlow = new();
  private int p_sessionsCount = 0;

  public WebSocketCtrlImpl(
    ILogger _log,
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime)
  {
    p_log = _log["ws"];
    p_settingsCtrl = _settingsController;
    p_lifetime = _lifetime;

    WsHelper.RegisterMsType("ws-msg-hello", typeof(WsMsgHello));
    WsHelper.RegisterMsType("ws-msg-path-wiped", typeof(WsMsgPathWiped));
    WsHelper.RegisterMsType("ws-msg-room-points-updated", typeof(WsMsgRoomPointsUpdated));
    WsHelper.RegisterMsType("ws-msg-data-updated", typeof(WsMsgUpdateAvailable));
    WsHelper.RegisterSerializationContext(WebSocketJsonCtx.Default);
  }

  public IObservable<object> IncomingMessages => p_incomingMsgs;
  public IObservable<WebSocketSession> ClientConnected => p_clientConnectedFlow;

  public async Task<bool> AcceptSocketAsync(
    WebSocket _webSocket,
    string _roomId,
    int _maxPointsInRoom)
  {
    if (_webSocket.State != WebSocketState.Open)
      return false;

    var session = new WebSocketSession(_roomId, _webSocket);
    using var semaphore = new SemaphoreSlim(0, 1);
    using var scheduler = new EventLoopScheduler();

    scheduler.ScheduleAsync(async (_s, _ct) => await CreateNewLoopAsync(session, semaphore, _maxPointsInRoom));

    try
    {
      await semaphore.WaitAsync(p_lifetime.Token);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
      p_log.Error($"Waiting for loop is failed", ex);
    }

    return true;
  }

  public async Task<int> BroadcastMsgAsync<T>(T _msg, CancellationToken _ct) where T : notnull
  {
    var buffer = WsHelper.CreateWsMessage(_msg);

    var totalSent = 0;
    foreach (var (index, session) in p_sessions)
    {
      try
      {
        if (session.Socket.State == WebSocketState.Open)
        {
          await session.Socket.SendAsync(buffer, WebSocketMessageType.Text, true, _ct);
          totalSent++;
        }
      }
      catch (Exception ex)
      {
        p_log.Warn($"Can't send msg to socket '{index}': {ex}");
      }
    }
    return totalSent;
  }

  public async Task SendMsgAsync<T>(WebSocketSession _session, T _msg, CancellationToken _ct) where T : notnull
  {
    var buffer = WsHelper.CreateWsMessage(_msg);

    try
    {
      if (_session.Socket.State == WebSocketState.Open)
        await _session.Socket.SendAsync(buffer, WebSocketMessageType.Text, true, _ct);
    }
    catch (Exception ex)
    {
      p_log.Warn($"Can't send msg to socket: {ex}");
    }
  }

  public async Task SendMsgByRoomIdAsync<T>(string _roomId, T _msg, CancellationToken _ct) where T : notnull
  {
    foreach (var (_, session) in p_sessions)
    {
      if (session.RoomId != _roomId)
        continue;

      await SendMsgAsync(session, _msg, _ct);
    }
  }

  private async Task CreateNewLoopAsync(
    WebSocketSession _session,
    SemaphoreSlim _completeSignal,
    int _maxPointsInRoom)
  {
    var session = _session;
    var sessionIndex = Interlocked.Increment(ref p_sessionsCount);
    p_sessions.TryAdd(sessionIndex, session);

    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    WebSocketReceiveResult? receiveResult = null;

    var buffer = ArrayPool<byte>.Shared.Rent(100 * 1024);

    try
    {
      var helloMsgData = new WsMsgHello(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _maxPointsInRoom);
      var helloMsg = WsHelper.CreateWsMessage(helloMsgData);

      await session.Socket.SendAsync(helloMsg, WebSocketMessageType.Text, true, cts.Token);
      p_clientConnectedFlow.OnNext(session);

      receiveResult = await session.Socket.ReceiveAsync(buffer, cts.Token);

      while (!receiveResult.CloseStatus.HasValue && !cts.IsCancellationRequested)
      {
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
          var msg = WsHelper.ParseWsMessage(buffer, receiveResult.Count);
          if (msg != null)
            p_incomingMsgs.OnNext(msg);
        }
        finally
        {
          receiveResult = await session.Socket.ReceiveAsync(buffer, cts.Token);
        }
      }
    }
    catch (OperationCanceledException)
    {
      // don't care
    }
    catch (WebSocketException wsEx) when (wsEx.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
    {
      // don't care
    }
    catch (Exception ex)
    {
      p_log.Error($"Error occured in loop: {ex}");
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer, false);
      p_sessions.TryRemove(sessionIndex, out _);
    }

    try
    {
      if (session.Socket.State == WebSocketState.Open)
      {
        if (receiveResult is not null)
          await session.Socket.CloseAsync(receiveResult.CloseStatus ?? WebSocketCloseStatus.NormalClosure, receiveResult.CloseStatusDescription, CancellationToken.None);
        else
          await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, $"Closed normally (session: '{sessionIndex}')", CancellationToken.None);
      }
    }
    catch (Exception ex)
    {
      p_log.Error($"Error occured while closing websocket: {ex}");
    }

    _completeSignal.Release();
  }

}
