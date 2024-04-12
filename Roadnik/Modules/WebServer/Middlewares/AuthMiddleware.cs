using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Toolkit;
using System.Collections.Frozen;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class AuthMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly FrozenSet<GenericController> p_ctrls;
  private readonly ILog p_log;

  public AuthMiddleware(
    RequestDelegate _next,
    ILog _log,
    GenericController[] _ctrls)
  {
    p_next = _next;
    p_ctrls = _ctrls.ToFrozenSet();
    p_log = _log;
  }

  public async Task Invoke(HttpContext _context)
  {
    var request = _context.Request;
    foreach (var ctrl in p_ctrls)
    {
      var normPath = request.Path.ToString().TrimStart('/');
      if (!normPath.StartsWith(ctrl.Route))
        continue;

      if (!await ctrl.AuthAsync(request, _context.RequestAborted))
      {
        p_log.Warn($"Unauthorized access from ip {_context.Connection.RemoteIpAddress}");
        _context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
      }
    }

    await p_next(_context);
  }
}
