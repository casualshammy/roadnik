using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ILogger = JustLogger.Interfaces.ILogger;

namespace Roadnik.Modules.RoomsController;

internal class RoomsControllerImpl : IRoomsController, IAppModule<RoomsControllerImpl>
{
  public static RoomsControllerImpl ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance(
      (IDocumentStorageAot _storage,
      IReadOnlyLifetime _lifetime,
      ISettingsController _settingsController,
      IWebSocketCtrl _webSocketCtrl,
      ILogger _log) => new RoomsControllerImpl(_storage, _lifetime, _settingsController, _webSocketCtrl, _log));
  }

  record UserWipeInfo(string RoomId, string Username, long UpToDateTimeUnixMs);

  private readonly IDocumentStorageAot p_storage;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();

  private RoomsControllerImpl(
    IDocumentStorageAot _storage,
    IReadOnlyLifetime _lifetime,
    ISettingsController _settingsController,
    IWebSocketCtrl _webSocketCtrl,
    ILogger _log)
  {
    p_storage = _storage;
    var log = _log["rooms-controller"];

    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var scheduler));

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
            var entries = p_storage.ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, _ct: _ct);

            await foreach (var roomGroup in entries.GroupBy(_ => _.Data.RoomId))
            {
              if (roomGroup.Key.IsNullOrEmpty())
              {
                await foreach (var entry in roomGroup)
                  await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

                continue;
              }

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
          var entries = p_storage.ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, new LikeExpr($"{_data.RoomId}.%"), _to: to, _ct: _ct);

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
    await p_storage.WriteSimpleDocumentAsync(_roomId, new User(_roomId, _email, null), DocStorageJsonCtx.Default.User, _ct);
  }

  public async Task UnregisterRoomAsync(string _roomId, CancellationToken _ct)
  {
    await p_storage.DeleteSimpleDocumentAsync<User>(_roomId.ToString(), _ct);
  }

  public async Task<User?> GetRoomAsync(string _roomId, CancellationToken _ct)
  {
    var doc = await p_storage.ReadSimpleDocumentAsync(_roomId, DocStorageJsonCtx.Default.User, _ct);
    return doc?.Data;
  }

  public async Task<IReadOnlyList<User>> ListRegisteredRoomsAsync(CancellationToken _ct)
  {
    var users = await p_storage
      .ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.User, _ct: _ct)
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
