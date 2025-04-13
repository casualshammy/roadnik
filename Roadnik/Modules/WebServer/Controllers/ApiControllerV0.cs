using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Data;
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
using Roadnik.Server.Data;
using Roadnik.Server.Data.WebServer;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using static Roadnik.Server.Data.Consts;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class ApiControllerV0 : GenericController
{
  private static long p_wsSessionsCount = 0;
  private static long p_reqCount = -1;

  private readonly IAppConfig p_appConfig;
  private readonly IDbProvider p_documentStorage;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fcmPublisher;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly ILog p_log;

  public ApiControllerV0(
    IAppConfig _appConfig,
    IDbProvider _documentStorage,
    ILog _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _usersController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fcmPublisher,
    IHttpClientProvider _httpClientProvider) : base("/", RestJsonCtx.Default)
  {
    p_appConfig = _appConfig;
    p_documentStorage = _documentStorage;
    p_log = _logger;
    p_webSocketCtrl = _webSocketCtrl;
    p_roomsController = _usersController;
    p_tilesCache = _tilesCache;
    p_reqRateLimiter = _reqRateLimiter;
    p_fcmPublisher = _fcmPublisher;
    p_httpClientProvider = _httpClientProvider;
  }

  public override void RegisterPaths(WebApplication _app)
  {
    _app.MapMethods("/r/", ["HEAD"], () => Results.Ok());
    _app.MapGet(ReqPaths.GET_VERSION, GetVersion);
    _app.MapGet("/", GetIndexFile);
    _app.MapGet("/r/{**path}", GetRoomStaticFile);
    _app.MapGet("{**path}", GetStaticFile);
    _app.MapGet("/ping", () => Results.Ok());
    _app.MapGet("/map-tile", GetMapTileAsync);
    _app.MapGet(ReqPaths.STORE_PATH_POINT, StoreRoomPointGetAsync);
    _app.MapPost(ReqPaths.STORE_PATH_POINT, StorePathPointAsync);
    _app.MapGet(ReqPaths.GET_ROOM_PATHS, ListRoomPathPoints);
    _app.MapPost(ReqPaths.CREATE_NEW_POINT, CreateRoomPointAsync);
    _app.MapGet(ReqPaths.LIST_ROOM_POINTS, ListRoomPoints);
    _app.MapPost(ReqPaths.DELETE_ROOM_POINT, DeleteRoomPointAsync);
    _app.MapGet(ReqPaths.GET_FREE_ROOM_ID, GetFreeRoomId);
    _app.MapGet(ReqPaths.IS_ROOM_ID_VALID, IsRoomIdValid);
    _app.MapGet("/ws", ConnectToWsAsync);
    _app.MapPost(ReqPaths.REGISTER_ROOM, RegisterRoom);
    _app.MapPost(ReqPaths.UNREGISTER_ROOM, DeleteRoomRegistration);
    _app.MapGet(ReqPaths.LIST_REGISTERED_ROOMS, ListRooms);
  }

  public IResult GetVersion(HttpContext _httpContext)
  {
    var log = GetLog(_httpContext.Request);
    try
    {
      log.Info($"Requested **version**");
      return Json(Consts.AppVersion);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'version' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public IResult GetIndexFile(HttpRequest _httpRequest)
    => GetStaticFile(_httpRequest, "/");

  public IResult GetRoomStaticFile(
    HttpRequest _httpRequest,
    [FromRoute(Name = "path")] string? _path)
  {
    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    return GetStaticFile(_httpRequest, $"room/{_path}");
  }

  [FailToBan]
  public IResult GetStaticFile(
    HttpRequest _httpRequest,
    [FromRoute(Name = "path")] string _path)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested **static path** __{_path}__");

      if (string.IsNullOrWhiteSpace(_path) || _path == "/")
        _path = "index.html";

      var path = Path.Combine(p_appConfig.WebrootDirPath, _path);
      if (!File.Exists(path))
      {
        log.Warn($"File '{_path}' is not found");
        return NotFound();
      }

      if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
      {
        log.Warn($"Tried to get file not from webroot: '{_path}'");
        return Forbidden(string.Empty);
      }

      var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
      var stream = File.OpenRead(path);

      log.Info($"**Handled** request of **static path** __{_path}__");
      return Results.Stream(stream, mime);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'static path {_path}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public async Task<IResult> GetMapTileAsync(
    HttpContext _httpCtx,
    [FromQuery(Name = "x")] int? _x,
    [FromQuery(Name = "y")] int? _y,
    [FromQuery(Name = "z")] int? _z,
    [FromQuery(Name = "type")] string? _mapType,
    CancellationToken _ct)
  {
    var log = GetLog(_httpCtx.Request);
    try
    {
      log.Info($"Requested **map tile** __{_mapType}/{_z}/{_x}/{_y}__");

      if (_x == null || _y == null || _z == null || _mapType == null)
      {
        log.Warn($"Incorrect query!");
        return BadRequest($"Incorrect query!");
      }

      if (p_tilesCache.TryGet(_x.Value, _y.Value, _z.Value, _mapType, out var cachedStream, out var hash))
      {
        log.Info($"**Handled** request of **map tile** __{_mapType}/{_z}/{_x}/{_y}__ (**cached**)");
        _httpCtx.Response.Headers.Append(CustomHeaders.XRoadnikCachedTile, hash);
        return Results.Stream(cachedStream, MimeMapping.KnownMimeTypes.Png);
      }

      var tfApiKey = p_appConfig.ThunderforestApiKey;
      var tfApiKeyParam = tfApiKey.IsNullOrWhiteSpace() ? string.Empty : $"?apikey={tfApiKey}";
      var url = _mapType switch
      {
        TILE_TYPE_OPENCYCLEMAP => $"https://tile.thunderforest.com/cycle/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
        TILE_TYPE_TF_OUTDOORS => $"https://tile.thunderforest.com/outdoors/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
        TILE_TYPE_TF_TRANSPORT => $"https://tile.thunderforest.com/transport/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
        TILE_TYPE_STRAVA_HEATMAP_RIDE => $"https://strava-heatmap.tiles.freemap.sk/ride/red/{_z}/{_x}/{_y}.jpg",
        TILE_TYPE_STRAVA_HEATMAP_RUN => $"https://strava-heatmap.tiles.freemap.sk/run/blue/{_z}/{_x}/{_y}.jpg",
        TILE_TYPE_CARTO_DARK => $"https://basemaps.cartocdn.com/dark_all/{_z}/{_x}/{_y}.png",
        _ => null
      };

      if (url == null)
      {
        log.Warn($"Map type is not available: '{_mapType}'");
        return BadRequest($"Map type is not available: '{_mapType}'");
      }

      var mapCacheSize = p_appConfig.MapTilesCacheSize;
      if (mapCacheSize != null && mapCacheSize.Value > 0)
        p_tilesCache.EnqueueUrl(_x.Value, _y.Value, _z.Value, _mapType, url);

      try
      {
        using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
        using var httpRes = await p_httpClientProvider.Value.SendAsync(httpReq, _ct);
        httpRes.EnsureSuccessStatusCode();

        var imageBytes = await httpRes.Content.ReadAsByteArrayAsync(_ct);

        log.Info($"**Handled** request of **map tile** __{_mapType}/{_z}/{_x}/{_y}__ (**live**)");
        return Results.Bytes(imageBytes, httpRes.Content.Headers.ContentType?.ToString());
      }
      catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.NotFound)
      {
        log.Warn($"Tile not found");
        return NotFound();
      }
      catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.Unauthorized)
      {
        log.Warn($"Can't download map tile (unauthorized)");
        return Results.Unauthorized();
      }
      catch (OperationCanceledException)
      {
        log.Warn($"Operation cancelled");
        return Results.NoContent();
      }
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'map tile {_mapType}/{_z}/{_x}/{_y}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  [Obsolete]
  public async Task<IResult> StoreRoomPointGetAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId,
    [FromQuery(Name = "username")] string? _username,
    [FromQuery(Name = "lat")] float? _lat,
    [FromQuery(Name = "lng")] float? _lng,
    [FromQuery(Name = "alt")] float? _alt, // metres
    [FromQuery(Name = "speed")] float? _speed, // m/s
    [FromQuery(Name = "acc")] float? _acc, // metres
    [FromQuery(Name = "battery")] float? _battery, // %
    [FromQuery(Name = "gsmSignal")] float? _gsmSignal, // %
    [FromQuery(Name = "bearing")] float? _bearing, // grad
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return Results.BadRequest("Room Id is incorrect!");
    if (_lat == null)
      return Results.BadRequest("Latitude is null!");
    if (_lng == null)
      return Results.BadRequest("Longitude is null!");
    if (_alt == null)
      return Results.BadRequest("Altitude is null!");
    if (!ReqResUtil.IsUsernameSafe(_username))
      return Results.BadRequest("Username is incorrect!");

    var log = GetLog(_httpRequest);
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;

    try
    {
      var savePointResult = await p_roomsController.SaveNewPathPointAsync(
        log,
        ip,
        _roomId,
        _username,
        int.MinValue,
        false,
        _lat.Value,
        _lng.Value,
        _alt.Value,
        _acc,
        _speed,
        _battery,
        _gsmSignal,
        _bearing,
        _ct);

      if (savePointResult.ErrorCode != null)
        return Results.StatusCode(statusCode: (int)savePointResult.ErrorCode.Value);

      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to save path point: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public async Task<IResult> StorePathPointAsync(
    HttpRequest _httpRequest,
    [FromBody] StorePathPointReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **store path point** of __{_req.RoomId}/{_req.Username}__");

      var savePointResult = await p_roomsController.SaveNewPathPointAsync(
        log,
        _httpRequest.HttpContext.Connection.RemoteIpAddress,
        _req.RoomId,
        _req.Username,
        _req.SessionId,
        _req.WipeOldPath ?? false,
        _req.Lat,
        _req.Lng,
        _req.Alt,
        _req.Acc,
        _req.Speed,
        _req.Battery,
        _req.GsmSignal,
        _req.Bearing,
        _ct);

      if (savePointResult.ErrorCode != null)
        return Problem(savePointResult.ErrorCode.Value, savePointResult.ErrorMsg);

      log.Info($"**Handled** request to **store path point** of __{_req.RoomId}/{_req.Username}__");
      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'store path point of {_req.RoomId}/{_req.Username}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public IResult ListRoomPathPoints(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId,
    [FromQuery(Name = "offset")] long _offsetUnixTimeMs)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested **room path points** of __{_roomId}__ from timestamp __{_offsetUnixTimeMs}__");

      if (!ReqResUtil.IsRoomIdValid(_roomId))
      {
        log.Info("Room Id is incorrect!");
        return BadRequest("Room Id is incorrect!");
      }

      var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
      if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_PATH_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      {
        log.Warn($"Too many requests from ip '{ip}'");
        return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
      }

      var now = DateTimeOffset.UtcNow;

      const int maxReturnEntries = 250;
      var offset = DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs + 1);
      var documents = p_documentStorage.Paths
        .ListDocuments<StorageEntry>(_roomId, _from: offset)
        .OrderBy(_ => _.Created)
        .Take(maxReturnEntries + 1)
        .ToArray();

      ListRoomPathPointsRes result;
      if (documents.Length == 0)
      {
        result = new(now.ToUnixTimeMilliseconds(), false, []);
      }
      else if (documents.Length <= maxReturnEntries)
      {
        var entries = documents.Select(TimedStorageEntry.FromStorageEntry);
        result = new(now.ToUnixTimeMilliseconds(), false, entries);
      }
      else
      {
        var lastEntryTime = documents[^2].Created.ToUnixTimeMilliseconds();
        var entries = documents
          .Take(maxReturnEntries)
          .Select(TimedStorageEntry.FromStorageEntry);

        result = new(lastEntryTime, true, entries);
      }

      log.Info($"**Handled** request **room path points** of __{_roomId}__ from timestamp __{_offsetUnixTimeMs}__");
      return Json(result);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'room path points of {_roomId} from timestamp {_offsetUnixTimeMs}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public async Task<IResult> CreateRoomPointAsync(
    HttpRequest _httpRequest,
    [FromBody] CreateRoomPointReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **create room point** in __{_req.RoomId}__ from user __{_req.Username}__, coords: __{_req.Lat}; {_req.Lng}__");

      if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      {
        log.Warn($"Incorrect room id!");
        return BadRequest($"Incorrect room id!");
      }
      if (!ReqResUtil.IsUserDefinedStringSafe(_req.Username))
      {
        log.Warn($"Incorrect username!");
        return BadRequest($"Incorrect username!");
      }

      var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
      if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.CREATE_ROOM_POINT, ip, () => new TimeWall(10, TimeSpan.FromSeconds(10))))
      {
        log.Warn($"Too many requests from ip '{ip}'");
        return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
      }

      var description = ReqResUtil.ClearUserMsg(_req.Description);
      var now = DateTimeOffset.UtcNow;
      var point = new RoomPointDocument(_req.RoomId, _req.Username, _req.Lat, _req.Lng, description);
      p_documentStorage.GenericData.WriteSimpleDocument($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point);

      await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

      var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgRoomPointAdded(_req.Username, _req.Description, _req.Lat, _req.Lng), AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);
      var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, pushMsgData);
      await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

      log.Info($"**Handled** request to **create room point** in __{_req.RoomId}__ from user __{_req.Username}__, coords: __{_req.Lat}; {_req.Lng}__");
      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'create room point in {_req.RoomId} from user {_req.Username}, coords: {_req.Lat}; {_req.Lng}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public IResult ListRoomPoints(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested **list of room points**, room id: __{_roomId}__");

      if (!ReqResUtil.IsRoomIdValid(_roomId))
      {
        log.Warn($"Incorrect room id: __'{_roomId}'__");
        return BadRequest($"Incorrect room id: '{_roomId}'!");
      }

      var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
      if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      {
        log.Warn($"Too many requests from ip '{ip}'");
        return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
      }

      var entries = p_documentStorage.GenericData.ListSimpleDocuments<RoomPointDocument>(new LikeExpr($"{_roomId}.%"))
        .Select(RoomPoint.From)
        .ToArray();

      log.Info($"**Handled** request of **list of room points**, room id: __{_roomId}__");
      return Json(new ListRoomPointsRes(entries));
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'list of room points, room id: {_roomId}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public async Task<IResult> DeleteRoomPointAsync(
    HttpRequest _httpRequest,
    [FromBody] DeleteRoomPointReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **delete room point** __{_req.PointId}__ from room __{_req.RoomId}__");

      if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      {
        log.Warn($"Incorrect room id!");
        return BadRequest($"Incorrect room id!");
      }

      var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
      if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.DELETE_ROOM_POINT, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
      {
        log.Warn($"Too many requests from ip '{ip}'");
        return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
      }

      var deleted = false;
      foreach (var entry in p_documentStorage.GenericData.ListSimpleDocuments<RoomPointDocument>(new LikeExpr($"{_req.RoomId}.%")))
        if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
        {
          p_documentStorage.GenericData.DeleteSimpleDocument<RoomPointDocument>(entry.Key);

          var now = DateTimeOffset.UtcNow;
          await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

          deleted = true;
          break;
        }

      if (deleted)
        log.Info($"**Handled** request to **delete room point** __{_req.PointId}__ from room __{_req.RoomId}__");
      else
        log.Warn($"Handled request to delete room point {_req.PointId} from room {_req.RoomId} (not found)");

      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'delete room point {_req.PointId} from room {_req.RoomId}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public IResult GetFreeRoomId(
    HttpRequest _httpRequest,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested **free room id**");

      var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
      if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.GET_FREE_ROOM_ID, ip, () => new TimeWall(10, TimeSpan.FromSeconds(60))))
      {
        log.Warn($"Too many requests from ip '{ip}'");
        return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
      }

      using var timedCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct, timedCts.Token);
      string? roomId = null;
      var roomIdValid = false;
      while (!cts.IsCancellationRequested && !roomIdValid)
      {
        roomId = Utilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false);
        roomIdValid = !p_documentStorage.Paths
          .ListDocumentsMeta(roomId)
          .Any();
      }

      if (!roomIdValid || roomId.IsNullOrEmpty())
      {
        log.Error($"Can't find free room id (last: '{roomId}')!");
        return InternalServerError("Can't find free room id!");
      }

      log.Info($"**Handled** request **free room id** (__{roomId}__)");
      return Results.Content(roomId, MimeTypes.Text, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'free room id' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public IResult IsRoomIdValid(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **check** if __'{_roomId}'__ is **valid** room id");

      var valid = ReqResUtil.IsRoomIdValid(_roomId);

      log.Info($"**Handled** request to **check** if __'{_roomId}'__ is **valid** room id ({(valid ? "valid" : "not valid")})");
      return valid
        ? Results.Ok()
        : Results.StatusCode((int)HttpStatusCode.NotAcceptable);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'check if '{_roomId}' is valid room id' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  public async Task<IResult> ConnectToWsAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string _roomId,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **connect to ws** of room __'{_roomId}'__");

      if (!ReqResUtil.IsRoomIdValid(_roomId))
      {
        log.Warn("Room Id is incorrect!");
        return BadRequest("Room Id is incorrect!");
      }
      if (!_httpRequest.HttpContext.WebSockets.IsWebSocketRequest)
      {
        log.Warn($"Expected web socket request");
        return BadRequest($"Expected web socket request");
      }

      var sessionIndex = Interlocked.Increment(ref p_wsSessionsCount);
      log.Info($"**Establishing ws connection** '__{sessionIndex}__' for room '__{_roomId}__'...");

      var roomInfo = p_roomsController.GetRoom(_roomId);
      var maxPointsInRoom = roomInfo?.MaxPathPoints ?? p_appConfig.MaxPathPointsPerRoom;

      using var websocket = await _httpRequest.HttpContext.WebSockets.AcceptWebSocketAsync();
      _ = await p_webSocketCtrl.AcceptSocketAsync(websocket, _roomId, maxPointsInRoom);
      log.Info($"**Ws connection** '__{sessionIndex}__' for room '__{_roomId}__' is **closed**");

      return Results.Empty;
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'connect to ws of room '{_roomId}'' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  [ApiTokenRequiredAttribute]
  public IResult RegisterRoom(
    HttpRequest _httpRequest,
    [FromBody] RoomInfo _req)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **register room** __'{_req.RoomId}'__");

      p_roomsController.RegisterRoom(_req);

      log.Info($"**Handled** request to **register room** __'{_req.RoomId}'__");
      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'register room '{_req.RoomId}'' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  [ApiTokenRequiredAttribute]
  public IResult DeleteRoomRegistration(
    HttpRequest _httpRequest,
    [FromBody] DeleteRoomReq _req)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **unregister room** __'{_req.RoomId}'__");

      p_roomsController.UnregisterRoom(_req.RoomId);

      log.Info($"**Handled** request to **unregister room** __'{_req.RoomId}'__");
      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'unregister room '{_req.RoomId}'' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  [ApiTokenRequiredAttribute]
  public IResult ListRooms(
    HttpRequest _httpRequest)
  {
    var log = GetLog(_httpRequest);
    try
    {
      log.Info($"Requested to **list rooms**");

      var users = p_roomsController.ListRegisteredRooms();

      log.Info($"**Handled** request to **list rooms**");
      return Results.Json(users, RestJsonCtx.Default.IReadOnlyListRoomInfo);
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to handle 'list rooms' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

  private ILog GetLog(HttpRequest _httpRequest)
  {
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    return p_log[Interlocked.Increment(ref p_reqCount).ToString()][$"{ip}"];
  }

}
