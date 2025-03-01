﻿using Ax.Fw;
using Roadnik.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace Roadnik.Server.Modules.ReqRateLimiter;

internal class ReqRateLimiterImpl : IReqRateLimiter
{
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<IPAddress, long>> p_limiter = new();
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<IPAddress, TimeWall>> p_timeWallLimiter = new();

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

  public bool IsReqTimewallOk(string _type, IPAddress? _ip, Func<TimeWall> _timewallFactory)
  {
    if (_ip == null)
      return false;

    var dictionary = p_timeWallLimiter.GetOrAdd(_type, new ConcurrentDictionary<IPAddress, TimeWall>());
    if (!dictionary.TryGetValue(_ip, out var timewall))
      dictionary.TryAdd(_ip, timewall = _timewallFactory());

    return timewall.TryGetTicket();
  }

}
