using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 工作事项类型
/// </summary>
public enum WorkEventType
{
    [Description("自定义")]
    Custom,

    [Description("开户")]
    AccountOpening,

    [Description("尽调")]
    DueDiligence,

    [Description("管理人事务")]
    ManagerAffairs,

    [Description("账户资料变更")]
    AccountInfoChange,
}

/// <summary>
/// 工作事项状态
/// </summary>
public enum WorkEventStatus
{
    [Description("待处理")]
    Pending,

    [Description("进行中")]
    InProgress,

    [Description("已完成")]
    Completed,

    [Description("已取消")]
    Cancelled,
}

/// <summary>
/// 工作事项基类，用于记录和管理各种工作事件
/// </summary>
public class WorkEvent
{
    public int Id { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 工作事项类型
    /// </summary>
    public virtual WorkEventType Type { get; set; } = WorkEventType.Custom;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 截止时间
    /// </summary>
    public DateTime? DueTime { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public WorkEventStatus Status { get; set; } = WorkEventStatus.Pending;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// 关联类型，如 Fund、Manager、Investor 等（旧版单关联，保留兼容）
    /// </summary>
    public string? LinkType { get; set; }

    /// <summary>
    /// 关联对象 Id（旧版单关联，保留兼容）
    /// </summary>
    public int LinkId { get; set; }

    /// <summary>
    /// 关联对象名称（旧版单关联，保留兼容）
    /// </summary>
    public string? LinkName { get; set; }

    /// <summary>
    /// 是否关联管理人
    /// </summary>
    public bool IsManagerLinked { get; set; }

    /// <summary>
    /// 是否关联基金
    /// </summary>
    public bool IsFundLinked { get; set; }

    /// <summary>
    /// 关联的基金 Id 列表
    /// </summary>
    public List<int> LinkedFundIds { get; set; } = [];

    /// <summary>
    /// 是否关联交易账户
    /// </summary>
    public bool IsAccountLinked { get; set; }

    /// <summary>
    /// 关联的交易账户 Id 列表
    /// </summary>
    public List<int> LinkedAccountIds { get; set; } = [];
}
