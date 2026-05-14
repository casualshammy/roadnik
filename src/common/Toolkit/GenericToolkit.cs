using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Roadnik.Common.Toolkit;

public static class GenericToolkit
{
  private static readonly ConcurrentDictionary<Guid, string> p_appInstanceIdCache = new();

  public static string ConcealAppInstanceId(Guid _appInstanceId)
  {
    if (p_appInstanceIdCache.TryGetValue(_appInstanceId, out var cached))
      return cached;

    Span<byte> appIdBytes = stackalloc byte[16];
    if (!MemoryMarshal.TryWrite(appIdBytes, in _appInstanceId))
      throw new InvalidOperationException("Can't convert GUID to bytes");

    Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(appIdBytes, hashBytes);
    var concealedAppId = Convert.ToBase64String(hashBytes)[..8];

    p_appInstanceIdCache[_appInstanceId] = concealedAppId;
    return concealedAppId;
  }
}
