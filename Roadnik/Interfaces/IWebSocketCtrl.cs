using Roadnik.Modules.WebSocketController.Parts;
using System.Net.WebSockets;

namespace Roadnik.Interfaces;

public interface IWebSocketCtrl
{
  IObservable<object> IncomingMessages { get; }
  IObservable<WebSocketSession> ClientConnected { get; }

  Task<bool> AcceptSocketAsync(
    WebSocket _webSocket,
    string _roomId,
    uint _maxPointsInRoom);

  Task<int> BroadcastMsgAsync<T>(T _msg, CancellationToken _ct) where T : notnull;
  Task SendMsgAsync<T>(WebSocketSession _session, T _msg, CancellationToken _ct) where T : notnull;
  Task SendMsgByRoomIdAsync<T>(string _roomId, T _msg, CancellationToken _ct) where T : notnull;
}
