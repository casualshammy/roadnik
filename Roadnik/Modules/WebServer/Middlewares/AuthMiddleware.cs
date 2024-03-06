using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

class AuthMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly GenericController p_ctrl;
  private readonly ILog p_log;

  public AuthMiddleware(
    RequestDelegate _next,
    GenericController _ctrl,
    ILog _log)
  {
    p_next = _next;
    p_ctrl = _ctrl;
    p_log = _log["auth"];
  }

  public async Task Invoke(HttpContext _context)
  {
    var request = _context.Request;
    var allowed = await p_ctrl.AuthAsync(request, _context.RequestAborted);
    if (allowed)
    {
      await p_next(_context);
      return;
    }

    p_log.Warn($"Unauthorized access from ip {_context.Connection.RemoteIpAddress}");
    _context.Response.StatusCode = StatusCodes.Status403Forbidden;
  }
}
