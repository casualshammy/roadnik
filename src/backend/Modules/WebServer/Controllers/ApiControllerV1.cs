using Ax.Fw;
using Ax.Fw.App.Interfaces;
using Ax.Fw.Extensions;
using Ax.Fw.Storage.Data;
using Ax.Fw.Web.Data;
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
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using static Roadnik.Server.Data.Consts;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class ApiControllerV1
{
  private static long p_wsSessionsCount = 0;

  private readonly IAppConfig p_appConfig;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IHttpClientProvider p_httpClientProvider;

  public ApiControllerV1(
    IAppConfig _appConfig,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _usersController,
    IReqRateLimiter _reqRateLimiter,
    IHttpClientProvider _httpClientProvider)
  {
    p_appConfig = _appConfig;
    p_webSocketCtrl = _webSocketCtrl;
    p_roomsController = _usersController;
    p_reqRateLimiter = _reqRateLimiter;
    p_httpClientProvider = _httpClientProvider;
  }

  public void RegisterPaths(WebApplication _app)
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
    apiGroup.MapGet("/ws", ConnectToWsAsync).WithMetadata(ctrlInfo);
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
        return Results.Stream(cachedStream, MimeTypes.Png.Mime);
      }
    }

    var tfApiKey = p_appConfig.ThunderforestApiKey;
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

      using var httpRes = await p_httpClientProvider.HttpClient.SendAsync(httpReq, _ct);
      httpRes.EnsureSuccessStatusCode();

      var imageBytes = await httpRes.Content.ReadAsByteArrayAsync(_ct);

      var mapCacheSize = p_appConfig.MapTilesCacheSize;
      if (mapCacheSize != null && mapCacheSize.Value > 0)
        await _dbProvider.Tiles.WriteBlobAsync(_mapType, cacheKey, imageBytes, _ct);

      _log.Info($"**Handled** request of **map tile** __{_mapType}/{_z}/{_x}/{_y}__ (**live**)");

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
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    IFCMPublisher _fcmPublisher,
    HttpRequest _httpRequest,
    [FromBody] StorePathPointReq _req,
    CancellationToken _ct)
  {
    _log.Info($"Got request to **store path point**: '__{_req.RoomId}__/**{_req.AppId}**/__{_req.Username}__'");

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return _reqToolkit.BadRequest("Room Id is incorrect!");
    if (!ReqResUtil.IsUsernameSafe(_req.Username))
      return _reqToolkit.BadRequest("Username is incorrect!");

    var room = p_roomsController.GetRoom(_req.RoomId);
    var maxPathPoints = room?.MaxPathPoints ?? p_appConfig.MaxPathPointsPerRoom;
    if (maxPathPoints == 0)
      return _reqToolkit.Forbidden("Publishing is forbidden!");

    var minInterval = room?.MinPathPointIntervalMs ?? p_appConfig.MinPathPointIntervalMs;
    if (!p_reqRateLimiter.IsReqOk($"{ReqPaths.STORE_PATH_POINT}/{_req.RoomId}", _httpRequest.HttpContext.Connection.RemoteIpAddress, minInterval))
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
        p_roomsController.EnqueueUserWipe(_req.RoomId, _req.AppId, _req.Username, nowUnixMs);

      var pushMsgPayload = new PushMsgNewTrackStarted(GenericToolkit.ConcealAppInstanceId(_req.AppId), _req.Username);
      var pushMsgData = JsonSerializer.SerializeToElement(pushMsgPayload, AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
      var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, pushMsgData);
      await _fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);
    }

    var record = StorageEntry.From(_req);
    _dbProvider.Paths.WriteDocument(_req.RoomId, $"{record.AppId}.{nowUnixMs}", record);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgUpdateAvailable(nowUnixMs), _ct);

    if (room?.MaxPointsPerPath > 0)
      p_roomsController.EnqueuePathTruncate(_req.RoomId, _req.AppId, _req.Username);

    return Results.Ok();
  }

  public IResult ListRoomPathPoints(
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
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_PATH_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
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
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    IFCMPublisher _fcmPublisher,
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
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.CREATE_ROOM_POINT, ip, () => new TimeWall(10, TimeSpan.FromSeconds(10))))
    {
      _log.Warn($"Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var description = ReqResUtil.ClearUserMsg(_req.Description);
    var now = DateTimeOffset.UtcNow;
    var point = new RoomPointDocument(_req.AppId, _req.RoomId, _req.Username, _req.Lat, _req.Lng, description);
    _dbProvider.GenericData.WriteSimpleDocument($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    var pushMsgData = JsonSerializer.SerializeToElement(
      new PushMsgRoomPointAdded(_req.AppId, _req.Username, _req.Description, _req.Lat, _req.Lng),
      AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);

    var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, pushMsgData);
    await _fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Results.Ok();
  }

  public IResult ListRoomPoints(
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
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
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
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    HttpRequest _httpRequest,
    [FromBody] DeleteRoomPointReq _req,
    CancellationToken _ct)
  {
    _log.Info($"Requested to **delete room point** __{_req.PointId}__ from room __{_req.RoomId}__");

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return _reqToolkit.BadRequest($"Incorrect room id!");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.DELETE_ROOM_POINT, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);

    foreach (var entry in _dbProvider.GenericData.ListSimpleDocuments<RoomPointDocument>(new LikeExpr($"{_req.RoomId}.%")))
      if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
      {
        _dbProvider.GenericData.DeleteSimpleDocument<RoomPointDocument>(entry.Key);

        var now = DateTimeOffset.UtcNow;
        await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);
        break;
      }

    return Results.Ok();
  }

  public IResult GetFreeRoomId(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    IDbProvider _dbProvider,
    HttpRequest _httpRequest,
    CancellationToken _ct)
  {
    _log.Info($"Requested **free room id**");

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.GET_FREE_ROOM_ID, ip, () => new TimeWall(10, TimeSpan.FromSeconds(60))))
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

  public async Task<IResult> ConnectToWsAsync(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId,
    CancellationToken _ct)
  {
    _log.Info($"Requested to **connect to ws** of room __'{_roomId}'__");

    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return _reqToolkit.BadRequest("Room Id is incorrect!");
    if (!_httpRequest.HttpContext.WebSockets.IsWebSocketRequest)
      return _reqToolkit.BadRequest($"Expected web socket request");

    var sessionIndex = Interlocked.Increment(ref p_wsSessionsCount);
    _log.Info($"**Establishing ws connection** '__{sessionIndex}__' for room '__{_roomId}__'...");

    using var websocket = await _httpRequest.HttpContext.WebSockets.AcceptWebSocketAsync();
    _ = await p_webSocketCtrl.AcceptSocketAsync(websocket, _roomId);
    _log.Info($"**Ws connection** '__{sessionIndex}__' for room '__{_roomId}__' is **closed**");

    return Results.Empty;
  }

  [ApiTokenRequired]
  public IResult RegisterRoom(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromBody] RoomInfo _req)
  {
    _log.Info($"Requested to **register room** __'{_req.RoomId}'__");

    p_roomsController.RegisterRoom(_req);
    return _reqToolkit.Ok();
  }

  [ApiTokenRequired]
  public IResult DeleteRoomRegistration(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromBody] DeleteRoomReq _req)
  {
    _log.Info($"Requested to **unregister room** __'{_req.RoomId}'__");

    p_roomsController.UnregisterRoom(_req.RoomId);
    return _reqToolkit.Ok();
  }

  [ApiTokenRequired]
  public IResult ListRooms(
    IScopedLog _log,
    IRequestToolkit _reqToolkit)
  {
    _log.Info($"Requested to **list rooms**");

    var users = p_roomsController.ListRegisteredRooms();
    return Results.Json(users, RestJsonCtx.Default.IReadOnlyListRoomInfo);
  }

}
