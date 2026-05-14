using System.Globalization;

namespace Roadnik.MAUI.Converters;

internal class MultiBoolConverter : IMultiValueConverter
{
  public object Convert(
    object[]? _values,
    Type _targetType,
    object _parameter,
    CultureInfo _culture)
  {
    if (_values == null || !_targetType.IsAssignableFrom(typeof(bool)))
      return false;

    foreach (var value in _values)
    {
      if (value is not bool b)
        return false;
      else if (!b)
        return false;
    }

    return true;
  }

  public object[] ConvertBack(
    object value, 
    Type[] targetTypes, 
    object parameter, 
    CultureInfo culture)
    => throw new NotImplementedException();

}
