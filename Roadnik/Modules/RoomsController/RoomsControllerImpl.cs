using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using JustLogger.Interfaces;
using Roadnik.Data;
using Roadnik.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime;

namespace Roadnik.Modules.RoomsController;

[ExportClass(typeof(IRoomsController), Singleton: true)]
internal class RoomsControllerImpl : IRoomsController
{
  record UserWipeInfo(string RoomId, string Username, long UpToDateTimeUnixMs);

  private readonly IDocumentStorage p_storage;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();

  public RoomsControllerImpl(
    IDocumentStorage _storage,
    IReadOnlyLifetime _lifetime,
    ISettingsController _settingsController,
    IWebSocketCtrl _webSocketCtrl,
    ILogger _log)
  {
    p_storage = _storage;
    var log = _log["users-controller"];

    _lifetime.ToDisposeOnEnded(Pool<EventLoopScheduler>.Get(out var scheduler));

    _settingsController.Settings
      .WhereNotNull()
      .HotAlive(_lifetime, (_conf, _life) =>
      {
        Observable
          .Interval(TimeSpan.FromHours(1), scheduler)
          .StartWithDefault()
          .Delay(TimeSpan.FromMinutes(10), scheduler)
          .ObserveOn(scheduler)
          .SelectAsync(async (_, _ct) =>
          {
            var entries = p_storage.ListSimpleDocumentsAsync<StorageEntry>(_ct: _ct);

            await foreach (var roomGroup in entries.GroupBy(_ => _.Data.RoomId))
            {
              var limit = _conf.AnonymousMaxPoints;
              var user = await GetRoomAsync(roomGroup.Key, _ct);
              if (user != null)
                limit = _conf.RegisteredMaxPoints;

              var counter = 0;
              await foreach (var entry in roomGroup.OrderByDescending(_ => _.Created))
                if (++counter > limit)
                  await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

              var deleted = counter - limit;
              if (deleted > 0)
                log.Warn($"Removed '{deleted}' geo entries of room id '{roomGroup.Key}'");
            }
          }, scheduler)
          .Subscribe(_life);
      });

    var wipeCounter = 0L;
    p_userWipeFlow
      .ObserveOn(scheduler)
      .SelectAsync(async (_data, _ct) =>
      {
        var to = DateTimeOffset.FromUnixTimeMilliseconds(_data.UpToDateTimeUnixMs);
        log.Info($"[{wipeCounter}] Removing all entries for '{_data.RoomId}/{_data.Username}' up to '{to}'");

        try
        {
          var entries = p_storage.ListSimpleDocumentsAsync<StorageEntry>(new LikeExpr($"{_data.RoomId}.%"), _to: to, _ct: _ct);

          var entriesDeleted = 0L;
          await foreach (var entry in entries)
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
      }, scheduler)
      .Do(_ => Interlocked.Increment(ref wipeCounter))
      .Subscribe(_lifetime);
  }

  public async Task RegisterRoomAsync(string _roomId, string _email, CancellationToken _ct)
  {
    await p_storage.WriteSimpleDocumentAsync(_roomId, new User(_roomId, _email, null), _ct);
  }

  public async Task UnregisterRoomAsync(string _roomId, CancellationToken _ct)
  {
    await p_storage.DeleteSimpleDocumentAsync<User>(_roomId.ToString(), _ct);
  }

  public async Task<User?> GetRoomAsync(string _roomId, CancellationToken _ct)
  {
    var doc = await p_storage.ReadSimpleDocumentAsync<User>(_roomId, _ct);
    return doc?.Data;
  }

  public async Task<IReadOnlyList<User>> ListRegisteredRoomsAsync(CancellationToken _ct)
  {
    var users = await p_storage
      .ListSimpleDocumentsAsync<User>(_ct: _ct)
      .Select(_ => _.Data)
      .ToListAsync(_ct);

    return users;
  }

  public void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs)
  {
    var data = new UserWipeInfo(_roomId, _username, _upToDateTimeUnixMs);
    p_userWipeFlow.OnNext(data);
  }

}
