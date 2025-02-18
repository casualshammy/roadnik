using Ax.Fw.SharedTypes.Interfaces;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class CorsMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly ILog p_log;

  public CorsMiddleware(
    RequestDelegate _next,
    ILog _log)
  {
    p_next = _next;
    p_log = _log["cors"];
  }

  public async Task Invoke(HttpContext _httpCtx)
  {
    var req = _httpCtx.Request;
    var originHeader = req.Headers.Origin.FirstOrDefault();
    if (originHeader == default)
    {
      await p_next(_httpCtx);
      return;
    }

    if (!originHeader.StartsWith("http://localhost") && originHeader != "https://webapp.local" && originHeader != "http://webapp.local:5544")
    {
      await p_next(_httpCtx);
      return;
    }

    if (req.Method == "OPTIONS")
    {
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Origin", $"{originHeader}");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Headers", "DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization");
      _httpCtx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
      _httpCtx.Response.Headers.Append("Access-Control-Max-Age", "1728000");
      _httpCtx.Response.Headers.Append("Content-Type", "text/plain; charset=utf-8");
      _httpCtx.Response.Headers.ContentLength = 0;
      _httpCtx.Response.StatusCode = 204;
      p_log.Info($"__OPTIONS__ request is **handled** for origin __'{originHeader}'__");
      return;
    }

    _httpCtx.Response.Headers.Append("Access-Control-Allow-Origin", $"{originHeader}");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Headers", "DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range,Authorization");
    _httpCtx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
    _httpCtx.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Length,Content-Range");
    p_log.Info($"__CORS headers__ were **injected** for origin __'{originHeader}'__");
    await p_next(_httpCtx);
  }
}
