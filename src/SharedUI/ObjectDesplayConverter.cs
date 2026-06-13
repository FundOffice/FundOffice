using FMO.Models;
using System.Globalization;
using System.Windows.Data;

namespace FMO.Shared;

internal class ObjectDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e),
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
