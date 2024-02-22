using System.Globalization;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages.Parts;

internal class BookmarksRoomConverter : IValueConverter
{
  public object? Convert(object? _value, Type _targetType, object? _parameter, CultureInfo _culture)
  {
    return $"{L.page_bookmarks_room}: {_value as string}";
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}

internal class BookmarksUsernameConverter : IValueConverter
{
  public object? Convert(object? _value, Type _targetType, object? _parameter, CultureInfo _culture)
  {
    return $"{L.page_bookmarks_username}: {_value as string}";
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
