using Ax.Fw;
using System.Net;

namespace Roadnik.Interfaces;

public interface IReqRateLimiter
{
  bool IsReqOk(string _type, IPAddress? _ip, long _intervalMs);
  bool IsReqTimewallOk(string _type, IPAddress? _ip, Func<TimeWall> _timewallFactory);
}
