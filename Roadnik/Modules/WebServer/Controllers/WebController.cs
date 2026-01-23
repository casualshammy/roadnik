using Ax.Fw;
using Ax.Fw.Web.Data;
using Ax.Fw.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Roadnik.Server.Interfaces;
using System.Net;

namespace Roadnik.Server.Modules.WebServer.Controllers;

internal class WebController
{
  private readonly IAppConfig p_appConfig;

  public WebController(IAppConfig _appConfig)
  {
    p_appConfig = _appConfig;
  }

  public void RegisterPaths(WebApplication _app)
  {
    var ctrlInfo = new RestControllerInfo("web-ctrl", "web-ctrl");

    _app.MapMethods("/r/", ["HEAD"], () => Results.Ok()).WithMetadata(ctrlInfo);
    _app.MapGet("/", GetIndexFile).WithMetadata(ctrlInfo);
    _app.MapGet("/r/{**path}", GetRoomStaticFile).WithMetadata(ctrlInfo);
    _app.MapGet("{**path}", GetStaticFile).WithMetadata(ctrlInfo);
  }

  public IResult GetIndexFile(
    IScopedLog _log,
    IRequestToolkit _reqToolkit)
    => GetStaticFile(_log, _reqToolkit, "/");

  public IResult GetRoomStaticFile(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromRoute(Name = "path")] string? _path)
  {
    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    return GetStaticFile(_log, _reqToolkit, $"room/{_path}");
  }

  [FailToBan(10, 600, HttpStatusCode.NotFound)]
  public IResult GetStaticFile(
    IScopedLog _log,
    IRequestToolkit _reqToolkit,
    [FromRoute(Name = "path")] string _path)
  {
    _log.Info($"Requested **static path** __{_path}__");

    if (string.IsNullOrWhiteSpace(_path) || _path == "/")
      _path = "index.html";

    var path = Path.Combine(p_appConfig.WebrootDirPath, _path);
    if (!File.Exists(path))
      return _reqToolkit.NotFound();

    if (_path.Contains("./") || _path.Contains(".\\") || _path.Contains("../") || _path.Contains("..\\"))
      return _reqToolkit.Forbidden($"Tried to get file not from webroot: '{_path}'");

    var mime = MimeTypes.GetMimeByExtension(path);
    var stream = File.OpenRead(path);

    return Results.Stream(stream, mime);
  }

}
