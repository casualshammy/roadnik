using Roadnik.Server.Interfaces;
using System.Net;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class LogMiddleware : IMiddleware
{
  private static long p_reqCount = -1;
  private readonly IScopedLog p_log;
  
  public LogMiddleware(
    IScopedLog _log)
  {
    p_log = _log;
  }

  public async Task InvokeAsync(
    HttpContext _ctx, 
    RequestDelegate _next)
  {
    var request = _ctx.Request;
    var reqIndex = Interlocked.Increment(ref p_reqCount);

    p_log.Info($"[{reqIndex}] --> **{request.Method}** __{request.Path}__");
    var startTime = Environment.TickCount64;
    await _next(_ctx);
    var elapsedMs = Environment.TickCount64 - startTime;
    p_log.Info($"[{reqIndex}] <-- **{request.Method}** __{request.Path}__ {(HttpStatusCode)_ctx.Response.StatusCode} (__{elapsedMs} ms__)");
  }
}
