using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Data;
using Roadnik.Common.JsonCtx;
using Roadnik.Common.ReqRes;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Toolkit;
using Roadnik.Interfaces;
using Roadnik.Server.Data.DbTypes;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Roadnik.Server.Modules.RoomsController;

record SaveNewPathPointResult(
  HttpStatusCode? ErrorCode,
  string? ErrorMsg);

internal class RoomsControllerImpl : IRoomsController, IAppModule<IRoomsController>
{
  public static IRoomsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IDbProvider _storage,
      IReadOnlyLifetime _lifetime,
      IAppConfig _appConfig,
      IWebSocketCtrl _webSocketCtrl,
      ILog _log,
      IReqRateLimiter _reqRateLimiter,
      IFCMPublisher _firebasePublisher)
      => new RoomsControllerImpl(_storage, _lifetime, _appConfig, _webSocketCtrl, _log, _reqRateLimiter, _firebasePublisher));
  }

  record UserWipeInfo(string RoomId, string Username, long UpToDateTimeUnixMs);
  record PathTruncateInfo(string RoomId, string Username);

  private readonly IDbProvider p_storage;
  private readonly IAppConfig p_appConfig;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_firebasePublisher;
  private readonly Subject<UserWipeInfo> p_userWipeFlow = new();
  private readonly Subject<PathTruncateInfo> p_pathTruncateFlow = new();

  private RoomsControllerImpl(
    IDbProvider _storage,
    IReadOnlyLifetime _lifetime,
    IAppConfig _appConfig,
    IWebSocketCtrl _webSocketCtrl,
    ILog _log,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _firebasePublisher)
  {
    p_storage = _storage;
    p_appConfig = _appConfig;
    p_webSocketCtrl = _webSocketCtrl;
    p_reqRateLimiter = _reqRateLimiter;
    p_firebasePublisher = _firebasePublisher;

    var log = _log["rooms-controller"];

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
              log.Warn($"Removed '{deleted}' geo entries of room id '{roomGroup.Key}'");
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
      .Buffer(TimeSpan.FromMinutes(1))
      .SelectAsync(async (_list, _ct) =>
      {
        if (_list.Count == 0)
          return;

        await semaphore.WaitAsync(_ct);

        try
        {
          foreach (var entry in _list.DistinctBy(_ => HashCode.Combine(_.RoomId, _.Username)))
          {
            var maxPointsPerPath = GetRoom(entry.RoomId)?.MaxPointsPerPath;
            if (maxPointsPerPath == null || maxPointsPerPath == 0)
              continue;

            log.Info($"Truncating path points for '{entry.RoomId}/{entry.Username}' to '{maxPointsPerPath}' points");

            var entriesEE = p_storage.Paths
              .ListDocuments<StorageEntry>(entry.RoomId)
              .Where(_ => _.Data.Username == entry.Username)
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

            await _webSocketCtrl.SendMsgByRoomIdAsync(entry.RoomId, new WsMsgPathTruncated(entry.Username, maxPointsPerPath.Value), _ct);

            log.Info($"Truncated '{entry.RoomId}/{entry.Username}' to '{maxPointsPerPath}' points (removed: {removedDocumentsCount})");
          }
        }
        catch (Exception ex)
        {
          log.Error($"Can't truncate entries", ex);
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

  public async Task<SaveNewPathPointResult> SaveNewPathPointAsync(
    ILog _log,
    IPAddress? _clientIpAddress,
    string _roomId,
    string _username,
    int _sessionId,
    bool _wipeOldPath,
    float _lat,
    float _lng,
    float _alt,
    float? _acc,
    float? _speed,
    float? _battery,
    float? _gsmSignal,
    float? _bearing,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return new(HttpStatusCode.BadRequest, "Room Id is incorrect!");
    if (!ReqResUtil.IsUsernameSafe(_username))
      return new(HttpStatusCode.BadRequest, "Username is incorrect!");

    _log.Info($"Got request to store path point: '{_roomId}/{_username}'");

    var room = GetRoom(_roomId);
    var maxPathPoints = room?.MaxPathPoints ?? p_appConfig.MaxPathPointsPerRoom;
    if (maxPathPoints == 0)
      return new(HttpStatusCode.Forbidden, "Publishing is forbidden!");

    var minInterval = room?.MinPathPointIntervalMs ?? p_appConfig.MinPathPointIntervalMs;
    var compositeKey = $"{ReqPaths.STORE_PATH_POINT}/{_roomId}/{_username}";
    if (!p_reqRateLimiter.IsReqOk(compositeKey, _clientIpAddress, minInterval))
    {
      _log.Warn($"Too many requests, room '{_roomId}', username: '{_username}', time limit: '{minInterval} ms'");
      return new(HttpStatusCode.TooManyRequests, string.Empty);
    }

    var now = DateTimeOffset.UtcNow;
    var nowUnixMs = now.ToUnixTimeMilliseconds();

    var sessionKey = $"{_roomId}/{_username}";
    var sessionDoc = p_storage.GenericData.ReadSimpleDocument<RoomUserSession>(sessionKey);
    if (sessionDoc == null || sessionDoc.Data.SessionId != _sessionId)
    {
      _log.Info($"New session {_sessionId} is started, username '{_username}', wipe: '{_wipeOldPath}'");

      p_storage.GenericData.WriteSimpleDocument(sessionKey, new RoomUserSession(_sessionId));

      if (_wipeOldPath == true)
        EnqueueUserWipe(_roomId, _username, nowUnixMs);

      var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgNewTrackStarted(_username), AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
      var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, pushMsgData);
      await p_firebasePublisher.SendDataAsync(_roomId, pushMsg, _ct);
    }

    var record = new StorageEntry(_username, _lat, _lng, _alt, _speed, _acc, _battery, _gsmSignal, _bearing);
    p_storage.Paths.WriteDocument(_roomId, nowUnixMs, record);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_roomId, new WsMsgUpdateAvailable(nowUnixMs), _ct);

    if (room?.MaxPointsPerPath > 0)
      EnqueuePathTruncate(_roomId, _username);

    _log.Info($"Successfully stored path point: '{_roomId}/{_username}'");

    return new(null, null);
  }

}
