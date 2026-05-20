using FMO.Models;
using System.Globalization;
using System.Windows.Data;

namespace FMO.Shared;


public class DisplayValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            IDisplay t => t.Transfrom(),
            Enum e => EnumDescriptionTypeConverter.GetEnumDescription(e),
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("此转换器仅支持单向绑定");
    }
}
