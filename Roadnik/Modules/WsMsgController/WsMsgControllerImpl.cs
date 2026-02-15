using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Data;
using Roadnik.Common.Toolkit;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
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
          var roomInfo = _roomsController.GetRoom(_client.SessionGroup);
          var maxPointsInRoom = roomInfo?.MaxPathPoints ?? _appConfig.MaxPathPointsPerRoom;

          var oldestEntriesLut = new Dictionary<Guid, DateTimeOffset>();
          foreach (var doc in _dbProvider.Paths.ListDocuments<StorageEntry>(_client.SessionGroup))
          {
            var appId = doc.Data.AppId;
            var created = doc.Created;

            if (!oldestEntriesLut.TryGetValue(appId, out var value) || value > created)
              oldestEntriesLut[appId] = created;
          }

          var helloMsgData = new WsMsgHello(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            maxPointsInRoom,
            oldestEntriesLut.ToDictionary(
              _ => GenericToolkit.ConcealAppInstanceId(_.Key),
              _ => _.Value.ToUnixTimeMilliseconds()));

          await _webSocketCtrl.SendMsgAsync(_client, helloMsgData, _ct);
        }
        catch (Exception ex)
        {
          _log.Error($"Error while sending hello message to ws client in room {_client.SessionGroup}: {ex}");
        }
      }, scheduler)
      .Subscribe(_lifetime);
  }

}
