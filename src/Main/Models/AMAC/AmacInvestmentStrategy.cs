using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 投资策略枚举
/// </summary>
public enum AmacInvestmentStrategy
{
    /// <summary>
    /// 股票策略
    /// </summary>
    [Description("股票策略")]
    StockStrategy,

    /// <summary>
    /// 事件驱动
    /// </summary>
    [Description("事件驱动")]
    EventDriven,

    /// <summary>
    /// 套利策略
    /// </summary>
    [Description("套利策略")]
    ArbitrageStrategy,

    /// <summary>
    /// 宏观策略
    /// </summary>
    [Description("宏观策略")]
    MacroStrategy,

    /// <summary>
    /// 固定收益
    /// </summary>
    [Description("固定收益")]
    FixedIncome,

    /// <summary>
    /// 组合基金
    /// </summary>
    [Description("组合基金")]
    FundOfFunds,

    /// <summary>
    /// 复合策略
    /// </summary>
    [Description("复合策略")]
    CompositeStrategy,

    /// <summary>
    /// 其他策略
    /// </summary>
    [Description("其他策略")]
    OtherStrategy
}
