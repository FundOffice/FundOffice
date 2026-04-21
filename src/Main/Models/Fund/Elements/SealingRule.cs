using System.ComponentModel;

namespace FMO.Models;

public enum SealingType
{
    [Description("无")] No,

    [Description("有")] Has,

    [Description("其它")] Other,
}


public class SealingRule
{
    /// <summary>
    /// 封闭类型
    /// </summary>
    public SealingType Type { get; set; }

    /// <summary>
    /// 封闭月数
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// 其它
    /// </summary>
    public string? Extra { get; set; }
}
