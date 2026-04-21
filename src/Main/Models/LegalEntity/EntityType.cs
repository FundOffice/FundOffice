using System.ComponentModel;

namespace FMO.Models;


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum EntityType
{
    Unk,

    [Description("自然人")] Natural,

    [Description("机构")] Institution,

    [Description("产品")] Product


}