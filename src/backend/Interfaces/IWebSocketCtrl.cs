using Ax.Fw.Web.Data.WsServer;
using System.Net.WebSockets;

namespace Roadnik.Server.Interfaces;

public interface IWebSocketCtrl
{
  IObservable<WebSocketSession<Guid, string>> ClientConnected { get; }

  Task<bool> AcceptSocketAsync(
    WebSocket _webSocket,
    string _roomId);

  Task SendMsgAsync<T>(WebSocketSession<Guid, string> _session, T _msg, CancellationToken _ct) where T : notnull;
  Task SendMsgByRoomIdAsync<T>(string _roomId, T _msg, CancellationToken _ct) where T : notnull;
}
