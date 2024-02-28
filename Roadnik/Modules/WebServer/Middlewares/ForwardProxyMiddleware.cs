using Ax.Fw.Extensions;
using System.Net;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class ForwardProxyMiddleware
{
  private readonly RequestDelegate p_next;

  public ForwardProxyMiddleware(
    RequestDelegate _next)
  {
    p_next = _next;
  }

  public async Task Invoke(HttpContext _ctx)
  {
    if (_ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfConnectingIp))
    {
      var headerValue = cfConnectingIp.ToString();
      if (!headerValue.IsNullOrWhiteSpace() && IPAddress.TryParse(headerValue, out var ip))
      {
        _ctx.Connection.RemoteIpAddress = ip;
        await p_next(_ctx);
        return;
      }
    }

    if (_ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xForwardedFor))
    {
      var headerValue = xForwardedFor.ToString();
      if (!headerValue.IsNullOrWhiteSpace())
      {
        var split = headerValue.Split(',', StringSplitOptions.TrimEntries);
        if (split.Length > 0 && IPAddress.TryParse(split[0], out var ip))
        {
          _ctx.Connection.RemoteIpAddress = ip;
          await p_next(_ctx);
          return;
        }
      }
    }

    await p_next(_ctx);
  }

}
