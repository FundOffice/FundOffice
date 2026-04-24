namespace FMO.Disclosure;

/// <summary>
/// 信批任务
/// NoticeId+ChannelCode唯一标识一个信批任务
/// </summary>
public class DisclosureInstance
{
    public string Id => $"{Channel}{NoticeId}";

    public required string WorkflowId { get; set; }

    public long NoticeId { get; set; }

    public int FundId { get; set; }

    public required string Channel { get; set; }

    public DisclosureType Type { get; set; }


}

