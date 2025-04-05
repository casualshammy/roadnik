namespace Roadnik.Server.Toolkit;

internal static class Extensions
{
  public static HttpRequestMessage WithNkHeaders(this HttpRequestMessage _msg)
  {
    _msg.Headers.Add("Referer", "https://nakarte.me/");
    _msg.Headers.Add("User-Agent", "Chrome");
    return _msg;
  }
}
