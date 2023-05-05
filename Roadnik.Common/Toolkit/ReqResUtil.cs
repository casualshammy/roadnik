using System.Text;
using System.Text.RegularExpressions;

namespace Roadnik.Common.Toolkit;

public static class ReqResUtil
{
  private static readonly Regex p_safeStringRegex = new(@"^[a-zA-Z0-9\-]*$", RegexOptions.Compiled);
  private static readonly Regex p_safeUserMessageRegex = new(@"^[\d\w\-_\s\!\,\.\:\?]*$", RegexOptions.Compiled);

  public static int MaxUserMsgLength { get; } = 1024;
  public static int MaxKeyKength { get; } = 16;
  public static int MinKeyKength { get; } = 4;

  public static bool IsKeySafe(string _data) => _data.Length >= MinKeyKength && _data.Length <= MaxKeyKength && p_safeStringRegex.IsMatch(_data);
  public static bool IsUserDefinedStringSafe(string _data) => _data.Length <= MaxUserMsgLength && p_safeUserMessageRegex.IsMatch(_data);
  public static string ClearUserMsg(string _data)
  {
    if (IsUserDefinedStringSafe(_data))
      return _data;

    var sb = new StringBuilder();
    foreach (var c in _data)
    {
      if (char.IsLetterOrDigit(c))
        sb.Append(c);
      if (c == '-' || c == '_' || c == ' ' || c == '!' || c == ',' || c == '.' || c == ':' || c == '?')
        sb.Append(c);
    }
    return sb.ToString();
  }

}
