using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FundAccountType
{
    None,

    [Description("资金存管账户")] Custody,
    /// <summary>
    /// 募集账户
    /// </summary>
    [Description("募集账户")]
    Collection,

    /// <summary>
    /// 券商保证金账户
    /// </summary>
    [Description("券商保证金账户")]
    Stock,

    /// <summary>
    /// 期货结算账户
    /// </summary>
    [Description("期货结算账户")]
    Futures,

    /// <summary>
    /// 衍生品结算账户
    /// </summary>
    [Description("衍生品结算账户")]
    Derivatives,

    /// <summary>
    /// 券商信用账户
    /// </summary>
    [Description("券商信用账户")]
    Credit,

    /// <summary>
    /// 黄金结算账户
    /// </summary>
    [Description("黄金结算账户")]
    Gold
}
