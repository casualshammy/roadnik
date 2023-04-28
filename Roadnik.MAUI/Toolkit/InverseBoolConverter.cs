using System.Globalization;

namespace Roadnik.MAUI.Toolkit;

public class InverseBoolConverter : IValueConverter
{
  public object Convert(object _value, Type _targetType, object _parameter, CultureInfo _culture)
  {
    return !((bool)_value);
  }

  public object ConvertBack(object _value, Type _targetType, object _parameter, CultureInfo _culture)
  {
    return _value;
  }
}
