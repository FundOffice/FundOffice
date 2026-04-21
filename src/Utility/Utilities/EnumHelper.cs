using System.ComponentModel;

namespace FMO.Utilities;

public static class EnumHelper
{

    public static T? FromDescription<T>(string str) where T : Enum
    {
        var type = typeof(T);
        foreach (var name in type.GetEnumNames())
        {
            var fieldInfo = type.GetField(name);

            var attributes = (DescriptionAttribute[])fieldInfo!.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes?.Length > 0 && attributes[0].Description == str)
            {
                return (T)Enum.Parse(type, name);
            }

        }

        throw new InvalidCastException($"{type}中未找到【{str}】对应的值");
        //return  default(T);
    }
}