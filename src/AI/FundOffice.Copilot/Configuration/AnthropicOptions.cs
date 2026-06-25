namespace FundOffice.Copilot.Configuration;

/// <summary>
/// Anthropic Provider 的构造配置。仅在创建 Provider 时使用，之后不再持有引用。
///
/// 使用示例：
/// <code>
/// var provider = new AnthropicTokenProvider(new AnthropicOptions
/// {
///     ApiKey = "sk-ant-xxx",
///     Model = "claude-sonnet-4-20250514",
///     BaseUrl = "https://api.anthropic.com"  // 可改为兼容的第三方代理地址
/// });
/// </code>
///
/// Provider 构造时会校验所有必填字段并存储为私有字段，
/// 请求级选项（如切换模型）通过 ChatOptions 在每次调用时传入。
///
/// BaseUrl 兼容所有 Anthropic 格式的第三方代理：
///   最终请求 URL = BaseUrl.TrimEnd('/') + "/v1/messages"
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>API Key（必填）。Anthropic 格式通常以 "sk-ant-" 开头。</summary>
    public required string ApiKey { get; set; }

    /// <summary>默认模型名称（必填）。可在每次请求的 ChatOptions.Model 中覆盖。</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>API 基础地址（必填）。兼容所有 Anthropic 格式的第三方代理。</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>Anthropic API 版本号，通过 anthropic-version 请求头传递。</summary>
    public string ApiVersion { get; set; } = "2023-06-01";
}
