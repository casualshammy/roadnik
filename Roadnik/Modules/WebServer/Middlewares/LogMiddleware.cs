using System.Net;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

public class LogMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly Ax.Fw.SharedTypes.Interfaces.ILog p_log;
  private long p_reqCount = -1;

  public LogMiddleware(
    RequestDelegate _next,
    Ax.Fw.SharedTypes.Interfaces.ILog _log)
  {
    p_next = _next;
    p_log = _log;
  }

  public async Task Invoke(HttpContext _context)
  {
    var request = _context.Request;
    var reqIndex = Interlocked.Increment(ref p_reqCount);

    p_log.Info($"[{reqIndex}] --> **{request.Method}** __{request.Path}__");
    await p_next(_context);
    p_log.Info($"[{reqIndex}] <-- **{request.Method}** __{request.Path}__ {(HttpStatusCode)_context.Response.StatusCode}");
  }
}
