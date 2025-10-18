using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;
using System.Net.WebSockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.WsMsgController;

internal sealed class WsMsgControllerImpl : IWsMsgController, IAppModule<IWsMsgController>
{
  public static IWsMsgController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILog _log,
      IReadOnlyLifetime _lifetime,
      IWebSocketCtrl _webSocketCtrl,
      IRoomsController _roomsController,
      IDbProvider _dbProvider,
      IAppConfig _appConfig)
      => new WsMsgControllerImpl(_log["ws-msg-ctrl"], _lifetime, _webSocketCtrl, _roomsController, _dbProvider, _appConfig));
  }

  private WsMsgControllerImpl(
    ILog _log,
    IReadOnlyLifetime _lifetime,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _roomsController,
    IDbProvider _dbProvider,
    IAppConfig _appConfig)
  {
    var scheduler = new EventLoopScheduler();

    _webSocketCtrl.ClientConnected
      .ObserveOn(scheduler)
      .SelectAsync(async (_client, _ct) =>
      {
        try
        {
          var roomInfo = _roomsController.GetRoom(_client.RoomId);
          var maxPointsInRoom = roomInfo?.MaxPathPoints ?? _appConfig.MaxPathPointsPerRoom;

          var oldestEntryMeta = _dbProvider.Paths
            .ListDocumentsMeta(_client.RoomId)
            .OrderBy(_ => _.Created)
            .FirstOrDefault();

          var helloMsgData = new WsMsgHello(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            maxPointsInRoom,
            oldestEntryMeta?.Created.ToUnixTimeMilliseconds() ?? 0L);

          var helloMsg = WsHelper.CreateWsMessage(helloMsgData);
          await _client.Socket.SendAsync(helloMsg, WebSocketMessageType.Text, true, _ct);
        }
        catch (Exception ex)
        {
          _log.Error($"Error while sending hello message to ws client in room {_client.RoomId}: {ex}");
        }
      }, scheduler)
      .Subscribe(_lifetime);
  }

}
