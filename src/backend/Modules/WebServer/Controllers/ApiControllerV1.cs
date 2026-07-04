using Ax.Fw;
using Ax.Fw.App.Interfaces;
using Ax.Fw.Extensions;
using Ax.Fw.Storage.Data;
using Ax.Fw.Web.Data;
using Ax.Fw.Web.Extensions;
using Ax.Fw.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Roadnik.Common.Data;
using Roadnik.Common.Data.DocumentStorage;
using Roadnik.Common.JsonCtx;
using Roadnik.Common.ReqRes;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.ReqResTypes;
using Roadnik.Common.Toolkit;
using Roadnik.Interfaces;
using Roadnik.Server.Attributes;
using Roadnik.Server.Data.DbTypes;
using Roadnik.Server.Data.SseServer;
using Roadnik.Server.Interfaces;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using static Roadnik.Server.Data.Consts;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class ApiControllerV1
{
  public ApiControllerV1(WebApplication _app)
  {
    var ctrlInfo = new RestControllerInfo("api-v1", "api-v1");

    var apiGroup = _app.MapGroup("/api/v1/");
    apiGroup.MapGet(ReqPaths.GET_VERSION, GetVersion).WithMetadata(ctrlInfo);
    apiGroup.MapGet("/ping", () => Results.Ok()).WithMetadata(ctrlInfo);
    apiGroup.MapGet("/map-tile/{type}/{z:int}/{x:int}/{y:int}.png", GetMapTileAsync).WithMetadata(ctrlInfo);
    apiGroup.MapPost(ReqPaths.STORE_PATH_POINT, StorePathPointAsync).WithMetadata(ctrlInfo);
    apiGroup.MapGet(ReqPaths.LIST_ROOM_PATH_POINTS, ListRoomPathPoints).WithMetadata(ctrlInfo);
    apiGroup.MapPost(ReqPaths.CREATE_ROOM_POINT, CreateRoomPointAsync).WithMetadata(ctrlInfo);
    apiGroup.MapGet(ReqPaths.LIST_ROOM_POINTS, ListRoomPoints).WithMetadata(ctrlInfo);
    apiGroup.MapPost(ReqPaths.DELETE_ROOM_POINT, DeleteRoomPointAsync).WithMetadata(ctrlInfo);
    apiGroup.MapGet(ReqPaths.GET_FREE_ROOM_ID, GetFreeRoomId).WithMetadata(ctrlInfo);
    apiGroup.MapGet(ReqPaths.IS_ROOM_ID_VALID, IsRoomIdValid).WithMetadata(ctrlInfo);
    apiGroup.MapGet("/events", EventsAsync).WithMetadata(ctrlInfo);
    apiGroup.MapPost(ReqPaths.REGISTER_ROOM, RegisterRoom).WithMetadata(ctrlInfo);
    apiGroup.MapPost(ReqPaths.UNREGISTER_ROOM, DeleteRoomRegistration).WithMetadata(ctrlInfo);
    apiGroup.MapGet(ReqPaths.LIST_REGISTERED_ROOMS, ListRooms).WithMetadata(ctrlInfo);
  }

  public IResult GetVersion(
    IScopedLog _log,
    IRequestToolkit _reqToolkit)
  {
    _log.Info($"Requested **version**");
    return _reqToolkit.Json(AppVersion);
  }

  public async Task<IResult> GetMapTileAsync(
    IHttpClientProvider _httpClientProvider,
    IAppConfig _appConfig,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    IStravaTilesProvider _stravaTilesProvider,
    HttpContext _httpCtx,
    [FromRoute(Name = "type")] string _mapType,
    [FromRoute(Name = "z")] int _z,
    [FromRoute(Name = "x")] int _x,
    [FromRoute(Name = "y")] int _y,
    CancellationToken _ct)
  {
    var cacheKey = $"{_z}/{_x}/{_y}";
    {
      if (_dbProvider.Tiles.TryReadBlob(_mapType, cacheKey, out BlobStream? cachedStream, out var cachedMeta))
      {
        _log.Info($"**Handled** request of **map tile** __{_mapType}/{_z}/{_x}/{_y}__ (**cached**)");
        _httpCtx.Response.Headers.Append(HEADER_CACHED_TILE, $"{cachedMeta.DocId}/{cachedMeta.Version}");
        _httpCtx.Response.Headers.Append("Cache-Control", "public, max-age=604800");
        return Results.Stream(cachedStream, MimeTypes.Png.Mime);
      }
    }

    var tfApiKey = _appConfig.ThunderforestApiKey;
    var tfApiKeyParam = tfApiKey.IsNullOrWhiteSpace() ? string.Empty : $"?apikey={tfApiKey}";
    var url = _mapType switch
    {
      TILE_TYPE_TF_OPENCYCLEMAP => $"https://tile.thunderforest.com/cycle/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
      TILE_TYPE_TF_OUTDOORS => $"https://tile.thunderforest.com/outdoors/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
      TILE_TYPE_TF_TRANSPORT => $"https://tile.thunderforest.com/transport/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
      TILE_TYPE_STRAVA_HEATMAP_RIDE => $"https://content-a.strava.com/identified/globalheat/ride/red/{_z}/{_x}/{_y}.png",
      TILE_TYPE_STRAVA_HEATMAP_RUN => $"https://content-a.strava.com/identified/globalheat/run/blue/{_z}/{_x}/{_y}.png",
      TILE_TYPE_CARTO_DARK => $"https://basemaps.cartocdn.com/dark_all/{_z}/{_x}/{_y}.png",
      _ => null
    };

    if (url == null)
      return _reqToolkit.BadRequest($"Map type is not available: '{_mapType}'");

    try
    {
      using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);

      if (_mapType == TILE_TYPE_STRAVA_HEATMAP_RIDE || _mapType == TILE_TYPE_STRAVA_HEATMAP_RUN)
        foreach (var (headerName, headerValue) in _stravaTilesProvider.Headers)
          httpReq.Headers.Add(headerName, headerValue);

      using var httpRes = await _httpClientProvider.HttpClient.SendAsync(httpReq, _ct);
      httpRes.EnsureSuccessStatusCode();

      var imageBytes = await httpRes.Content.ReadAsByteArrayAsync(_ct);

      var mapCacheSize = _appConfig.MapTilesCacheSize;
      if (mapCacheSize != null && mapCacheSize.Value > 0)
        await _dbProvider.Tiles.WriteBlobAsync(_mapType, cacheKey, imageBytes, _ct);

      _log.Info($"**Handled** request of **map tile** __{_mapType}/{_z}/{_x}/{_y}__ (**live**)");

      _httpCtx.Response.Headers.Append("Cache-Control", "public, max-age=604800");
      return Results.Bytes(imageBytes, httpRes.Content.Headers.ContentType?.ToString());
    }
    catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.NotFound)
    {
      _log.Warn($"Tile not found");
      return _reqToolkit.NotFound();
    }
    catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.Unauthorized)
    {
      _log.Warn($"Can't download map tile (unauthorized)");
      return Results.Unauthorized();
    }
  }

  public async Task<IResult> StorePathPointAsync(
    IAppConfig _appConfig,
    IReqRateLimiter _reqRateLimiter,
    IRoomsController _roomsController,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    IFCMPublisher _fcmPublisher,
    ISseServerCtrl _sseServerCtrl,
    HttpRequest _httpRequest,
    [FromBody] StorePathPointReq _req,
    CancellationToken _ct)
  {
    _log.Info($"Got request to **store path point**: '__{_req.RoomId}__/**{_req.AppId}**/__{_req.Username}__'");

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return _reqToolkit.BadRequest("Room Id is incorrect!");
    if (!ReqResUtil.IsUsernameSafe(_req.Username))
      return _reqToolkit.BadRequest("Username is incorrect!");

    var room = _roomsController.GetRoom(_req.RoomId);
    var maxPathPoints = room?.MaxPathPoints ?? _appConfig.MaxPathPointsPerRoom;
    if (maxPathPoints == 0)
      return _reqToolkit.Forbidden("Publishing is forbidden!");

    var minInterval = room?.MinPathPointIntervalMs ?? _appConfig.MinPathPointIntervalMs;
    if (!_reqRateLimiter.IsReqOk($"{ReqPaths.STORE_PATH_POINT}/{_req.RoomId}", _httpRequest.HttpContext.Connection.RemoteIpAddress, minInterval))
    {
      _log.Warn($"Too many requests, time limit: {minInterval} ms");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;
    var nowUnixMs = now.ToUnixTimeMilliseconds();

    var sessionKey = $"{_req.RoomId}/{_req.AppId}";
    var sessionDoc = _dbProvider.GenericData.ReadSimpleDocument<RoomUserSession>(sessionKey);
    if (sessionDoc == null || sessionDoc.Data.SessionId != _req.SessionId)
    {
      _log.Info($"New session {_req.SessionId} is started, wipe: '{_req.WipeOldPath}'");

      _dbProvider.GenericData.WriteSimpleDocument(sessionKey, new RoomUserSession(_req.SessionId));

      if (_req.WipeOldPath == true)
        _roomsController.EnqueueUserWipe(_req.RoomId, _req.AppId, _req.Username, nowUnixMs);

      var pushMsgPayload = new PushMsgNewTrackStarted(GenericToolkit.ConcealAppInstanceId(_req.AppId), _req.Username);
      var pushMsgData = JsonSerializer.SerializeToElement(pushMsgPayload, AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
      var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, pushMsgData);
      await _fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);
    }

    var record = StorageEntry.From(_req);
    _dbProvider.Paths.WriteDocument(_req.RoomId, $"{record.AppId}.{nowUnixMs}", record);

    _sseServerCtrl.SendMsgByRoomId(_req.RoomId, new SseMsgUpdateAvailable(nowUnixMs));

    if (room?.MaxPointsPerPath > 0)
      _roomsController.EnqueuePathTruncate(_req.RoomId, _req.AppId, _req.Username);

    return Results.Ok();
  }

  public IResult ListRoomPathPoints(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId,
    [FromQuery(Name = "offset")] long _offsetUnixTimeMs)
  {
    _log.Info($"Requested **room path points** of __{_roomId}__ from timestamp __{_offsetUnixTimeMs}__");

    if (!ReqResUtil.IsRoomIdValid(_roomId))
    {
      _log.Warn("Room Id is incorrect!");
      return _reqToolkit.BadRequest("Room Id is incorrect!");
    }

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_PATH_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      _log.Warn($"Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    const int maxReturnEntries = 250;
    var offset = DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs + 1);
    var entries = _dbProvider.Paths
      .ListDocuments<StorageEntry>(_roomId, _from: offset)
      .OrderBy(_ => _.Created)
      .Take(maxReturnEntries + 1)
      .Select(TimedStorageEntry.FromStorageEntry)
      .ToArray();

    ListRoomPathPointsRes result;
    if (entries.Length == 0)
    {
      result = new(now.ToUnixTimeMilliseconds(), false, []);
    }
    else if (entries.Length <= maxReturnEntries)
    {
      result = new(now.ToUnixTimeMilliseconds(), false, entries);
    }
    else
    {
      var lastEntryTime = entries[^2].UnixTimeMs;
      result = new(lastEntryTime, true, entries.Take(maxReturnEntries));
    }

    return _reqToolkit.Json(result);
  }

  public async Task<IResult> CreateRoomPointAsync(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    IFCMPublisher _fcmPublisher,
    ISseServerCtrl _sseServerCtrl,
    HttpRequest _httpRequest,
    [FromBody] CreateRoomPointReq _req,
    CancellationToken _ct)
  {
    _log.Info($"Requested to **create room point** in __{_req.RoomId}__ from user __{_req.Username}__, coords: __{_req.Lat}; {_req.Lng}__");

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return _reqToolkit.BadRequest($"Incorrect room id!");
    if (!ReqResUtil.IsUsernameSafe(_req.Username))
      return _reqToolkit.BadRequest($"Incorrect username!");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.CREATE_ROOM_POINT, ip, () => new TimeWall(10, TimeSpan.FromSeconds(10))))
    {
      _log.Warn($"Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var description = ReqResUtil.ClearUserMsg(_req.Description);
    var now = DateTimeOffset.UtcNow;
    var point = new RoomPointDocument(_req.AppId, _req.RoomId, _req.Username, _req.Lat, _req.Lng, description);
    _dbProvider.GenericData.WriteSimpleDocument($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point);

    _sseServerCtrl.SendMsgByRoomId(_req.RoomId, new SseMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()));

    var pushMsgData = JsonSerializer.SerializeToElement(
      new PushMsgRoomPointAdded(_req.AppId, _req.Username, _req.Description, _req.Lat, _req.Lng),
      AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);

    var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, pushMsgData);
    await _fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Results.Ok();
  }

  public IResult ListRoomPoints(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId)
  {
    _log.Info($"Requested **list of room points**, room id: __{_roomId}__");

    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return _reqToolkit.BadRequest($"Incorrect room id: '{_roomId}'!");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      _log.Warn($"Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var entries = _dbProvider.GenericData.ListSimpleDocuments<RoomPointDocument>(new LikeExpr($"{_roomId}.%"))
      .Select(RoomPoint.From)
      .ToArray();

    return _reqToolkit.Json(new ListRoomPointsRes(entries));
  }

  public async Task<IResult> DeleteRoomPointAsync(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    ISseServerCtrl _sseServerCtrl,
    HttpRequest _httpRequest,
    [FromBody] DeleteRoomPointReq _req,
    CancellationToken _ct)
  {
    _log.Info($"Requested to **delete room point** __{_req.PointId}__ from room __{_req.RoomId}__");

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return _reqToolkit.BadRequest($"Incorrect room id!");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.DELETE_ROOM_POINT, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);

    foreach (var entry in _dbProvider.GenericData.ListSimpleDocuments<RoomPointDocument>(new LikeExpr($"{_req.RoomId}.%")))
      if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
      {
        _dbProvider.GenericData.DeleteSimpleDocument<RoomPointDocument>(entry.Key);
        _sseServerCtrl.SendMsgByRoomId(_req.RoomId, new SseMsgRoomPointsUpdated(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        break;
      }

    return Results.Ok();
  }

  public IResult GetFreeRoomId(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    HttpRequest _httpRequest,
    CancellationToken _ct)
  {
    _log.Info($"Requested **free room id**");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.GET_FREE_ROOM_ID, ip, () => new TimeWall(10, TimeSpan.FromSeconds(60))))
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);

    using var timedCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct, timedCts.Token);
    string? roomId = null;
    var roomIdValid = false;
    while (!cts.IsCancellationRequested && !roomIdValid)
    {
      roomId = CommonUtilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false);
      roomIdValid = !_dbProvider.Paths
        .ListDocumentsMeta(roomId)
        .Any();
    }

    if (!roomIdValid || roomId.IsNullOrEmpty())
    {
      _log.Error($"Can't find free room id (last: '{roomId}')!");
      return _reqToolkit.InternalServerError("Can't find free room id!");
    }

    return Results.Content(roomId, MimeTypes.Text.Mime, Encoding.UTF8);
  }

  public IResult IsRoomIdValid(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromQuery(Name = "roomId")] string _roomId)
  {
    _log.Info($"Requested to **check** if __'{_roomId}'__ is **valid** room id");

    var valid = ReqResUtil.IsRoomIdValid(_roomId);

    return valid
      ? _reqToolkit.Ok()
      : Results.StatusCode((int)HttpStatusCode.NotAcceptable);
  }

  public async Task<IResult> EventsAsync(
    IReqRateLimiter _reqRateLimiter,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    ISseServerCtrl _sseServerCtrl,
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return _reqToolkit.BadRequest("Room Id is incorrect!");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_PATH_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);

    _httpRequest.HttpContext.Response.SetSseHeaders();

    try
    {
      using var _ = _sseServerCtrl.AcceptClient(_roomId, out var session);
      await foreach (var entry in session.ReadMessagesAsync(_httpRequest, _ct))
        await _httpRequest.HttpContext.Response.WriteSseMsgAsync(entry, _ct);
    }
    catch (OperationCanceledException)
    { /* we don't care if user disconnects */ }

    return Results.Empty;
  }

  [ApiTokenRequired]
  public IResult RegisterRoom(
    IRoomsController _roomsController,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromBody] RoomInfo _req)
  {
    _log.Info($"Requested to **register room** __'{_req.RoomId}'__");

    _roomsController.RegisterRoom(_req);
    return _reqToolkit.Ok();
  }

  [ApiTokenRequired]
  public IResult DeleteRoomRegistration(
    IRoomsController _roomsController,
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromBody] DeleteRoomReq _req)
  {
    _log.Info($"Requested to **unregister room** __'{_req.RoomId}'__");

    _roomsController.UnregisterRoom(_req.RoomId);
    return _reqToolkit.Ok();
  }

  [ApiTokenRequired]
  public IResult ListRooms(
    IRoomsController _roomsController,
    IScopedLog _log,
    IRequestToolkit _reqToolkit)
  {
    _log.Info($"Requested to **list rooms**");

    var users = _roomsController.ListRegisteredRooms();
    return Results.Json(users, RestJsonCtx.Default.IReadOnlyListRoomInfo);
  }

}
