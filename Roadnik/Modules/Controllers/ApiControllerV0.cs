using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using JustLogger.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Roadnik.Attributes;
using Roadnik.Common.ReqRes;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Toolkit;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Toolkit;
using System.Net;

namespace Roadnik.Modules.Controllers;

[ApiController]
[Route("/")]
public class ApiControllerV0 : JsonNetController
{
  record DeleteRoomReq(string RoomId);

  private static readonly HttpClient p_httpClient = new();
  private static long p_wsSessionsCount = 0;
  private static long p_reqCount = -1;

  private readonly ISettings p_settings;
  private readonly IDocumentStorage p_documentStorage;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_usersController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fcmPublisher;
  private readonly Lazy<ILogger> p_log;

  public ApiControllerV0(
    ISettings _settings,
    IDocumentStorage _documentStorage,
    ILogger _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _usersController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fcmPublisher)
  {
    p_settings = _settings;
    p_documentStorage = _documentStorage;
    p_webSocketCtrl = _webSocketCtrl;
    p_usersController = _usersController;
    p_tilesCache = _tilesCache;
    p_reqRateLimiter = _reqRateLimiter;
    p_fcmPublisher = _fcmPublisher;

    p_log = new Lazy<ILogger>(() =>
    {
      var ip = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;
      var logPrefix = $"{Interlocked.Increment(ref p_reqCount)} | {ip}";
      return _logger[logPrefix];
    }, true);
  }

  [HttpGet("/")]
  public async Task<IActionResult> GetIndexFileAsync()
  {
    //foreach (var (key, value) in HttpContext.Request.Headers)
    //  if (key.Equals("X-Forwarded-For", StringComparison.InvariantCultureIgnoreCase))
    //    p_log.Value.Warn($"X-Forwarded-For: '{value}'");
    //  else if (key.Equals("X-Real-IP", StringComparison.InvariantCultureIgnoreCase))
    //    p_log.Value.Warn($"X-Real-IP: '{value}'");
    //  else if (key.Equals("CF-Connecting-IP", StringComparison.InvariantCultureIgnoreCase))
    //    p_log.Value.Warn($"CF-Connecting-IP: '{value}'");

    return await GetStaticFileAsync("/");
  }

  [HttpGet("{**path}")]
  public async Task<IActionResult> GetStaticFileAsync(
    [FromRoute(Name = "path")] string _path)
  {
    p_log.Value.Info($"Requested static path '{_path}'");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var path = Path.Combine(p_settings.WebrootDirPath!, _path);
    if (!System.IO.File.Exists(path))
    {
      p_log.Value.Warn($"File '{_path}' is not found");
      return await Task.FromResult(StatusCode(404));
    }

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
    {
      p_log.Value.Error($"Tried to get file not from webroot: '{_path}'");
      return StatusCode(403);
    }

    var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
    var stream = System.IO.File.OpenRead(path);
    return File(stream, mime);
  }

  [HttpHead("/r/")]
  public async Task<IActionResult> GetIndexFileHeadAsync() => await Task.FromResult(Ok());

  [HttpGet("/r/{**path}")]
  public async Task<IActionResult> GetRoomAsync(
    [FromRoute(Name = "path")] string? _path)
  {
    p_log.Value.Info($"Requested static path '/room/{_path}'");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var path = Path.Combine(p_settings.WebrootDirPath!, "room", _path);
    if (!System.IO.File.Exists(path))
    {
      p_log.Value.Warn($"File '{path}' is not found");
      return await Task.FromResult(StatusCode(404));
    }

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
    {
      p_log.Value.Error($"Tried to get file not from webroot: '{path}'");
      return StatusCode(403);
    }

    var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
    var stream = System.IO.File.OpenRead(path);
    return File(stream, mime);
  }

