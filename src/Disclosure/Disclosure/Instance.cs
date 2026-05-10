[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Disclosure.UI")]

namespace FMO.Disclosure;

/// <summary>
/// 信批任务
/// NoticeId+ChannelCode唯一标识一个信批任务
/// </summary>
public class DisclosureInstance
{
    public string Id => $"{Channel}-{NoticeId}";

    public required string WorkflowId { get; init; }

    public long NoticeId { get; init; }

    public int FundId { get; init; }

    public required string Channel { get; init; }

    public DisclosureType Type { get; init; }

    /// <summary>
    /// 自动加入执行队列
    /// 错误5次后不再自动加入执行队列，需人工干预
    /// </summary>
    public bool AutoRun { get; set; }

    public DisclosureStatus Status { get; internal set; }

    public DateTime StartedTime { get; internal set; }

    public DateTime LastRunTime { get; internal set; }

    public DateTime CompletedTime { get; internal set; }

    public int FailedTimes { get; internal set; }

    public string? Error { get; internal set; }
}

