using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.Modules.RoomsController;

internal class RoomsControllerImpl : IRoomsController, IAppModule<IRoomsController>
{
  public static IRoomsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance(
      (IDbProvider _storage,
      IReadOnlyLifetime _lifetime,
      ISettingsController _settingsController,
      IWebSocketCtrl _webSocketCtrl,
      ILog _log) => new RoomsControllerImpl(_storage, _lifetime, _settingsController, _webSocketCtrl, _log));
  }

  record UserWipeInfo(string RoomId, string Username, long UpToDateTimeUnixMs);
  record PathTruncateInfo(string RoomId, string Username);

  private readonly IDbProvider p_storage;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();
  private readonly Subject<PathTruncateInfo> p_pathTruncateFlow = new();

  private RoomsControllerImpl(
    IDbProvider _storage,
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
              var now = DateTimeOffset.UtcNow;

              foreach (var roomGroup in p_storage.Paths.ListDocumentsMeta().GroupBy(_ => _.Namespace))
              {
                var roomInfo = GetRoom(roomGroup.Key);

                var maxPathPoints = roomInfo?.MaxPathPoints ?? _conf.MaxPathPointsPerRoom;
                var maxPathPointAgeHours = roomInfo?.MaxPathPointAgeHours ?? _conf.MaxPathPointAgeHours;

                var counter = 0;
                var deleted = 0;
                foreach (var entry in roomGroup.OrderByDescending(_ => _.Created))
                {
                  if (maxPathPointAgeHours != null && (now - entry.Created).TotalHours > maxPathPointAgeHours)
                  {
                    p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
                    ++deleted;
                  }
                  else if (maxPathPoints != null && ++counter > maxPathPoints)
                  {
                    p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
                    ++deleted;
                  }
                }

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
            var entriesDeleted = 0L;
            foreach (var entry in p_storage.Paths.ListDocuments<StorageEntry>(_data.RoomId, _to: to))
            {
              if (entry.Data.Username != _data.Username)
                continue;

              p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
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

    p_pathTruncateFlow
      .SelectAsync(async (_data, _ct) =>
      {
        await semaphore.WaitAsync(_ct);

        try
        {
          var maxPointsPerPath = GetRoom(_data.RoomId)?.MaxPointsPerPath;
          if (maxPointsPerPath == null || maxPointsPerPath == 0)
            return;

          log.Info($"Truncating path points for '{_data.RoomId}/{_data.Username}' to '{maxPointsPerPath}' points");

          var entriesEE = p_storage.Paths
            .ListDocuments<StorageEntry>(_data.RoomId)
            .Where(_ => _.Data.Username == _data.Username)
            .OrderByDescending(_ => _.Created);

          var counter = 0;
          var removedDocumentsCount = 0;
          foreach (var entry in entriesEE)
          {
            if (++counter > maxPointsPerPath)
            {
              p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
              ++removedDocumentsCount;
            }
          }

          await _webSocketCtrl.SendMsgByRoomIdAsync(_data.RoomId, new WsMsgPathTruncated(_data.Username, maxPointsPerPath.Value), _ct);

          log.Info($"Truncated '{_data.RoomId}/{_data.Username}' to '{maxPointsPerPath}' points (removed: {removedDocumentsCount})");
        }
        catch (Exception ex)
        {
          log.Error($"Can't truncate entries for {_data.RoomId}/{_data.Username}", ex);
        }
        finally
        {
          semaphore.Release();
        }
      })
      .Subscribe(_lifetime);
  }

  public void RegisterRoom(RoomInfo _roomInfo) => p_storage.GenericData.WriteSimpleDocument(_roomInfo.RoomId, _roomInfo);

  public void UnregisterRoom(string _roomId) => p_storage.GenericData.DeleteSimpleDocument<RoomInfo>(_roomId);

  public RoomInfo? GetRoom(string _roomId)
  {
    var doc = p_storage.GenericData.ReadSimpleDocument<RoomInfo>(_roomId);
    return doc?.Data;
  }

  public IReadOnlyList<RoomInfo> ListRegisteredRooms()
  {
    var users = p_storage.GenericData
      .ListSimpleDocuments<RoomInfo>()
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

  public void EnqueuePathTruncate(
    string _roomId,
    string _username)
  {
    var data = new PathTruncateInfo(_roomId, _username);
    p_pathTruncateFlow.OnNext(data);
  }

}