  [HttpGet("/thunderforest")]
  public async Task<IActionResult> GetThunderforestImageAsync(
    [FromQuery(Name = "x")] int? _x = null,
    [FromQuery(Name = "y")] int? _y = null,
    [FromQuery(Name = "z")] int? _z = null,
    [FromQuery(Name = "type")] string? _type = null,
    CancellationToken _ct = default)
  {
    if (_x is null)
      return BadRequest("X is null!");
    if (_y is null)
      return BadRequest("Y is null!");
    if (_z is null)
      return BadRequest("Z is null!");
    if (string.IsNullOrWhiteSpace(_type))
      return BadRequest("Type is null!");
    if (!ReqResUtil.IsRoomIdSafe(_type))
      return BadRequest("Type is incorrect!");
    if (string.IsNullOrEmpty(p_settings.ThunderforestApikey))
      return StatusCode((int)HttpStatusCode.InternalServerError, $"Thunderforest API key is not set!");

    if (p_settings.ThunderforestCacheSize > 0)
    {
      var cachedStream = p_tilesCache.GetOrDefault(_x.Value, _y.Value, _z.Value, _type);
      if (cachedStream != null)
      {
        p_log.Value.Info($"Sending **cached** thunderforest tile; type:{_type}; x:{_x}; y:{_y}; z:{_z}");
        return File(cachedStream, MimeMapping.KnownMimeTypes.Png);
      }
    }

    p_log.Value.Info($"Sending thunderforest tile; type:{_type}; x:{_x}; y:{_y}; z:{_z}");
    var url = $"https://tile.thunderforest.com/{_type}/{_z}/{_x}/{_y}.png?apikey={p_settings.ThunderforestApikey}";

    if (p_settings.ThunderforestCacheSize <= 0)
      return File(await p_httpClient.GetStreamAsync(url, _ct), MimeMapping.KnownMimeTypes.Png);

    var ms = new MemoryStream();
    using (var stream = await p_httpClient.GetStreamAsync(url, _ct))
      await stream.CopyToAsync(ms, _ct);

    ms.Position = 0;
    await p_tilesCache.StoreAsync(_x.Value, _y.Value, _z.Value, _type, ms, _ct);

    ms.Position = 0;
    return File(ms, MimeMapping.KnownMimeTypes.Png);
  }

  [HttpGet("/store")]
  public async Task<IActionResult> StoreAsync(
    [FromQuery(Name = "roomId")] string? _roomId = null,
    [FromQuery(Name = "username")] string? _username = null,
    [FromQuery(Name = "lat")] float? _lat = null,
    [FromQuery(Name = "lon")] float? _lon = null,
    [FromQuery(Name = "alt")] float? _alt = null, // metres
    [FromQuery(Name = "speed")] float? _speed = null, // m/s
    [FromQuery(Name = "acc")] float? _acc = null, // metres
    [FromQuery(Name = "battery")] float? _battery = null, // %
    [FromQuery(Name = "gsm_signal")] float? _gsmSignal = null, // %
    [FromQuery(Name = "bearing")] float? _bearing = null, // grad
    [FromQuery(Name = "var")] string? _message = null,
    CancellationToken _ct = default)
  {
    if (string.IsNullOrWhiteSpace(_roomId))
      return BadRequest("Room Id is null!");
    if (!ReqResUtil.IsRoomIdSafe(_roomId))
      return BadRequest("Room Id is incorrect!");
    if (_lat == null)
      return BadRequest("Latitude is null!");
    if (_lon == null)
      return BadRequest("Longitude is null!");
    if (_alt == null)
      return BadRequest("Altitude is null!");
    if (!string.IsNullOrWhiteSpace(_message) && !ReqResUtil.IsUserDefinedStringSafe(_message))
      return BadRequest("Message is incorrect!");
    if (!string.IsNullOrWhiteSpace(_username) && !ReqResUtil.IsUserDefinedStringSafe(_username))
      return BadRequest("Username is incorrect!");

    p_log.Value.Info($"Requested to store geo data, room: '{_roomId}'");

    var user = await p_usersController.GetRoomAsync(_roomId, _ct);
    if (!p_settings.AllowAnonymousPublish && user == null)
      return Forbidden("Anonymous publishing is forbidden!");

    var timeLimit = user != null ? p_settings.RegisteredMinIntervalMs : p_settings.AnonymousMinIntervalMs;
    var compositeKey = $"{ReqPaths.GET}/{_roomId}/{_username ?? ""}";

    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(compositeKey, ip, (long)timeLimit))
    {
      p_log.Value.Warn($"[{ReqPaths.GET}] Too many requests, room '{_roomId}', username: '{_username}', time limit: '{timeLimit} ms'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    var record = new StorageEntry(_roomId, _username ?? _roomId, _lat.Value, _lon.Value, _alt.Value, _speed, _acc, _battery, _gsmSignal, _bearing, _message);
    await p_documentStorage.WriteSimpleDocumentAsync($"{_roomId}.{now.ToUnixTimeMilliseconds()}", record, _ct);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_roomId, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

    return Ok();
  }

  [HttpGet(ReqPaths.GET)]
  public async Task<IActionResult> GetAsync(
    [FromQuery(Name = "roomId")] string? _roomId = null,
    [FromQuery(Name = "offset")] long? _offsetUnixTimeMs = null,
    CancellationToken _ct = default)
  {
    if (string.IsNullOrWhiteSpace(_roomId))
      return BadRequest("Room Id is null!");
    if (!ReqResUtil.IsRoomIdSafe(_roomId))
      return BadRequest("Room Id is incorrect!");

    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.GET, ip, 1000))
    {
      p_log.Value.Warn($"[{ReqPaths.GET}] Too many requests from ip '{ip}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    p_log.Value.Info($"Requested to get geo data, room: '{_roomId}'");

    var offset = _offsetUnixTimeMs != null ? DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs.Value + 1) : (DateTimeOffset?)null;
    var documents = await p_documentStorage
      .ListSimpleDocumentsAsync<StorageEntry>(new LikeExpr($"{_roomId}.%"), _from: offset ?? null, _ct: _ct)
      .OrderByDescending(_ => _.Created)
      .Take(p_settings.GetRequestReturnsEntriesCount)
      .ToListAsync(_ct);

    GetResData result;
    if (!documents.Any())
    {
      result = new GetResData(false, 0, Array.Empty<TimedStorageEntry>());
    }
    else
    {
      var lastEntryTime = documents[0].Created.ToUnixTimeMilliseconds();
      var entries = Enumerable
        .Reverse(documents)
        .Select(TimedStorageEntry.FromStorageEntry);

      result = new GetResData(true, lastEntryTime, entries);
    }

    return Json(result);
  }

