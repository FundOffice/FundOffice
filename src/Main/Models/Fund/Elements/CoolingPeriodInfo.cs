using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum CoolingPeriodType
{
    [Description("24小时")]OneDay,

    [Description("其它")] Other
}


public class CoolingPeriodInfo
{
    public CoolingPeriodType Type { get; set; }

    public string? Other { get; set; }


    public override string ToString()
    {
        return Type switch
        {
            CoolingPeriodType.OneDay => "24小时",
            CoolingPeriodType.Other => Other ?? "其它",
            _ => "未知"
        };
    }
}