using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik.Modules.RoomsController;

internal class RoomsControllerImpl : IRoomsController, IAppModule<IRoomsController>
{
  public static IRoomsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance(
      (IDocumentStorage _storage,
      IReadOnlyLifetime _lifetime,
      ISettingsController _settingsController,
      IWebSocketCtrl _webSocketCtrl,
      ILog _log) => new RoomsControllerImpl(_storage, _lifetime, _settingsController, _webSocketCtrl, _log));
  }

  record UserWipeInfo(string RoomId, string Username, long UpToDateTimeUnixMs);

  private readonly IDocumentStorage p_storage;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();

  private RoomsControllerImpl(
    IDocumentStorage _storage,
    IReadOnlyLifetime _lifetime,
    ISettingsController _settingsController,
    IWebSocketCtrl _webSocketCtrl,
    ILog _log)
  {
    p_storage = _storage;
    var log = _log["rooms-controller"];

    var semaphore = new SemaphoreSlim(1, 1);

    _settingsController.Settings
      .WhereNotNull()
      .HotAlive(_lifetime, (_conf, _life) =>
      {
        Observable
          .Interval(TimeSpan.FromHours(1))
          .StartWithDefault()
          .Delay(TimeSpan.FromMinutes(10))
          .SelectAsync(async (_, _ct) =>
          {
            await semaphore.WaitAsync(_ct);

            try
            {
              var entries = await p_storage.ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, _ct: _ct);

              foreach (var roomGroup in entries.GroupBy(_ => _.Data.RoomId))
              {
                if (roomGroup.Key.IsNullOrEmpty())
                {
                  foreach (var entry in roomGroup)
                    await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

                  continue;
                }

                var limit = _conf.AnonymousMaxPoints;
                var room = await GetRoomAsync(roomGroup.Key, _ct);
                if (room?.MaxPoints != null)
                  limit = room.MaxPoints.Value;

                var counter = 0;
                foreach (var entry in roomGroup.OrderByDescending(_ => _.Created))
                  if (++counter > limit)
                    await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

                var deleted = counter - limit;
                if (deleted > 0)
                  log.Warn($"Removed '{deleted}' geo entries of room id '{roomGroup.Key}'");
              }
            }
            finally
            {
              semaphore.Release();
            }
          })
          .Subscribe(_life);
      });

    var wipeCounter = 0L;
    p_userWipeFlow
      .SelectAsync(async (_data, _ct) =>
      {
        await semaphore.WaitAsync(_ct);

        try
        {
          var to = DateTimeOffset.FromUnixTimeMilliseconds(_data.UpToDateTimeUnixMs);
          log.Info($"[{wipeCounter}] Removing all entries for '{_data.RoomId}/{_data.Username}' up to '{to}'");

          try
          {
            var entries = await p_storage.ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, new LikeExpr($"{_data.RoomId}.%"), _to: to, _ct: _ct);

            var entriesDeleted = 0L;
            foreach (var entry in entries)
            {
              if (entry.Data.Username != _data.Username)
                continue;

              await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);
              ++entriesDeleted;
            }

            await _webSocketCtrl.SendMsgByRoomIdAsync(_data.RoomId, new WsMsgPathWiped(_data.Username), _ct);

            log.Info($"[{wipeCounter}] Removed '{entriesDeleted}' entries for {_data.RoomId}/{_data.Username}");
          }
          catch (Exception ex)
          {
            log.Error($"[{wipeCounter}] Can't delete entries for {_data.RoomId}/{_data.Username}", ex);
          }
        }
        finally
        {
          semaphore.Release();
        }
      })
      .Do(_ => Interlocked.Increment(ref wipeCounter))
      .Subscribe(_lifetime);
  }

  public async Task RegisterRoomAsync(
    string _roomId,
    string _email,
    int? _maxPoints,
    double? _minPointInterval,
    DateTimeOffset? _validUntil,
    CancellationToken _ct)
  {
    var info = new RoomInfo(_roomId, _email, _maxPoints, _minPointInterval, _validUntil);
    await p_storage.WriteSimpleDocumentAsync(_roomId, info, DocStorageJsonCtx.Default.RoomInfo, _ct);
  }

  public async Task UnregisterRoomAsync(string _roomId, CancellationToken _ct)
  {
    await p_storage.DeleteSimpleDocumentAsync<RoomInfo>(_roomId.ToString(), _ct);
  }

  public async Task<RoomInfo?> GetRoomAsync(string _roomId, CancellationToken _ct)
  {
    var doc = await p_storage.ReadSimpleDocumentAsync(_roomId, DocStorageJsonCtx.Default.RoomInfo, _ct);
    return doc?.Data;
  }

  public async Task<IReadOnlyList<RoomInfo>> ListRegisteredRoomsAsync(CancellationToken _ct)
  {
    var users = (await p_storage
      .ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.RoomInfo, _ct: _ct))
      .Select(_ => _.Data)
      .ToList();

    return users;
  }

  public void EnqueueUserWipe(
    string _roomId,
    string _username,
    long _upToDateTimeUnixMs)
  {
    var data = new UserWipeInfo(_roomId, _username, _upToDateTimeUnixMs);
    p_userWipeFlow.OnNext(data);
  }

}
