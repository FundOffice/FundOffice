namespace FMO.Disclosure;

/// <summary>
/// 自定义/其他平台通道配置
/// </summary>
public class CustomChannelConfig : IDisclosureChannelConfig
{
    public string ChannelCode { get; set; } = DisclosureChannelCode.Custom;

    public bool IsAvailable { get; set; }

    /// <summary>
    /// 平台名称
    /// </summary>
    public required string PlatformName { get; set; }

    /// <summary>
    /// API地址
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// API密钥
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 其他配置（JSON格式）
    /// </summary>
    public string? ExtraSettings { get; set; }
}
