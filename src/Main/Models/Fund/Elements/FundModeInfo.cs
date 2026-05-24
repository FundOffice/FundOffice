using System.ComponentModel;

namespace FMO.Models;


//[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum FundMode
{
    [Description("开放式")] Open,

    [Description("封闭式")] Close,

    [Description("其它")] Other,
}




public class FundModeInfo 
{
    public FundMode Mode { get; set; }

    public string? Other { get; set; }

    public override string ToString() => Mode switch { FundMode.Open => "开放式", FundMode.Close => "封闭式", FundMode.Other => Other ?? "未设置", _ => "未设置" };
}
