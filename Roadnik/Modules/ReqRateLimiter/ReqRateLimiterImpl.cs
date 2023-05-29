using Ax.Fw.Attributes;
using Roadnik.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace Roadnik.Modules.ReqRateLimiter;

[ExportClass(typeof(IReqRateLimiter), Singleton: true)]
internal class ReqRateLimiterImpl : IReqRateLimiter
{
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<IPAddress, long>> p_limiter = new();

  public bool IsReqOk(string _type, IPAddress? _ip, long _intervalMs)
  {
    if (_ip == null)
      return false;

    var now = Environment.TickCount64;
    var dictionary = p_limiter.GetOrAdd(_type, new ConcurrentDictionary<IPAddress, long>());
    if (dictionary.TryGetValue(_ip, out var lastGetReq) && now - lastGetReq < _intervalMs)
      return false;

    dictionary[_ip] = now;
    return true;
  }

}
