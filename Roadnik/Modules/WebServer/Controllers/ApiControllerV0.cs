using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Microsoft.AspNetCore.Mvc;
using Roadnik.Common.ReqRes;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Serializers;
using Roadnik.Common.Toolkit;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Server.Data.ReqRes;
using Roadnik.Server.Data.WebSockets;
using Roadnik.Server.Interfaces;
using System.Net;
using System.Text;
using System.Text.Json;
using ILogger = JustLogger.Interfaces.ILogger;

namespace Roadnik.Modules.Controllers;

public class ApiControllerV0
{
  private static readonly HttpClient p_httpClient = new();
  private static long p_wsSessionsCount = 0;
  private static long p_reqCount = -1;

  private readonly ISettingsController p_settingsCtrl;
  private readonly IDocumentStorageAot p_documentStorage;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_usersController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fcmPublisher;
  private readonly ILogger p_log;

  public ApiControllerV0(
    ISettingsController _settingsCtrl,
    IDocumentStorageAot _documentStorage,
    ILogger _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _usersController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fcmPublisher)
  {
    p_settingsCtrl = _settingsCtrl;
    p_documentStorage = _documentStorage;
    p_log = _logger;
    p_webSocketCtrl = _webSocketCtrl;
    p_usersController = _usersController;
    p_tilesCache = _tilesCache;
    p_reqRateLimiter = _reqRateLimiter;
    p_fcmPublisher = _fcmPublisher;
  }

  //[HttpGet("/")]
  public IResult GetIndexFile(HttpRequest _httpRequest) => GetStaticFile(_httpRequest, "/");

