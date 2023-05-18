using Ax.Fw.Attributes;
using JustLogger.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Roadnik.Attributes;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Modules.WebSocketController.Parts;
using Roadnik.Toolkit;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Roadnik.Modules.WebSocketController;

[ExportClass(typeof(IWebSocketCtrl), Singleton: true)]
public class WebSocketCtrlImpl : IWebSocketCtrl
{
  private readonly ILogger p_log;
  private readonly ConcurrentDictionary<int, WebSocketSession> p_sessions = new();
  private readonly Subject<object> p_incomingMsgs = new();
  private readonly Subject<WebSocketSession> p_clientConnectedFlow = new();
  private int p_sessionsCount = 0;

  public WebSocketCtrlImpl(
      ILogger _log)
  {
    p_log = _log["ws"];
  }

  public IObservable<object> IncomingMessages => p_incomingMsgs;
  public IObservable<WebSocketSession> ClientConnected => p_clientConnectedFlow;

  public async Task<bool> AcceptSocket(string _roomId, WebSocket _webSocket)
  {
    if (_webSocket.State != WebSocketState.Open)
      return false;

    var session = new WebSocketSession(_roomId, _webSocket);
    var mre = new ManualResetEvent(false);

    await Task.Factory.StartNew(async () => await CreateNewLoopAsync(session, mre), TaskCreationOptions.LongRunning);
    mre.WaitOne();
    return true;
  }

  public async Task<int> BroadcastMsgAsync<T>(T _msg, CancellationToken _ct) where T : notnull
  {
    var attr = GetAttribute<WebSocketMsgAttribute>(typeof(T));
    if (attr is null)
      throw new FormatException($"Data object must have '{nameof(WebSocketMsgAttribute)}' attribute!");

    var baseMsg = new WsBaseMsg(attr.Type, JToken.FromObject(_msg));
    var json = JsonConvert.SerializeObject(baseMsg);
    var buffer = Encoding.UTF8.GetBytes(json);

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
    var attr = GetAttribute<WebSocketMsgAttribute>(typeof(T));
    if (attr is null)
      throw new FormatException($"Data object must have '{nameof(WebSocketMsgAttribute)}' attribute!");

    var baseMsg = new WsBaseMsg(attr.Type, JToken.FromObject(_msg));
    var json = JsonConvert.SerializeObject(baseMsg);
    var buffer = Encoding.UTF8.GetBytes(json);

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

  private async Task CreateNewLoopAsync(WebSocketSession _session, ManualResetEvent _completeSignal)
  {
    var session = _session;
    var sessionIndex = Interlocked.Increment(ref p_sessionsCount);
    p_sessions.TryAdd(sessionIndex, session);

    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    WebSocketReceiveResult? receiveResult = null;

    var buffer = ArrayPool<byte>.Shared.Rent(100 * 1024);

    try
    {
      var helloMsg = WsHelper.CreateWsMessage(new WsMsgHello(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
      await session.Socket.SendAsync(helloMsg, WebSocketMessageType.Text, true, cts.Token);
      p_clientConnectedFlow.OnNext(session);

      receiveResult = await session.Socket.ReceiveAsync(buffer, cts.Token);

      while (!receiveResult.CloseStatus.HasValue && !cts.IsCancellationRequested)
      {
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var msg = WsHelper.ParseWsMessage(buffer, receiveResult.Count);
        if (msg is null)
        {
          receiveResult = await session.Socket.ReceiveAsync(buffer, cts.Token);
          continue;
        }

        p_incomingMsgs.OnNext(msg);

        receiveResult = await session.Socket.ReceiveAsync(buffer, cts.Token);
      }
    }
    catch (OperationCanceledException)
    {
      // don't care
    }
    catch (WebSocketException wsEx) when (wsEx.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
    {
      p_log.Info($"WS connection '{sessionIndex}' was closed prematurely for room '{session.RoomId}'");
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

    p_sessions.TryRemove(sessionIndex, out _);
    _completeSignal.Set();
  }

  private static T? GetAttribute<T>(Type _type) where T : Attribute
  {
    var attr = Attribute.GetCustomAttribute(_type, typeof(T)) as T;
    return attr;
  }

}
