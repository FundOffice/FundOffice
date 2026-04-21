using System.ComponentModel;

namespace FMO.Models;




[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FeePayFrequency
{
    [Description("月")]Month,

    [Description("季")]Quarter,

    [Description("其它")] Other
}


public class FeePayInfo
{
    public FeePayFrequency Type { get; set; }

    public string? Other { get; set; }

    public override string? ToString()
    {
        return Type switch { FeePayFrequency.Month => "按月支付", FeePayFrequency.Quarter => "按季支付", FeePayFrequency.Other => Other, _ => "未设置" };
    }
}