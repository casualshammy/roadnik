using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Data;
using Roadnik.Common.Toolkit;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.Server.Modules.RoomsController;

internal class RoomsControllerImpl : IRoomsController, IAppModule<IRoomsController>
{
  public static IRoomsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IDbProvider _storage,
      IReadOnlyLifetime _lifetime,
      IAppConfig _appConfig,
      IWebSocketCtrl _webSocketCtrl,
      ILog _log)
      => new RoomsControllerImpl(_storage, _lifetime, _appConfig, _webSocketCtrl, _log["rooms-controller"]));
  }

  record UserWipeInfo(string RoomId, Guid AppInstanceId, string UserName, long UpToDateTimeUnixMs);
  record PathTruncateInfo(string RoomId, Guid AppInstanceId, string UserName);

  private readonly IDbProvider p_storage;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();
  private readonly Subject<PathTruncateInfo> p_pathTruncateFlow = new();

  private RoomsControllerImpl(
    IDbProvider _storage,
    IReadOnlyLifetime _lifetime,
    IAppConfig _appConfig,
    IWebSocketCtrl _webSocketCtrl,
    ILog _log)
  {
    p_storage = _storage;

    var semaphore = new SemaphoreSlim(1, 1);
    var cleanerScheduler = new EventLoopScheduler();

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

            var maxPathPoints = roomInfo?.MaxPathPoints ?? _appConfig.MaxPathPointsPerRoom;
            var maxPathPointAgeHours = roomInfo?.MaxPathPointAgeHours ?? _appConfig.MaxPathPointAgeHours;

            var counter = 0;
            var deleted = 0;
            foreach (var entry in roomGroup.OrderByDescending(_ => _.Created))
            {
              if ((now - entry.Created).TotalHours > maxPathPointAgeHours)
              {
                p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
                ++deleted;
              }
              else if (++counter > maxPathPoints)
              {
                p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
                ++deleted;
              }
            }

            if (deleted > 0)
              _log.Warn($"Removed '{deleted}' geo entries of room id '{roomGroup.Key}'");
          }
        }
        finally
        {
          semaphore.Release();
        }
      })
      .Subscribe(_lifetime);

    var wipeCounter = 0L;
    p_userWipeFlow
      .SelectAsync(async (_data, _ct) =>
      {
        await semaphore.WaitAsync(_ct);

        try
        {
          var to = DateTimeOffset.FromUnixTimeMilliseconds(_data.UpToDateTimeUnixMs);
          _log.Info($"[{wipeCounter}] Removing all entries for '{_data.RoomId}/{_data.AppInstanceId}' up to '{to}'");

          try
          {
            var entriesDeleted = 0L;
            foreach (var entry in p_storage.Paths.ListDocuments<StorageEntry>(_data.RoomId, _to: to))
            {
              if (entry.Data.AppId != _data.AppInstanceId)
                continue;

              p_storage.Paths.DeleteDocuments(entry.Namespace, entry.Key);
              ++entriesDeleted;
            }

            await _webSocketCtrl.SendMsgByRoomIdAsync(
              _data.RoomId,
              new WsMsgPathWiped(GenericToolkit.ConcealAppInstanceId(_data.AppInstanceId), _data.UserName),
              _ct);

            _log.Info($"[{wipeCounter}] Removed '{entriesDeleted}' entries for {_data.RoomId}/{_data.AppInstanceId}");
          }
          catch (Exception ex)
          {
            _log.Error($"[{wipeCounter}] Can't delete entries for {_data.RoomId}/{_data.AppInstanceId}", ex);
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
      .Buffer(TimeSpan.FromMinutes(1))
      .SelectAsync(async (_list, _ct) =>
      {
        if (_list.Count == 0)
          return;

        await semaphore.WaitAsync(_ct);

        try
        {
          foreach (var entry in _list.Distinct())
          {
            var maxPointsPerPath = GetRoom(entry.RoomId)?.MaxPointsPerPath;
            if (maxPointsPerPath == null || maxPointsPerPath == 0)
              continue;

            _log.Info($"Truncating path points for '{entry.RoomId}/{entry.AppInstanceId}' to '{maxPointsPerPath}' points");

            var entriesEE = p_storage.Paths
              .ListDocuments<StorageEntry>(entry.RoomId)
              .Where(_ => _.Data.AppId == entry.AppInstanceId)
              .OrderByDescending(_ => _.Created);

            var counter = 0;
            var removedDocumentsCount = 0;
            foreach (var e in entriesEE)
            {
              if (++counter > maxPointsPerPath)
              {
                p_storage.Paths.DeleteDocuments(e.Namespace, e.Key);
                ++removedDocumentsCount;
              }
            }

            await _webSocketCtrl.SendMsgByRoomIdAsync(
              entry.RoomId, 
              new WsMsgPathTruncated(GenericToolkit.ConcealAppInstanceId(entry.AppInstanceId), entry.UserName, maxPointsPerPath.Value), 
              _ct);

            _log.Info($"Truncated '{entry.RoomId}/{entry.AppInstanceId}' to '{maxPointsPerPath}' points (removed: {removedDocumentsCount})");
          }
        }
        catch (Exception ex)
        {
          _log.Error($"Can't truncate entries", ex);
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
    Guid _appInstanceId,
    string _userName,
    long _upToDateTimeUnixMs)
  {
    var data = new UserWipeInfo(_roomId, _appInstanceId, _userName, _upToDateTimeUnixMs);
    p_userWipeFlow.OnNext(data);
  }

  public void EnqueuePathTruncate(
    string _roomId,
    Guid _appInstanceId,
    string _userName)
  {
    var data = new PathTruncateInfo(_roomId, _appInstanceId, _userName);
    p_pathTruncateFlow.OnNext(data);
  }

}
