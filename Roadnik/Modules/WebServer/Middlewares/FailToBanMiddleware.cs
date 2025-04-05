using Ax.Fw;
using Ax.Fw.Cache;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Attributes;
using System.Collections.Concurrent;
using System.Net;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

public class FailToBanMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly ConcurrentDictionary<IPAddress, int> p_failedReqLut;
  private readonly ILog p_log;
  private long p_reqCount = -1;

  public FailToBanMiddleware(
    RequestDelegate _next,
    ILog _log)
  {
    p_next = _next;
    p_log = _log["fail-to-ban"];
    p_failedReqLut = new();

    // add clean of p_failedReqLut
  }

  public async Task Invoke(HttpContext _ctx)
  {
    var attrExist = _ctx.GetEndpoint()?.Metadata.GetMetadata<FailToBanAttribute>();
    if (attrExist == null)
    {
      await p_next(_ctx);
      return;
    }

    var remoteIP = _ctx.Connection.RemoteIpAddress;
    if (remoteIP == null)
    {
      p_log.Warn($"IP address is unknown");
      _ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
      return;
    }

    if (p_failedReqLut.TryGetValue(remoteIP, out var failedReq) && failedReq >= 10)
    {
      p_log.Warn($"IP address '{remoteIP}' is banned, but still trying to make requests");
      _ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
      return;
    }

    await p_next(_ctx);

    var response = _ctx.Response;
    if (response.StatusCode < 400 || response.StatusCode >= 500)
      return;

    p_failedReqLut.AddOrUpdate(remoteIP, 1, (_, _prev) => ++_prev);
  }
}