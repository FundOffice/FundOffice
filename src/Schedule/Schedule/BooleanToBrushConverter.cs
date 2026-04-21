using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FMO.Schedule;

public class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            bool b => b ? Brushes.Green : Brushes.Red,
            _ => value
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}