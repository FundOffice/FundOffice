namespace FMO.Models;

/// <summary>
/// 自定义工作事项
/// </summary>
public class CustomWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.Custom;

    /// <summary>
    /// 自定义分类
    /// </summary>
    public string? Category { get; set; }
}
