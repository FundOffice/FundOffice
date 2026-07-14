namespace FMO.Models;

/// <summary>
/// 销户工作事项
/// </summary>
public class AccountCancellationWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.AccountCancellation;
}
