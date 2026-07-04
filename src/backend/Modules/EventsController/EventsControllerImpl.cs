using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Data;
using Roadnik.Common.Toolkit;
using Roadnik.Server.Data.SseServer;
using Roadnik.Server.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.EventsController;

internal sealed class EventsControllerImpl : IEventsController, IAppModule<IEventsController>
{
  public static IEventsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILog _log,
      IReadOnlyLifetime _lifetime,
      ISseServerCtrl _sseServerCtrl,
      IRoomsController _roomsController,
      IDbProvider _dbProvider,
      IAppConfig _appConfig)
      => new EventsControllerImpl(_log["events-ctrl"], _lifetime, _sseServerCtrl, _roomsController, _dbProvider, _appConfig));
  }

  private EventsControllerImpl(
    ILog _log,
    IReadOnlyLifetime _lifetime,
    ISseServerCtrl _sseServerCtrl,
    IRoomsController _roomsController,
    IDbProvider _dbProvider,
    IAppConfig _appConfig)
  {
    var scheduler = new EventLoopScheduler();

    _sseServerCtrl.ClientConnected
      .ObserveOn(scheduler)
      .Subscribe(_client =>
      {
        try
        {
          var roomInfo = _roomsController.GetRoom(_client.ClientGroup);
          var maxPointsInRoom = roomInfo?.MaxPathPoints ?? _appConfig.MaxPathPointsPerRoom;

          var oldestEntriesLut = new Dictionary<Guid, DateTimeOffset>();
          foreach (var doc in _dbProvider.Paths.ListDocuments<StorageEntry>(_client.ClientGroup))
          {
            var appId = doc.Data.AppId;
            var created = doc.Created;

            if (!oldestEntriesLut.TryGetValue(appId, out var value) || value > created)
              oldestEntriesLut[appId] = created;
          }

          var helloMsgData = new SseMsgHello(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            maxPointsInRoom,
            oldestEntriesLut.ToDictionary(
              _ => GenericToolkit.ConcealAppInstanceId(_.Key),
              _ => _.Value.ToUnixTimeMilliseconds()));

          _sseServerCtrl.SendMsg(_client, helloMsgData);
        }
        catch (Exception ex)
        {
          _log.Error($"Error while sending hello message to ws client in room {_client.ClientGroup}: {ex}");
        }
      }, _lifetime);
  }

}
