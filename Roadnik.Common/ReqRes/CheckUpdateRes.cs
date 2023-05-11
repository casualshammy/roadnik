using Ax.Fw;

namespace Roadnik.Common.ReqRes;

public record CheckUpdateRes(bool Success, SerializableVersion Version, string Url)
{
  public static  CheckUpdateRes Fail { get; } =  new(false, new SerializableVersion(0, 0, 0), string.Empty);
}