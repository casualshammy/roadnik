using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Attributes;

namespace Roadnik.Server.Modules.WebServer.Middlewares;

public class FailToBanMiddleware
{
  private readonly RequestDelegate p_next;
  private readonly TimeWall p_failToBanTimeWall;
  private readonly ILog p_log;
  private long p_reqCount = -1;

  public FailToBanMiddleware(
    RequestDelegate _next,
    ILog _log)
  {
    p_next = _next;
    p_log = _log;
    p_failToBanTimeWall = new TimeWall(10, TimeSpan.FromSeconds(10));
  }

  public async Task Invoke(HttpContext _ctx)
  {
    var attrExist = _ctx.GetEndpoint()?.Metadata.GetMetadata<FailToBanAttribute>();
    if (attrExist == null)
    {
      await p_next(_ctx);
      return;
    }

    await p_next(_ctx);

    var response = _ctx.Response;
    if (response.StatusCode >= 200 && response.StatusCode < 300)
      return;
  }
}