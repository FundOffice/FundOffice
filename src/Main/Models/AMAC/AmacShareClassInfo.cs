using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 份额类别信息
/// </summary>
public class AmacShareClassInfo
{
    /// <summary>
    /// 份额名称 (例如: "优先级A", "劣后级B")
    /// </summary>
    public required string ShareName { get; set; }

    /// <summary>
    /// 份额类别 (例如: "优先级", "劣后级")
    /// </summary>
    public ShareClassType ShareType { get; set; }

    /// <summary>
    /// 收益安排描述 (例如: "固定收益", "浮动收益", "参与剩余收益分配")
    /// </summary>
    public IncomeArrangementType ReturnArrangement { get; set; }
}



/// <summary>
/// 份额类别（用于结构化产品）
/// </summary>
public enum ShareClassType
{
    /// <summary>
    /// 优先级（低风险）
    /// </summary>
    [Description("优先级（低风险）")]
    Senior = 0,

    /// <summary>
    /// 劣后级（高风险）
    /// </summary>
    [Description("劣后级（高风险）")]
    Junior = 1,

    /// <summary>
    /// 其他
    /// </summary>
    [Description("其他")]
    Other = 2
}


/// <summary>
/// 收益安排类型
/// </summary>
public enum IncomeArrangementType
{
    /// <summary>
    /// 设定基准收益
    /// </summary>
    [Description("设定基准收益")]
    FixedBenchmark = 0,

    /// <summary>
    /// 分配比例
    /// </summary>
    [Description("分配比例")]
    DistributionRatio = 1,

    /// <summary>
    /// 剩余收益
    /// </summary>
    [Description("剩余收益")]
    ResidualIncome = 2,

    /// <summary>
    /// 设定基准收益+部分浮动收益
    /// </summary>
    [Description("设定基准收益+部分浮动收益")]
    FixedPlusFloating = 3,

    /// <summary>
    /// 业绩比较基准收益
    /// </summary>
    [Description("业绩比较基准收益")]
    PerformanceBenchmark = 4,

    /// <summary>
    /// 其他
    /// </summary>
    [Description("其他")]
    Other = 5
}