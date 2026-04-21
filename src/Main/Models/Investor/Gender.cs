using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum Gender
{
    [Description("请选择")] UNK,

    [Description("男")] Male,

    [Description("女")] Femaile,
}
