using Ax.Fw.SharedTypes.Interfaces;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class DebugCorsMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly ILog p_log;

  public DebugCorsMiddleware(
    RequestDelegate _next,
    ILog _log)
  {
    p_next = _next;
    p_log = _log["debug-cors"];
  }

  public async Task Invoke(HttpContext _httpCtx)
  {
#if !DEBUG
    await p_next(_httpCtx);
    return;
#endif

    var remoteAddress = _httpCtx.Connection.RemoteIpAddress?.ToString();
    if (remoteAddress != "localhost" && remoteAddress != "127.0.0.1")
    {
      await p_next(_httpCtx);
      return;
    }

    var req = _httpCtx.Request;
    if (req.Method == "OPTIONS")
    {
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost:5173");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Headers", "DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
      _httpCtx.Response.Headers.Append("Access-Control-Max-Age", "1728000");
      _httpCtx.Response.Headers.Append("Content-Type", "text/plain; charset=utf-8");
      _httpCtx.Response.Headers.ContentLength = 0;
      _httpCtx.Response.StatusCode = 204;
      p_log.Info($"__OPTIONS__ request is **handled**");
      return;
    }

    _httpCtx.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost:5173");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Headers", "DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
    _httpCtx.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Length,Content-Range");
    p_log.Info($"__CORS headers__ were **injected**");
    await p_next(_httpCtx);
  }
}
