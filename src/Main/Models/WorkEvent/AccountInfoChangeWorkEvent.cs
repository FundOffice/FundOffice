using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 账户资料变更类型
/// </summary>
public enum AccountInfoChangeType
{
    [Description("名称")]
    Name,

    [Description("地址")]
    Address,

    [Description("联系方式")]
    Contact,

    [Description("银行信息")]
    BankInfo,

    [Description("其他")]
    Other,
}

/// <summary>
/// 账户资料变更工作事项
/// </summary>
public class AccountInfoChangeWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.AccountInfoChange;

    /// <summary>
    /// 变更类型
    /// </summary>
    public AccountInfoChangeType ChangeType { get; set; }

    /// <summary>
    /// 原始值
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// 新值
    /// </summary>
    public string? NewValue { get; set; }
}
