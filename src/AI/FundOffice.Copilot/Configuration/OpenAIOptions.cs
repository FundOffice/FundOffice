namespace FundOffice.Copilot.Configuration;

/// <summary>
/// OpenAI Provider 的构造配置。仅在创建 Provider 时使用，之后不再持有引用。
///
/// 使用示例：
/// <code>
/// var provider = new OpenAiTokenProvider(new OpenAIOptions
/// {
///     ApiKey = "sk-xxx",
///     Model = "gpt-4o",
///     BaseUrl = "https://api.openai.com"  // 可改为兼容的第三方代理地址
/// });
/// </code>
///
/// Provider 构造时会校验所有必填字段并存储为私有字段，
/// 请求级选项（如切换模型）通过 ChatOptions 在每次调用时传入。
///
/// BaseUrl 兼容所有 OpenAI 格式的第三方代理：
///   ChatCompletions: 最终请求 URL = BaseUrl.TrimEnd('/') + "/v1/chat/completions"
///   Responses:       最终请求 URL = BaseUrl.TrimEnd('/') + "/v1/responses"
/// </summary>
public sealed class OpenAIOptions
{
    public required string Identifier { get; set; }

    /// <summary>API Key（必填）。OpenAI 格式通常以 "sk-" 开头。</summary>
    public required string ApiKey { get; set; }

    /// <summary>默认模型名称（必填）。可在每次请求的 ChatOptions.Model 中覆盖。</summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>API 基础地址（必填）。兼容所有 OpenAI 格式的第三方代理。</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>
    /// API 版本。决定使用哪个端点和请求/响应格式。
    ///   ChatCompletions — POST /v1/chat/completions（默认，兼容所有第三方代理）
    ///   Responses       — POST /v1/responses（OpenAI 新一代 API，支持服务端对话状态等新特性）
    /// </summary>
    public OpenAIApiVersion ApiVersion { get; set; } = OpenAIApiVersion.ChatCompletions;
}

/// <summary>
/// OpenAI API 版本枚举。
/// </summary>
public enum OpenAIApiVersion
{
    /// <summary>Chat Completions API：POST /v1/chat/completions（默认，兼容第三方代理）</summary>
    ChatCompletions,

    /// <summary>Responses API：POST /v1/responses（OpenAI 新一代 API）</summary>
    Responses
}
