using Microsoft.AspNetCore.Mvc;
using Roadnik.Common.JsonCtx;
using Roadnik.Server.Attributes;
using Roadnik.Server.Data.WebServer;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class WebController : GenericController
{
  private readonly IAppConfig p_appConfig;

  public WebController(IAppConfig _appConfig) : base(RestJsonCtx.Default)
  {
    p_appConfig = _appConfig;
  }

  public override void RegisterPaths(WebApplication _app)
  {
    var ctrlInfo = new ControllerInfo("web-ctrl");

    _app.MapMethods("/r/", ["HEAD"], () => Results.Ok()).WithMetadata(ctrlInfo);
    _app.MapGet("/", GetIndexFile).WithMetadata(ctrlInfo);
    _app.MapGet("/r/{**path}", GetRoomStaticFile).WithMetadata(ctrlInfo);
    _app.MapGet("{**path}", GetStaticFile).WithMetadata(ctrlInfo);
  }

  public IResult GetIndexFile(
    HttpRequest _httpRequest,
    IScopedLog _log)
    => GetStaticFile(_httpRequest, _log, "/");

  public IResult GetRoomStaticFile(
    HttpRequest _httpRequest,
    IScopedLog _log,
    [FromRoute(Name = "path")] string? _path)
  {
    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    return GetStaticFile(_httpRequest, _log, $"room/{_path}");
  }

  [FailToBan]
  public IResult GetStaticFile(
    HttpRequest _httpRequest,
    IScopedLog _log,
    [FromRoute(Name = "path")] string _path)
  {
    try
    {
      _log.Info($"Requested **static path** __{_path}__");

      if (string.IsNullOrWhiteSpace(_path) || _path == "/")
        _path = "index.html";

      var path = Path.Combine(p_appConfig.WebrootDirPath, _path);
      if (!File.Exists(path))
      {
        _log.Warn($"File '{_path}' is not found");
        return NotFound();
      }

      if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
      {
        _log.Warn($"Tried to get file not from webroot: '{_path}'");
        return Forbidden(string.Empty);
      }

      var mime = MimeMapping.MimeUtility.GetMimeMapping(path);
      var stream = File.OpenRead(path);

      _log.Info($"**Handled** request of **static path** __{_path}__");
      return Results.Stream(stream, mime);
    }
    catch (Exception ex)
    {
      _log.Error($"Error occured while trying to handle 'static path {_path}' request: {ex}");
      return InternalServerError(ex.Message);
    }
  }

}
