using Ax.Fw.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Roadnik.Common.Toolkit;

public static class ReqResUtil
{
  private static readonly Regex p_roomIdRegex = new(@"^[a-zA-Z0-9\-]*$", RegexOptions.Compiled);
  private static readonly Regex p_safeUserMessageRegex = new(@"^[\d\w\-_\s\!\,\.\:\?]*$", RegexOptions.Compiled);
  private static readonly Regex p_safeUsernameRegex = new(@"^[a-zA-Z0-9\-_\@\#\$]*$", RegexOptions.Compiled);

  public static int MaxUserMsgLength { get; } = 1024;

  public static int MaxRoomIdLength { get; } = 16;
  public static int MinRoomIdLength { get; } = 8;

  public static int MinUsernameLength { get; } = 4;
  public static int MaxUsernameLength { get; } = 16;

  public static string UserAgent { get; } = "RoadnikApp";

  public static string[] ValidMapTypes { get; } = new[] { "cycle", "transport", "landscape", "outdoors" };

  public static bool IsRoomIdValid([NotNullWhen(true)] string? _data) => _data != null && _data.Length >= MinRoomIdLength && _data.Length <= MaxRoomIdLength && p_roomIdRegex.IsMatch(_data);
  public static bool IsUsernameSafe([NotNullWhen(true)] string? _data) => _data != null && _data.Length >= MinUsernameLength && _data.Length <= MaxUsernameLength && p_safeUsernameRegex.IsMatch(_data);
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

  public static string? GetMapAddress(
    string? _serverAddress, 
    string? _roomId, 
    string? _mapLayer,
    double? _lat,
    double? _lng,
    int? _zoom)
  {
    if (string.IsNullOrWhiteSpace(_serverAddress) || string.IsNullOrWhiteSpace(_roomId))
      return null;

    var urlBuilder = new UriBuilder(_serverAddress);
    urlBuilder.Path = "/r/";

    var query = HttpUtility.ParseQueryString(urlBuilder.Query);
    query["id"] = _roomId;
    if (!_mapLayer.IsNullOrWhiteSpace())
      query["map"] = _mapLayer;
    if (_lat != null)
      query["lat"] = _lat.Value.ToString(CultureInfo.InvariantCulture);
    if (_lng != null)
      query["lng"] = _lng.Value.ToString(CultureInfo.InvariantCulture);
    if (_zoom != null)
      query["zoom"] = _zoom.Value.ToString(CultureInfo.InvariantCulture);

    urlBuilder.Query = query.ToString();

    var url = urlBuilder.ToString();
    return url;
  }

}
