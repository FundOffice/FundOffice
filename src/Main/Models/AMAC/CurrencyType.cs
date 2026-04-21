using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 币种类型
/// </summary>
public enum CurrencyType
{
    /// <summary>
    /// 人民币
    /// </summary>
    [Description("人民币")]
    CNY = 0,

    /// <summary>
    /// 美元
    /// </summary>
    [Description("美元")]
    USD = 1,

    /// <summary>
    /// 港元
    /// </summary>
    [Description("港元")]
    HKD = 2,

    /// <summary>
    /// 澳元
    /// </summary>
    [Description("澳元")]
    AUD = 3,

    /// <summary>
    /// 欧元
    /// </summary>
    [Description("欧元")]
    EUR = 4,

    /// <summary>
    /// 英镑
    /// </summary>
    [Description("英镑")]
    GBP = 5,

    /// <summary>
    /// 日元
    /// </summary>
    [Description("日元")]
    JPY = 6,

    /// <summary>
    /// 加元
    /// </summary>
    [Description("加元")]
    CAD = 7,

    /// <summary>
    /// 新西兰元
    /// </summary>
    [Description("新西兰元")]
    NZD = 8,

    /// <summary>
    /// 新加坡元
    /// </summary>
    [Description("新加坡元")]
    SGD = 9,

    /// <summary>
    /// 多币种
    /// </summary>
    [Description("多币种")]
    MultiCurrency = 10
}