  //[HttpGet("{**path}")]
  public IResult GetStaticFile(
    HttpRequest _httpRequest,
    [FromRoute(Name = "path")] string _path)
  {
    var log = GetLog(_httpRequest);
    log.Info($"Requested static path '{_path}'");

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

  //[HttpGet("/thunderforest")]
  public async Task<IResult> GetThunderforestImageAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "x")] int? _x,
    [FromQuery(Name = "y")] int? _y,
    [FromQuery(Name = "z")] int? _z,
    [FromQuery(Name = "type")] string? _type,
    CancellationToken _ct)
  {
    if (_x is null)
      return Results.BadRequest("X is null!");
    if (_y is null)
      return Results.BadRequest("Y is null!");
    if (_z is null)
      return Results.BadRequest("Z is null!");
    if (_type.IsNullOrWhiteSpace())
      return Results.BadRequest("Type is null!");
    if (!ReqResUtil.ValidMapTypes.Contains(_type))
      return Results.BadRequest("Type is incorrect!");

    var tfApiKey = p_settingsCtrl.Settings.Value?.ThunderforestApikey;
    if (tfApiKey.IsNullOrWhiteSpace())
      return Results.Problem($"Thunderforest API key is not set!", statusCode: (int)HttpStatusCode.InternalServerError);

    var log = GetLog(_httpRequest);

    var tfCacheSize = p_settingsCtrl.Settings.Value?.ThunderforestCacheSize;
    if (tfCacheSize != null && tfCacheSize.Value > 0)
    {
      var cachedStream = p_tilesCache.GetOrDefault(_x.Value, _y.Value, _z.Value, _type);
      if (cachedStream != null)
      {
        log.Info($"Sending **cached** thunderforest tile; type:{_type}; x:{_x}; y:{_y}; z:{_z}");
        return Results.Stream(cachedStream, MimeMapping.KnownMimeTypes.Png);
      }
    }

    log.Info($"Sending thunderforest tile; type:{_type}; x:{_x}; y:{_y}; z:{_z}");
    var url = $"https://tile.thunderforest.com/{_type}/{_z}/{_x}/{_y}.png?apikey={tfApiKey}";

    if (tfCacheSize == null || tfCacheSize.Value <= 0)
      return Results.Stream(await p_httpClient.GetStreamAsync(url, _ct), MimeMapping.KnownMimeTypes.Png);

    using (var stream = await p_httpClient.GetStreamAsync(url, _ct))
      await p_tilesCache.StoreAsync(_x.Value, _y.Value, _z.Value, _type, stream, _ct);

    var newCachedStream = p_tilesCache.GetOrDefault(_x.Value, _y.Value, _z.Value, _type);
    if (newCachedStream != null)
      return Results.Stream(newCachedStream, MimeMapping.KnownMimeTypes.Png);

    var errMsg = $"Can't find cached thunderforest tile: x:{_x.Value};y:{_y.Value};z:{_z.Value};t:{_type}";
    log.Error(errMsg);
    return InternalServerError(errMsg);
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

    var user = await p_usersController.GetRoomAsync(_roomId, _ct);
    if (!(p_settingsCtrl.Settings.Value?.AllowAnonymousPublish == true) && user == null)
      return Forbidden("Anonymous publishing is forbidden!");

    var registeredMinIntervalMs = p_settingsCtrl.Settings.Value?.RegisteredMinIntervalMs;
    var anonymousMinIntervalMs = p_settingsCtrl.Settings.Value?.AnonymousMinIntervalMs;
    if (registeredMinIntervalMs == null || anonymousMinIntervalMs == null)
      return InternalServerError($"Store minimum intervals are misconfigured");

    var timeLimit = user != null ? registeredMinIntervalMs : anonymousMinIntervalMs;
    var compositeKey = $"{ReqPaths.GET_ROOM_PATHS}/{_roomId}/{_username ?? ""}";

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(compositeKey, ip, (long)timeLimit))
    {
      log.Warn($"[{ReqPaths.GET_ROOM_PATHS}] Too many requests, room '{_roomId}', username: '{_username}', time limit: '{timeLimit} ms'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    var record = new StorageEntry(
      _roomId,
      _username ?? _roomId,
      _lat.Value,
      _lng.Value,
      _alt.Value,
      _speed,
      _acc,
      (_battery ?? 0) / 100,
      (_gsmSignal ?? 0) / 100,
      _bearing);
    await p_documentStorage.WriteSimpleDocumentAsync($"{_roomId}.{now.ToUnixTimeMilliseconds()}", record, DocStorageJsonCtx.Default.StorageEntry, _ct);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_roomId, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

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

    var user = await p_usersController.GetRoomAsync(_req.RoomId, _ct);
    if (!(p_settingsCtrl.Settings.Value?.AllowAnonymousPublish == true) && user == null)
      return Forbidden("Anonymous publishing is forbidden!");

    var registeredMinIntervalMs = p_settingsCtrl.Settings.Value?.RegisteredMinIntervalMs;
    var anonymousMinIntervalMs = p_settingsCtrl.Settings.Value?.AnonymousMinIntervalMs;
    if (registeredMinIntervalMs == null || anonymousMinIntervalMs == null)
      return InternalServerError($"Store minimum intervals are misconfigured");

    var timeLimit = user != null ? registeredMinIntervalMs : anonymousMinIntervalMs;
    var compositeKey = $"{ReqPaths.GET_ROOM_PATHS}/{_req.RoomId}/{_req.Username}";

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(compositeKey, ip, (long)timeLimit))
    {
      log.Warn($"[{ReqPaths.GET_ROOM_PATHS}] Too many requests, room '{_req.RoomId}', username: '{_req.Username}', time limit: '{timeLimit} ms'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    var record = new StorageEntry(_req.RoomId, _req.Username, _req.Lat, _req.Lng, _req.Alt, _req.Speed, _req.Acc, _req.Battery, _req.GsmSignal, _req.Bearing);
    await p_documentStorage.WriteSimpleDocumentAsync($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", record, DocStorageJsonCtx.Default.StorageEntry, _ct);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.GET_ROOM_PATHS)]
  public async Task<IResult> GetRoomPathsAsync(
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

    log.Info($"Requested to get geo data, room: '{_roomId}'");

    var now = DateTimeOffset.UtcNow;

    const int maxReturnEntries = 250;
    var offset = _offsetUnixTimeMs != null ? DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs.Value + 1) : (DateTimeOffset?)null;
    var documents = await p_documentStorage
      .ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, new LikeExpr($"{_roomId}.%"), _from: offset ?? null, _ct: _ct)
      .OrderBy(_ => _.Created)
      .Take(maxReturnEntries + 1)
      .ToListAsync(_ct);

    GetPathResData result;
    if (documents.Count == 0)
    {
      result = new GetPathResData(now.ToUnixTimeMilliseconds(), false, Array.Empty<TimedStorageEntry>());
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

  //[HttpPost(ReqPaths.START_NEW_PATH)]
  public async Task<IResult> StartNewPathAsync(
    HttpRequest _httpRequest,
    [FromBody] StartNewPathReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.START_NEW_PATH, ip, 10 * 1000))
    {
      log.Warn($"[{ReqPaths.START_NEW_PATH}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    log.Info($"Requested to start new path, room '{_req.RoomId}', username '{_req.Username}', wipe: '{_req.WipeData}'");
    if (_req.WipeData)
      p_usersController.EnqueueUserWipe(_req.RoomId, _req.Username, now.ToUnixTimeMilliseconds());

    var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgNewTrackStarted(_req.Username), AndroidPushJsonCtx.Default.PushMsgNewTrackStarted);
    var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, pushMsgData);
    await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Results.Ok();
  }

  //[HttpPost(ReqPaths.CREATE_NEW_POINT)]
  public async Task<IResult> CreateNewPointAsync(
    HttpRequest _httpRequest,
    [FromBody] CreateNewPointReq _req,
    CancellationToken _ct)
  {
    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.CREATE_NEW_POINT, ip, () => new Ax.Fw.TimeWall(10, TimeSpan.FromSeconds(10))))
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
    await p_documentStorage.WriteSimpleDocumentAsync($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point, DocStorageJsonCtx.Default.GeoPointEntry, _ct);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    var pushMsgData = JsonSerializer.SerializeToElement(new PushMsgRoomPointAdded(_req.Username, _req.Description, _req.Lat, _req.Lng), AndroidPushJsonCtx.Default.PushMsgRoomPointAdded);
    var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, pushMsgData);
    await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.LIST_ROOM_POINTS)]
  public async Task<IResult> GetRoomPointsAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdValid(_roomId))
      return BadRequest($"Incorrect room id!");

    var log = GetLog(_httpRequest);

    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.LIST_ROOM_POINTS, ip, () => new Ax.Fw.TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.LIST_ROOM_POINTS}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Got req to list points in room '{_roomId}'");

    var entries = new List<ListRoomPointsResData>();
    await foreach (var entry in p_documentStorage.ListSimpleDocumentsAsync<GeoPointEntry>(DocStorageJsonCtx.Default.GeoPointEntry, new LikeExpr($"{_roomId}.%"), _ct: _ct))
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
    if (!p_reqRateLimiter.IsReqTimewallOk(ReqPaths.DELETE_ROOM_POINT, ip, () => new Ax.Fw.TimeWall(60, TimeSpan.FromSeconds(60))))
    {
      log.Warn($"[{ReqPaths.DELETE_ROOM_POINT}] Too many requests from ip '{ip}'");
      return Results.StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    log.Info($"Got request to delete point '{_req.PointId}' from room '{_req.RoomId}'");

    await foreach (var entry in p_documentStorage.ListSimpleDocumentsAsync<GeoPointEntry>(DocStorageJsonCtx.Default.GeoPointEntry, new LikeExpr($"{_req.RoomId}.%"), _ct: _ct))
      if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
      {
        await p_documentStorage.DeleteSimpleDocumentAsync<GeoPointEntry>(entry.Key, _ct);
        break;
      }

    var now = DateTimeOffset.UtcNow;
    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    return Results.Ok();
  }

  //[HttpGet(ReqPaths.GET_FREE_ROOM_ID)]
  public async Task<IResult> GetFreeRoomIdAsync(
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
      roomIdValid = !await p_documentStorage
        .ListSimpleDocumentsAsync(DocStorageJsonCtx.Default.StorageEntry, new LikeExpr($"{roomId}.%"), _ct: _ct)
        .AnyAsync(_ct);
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

  //[HttpGet("/ws")]
  public async Task<IResult> StartWebSocketAsync(
    HttpRequest _httpRequest,
    [FromQuery(Name = "roomId")] string? _roomId)
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

    using var websocket = await _httpRequest.HttpContext.WebSockets.AcceptWebSocketAsync();
    _ = await p_webSocketCtrl.AcceptSocketAsync(_roomId, websocket);
    log.Info($"WS connection '{sessionIndex}' for room '{_roomId}' is closed");

    return Results.Empty;
  }

  //[ApiKeyRequired]
  //[HttpPost("register-room")]
  public async Task<IResult> RegisterRoomAsync(
    [FromBody] User? _req,
    CancellationToken _ct)
  {
    if (_req == null)
      return BadRequest("User is null");

    await p_usersController.RegisterRoomAsync(_req.RoomId, _req.Email, _ct);
    return Results.Ok();
  }

  //[ApiKeyRequired]
  //[HttpPost("unregister-room")]
  public async Task<IResult> DeleteRoomRegistrationAsync(
    [FromBody] DeleteRoomReq? _req,
    CancellationToken _ct)
  {
    if (_req == null || _req.RoomId == null)
      return BadRequest("Room Id is null");

    await p_usersController.UnregisterRoomAsync(_req.RoomId, _ct);
    return Results.Ok();
  }

  //[ApiKeyRequired]
  //[HttpGet("list-registered-rooms")]
  public async Task<IResult> ListUsersAsync(CancellationToken _ct)
  {
    var users = await p_usersController.ListRegisteredRoomsAsync(_ct);
    return Results.Json(users, ControllersJsonCtx.Default.IReadOnlyListUser);
  }

  private ILogger GetLog(HttpRequest _httpRequest)
  {
    var ip = _httpRequest.HttpContext.Connection.RemoteIpAddress;
    var logPrefix = $"{Interlocked.Increment(ref p_reqCount)} | {ip}";
    return p_log[logPrefix];
  }

  private static IResult Forbidden(string _details) => Results.Problem(_details, statusCode: 403);
  private static IResult InternalServerError(string? _details = null) => Results.Problem(_details, statusCode: (int)HttpStatusCode.InternalServerError);
  private static IResult BadRequest(string _details) => Results.BadRequest(_details);

}
