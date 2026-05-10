[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Disclosure.UI, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a16e8900f3f992a30fe51139f12a0ad0249c42c842f70f7fd1bc900abe8f4f3899b636767156391116cad31f07bd005e6cc115f78f706b8b96df6e2444d42c9fb89aa60b86437096786b57ec64cedecbb65cfd16315d1e1a7533210bbf12f9ea77522a2456b06422d8066b00387723681493efd8ddafbe9246efb03f88c9bebc")]

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

