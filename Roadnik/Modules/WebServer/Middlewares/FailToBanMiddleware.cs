using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Attributes;
using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

internal class FailToBanMiddleware : IMiddleware
{
  private readonly ConcurrentDictionary<IPAddress, int> p_failedReqLut;
  private readonly ILog p_log;

  public FailToBanMiddleware(
    IReadOnlyLifetime _lifetime,
    ILog _log)
  {
    p_log = _log["fail-to-ban"];
    p_failedReqLut = new();

    Observable
      .Interval(TimeSpan.FromSeconds(1))
      .Subscribe(__ =>
      {
        foreach (var (ip, failCount) in p_failedReqLut)
        {
          var newValue = failCount - 1;
          if (newValue <= 0)
          {
            p_failedReqLut.TryRemove(ip, out _);
            p_log.Info($"IP address '__{ip}__' is **unbanned**");
          }
          else
          {
            p_failedReqLut.TryUpdate(ip, newValue, failCount);
          }
        }
      }, _lifetime);
  }

  public async Task InvokeAsync(HttpContext _ctx, RequestDelegate _next)
  {
    var attrExist = _ctx.GetEndpoint()?.Metadata.GetMetadata<FailToBanAttribute>();
    if (attrExist == null)
    {
      await _next(_ctx);
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
      p_log.Warn($"IP address '{remoteIP}' is banned, but still trying to make requests ({failedReq})");
      _ctx.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
      p_failedReqLut.AddOrUpdate(remoteIP, 1, (_, _prev) => ++_prev);
      return;
    }

    await _next(_ctx);

    var response = _ctx.Response;
    if (response.StatusCode != (int)HttpStatusCode.NotFound)
      return;

    p_failedReqLut.AddOrUpdate(remoteIP, 1, (_, _prev) => ++_prev);
  }

}