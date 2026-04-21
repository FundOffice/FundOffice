using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FMO.Shared;

public class BooleanToVisibilityReverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch { true => Visibility.Collapsed, false => Visibility.Visible, _ => value };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch { Visibility.Collapsed or Visibility.Hidden => true, Visibility.Visible => false, _ => value };
    }
}
