using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Web.Data.WsServer;
using Ax.Fw.Web.Modules.WebSocketServer;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Net.WebSockets;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik.Server.Modules.WebSocketController;

public class WebSocketCtrlImpl : IWebSocketCtrl, IAppModule<IWebSocketCtrl>
{
  public static IWebSocketCtrl ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILog _log,
      IReadOnlyLifetime _lifetime) 
      => new WebSocketCtrlImpl(_log["ws"], _lifetime));
  }

  private readonly WebSocketServer<Guid, string> p_wsServer;

  private WebSocketCtrlImpl(
    ILog _log,
    IReadOnlyLifetime _lifetime)
  {
    p_wsServer = new WebSocketServer<Guid, string>(
      _lifetime,
      WebSocketJsonCtx.Default,
      new Dictionary<string, Type>
      {
        {"ws-msg-hello", typeof(WsMsgHello) },
        {"ws-msg-path-wiped", typeof(WsMsgPathWiped) },
        {"ws-msg-room-points-updated", typeof(WsMsgRoomPointsUpdated) },
        {"ws-msg-data-updated", typeof(WsMsgUpdateAvailable) },
        {"ws-msg-path-truncated", typeof(WsMsgPathTruncated) },
      },
      _onError: _e => _log.Error($"Error is ws server: {_e}"));
  }

  public IObservable<WebSocketSession<Guid, string>> ClientConnected => p_wsServer.ClientConnected;

  public async Task<bool> AcceptSocketAsync(
    WebSocket _webSocket,
    string _roomId)
    => await p_wsServer.AcceptSocketAsync(Guid.NewGuid(), _roomId, _webSocket);

  public async Task SendMsgAsync<T>(WebSocketSession<Guid, string> _session, T _msg, CancellationToken _ct) where T : notnull
    => await p_wsServer.SendMsgAsync(_session, _msg, false, _ct);

  public async Task SendMsgByRoomIdAsync<T>(string _roomId, T _msg, CancellationToken _ct) where T : notnull
    => await p_wsServer.BroadcastMsgAsync(_roomId, _msg, false, _ct);

}
