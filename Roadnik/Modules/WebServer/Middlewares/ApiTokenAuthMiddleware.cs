using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Attributes;
using Roadnik.Server.Interfaces;
using System.Net;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class ApiTokenAuthMiddleware
{
  private readonly ILog p_log;
  private readonly RequestDelegate p_next;
  private readonly IAppConfig p_config;

  public ApiTokenAuthMiddleware(
    ILog _log,
    RequestDelegate _next,
    IAppConfig _config)
  {
    p_log = _log["auth-middleware"];
    p_next = _next;
    p_config = _config;
  }

  public async Task Invoke(HttpContext _ctx)
  {
    var isProtected = _ctx.GetEndpoint()?.Metadata.GetMetadata<ApiTokenRequired>();
    if (isProtected == null)
    {
      await p_next(_ctx);
      return;
    }

    var apiKeyStr = _ctx.Request.Headers["api-key"].FirstOrDefault();
    if (apiKeyStr.IsNullOrWhiteSpace())
    {
      p_log.Warn($"Unauthorized access from ip {_ctx.Connection.RemoteIpAddress} (api key is missed)");
      _ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
      return;
    }

    var adminApiKey = p_config.AdminApiKey;
    if (adminApiKey.IsNullOrWhiteSpace())
    {
      p_log.Warn($"Unauthorized access from ip {_ctx.Connection.RemoteIpAddress} (api key is not set in config)");
      _ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
      return;
    }

    if (adminApiKey != apiKeyStr)
    {
      p_log.Warn($"Unauthorized access from ip {_ctx.Connection.RemoteIpAddress} (invalid api key)");
      _ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
      return;
    }

    await p_next(_ctx);
  }
}
