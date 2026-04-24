namespace FMO.Disclosure;

/// <summary>
/// 信批配置
/// 有两种模型
/// a 是以基金为中心的模型，配置项里包含基金ID列表，适用于大多数情况
/// b 以管理人中心，与基金无关
/// </summary>
public class DisclosureWorkflow
{
    public string Id => Channel + Type;

    public bool IsEnabled { get; set; }

    // 信批类型
    public DisclosureType Type { get; init; }

    /// <summary>
    /// 管理人维度，如果为true，则适用于管理人层面；
    /// 如果为false，则适用于基金层面，需要指定TargetFunds
    /// </summary>
    public bool IsManagerLevel => Type > DisclosureType.ManagerLevel;

    /// <summary>
    /// 适用全部产品
    /// IsManagerLevel为true时，无效
    /// </summary>
    public bool ForAllFunds { get; set; }

    /// <summary>
    /// 适用的基金ID列表，仅当ForAllFunds为false时有效
    /// IsManagerLevel为true时，无效
    /// </summary>
    public int[] TargetFunds { get; set; } = [];


    public required string Channel { get; set; }

    //public Dictionary<string, object> Params { get; set; } = [];
    public IWorkConfig? Config { get; set; }
}

