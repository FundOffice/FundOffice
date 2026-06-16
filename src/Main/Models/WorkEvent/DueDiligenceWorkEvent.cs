using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 尽调类型
/// </summary>
public enum DueDiligenceType
{
    [Description("首次尽调")]
    Initial,

    [Description("常规尽调")]
    Regular,

    [Description("临时尽调")]
    AdHoc,
}

/// <summary>
/// 尽调工作事项
/// </summary>
public class DueDiligenceWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.DueDiligence;

    /// <summary>
    /// 尽调类型
    /// </summary>
    public DueDiligenceType DueDiligenceType { get; set; }

    /// <summary>
    /// 尽调结果
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 尽调对象
    /// </summary>
    public string? Target { get; set; }
}
