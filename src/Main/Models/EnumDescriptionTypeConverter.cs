using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FMO.Models;

 
public class EnumDescriptionTypeConverter : EnumConverter
{
    public EnumDescriptionTypeConverter(Type type) : base(type)
    {
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || sourceType == typeof(long) || TypeDescriptor.GetConverter(typeof(Enum)).CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string)
            return GetEnumValue(EnumType, (string)value);
        if (value is Enum)
            return GetEnumDescription((Enum)value);
        return base.ConvertFrom(context, culture, value);
    }


    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return value is Enum && destinationType == typeof(string)
            ? GetEnumDescription((Enum)value)
            : (value is string && destinationType == typeof(string)
              ? GetEnumDescription(EnumType, (string)value)
              : base.ConvertTo(context, culture, value, destinationType));
    }

    public static string GetEnumDescription(Enum value)
    {
        var type = value.GetType();

        // 支持 [Flags] 枚举：逐标志位拆解并拼接描述
        if (type.IsDefined(typeof(FlagsAttribute), false))
        {
            var longValue = Convert.ToInt64(value);
            if (longValue == 0)
                return GetSingleFieldDescription(type, value.ToString()) ?? value.ToString();

            var descriptions = new List<string>();
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var fieldValue = Convert.ToInt64(field.GetRawConstantValue());
                if (fieldValue == 0) continue;
                if ((longValue & fieldValue) == fieldValue)
                {
                    descriptions.Add(GetSingleFieldDescription(type, field.Name) ?? field.Name);
                }
            }
            return descriptions.Count > 0 ? string.Join('、', descriptions) : value.ToString();
        }

        return GetSingleFieldDescription(type, value.ToString()) ?? value.ToString();
    }

    private static string? GetSingleFieldDescription(Type type, string name)
    {
        var fieldInfo = type.GetField(name);
        if (fieldInfo == null) return null;
        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? string.Join(',', attributes.Select(x => x.Description)) : null;
    }




    public static string GetEnumDescription(Type value, string name)
    {
        var fieldInfo = value.GetField(name);
        if (fieldInfo is null) return name;

        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return (attributes.Length > 0) ? attributes[0].Description : name;
    }

    public static object? GetEnumValue(Type value, string description)
    {
        var fields = value.GetFields();
        foreach (var fieldInfo in fields)
        {
            var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes.Length > 0 && attributes[0].Description == description)
                return fieldInfo.GetValue(fieldInfo.Name);
            if (fieldInfo.Name == description)
                return fieldInfo.GetValue(fieldInfo.Name);
        }
        return description;
    }
}
