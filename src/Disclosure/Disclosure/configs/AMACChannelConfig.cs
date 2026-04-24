namespace FMO.Disclosure;

/// <summary>
/// 基金业协会通道配置
/// </summary>
public class AMACChannelConfig : DisclosureChannelConfig
{
    public override string ChannelCode => DisclosureChannelCode.AMAC;

     

    /// <summary>
    /// 系统编号
    /// </summary>
    public required string SystemCode { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// 报备类型：月度、季度、半年报、年度
    /// </summary>
    public string? ReportType { get; set; }
}
