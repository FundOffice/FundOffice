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

    public string? Extra { get; set; }
}
