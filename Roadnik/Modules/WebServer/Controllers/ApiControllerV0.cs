using Ax.Fw;
using Ax.Fw.Cache;
using Ax.Fw.Extensions;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Roadnik.Common.ReqRes;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Serializers;
using Roadnik.Common.Toolkit;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Data.DbTypes;
using Roadnik.Server.Data.ReqRes;
using Roadnik.Server.Data.WebServer;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using Roadnik.Server.Toolkit;
using System.Collections.Frozen;
using System.Net;
using System.Text;
using System.Text.Json;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class ApiControllerV0 : GenericController
{
  private static readonly FrozenSet<string> p_authRequiredPaths = new string[] {
    ReqPaths.REGISTER_ROOM,
    ReqPaths.UNREGISTER_ROOM,
    ReqPaths.LIST_REGISTERED_ROOMS }
    .Select(_ => _.Trim('/', ' '))
    .ToFrozenSet();

  private static long p_wsSessionsCount = 0;
  private static long p_reqCount = -1;

  private readonly ISettingsController p_settingsCtrl;
  private readonly IDbProvider p_documentStorage;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fcmPublisher;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly ILog p_log;

  public ApiControllerV0(
    ISettingsController _settingsCtrl,
    IDbProvider _documentStorage,
    ILog _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _usersController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fcmPublisher,
    IHttpClientProvider _httpClientProvider) : base("/", ControllersJsonCtx.Default)
  {
    p_settingsCtrl = _settingsCtrl;
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
    _app.MapGet("/", GetIndexFile);
    _app.MapGet("{**path}", GetStaticFile);
    _app.MapGet("/ping", () => Results.Ok());
    _app.MapGet("/r/{**path}", GetRoom);
    _app.MapGet("/map-tile", GetMapTileAsync);
    _app.MapGet(ReqPaths.STORE_PATH_POINT, StoreRoomPointGetAsync);
    _app.MapPost(ReqPaths.STORE_PATH_POINT, StoreRoomPointPostAsync);
    _app.MapGet(ReqPaths.GET_ROOM_PATHS, GetRoomPaths);
    _app.MapPost(ReqPaths.CREATE_NEW_POINT, CreateNewPointAsync);
    _app.MapGet(ReqPaths.LIST_ROOM_POINTS, GetRoomPoints);
    _app.MapPost(ReqPaths.DELETE_ROOM_POINT, DeleteRoomPointAsync);
    _app.MapGet(ReqPaths.GET_FREE_ROOM_ID, GetFreeRoomId);
    _app.MapPost(ReqPaths.UPLOAD_LOG, UploadLogAsync);
    _app.MapGet(ReqPaths.IS_ROOM_ID_VALID, IsRoomIdValid);
    _app.MapGet("/ws", StartWebSocketAsync);
    _app.MapPost(ReqPaths.REGISTER_ROOM, RegisterRoom);
    _app.MapPost(ReqPaths.UNREGISTER_ROOM, DeleteRoomRegistration);
    _app.MapGet(ReqPaths.LIST_REGISTERED_ROOMS, ListRooms);
    _app.MapGet(ReqPaths.IS_UDP_AVAILABLE, IsUdpTransportAvailableAsync);
  }

  public override Task<bool> AuthAsync(HttpRequest _req, CancellationToken _ct)
  {
    var path = _req.Path.ToString().Trim('/', ' ');
    if (!p_authRequiredPaths.Contains(path))
      return Task.FromResult(true);

    var apiKeyStr = _req.Headers["api-key"].FirstOrDefault();
    if (apiKeyStr.IsNullOrWhiteSpace())
      return Task.FromResult(false);

    var adminApiKey = p_settingsCtrl.Settings.Value?.AdminApiKey;
    if (adminApiKey.IsNullOrWhiteSpace())
      return Task.FromResult(false);

    return Task.FromResult(adminApiKey == apiKeyStr);
  }

  //[HttpGet("/")]
  public IResult GetIndexFile(HttpRequest _httpRequest) => GetStaticFile(_httpRequest, "/");

  //[HttpGet("{**path}")]
  public IResult GetStaticFile(
    HttpRequest _httpRequest,
    [FromRoute(Name = "path")] string _path)
  {
    var log = GetLog(_httpRequest);
    log.Info($"Requested **static path** __{_path}__");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var webroot = p_settingsCtrl.Settings.Value?.WebrootDirPath;
    if (webroot.IsNullOrWhiteSpace())
      return Results.Problem($"Webroot dir is misconfigured", statusCode: 502);

    var path = Path.Combine(webroot, _path);
    if (!File.Exists(path))
    {
      log.Warn($"File '{_path}' is not found");
      return Results.NotFound();
    }

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
    {
      log.Error($"Tried to get file not from webroot: '{_path}'");
      return Results.StatusCode(403);
    }

    var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
    var stream = File.OpenRead(path);
    return Results.Stream(stream, mime);
  }

  //[HttpGet("/r/{**path}")]
  public IResult GetRoom(
    HttpRequest _httpRequest,
    [FromRoute(Name = "path")] string? _path)
  {
    var log = GetLog(_httpRequest);
    log.Info($"Requested static path '/r/{_path}'");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var webroot = p_settingsCtrl.Settings.Value?.WebrootDirPath;
    if (webroot.IsNullOrWhiteSpace())
      return Results.Problem($"Webroot dir is misconfigured", statusCode: 502);

    var path = Path.Combine(webroot, "room", _path);
    if (!File.Exists(path))
    {
      log.Warn($"File '{path}' is not found");
      return Results.NotFound();
    }

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
    {
      log.Error($"Tried to get file not from webroot: '{path}'");
      return Results.StatusCode(statusCode: 403);
    }

    var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
    var stream = File.OpenRead(path);
    return Results.Stream(stream, mime);
  }

  //[HttpGet("/map-tile")]
  public async Task<IResult> GetMapTileAsync(
    HttpContext _httpCtx,
    [FromQuery(Name = "x")] int? _x,
    [FromQuery(Name = "y")] int? _y,
    [FromQuery(Name = "z")] int? _z,
    [FromQuery(Name = "type")] string? _mapType,
    CancellationToken _ct)
  {
    if (_x is null)
      return Results.BadRequest("X is null!");
    if (_y is null)
      return Results.BadRequest("Y is null!");
    if (_z is null)
      return Results.BadRequest("Z is null!");

    if (_mapType == null)
      return BadRequest($"Unknown map type: '{_mapType}'");

    var log = GetLog(_httpCtx.Request);

    if (p_tilesCache.TryGet(_x.Value, _y.Value, _z.Value, _mapType, out var cachedStream, out var hash))
    {
      log.Info($"Sending **cached map tile** __{_mapType}__/{_z}/{_x}/{_y}");
      _httpCtx.Response.Headers.Append(CustomHeaders.XRoadnikCachedTile, hash);
      return Results.Stream(cachedStream, MimeMapping.KnownMimeTypes.Png);
    }

    log.Info($"Sending **map tile** __{_mapType}__/{_z}/{_x}/{_y}...");

    var tfApiKey = p_settingsCtrl.Settings.Value?.ThunderforestApiKey;
    var tfApiKeyParam = tfApiKey.IsNullOrWhiteSpace() ? string.Empty : $"?apikey={tfApiKey}";

    var url = _mapType switch
    {
      Consts.TILE_TYPE_OPENCYCLEMAP => $"https://tile.thunderforest.com/cycle/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
      Consts.TILE_TYPE_TF_OUTDOORS => $"https://tile.thunderforest.com/outdoors/{_z}/{_x}/{_y}.png{tfApiKeyParam}",
      Consts.TILE_TYPE_STRAVA_HEATMAP_RIDE => $"https://proxy.nakarte.me/https/heatmap-external-a.strava.com/tiles-auth/ride/hot/{_z}/{_x}/{_y}.png?px=256",
      Consts.TILE_TYPE_STRAVA_HEATMAP_RUN => $"https://proxy.nakarte.me/https/heatmap-external-a.strava.com/tiles-auth/run/hot/{_z}/{_x}/{_y}.png?px=256",
      Consts.TILE_TYPE_CARTO_DARK => $"https://basemaps.cartocdn.com/dark_all/{_z}/{_x}/{_y}.png",
      _ => null
    };

    if (url == null)
    {
      log.Warn($"Map type is not available: '{_mapType}'");
      return BadRequest($"Map type is not available: '{_mapType}'");
    }

    var mapCacheSize = p_settingsCtrl.Settings.Value?.MapTilesCacheSize;
    if (mapCacheSize != null && mapCacheSize.Value > 0)
      p_tilesCache.EnqueueUrl(_x.Value, _y.Value, _z.Value, _mapType, url);

    try
    {
      var stream = await p_httpClientProvider.Value.GetStreamAsync(url, _ct);
      return Results.Stream(stream, MimeMapping.KnownMimeTypes.Png);
    }
    catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.NotFound)
    {
      log.Warn($"Tile not found: x:{_x.Value}; y:{_y.Value}; z:{_z.Value}; t:{_mapType}");
      return Results.NotFound();
    }
    catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.Unauthorized)
    {
      log.Warn($"Can't download map tile - Unauthorized: x:{_x.Value}; y:{_y.Value}; z:{_z.Value}; t:{_mapType}");
      return Results.Unauthorized();
    }
    catch (OperationCanceledException)
    {
      return Results.NoContent();
    }
    catch (Exception ex)
    {
      log.Error($"Can't download map tile x:{_x.Value}; y:{_y.Value}; z:{_z.Value}; t:{_mapType}", ex);
      return InternalServerError();
    }
  }

  //[HttpGet(ReqPaths.STORE_PATH_POINT)]
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

    log.Info($"Requested to store geo data, room: '{_roomId}'");

    var room = p_roomsController.GetRoom(_roomId);
    var maxPathPoints = room?.MaxPathPoints ?? p_settingsCtrl.Settings.Value?.MaxPathPointsPerRoom;
    if (maxPathPoints != null && maxPathPoints == 0)
      return Forbidden("Publishing is forbidden!");

    var minInterval = room?.MinPathPointIntervalMs ?? p_settingsCtrl.Settings.Value?.MinPathPointIntervalMs ?? 0u;

    var compositeKey = $"{ReqPaths.GET_ROOM_PATHS}/{_roomId}/{_username}";
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(compositeKey, ip, minInterval))
    {
      log.Warn($"[{ReqPaths.GET_ROOM_PATHS}] Too many requests, room '{_roomId}', username: '{_username}', time limit: '{minInterval} ms'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    var record = new StorageEntry(
      _username ?? _roomId,
      _lat.Value,
      _lng.Value,
      _alt.Value,
      _speed,
      _acc,
      (_battery ?? 0) / 100,
      (_gsmSignal ?? 0) / 100,
      _bearing);
    p_documentStorage.Paths.WriteDocument(_roomId, now.ToUnixTimeMilliseconds().ToString(), record);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_roomId, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

    if (room?.MaxPointsPerPath > 0)
      p_roomsController.EnqueuePathTruncate(_roomId, _username ?? _roomId);

    return Results.Ok();
  }

  //[HttpPost(ReqPaths.STORE_PATH_POINT)]
  public async Task<IResult> StoreRoomPointPostAsync(
    HttpRequest _httpRequest,
    [FromBody] StorePathPointReq _req,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return BadRequest("Room Id is incorrect!");
    if (!ReqResUtil.IsUsernameSafe(_req.Username))
      return BadRequest("Username is incorrect!");

    var log = GetLog(_httpRequest);
    log.Info($"Requested to store geo data, room: '{_req.RoomId}'");

    //var udpPayload = StoreLocationUdpMsg.FromStorePathPointReq(_req);
    //var udpMsg = new GenericUdpMsg(0, udpPayload.ToByteArray());
    //var udpMsgBytes = udpMsg.ToByteArray();
    //var privateKey = File.ReadAllText(p_settingsCtrl.Settings.Value!.UdpTransportPrivateKeyPath!);
    //using var rsaAes = new RsaAesGcm(null, privateKey, p_settingsCtrl.Settings.Value.UdpTransportPrivateKeyPassphrase);
    //var encUdpMsgBytes = BitConverter.ToString(rsaAes.Encrypt(udpMsgBytes).ToArray()).Replace("-", "");

    var room = p_roomsController.GetRoom(_req.RoomId);
    var maxPathPoints = room?.MaxPathPoints ?? p_settingsCtrl.Settings.Value?.MaxPathPointsPerRoom;
    if (maxPathPoints != null && maxPathPoints == 0)
      return Forbidden("Publishing is forbidden!");

    var minInterval = room?.MinPathPointIntervalMs ?? p_settingsCtrl.Settings.Value?.MinPathPointIntervalMs ?? 0u;
    var compositeKey = $"{ReqPaths.GET_ROOM_PATHS}/{_req.RoomId}/{_req.Username}";
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(compositeKey, ip, minInterval))
    {
      log.Warn($"[{ReqPaths.GET_ROOM_PATHS}] Too many requests, room '{_req.RoomId}', username: '{_req.Username}', time limit: '{minInterval} ms'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    var sessionKey = $"{_req.RoomId}/{_req.Username}";
    var sessionDoc = p_documentStorage.GenericData.ReadSimpleDocument<RoomUserSession>(sessionKey);
    if (sessionDoc == null || sessionDoc.Data.SessionId != _req.SessionId)
    {
      log.Info($"New session {_req.SessionId} is started, username '{_req.Username}', wipe: '{_req.WipeOldPath}'");

      p_documentStorage.GenericData.WriteSimpleDocument(sessionKey, new RoomUserSession(_req.SessionId));

      if (_req.WipeOldPath == true)
        p_roomsController.EnqueueUserWipe(_req.RoomId, _req.Username, now.ToUnixTimeMilliseconds());

      var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgNewTrackStarted(_req.Username), AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
      var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, pushMsgData);
      await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);
    }

    var record = new StorageEntry(_req.Username, _req.Lat, _req.Lng, _req.Alt, _req.Speed, _req.Acc, _req.Battery, _req.GsmSignal, _req.Bearing);
    p_documentStorage.Paths.WriteDocument(_req.RoomId, now.ToUnixTimeMilliseconds().ToString(), record);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

    if (room?.MaxPointsPerPath > 0)
      p_roomsController.EnqueuePathTruncate(_req.RoomId, _req.Username);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.GET_ROOM_PATHS)]
  public IResult GetRoomPaths(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId,
    [FromQuery(Name = "offset")] long? _offsetUnixTimeMs,
    CancellationToken _ct)
  {
    if (string.IsNullOrWhiteSpace(_roomId))
      return BadRequest("Room Id is null!");
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return BadRequest("Room Id is incorrect!");

    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.GET_ROOM_PATHS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.GET_ROOM_PATHS}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Requested **paths** of room: __{_roomId}__; offset __{_offsetUnixTimeMs}__");

    var now = DateTimeOffset.UtcNow;

    const int maxReturnEntries = 250;
    var offset = _offsetUnixTimeMs != null ? DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs.Value + 1) : (DateTimeOffset?)null;
    var documents = p_documentStorage.Paths
      .ListDocuments<StorageEntry>(_roomId, _from: offset ?? null)
      .OrderBy(_ => _.Created)
      .Take(maxReturnEntries + 1)
      .ToList();

    GetPathResData result;
    if (documents.Count == 0)
    {
      result = new GetPathResData(now.ToUnixTimeMilliseconds(), false, []);
    }
    else if (documents.Count <= maxReturnEntries)
    {
      var entries = documents.Select(TimedStorageEntry.FromStorageEntry);
      result = new GetPathResData(now.ToUnixTimeMilliseconds(), false, entries);
    }
    else
    {
      var lastEntryTime = documents[^2].Created.ToUnixTimeMilliseconds();
      var entries = documents
        .Take(maxReturnEntries)
        .Select(TimedStorageEntry.FromStorageEntry);

      result = new GetPathResData(lastEntryTime, true, entries);
    }

    return Results.Json(result, ControllersJsonCtx.Default.GetPathResData);
  }

  //[HttpPost(ReqPaths.CREATE_NEW_POINT)]
  public async Task<IResult> CreateNewPointAsync(
    HttpRequest _httpRequest,
    [FromBody] CreateNewPointReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.CREATE_NEW_POINT, ip, () => new TimeWall(10, TimeSpan.FromSeconds(10))))
    {
      log.Warn($"[{ReqPaths.CREATE_NEW_POINT}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return BadRequest($"Incorrect room id!");
    if (!ReqResUtil.IsUserDefinedStringSafe(_req.Username))
      return BadRequest($"Incorrect username!");

    var description = ReqResUtil.ClearUserMsg(_req.Description);

    log.Info($"Got request to save point [{(int)_req.Lat}, {(int)_req.Lng}] for room '{_req.RoomId}'");

    var now = DateTimeOffset.UtcNow;
    var point = new GeoPointEntry(_req.RoomId, _req.Username, _req.Lat, _req.Lng, description);
    p_documentStorage.GenericData.WriteSimpleDocument($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgRoomPointAdded(_req.Username, _req.Description, _req.Lat, _req.Lng), AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);
    var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, pushMsgData);
    await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.LIST_ROOM_POINTS)]
  public IResult GetRoomPoints(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return BadRequest($"Incorrect room id!");

    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_POINTS, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.LIST_ROOM_POINTS}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Got req to **list points** in room __{_roomId}__");

    var entries = new List<ListRoomPointsResData>();
    foreach (var entry in p_documentStorage.GenericData.ListSimpleDocuments<GeoPointEntry>(new LikeExpr($"{_roomId}.%")))
      entries.Add(new ListRoomPointsResData(entry.Created.ToUnixTimeMilliseconds(), entry.Data.Username, entry.Data.Lat, entry.Data.Lng, entry.Data.Description));

    return Results.Json(entries, ControllersJsonCtx.Default.IReadOnlyListListRoomPointsResData);
  }

  //[HttpPost(ReqPaths.DELETE_ROOM_POINT)]
  public async Task<IResult> DeleteRoomPointAsync(
    HttpRequest _httpRequest,
    [FromBody] DeleteRoomPointReq _req,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_req.RoomId))
      return BadRequest($"Incorrect room id!");

    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.DELETE_ROOM_POINT, ip, () => new TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.DELETE_ROOM_POINT}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Got request to delete point '{_req.PointId}' from room '{_req.RoomId}'");

    foreach (var entry in p_documentStorage.GenericData.ListSimpleDocuments<GeoPointEntry>(new LikeExpr($"{_req.RoomId}.%")))
      if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
      {
        p_documentStorage.GenericData.DeleteSimpleDocument<GeoPointEntry>(entry.Key);
        break;
      }

    var now = DateTimeOffset.UtcNow;
    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.GET_FREE_ROOM_ID)]
  public IResult GetFreeRoomId(
    HttpRequest _httpRequest,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.GET_FREE_ROOM_ID, ip, () => new TimeWall(10, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.GET_FREE_ROOM_ID}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Got req to get free room id");

    string? roomId = null;
    var roomIdValid = false;
    while (!_ct.IsCancellationRequested && !roomIdValid)
    {
      roomId = Utilities.GetRandomString(ReqResUtil.MaxRoomIdLength, false);
      roomIdValid = !p_documentStorage.Paths
        .ListDocumentsMeta(roomId)
        .Any();
    }

    if (!roomIdValid || string.IsNullOrEmpty(roomId))
    {
      log.Error("Can't find free room id!");
      return InternalServerError("Can't find free room id!");
    }

    log.Info($"Sending free room id: '{roomId}'");
    return Results.Content(roomId, "text/plain", Encoding.UTF8);
  }

  //[HttpPost(ReqPaths.UPLOAD_LOG)]
  [RequestSizeLimit(10 * 1024 * 1024)]
  public async Task<IResult> UploadLogAsync(
    HttpRequest _httpRequest,
    [FromHeader(Name = "roomId")] string? _roomId,
    [FromHeader(Name = "username")] string? _username,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return BadRequest("Room Id is incorrect!");
    if (!ReqResUtil.IsUsernameSafe(_username))
      return BadRequest("Username is incorrect!");

    var dataDir = p_settingsCtrl.Settings.Value?.DataDirPath;
    if (dataDir.IsNullOrWhiteSpace())
      return InternalServerError($"Data dir path is not set!");

    var folder = Path.Combine(dataDir, "user-logs", _roomId, _username);
    if (!Directory.Exists(folder))
      Directory.CreateDirectory(folder);

    var log = GetLog(_httpRequest);

    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    var filePath = Path.Combine(folder, $"{timestamp}.gzip");
    log.Info($"Requested to store user log file to '{filePath}'");

    try
    {
      using (var file = File.OpenWrite(filePath))
        await _httpRequest.Body.CopyToAsync(file, _ct);

      log.Info($"User log file is stored to '{filePath}'");
      return Results.Ok();
    }
    catch (Exception ex)
    {
      log.Error($"Error occured while trying to save user log file '{filePath}'", ex);
      return InternalServerError();
    }
  }

  //[HttpGet(ReqPaths.IS_ROOM_ID_VALID)]
  public IResult IsRoomIdValid(
    [FromQuery(Name = "roomId")] string? _roomId)
  {
    var valid = ReqResUtil.IsRoomIdValid(_roomId);
    return valid ? Results.Ok() : Results.StatusCode((int)HttpStatusCode.NotAcceptable);
  }

  //[HttpGet(ReqPaths.IS_UDP_AVAILABLE)]
  public async Task<IResult> IsUdpTransportAvailableAsync(CancellationToken _ct)
  {
    var publicKeyPath = p_settingsCtrl.Settings.Value?.UdpTransportPublicKeyPath;
    if (publicKeyPath == null || !File.Exists(publicKeyPath))
      return Results.StatusCode((int)HttpStatusCode.NotImplemented);

    var port = p_settingsCtrl.Settings.Value?.PortBind;
    if (port == null)
      return Results.StatusCode((int)HttpStatusCode.NotImplemented);

    var hashString = await ReqResUtil.GetUdpPublicKeyHashAsync(publicKeyPath, _ct);
    var ipAddress = await p_httpClientProvider.Value.GetStringAsync("https://api.ipify.org/?format=text");
    return Json(new IsUdpTransportAvailableRes($"{ipAddress}:{port}", hashString));
  }

  //[HttpGet("/ws")]
  public async Task<IResult> StartWebSocketAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId,
    CancellationToken _ct)
  {
    if (string.IsNullOrWhiteSpace(_roomId))
      return BadRequest("Room Id is null!");
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return BadRequest("Room Id is incorrect!");

    if (!_httpRequest.HttpContext.WebSockets.IsWebSocketRequest)
      return BadRequest($"Expected web socket request");

    var log = GetLog(_httpRequest);

    var sessionIndex = Interlocked.Increment(ref p_wsSessionsCount);
    log.Info($"Establishing WS connection '{sessionIndex}' for room '{_roomId}'...");

    var roomInfo = p_roomsController.GetRoom(_roomId);
    var maxPointsInRoom = roomInfo?.MaxPathPoints ?? p_settingsCtrl.Settings.Value?.MaxPathPointsPerRoom ?? int.MaxValue;

    using var websocket = await _httpRequest.HttpContext.WebSockets.AcceptWebSocketAsync();
    _ = await p_webSocketCtrl.AcceptSocketAsync(websocket, _roomId, maxPointsInRoom);
    log.Info($"WS connection '{sessionIndex}' for room '{_roomId}' is closed");

    return Results.Empty;
  }

  //[ApiKeyRequired]
  //[HttpPost("register-room")]
  public IResult RegisterRoom(
    [FromBody] RoomInfo? _req)
  {
    if (_req == null)
      return BadRequest("Room data is null");

    p_roomsController.RegisterRoom(_req);
    return Results.Ok();
  }

  //[ApiKeyRequired]
  //[HttpPost("unregister-room")]
  public IResult DeleteRoomRegistration(
    [FromBody] DeleteRoomReq? _req)
  {
    if (_req == null || _req.RoomId == null)
      return BadRequest("Room Id is null");

    p_roomsController.UnregisterRoom(_req.RoomId);
    return Results.Ok();
  }

  //[ApiKeyRequired]
  //[HttpGet("list-registered-rooms")]
  public IResult ListRooms()
  {
    var users = p_roomsController.ListRegisteredRooms();
    return Results.Json(users, ControllersJsonCtx.Default.IReadOnlyListRoomInfo);
  }

  private ILog GetLog(HttpRequest _httpRequest)
  {
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    return p_log[Interlocked.Increment(ref p_reqCount).ToString()][$"{ip}"];
  }

}
