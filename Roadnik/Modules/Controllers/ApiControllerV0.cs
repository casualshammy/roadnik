using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using JustLogger.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roadnik.Attributes;
using Roadnik.Data;
using Roadnik.Interfaces;
using Roadnik.Toolkit;
using System.Collections.Concurrent;
using System.Net;

namespace Roadnik.Modules.Controllers;

[ApiController]
[Route("/")]
public class ApiControllerV0 : JsonNetController
{
  record DeleteUserReq(string Key);

  private static long p_wsSessionsCount = 0;
  private static readonly TimeSpan p_getTooFastLimitTime = TimeSpan.FromSeconds(1);
  private static readonly ConcurrentDictionary<string, DateTimeOffset> p_storeLimiter = new();
  private static readonly ConcurrentDictionary<IPAddress, DateTimeOffset> p_getLimiter = new();
  private static readonly HttpClient p_httpClient = new();
  private readonly ISettings p_settings;
  private readonly IDocumentStorage p_documentStorage;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IUsersController p_usersController;
  private readonly ILogger p_logger;

  public ApiControllerV0(
    ISettings _settings,
    IDocumentStorage _documentStorage,
    ILogger _logger,
    IWebSocketCtrl _webSocketCtrl,
    IUsersController _usersController)
  {
    p_settings = _settings;
    p_documentStorage = _documentStorage;
    p_webSocketCtrl = _webSocketCtrl;
    p_usersController = _usersController;
    p_logger = _logger["api-v0"];
  }

  [HttpGet("/")]
  public async Task<IActionResult> GetIndexFileAsync() => await GetStaticFileAsync("/");

  [HttpHead("/")]
  public async Task<IActionResult> GetIndexFileHeadAsync() => Ok();

