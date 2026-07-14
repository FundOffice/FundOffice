namespace FMO.Models;

/// <summary>
/// 账户其它工作事项
/// </summary>
public class AccountOtherWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.AccountOther;
}