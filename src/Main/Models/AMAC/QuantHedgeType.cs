using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 量化/对冲基金类型
/// </summary>
public enum QuantHedgeType
{
    /// <summary>
    /// 量化
    /// </summary>
    [Description("量化")]
    Quantitative = 1, // 对应 value="PA01"

    /// <summary>
    /// 对冲
    /// </summary>
    [Description("对冲")]
    Hedge = 2, // 对应 value="PA02"

    /// <summary>
    /// 非量化对冲
    /// </summary>
    [Description("非量化对冲")]
    NonQuantitativeHedge = 4 // 对应 value="PA04"
}
