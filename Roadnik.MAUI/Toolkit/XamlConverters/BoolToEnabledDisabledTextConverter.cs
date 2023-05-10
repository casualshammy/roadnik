using System.Globalization;

namespace Roadnik.MAUI.Toolkit.XamlConverters;

public class BoolToEnabledDisabledTextConverter : IValueConverter
{
    private const string p_enabled = "Enabled";
    private const string p_disabled = "Disabled";

    public object Convert(object _value, Type _targetType, object _parameter, CultureInfo _culture)
    {
        if (_value is not bool b)
            throw new FormatException();

        return b ? p_enabled : p_disabled;
    }

    public object ConvertBack(object _value, Type _targetType, object _parameter, CultureInfo _culture)
    {
        if (_value is not string str)
            throw new FormatException();

        return str == p_enabled;
    }
}