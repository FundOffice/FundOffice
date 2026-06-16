namespace FMO.Models;

/// <summary>
/// 自查工作事项
/// </summary>
public class SelfInspectionWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.SelfInspection;
}
