namespace FundOffice.Copilot.Configuration;

/// <summary>
/// Google Gemini Provider 的构造配置。仅在创建 Provider 时使用，之后不再持有引用。
///
/// 使用示例：
/// <code>
/// var provider = new GoogleTokenProvider(new GoogleOptions
/// {
///     Identifier = "My Gemini",
///     ApiKey = "xxx",
///     Model = "gemini-2.5-pro",
///     BaseUrl = "https://generativelanguage.googleapis.com"
/// });
/// </code>
///
/// Provider 构造时会校验所有必填字段并存储为私有字段，
/// 请求级选项（如切换模型）通过 ChatOptions 在每次调用时传入。
///
/// BaseUrl 指向 Google Generative Language API：
///   最终请求 URL = BaseUrl.TrimEnd('/') + "/v1beta/models/{model}:generateContent"
///   模型列表 URL = BaseUrl.TrimEnd('/') + "/v1beta/models?key={apiKey}"
/// </summary>
public sealed class GoogleOptions
{
    public required string Identifier { get; set; }

    /// <summary>API Key（必填）。从 Google Cloud Console 获取。</summary>
    public required string ApiKey { get; set; }

    /// <summary>默认模型名称（必填）。可在每次请求的 ChatOptions.Model 中覆盖。</summary>
    public string Model { get; set; } = "gemini-2.5-pro";

    /// <summary>API 基础地址（必填）。</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}