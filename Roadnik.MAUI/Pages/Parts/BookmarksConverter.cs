using Roadnik.MAUI.Data;
using System.Globalization;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages.Parts;

internal class BookmarksConverter : IValueConverter
{
  public object? Convert(object? _value, Type _targetType, object? _parameter, CultureInfo _culture)
  {
    var entry = _value as BookmarkEntry;
    if (entry == null)
      return string.Empty;

    if (_parameter is not string param)
      return string.Empty;

    if (_targetType != typeof(string))
      return string.Empty;

    if (param.Equals("roomid", StringComparison.InvariantCultureIgnoreCase))
      return $"{L.page_bookmarks_room}: {entry.RoomId}";
    else if (param.Equals("username", StringComparison.InvariantCultureIgnoreCase))
      return $"{L.page_bookmarks_username}: {entry.Username}";
    else
      return string.Empty;
  }

  public object? ConvertBack(object? _value, Type _targetType, object? _parameter, CultureInfo _culture) => null;

}
