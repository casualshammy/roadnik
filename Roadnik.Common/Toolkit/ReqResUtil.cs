using System.Text.RegularExpressions;

namespace Roadnik.Common.Toolkit;

public static class ReqResUtil
{
  private static readonly Regex p_safeStringRegex = new(@"^[a-zA-Z0-9-_]*$", RegexOptions.Compiled);
  private static readonly Regex p_safeUserMessageRegex = new(@"^[a-zA-Z0-9\-_\s\!\,\.]*$", RegexOptions.Compiled);

  public static bool IsKeySafe(string _data) => _data.Length > 3 && _data.Length <= 16 && p_safeStringRegex.IsMatch(_data);
  public static bool IsUserMessageSafe(string _data) => _data.Length <= 1024 && p_safeUserMessageRegex.IsMatch(_data);

}
