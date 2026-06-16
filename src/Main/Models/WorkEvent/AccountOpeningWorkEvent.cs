using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 账户类型
/// </summary>
public enum AccountOpeningType
{
    [Description("证券")]
    Securities,

    [Description("期货")]
    Futures,

    [Description("银行")]
    Bank,

    [Description("其他")]
    Other,
}

/// <summary>
/// 开户工作事项
/// </summary>
public class AccountOpeningWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.AccountOpening;

    /// <summary>
    /// 账户类型
    /// </summary>
    public AccountOpeningType AccountType { get; set; }

    /// <summary>
    /// 开户日期
    /// </summary>
    public DateTime? OpenDate { get; set; }

    /// <summary>
    /// 开户机构
    /// </summary>
    public string? Institution { get; set; }
}
