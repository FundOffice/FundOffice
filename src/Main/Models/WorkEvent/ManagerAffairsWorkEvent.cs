using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 管理人事务类型
/// </summary>
public enum ManagerAffairsType
{
    [Description("登记备案")]
    Registration,

    [Description("资质申请")]
    Qualification,

    [Description("信息变更")]
    InformationChange,

    [Description("其他")]
    Other,
}

/// <summary>
/// 管理人事务工作事项
/// </summary>
public class ManagerAffairsWorkEvent : WorkEvent
{
    public override WorkEventType Type => WorkEventType.ManagerAffairs;

    /// <summary>
    /// 事务类型
    /// </summary>
    public ManagerAffairsType AffairsType { get; set; }

    /// <summary>
    /// 处理人
    /// </summary>
    public string? Handler { get; set; }
}
