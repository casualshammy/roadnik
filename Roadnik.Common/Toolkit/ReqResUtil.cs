using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Roadnik.Common.Toolkit;

public static partial class ReqResUtil
{
  private static readonly Regex p_roomIdRegex = GetRoomIdRegex();
  private static readonly Regex p_safeUserMessageRegex = GetSafeUserMsgRegex();
  private static readonly Regex p_safeUsernameRegex = GetSafeUsernameRegex();

  public static int MaxUserMsgLength { get; } = 1024;

  public const int MaxRoomIdLength = 32;
  public static int MinRoomIdLength { get; } = 8;

  public const int MinUsernameLength = 4;
  public const int MaxUsernameLength = 16;

  public static string UserAgent { get; } = "RoadnikApp";

  public static FrozenSet<string> ValidMapTypes { get; } = new[] { "cycle", "transport", "landscape", "outdoors" }.ToFrozenSet();

  public static bool IsRoomIdValid([NotNullWhen(true)] string? _data) 
    => _data != null && _data.Length >= MinRoomIdLength && _data.Length <= MaxRoomIdLength && p_roomIdRegex.IsMatch(_data);

  public static bool IsUsernameSafe([NotNullWhen(true)] string? _data) 
    => _data != null && _data.Length >= MinUsernameLength && _data.Length <= MaxUsernameLength && p_safeUsernameRegex.IsMatch(_data);

  public static bool IsUserDefinedStringSafe([NotNullWhen(true)] string? _data) => _data != null && _data.Length <= MaxUserMsgLength && p_safeUserMessageRegex.IsMatch(_data);
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

  [GeneratedRegex(@"^[a-zA-Z0-9\-]*$", RegexOptions.Compiled)]
  private static partial Regex GetRoomIdRegex();
  [GeneratedRegex(@"^[\d\w\-_\s\!\,\.\:\?]*$", RegexOptions.Compiled)]
  private static partial Regex GetSafeUserMsgRegex();
  [GeneratedRegex(@"^[a-zA-Z0-9\-_\@\#\$]*$", RegexOptions.Compiled)]
  private static partial Regex GetSafeUsernameRegex();
}
