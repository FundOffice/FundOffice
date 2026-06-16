namespace FMO.Models;

/// <summary>
/// 账户类型
/// </summary>
public enum AccountOpeningType
{
    Securities,
    Futures,
    Bank,
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