  [HttpPost(ReqPaths.START_NEW_PATH)]
  public async Task<IActionResult> StartNewPathAsync(
    [FromBody] StartNewPathReq _req,
    CancellationToken _ct)
  {
    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.START_NEW_PATH, ip, 10 * 1000))
    {
      p_log.Value.Warn($"[{ReqPaths.START_NEW_PATH}] Too many requests from ip '{ip}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var now = DateTimeOffset.UtcNow;

    p_log.Value.Info($"Requested to start new path, room '{_req.RoomId}', username '{_req.Username}', wipe: '{_req.WipeData}'");
    if (_req.WipeData)
      p_usersController.EnqueueUserWipe(_req.RoomId, _req.Username, now.ToUnixTimeMilliseconds());

    var pushMsg = new PushMsg(PushMsgType.NewTrackStarted, JToken.FromObject(new PushMsgNewTrackStarted(_req.Username)));
    await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Ok();
  }

  [HttpPost(ReqPaths.CREATE_NEW_POINT)]
  public async Task<IActionResult> CreateNewPointAsync(
    [FromBody] CreateNewPointReq _req,
    CancellationToken _ct)
  {
    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.CREATE_NEW_POINT, ip, 1000))
    {
      p_log.Value.Warn($"[{ReqPaths.CREATE_NEW_POINT}] Too many requests from ip '{ip}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    if (!ReqResUtil.IsRoomIdSafe(_req.RoomId))
      return BadRequest($"Incorrect room id!");
    if (!ReqResUtil.IsUserDefinedStringSafe(_req.Username))
      return BadRequest($"Incorrect username!");

    var description = ReqResUtil.ClearUserMsg(_req.Description);

    p_log.Value.Info($"Got request to save point [{(int)_req.Lat}, {(int)_req.Lng}] for room '{_req.RoomId}'");

    var now = DateTimeOffset.UtcNow;
    var point = new GeoPointEntry(_req.RoomId, _req.Username, _req.Lat, _req.Lng, description);
    await p_documentStorage.WriteSimpleDocumentAsync($"{_req.RoomId}.{now.ToUnixTimeMilliseconds()}", point, _ct);

    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    var pushMsg = new PushMsg(PushMsgType.RoomPointAdded, JToken.FromObject(new PushMsgRoomPointAdded(_req.Username, _req.Description)));
    await p_fcmPublisher.SendDataAsync(_req.RoomId, pushMsg, _ct);

    return Ok();
  }

  [HttpGet(ReqPaths.LIST_ROOM_POINTS)]
  public async Task<IActionResult> GetRoomPointsAsync(
    [FromQuery(Name = "roomId")] string? _roomId = null,
    CancellationToken _ct = default)
  {
    if (!ReqResUtil.IsRoomIdSafe(_roomId))
      return BadRequest($"Incorrect room id!");

    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.LIST_ROOM_POINTS, ip, 1000))
    {
      p_log.Value.Warn($"[{ReqPaths.LIST_ROOM_POINTS}] Too many requests from ip '{ip}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var entries = new List<ListRoomPointsResData>();
    await foreach (var entry in p_documentStorage.ListSimpleDocumentsAsync<GeoPointEntry>(new LikeExpr($"{_roomId}.%"), _ct: _ct))
      entries.Add(new ListRoomPointsResData(entry.Created.ToUnixTimeMilliseconds(), entry.Data.Username, entry.Data.Lat, entry.Data.Lng, entry.Data.Description));

    return Json(entries);
  }

  [HttpPost(ReqPaths.DELETE_ROOM_POINT)]
  public async Task<IActionResult> RemoveRoomPointAsync(
    [FromBody] DeleteRoomPointReq _req,
    CancellationToken _ct)
  {
    if (!ReqResUtil.IsRoomIdSafe(_req.RoomId))
      return BadRequest($"Incorrect room id!");

    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (!p_reqRateLimiter.IsReqOk(ReqPaths.DELETE_ROOM_POINT, ip, 1000))
    {
      p_log.Value.Warn($"[{ReqPaths.DELETE_ROOM_POINT}] Too many requests from ip '{ip}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    p_log.Value.Info($"Got request to delete point '{_req.PointId}' from room '{_req.RoomId}'");

    await foreach (var entry in p_documentStorage.ListSimpleDocumentsAsync<GeoPointEntry>(new LikeExpr($"{_req.RoomId}.%"), _ct: _ct))
      if (entry.Created.ToUnixTimeMilliseconds() == _req.PointId)
      {
        await p_documentStorage.DeleteSimpleDocumentAsync<GeoPointEntry>(entry.Key, _ct);
        break;
      }

    var now = DateTimeOffset.UtcNow;
    await p_webSocketCtrl.SendMsgByRoomIdAsync(_req.RoomId, new WsMsgRoomPointsUpdated(now.ToUnixTimeMilliseconds()), _ct);

    return Ok();
  }

  [HttpPost(ReqPaths.UPLOAD_LOG)]
  [RequestSizeLimit(10 * 1024 * 1024)]
  public async Task<IActionResult> UploadLogAsync(
    [FromHeader(Name = "roomId")] string _roomId,
    [FromHeader(Name = "username")] string _username,
    CancellationToken _ct)
  {
    var folder = Path.Combine(p_settings.DataDirPath, "user-logs", _roomId, _username);
    if (!Directory.Exists(folder))
      Directory.CreateDirectory(folder);

    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    var filePath = Path.Combine(folder, $"{timestamp}.gzip");
    p_log.Value.Info($"Requested to store user log file to '{filePath}'");

    try
    {
      using (var file = System.IO.File.OpenWrite(filePath))
        await Request.Body.CopyToAsync(file, _ct);

      p_log.Value.Info($"User log file is stored to '{filePath}'");
      return Ok();
    }
    catch (Exception ex)
    {
      p_log.Value.Error($"Error occured while trying to save user log file '{filePath}'", ex);
      return StatusCode((int)HttpStatusCode.InternalServerError);
    }
  }

  [HttpGet("/ws")]
  public async Task<IActionResult> StartWebSocketAsync(
    [FromQuery(Name = "roomId")] string? _roomId = null)
  {
    if (string.IsNullOrWhiteSpace(_roomId))
      return BadRequest("Room Id is null!");
    if (!ReqResUtil.IsRoomIdSafe(_roomId))
      return BadRequest("Room Id is incorrect!");

    if (!HttpContext.WebSockets.IsWebSocketRequest)
      return StatusCode((int)HttpStatusCode.BadRequest, $"Expected web socket request");

    var sessionIndex = Interlocked.Increment(ref p_wsSessionsCount);
    p_log.Value.Info($"Establishing WS connection '{sessionIndex}' for room '{_roomId}'...");

    using var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
    _ = await p_webSocketCtrl.AcceptSocket(_roomId, websocket);
    p_log.Value.Info($"WS connection '{sessionIndex}' for room '{_roomId}' is closed");

    return new EmptyResult();
  }

  [ApiKeyRequired]
  [HttpPost("register-room")]
  public async Task<IActionResult> AddUserAsync(CancellationToken _ct)
  {
    var req = await GetJsonRequest<User>();
    if (req == null)
      return BadRequest("User is null");

    await p_usersController.RegisterRoomAsync(req.RoomId, req.Email, _ct);
    return Ok();
  }

  [ApiKeyRequired]
  [HttpPost("unregister-room")]
  public async Task<IActionResult> DeleteUserAsync(CancellationToken _ct)
  {
    var req = await GetJsonRequest<DeleteRoomReq>();
    if (req == null || req.RoomId == null)
      return BadRequest("Room Id is null");

    await p_usersController.UnregisterRoomAsync(req.RoomId, _ct);
    return Ok();
  }

  [ApiKeyRequired]
  [HttpGet("list-registered-rooms")]
  public async Task<IActionResult> ListUsersAsync(CancellationToken _ct)
  {
    var users = await p_usersController.ListRegisteredRoomsAsync(_ct);
    return Json(users, true);
  }

}