  [HttpGet("{**path}")]
  public async Task<IActionResult> GetStaticFileAsync(
    [FromRoute(Name = "path")] string _path)
  {
    p_logger.Info($"Requested static path '{_path}'");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var path = Path.Combine(p_settings.WebrootDirPath!, _path);
    if (!System.IO.File.Exists(path))
    {
      p_logger.Warn($"File '{_path}' is not found");
      return await Task.FromResult(StatusCode(404));
    }

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
    {
      p_logger.Error($"Tryed to get file not from webroot: '{_path}'");
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
    if (string.IsNullOrEmpty(p_settings.TrunderforestApikey))
      return StatusCode((int)HttpStatusCode.InternalServerError, $"Thunderforest API key is not set!");

    p_logger.Info($"Sending thunderforest tile; type:{_type}; x:{_x}; y:{_y}; z:{_z}");
    var url = $"https://tile.thunderforest.com/{_type}/{_z}/{_x}/{_y}.png?apikey={p_settings.TrunderforestApikey}";
    var stream = await p_httpClient.GetStreamAsync(url, _ct);
    return File(stream, MimeMapping.KnownMimeTypes.Png);
  }

  [HttpGet("/store")]
  public async Task<IActionResult> StoreAsync(
    [FromQuery(Name = "key")] string? _key = null,
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
    if (string.IsNullOrWhiteSpace(_key))
      return BadRequest("Key is null!");
    if (_lat == null)
      return BadRequest("Latitude is null!");
    if (_lon == null)
      return BadRequest("Longitude is null!");
    if (_alt == null)
      return BadRequest("Altitude is null!");

    p_logger.Info($"Requested to store geo data, key: '{_key}'");

    var user = await p_usersController.GetUserAsync(_key, _ct);
    var timeLimit = user != null ? TimeSpan.FromSeconds(0.9) : TimeSpan.FromSeconds(9);
    var now = DateTimeOffset.UtcNow;
    if (p_storeLimiter.TryGetValue(_key, out var lastStoredTime) && now - lastStoredTime < timeLimit)
    {
      p_logger.Warn($"Too many requests for storing geo data, key '{_key}', interval: '{now - lastStoredTime}', time limit: '{timeLimit}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    var record = new StorageEntry(_key, _lat.Value, _lon.Value, _alt.Value, _speed, _acc, _battery, _gsmSignal, _bearing, _message);
    await p_documentStorage.WriteSimpleDocumentAsync($"{_key}.{now.ToUnixTimeMilliseconds()}", record, _ct);
    p_storeLimiter[_key] = now;
    await p_webSocketCtrl.SendMsgByKeyAsync(_key, new WsMsgUpdateAvailable(now.ToUnixTimeMilliseconds()), _ct);

    return Ok();
  }

  [HttpGet("/get")]
  public async Task<IActionResult> GetAsync(
    [FromQuery(Name = "key")] string? _key = null,
    [FromQuery(Name = "limit")] int? _limit = null,
    [FromQuery(Name = "offset")] long? _offsetUnixTimeMs = null,
    CancellationToken _ct = default)
  {
    if (string.IsNullOrWhiteSpace(_key))
      return BadRequest("Key is null!");

    var ip = Request.HttpContext.Connection.RemoteIpAddress;
    if (ip == null)
    {
      p_logger.Error($"Ip is null, key: '{_key}'");
      return BadRequest("Ip is null!");
    }
    var now = DateTimeOffset.UtcNow;
    if (p_getLimiter.TryGetValue(ip, out var lastGetReq) && now - lastGetReq < p_getTooFastLimitTime)
    {
      p_logger.Warn($"Too many requests from ip '{ip}', key: '{_key}'");
      return StatusCode((int)HttpStatusCode.TooManyRequests);
    }

    p_logger.Info($"Requested to get geo data, key: '{_key}'");

    var offset = _offsetUnixTimeMs != null ? DateTimeOffset.FromUnixTimeMilliseconds(_offsetUnixTimeMs.Value + 1) : (DateTimeOffset?)null;
    var documents = await p_documentStorage
      .ListSimpleDocumentsAsync<StorageEntry>(new LikeExpr($"{_key}%"), _ct: _ct)
      .Where(_ => offset == null || _.Created > offset)
      .OrderByDescending(_ => _.Created)
      .Take(_limit != null ? Math.Min(_limit.Value, 1000) : 1000)
      .ToListAsync(_ct);

    GetResData result;
    if (!documents.Any())
    {
      result = new GetResData(false, null, null, Array.Empty<StorageEntry>());
    }
    else
    {
      var list = new List<StorageEntry>(documents.Count);
      var lastEntryTime = DateTimeOffset.MinValue;
      StorageEntry? lastEntry = null;
      foreach (var doc in documents)
      {
        list.Add(new StorageEntry(_key, doc.Data.Latitude, doc.Data.Longitude, doc.Data.Altitude));
        if (doc.Created > lastEntryTime)
        {
          lastEntryTime = doc.Created;
          lastEntry = doc.Data;
        }
      }

      result = new GetResData(true, lastEntryTime.ToUnixTimeMilliseconds(), lastEntry!, list);
    }

    p_getLimiter[ip] = now;
    return Json(result);
  }

  [HttpGet("/ws")]
  public async Task<IActionResult> StartWebSocketAsync(
    [FromQuery(Name = "key")] string? _key = null,
    CancellationToken _ct = default)
  {
    if (string.IsNullOrWhiteSpace(_key))
      return BadRequest("Key is null!");

    if (!HttpContext.WebSockets.IsWebSocketRequest)
      return StatusCode((int)HttpStatusCode.BadRequest, $"Expected web socket request");

    var sessionIndex = Interlocked.Increment(ref p_wsSessionsCount);
    p_logger.Info($"Establishing WS connection '{sessionIndex}' for key '{_key}'...");

    using var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
    _ = await p_webSocketCtrl.AcceptSocket(_key, websocket);
    p_logger.Info($"WS connection '{sessionIndex}' for key '{_key}' is closed");

    return new EmptyResult();
  }

  [ApiKeyRequired]
  [HttpPost("add-user")]
  public async Task<IActionResult> AddUserAsync(CancellationToken _ct)
  {
    var req = await GetJsonRequest<User>();
    if (req == null)
      return BadRequest("User is null");

    await p_usersController.AddUserAsync(req.Key, req.Email, _ct);
    return Ok();
  }

  [ApiKeyRequired]
  [HttpPost("delete-user")]
  public async Task<IActionResult> DeleteUserAsync(CancellationToken _ct)
  {
    var req = await GetJsonRequest<DeleteUserReq>();
    if (req == null || req.Key == null)
      return BadRequest("Key is null");

    await p_usersController.DeleteUserAsync(req.Key, _ct);
    return Ok();
  }

  [ApiKeyRequired]
  [HttpGet("list-users")]
  public async Task<IActionResult> ListUsersAsync(CancellationToken _ct)
  {
    var users = await p_usersController.ListUsersAsync(_ct);
    return Json(users, true);
  }

}
