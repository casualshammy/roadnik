using Ax.Fw.Extensions;
using Roadnik.Server.Interfaces;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

class AdminAccessMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly IReadOnlySet<string> p_reqAllowedWithoutAuth;
  private readonly ISettingsController p_settingsController;

  public AdminAccessMiddleware(
    RequestDelegate _next,
    IReadOnlySet<string> _pathsRequiresAdminRignts,
    ISettingsController _settingsController)
  {
    p_next = _next;
    p_reqAllowedWithoutAuth = _pathsRequiresAdminRignts;
    p_settingsController = _settingsController;
  }

  public async Task Invoke(HttpContext _context)
  {
    var request = _context.Request;
    var path = request.Path.ToString().Trim().Trim('/').Trim();
    if (!p_reqAllowedWithoutAuth.Contains(path))
    {
      await p_next(_context);
      return;
    }

    int? errorStatusCode = null;
    var apiKeyStr = _context.Request.Headers["api-key"].FirstOrDefault();
    if (apiKeyStr.IsNullOrEmpty())
      errorStatusCode = StatusCodes.Status403Forbidden;
    else if (p_settingsController.Settings.Value?.AdminApiKey != apiKeyStr)
      errorStatusCode = StatusCodes.Status403Forbidden;

    if (errorStatusCode != null)
    {
      _context.Response.StatusCode = errorStatusCode.Value;
      return;
    }

    await p_next(_context);
  }
}
