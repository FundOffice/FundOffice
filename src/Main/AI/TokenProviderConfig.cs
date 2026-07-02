using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Providers;

namespace FMO.AI;

/// <summary>
/// Provider 类型枚举 — 替代旧的 TokenProviderStyle
/// </summary>
public enum AIProviderType
{
    /// <summary>OpenAI Chat Completions API（兼容所有 OpenAI 格式的第三方代理）</summary>
    OpenAI,

    /// <summary>OpenAI Responses API</summary>
    OpenAIResponses,

    /// <summary>Anthropic Messages API</summary>
    Anthropic,

    /// <summary>Google Gemini API</summary>
    Google,

}

/// <summary>
/// Token Provider 配置
/// 用于 LiteDB 持久化 — 无继承、无多态的扁平 POCO
/// </summary>
public class TokenProviderConfig
{
    public int Id { get; set; }

    /// <summary>用户自定义名称（如 "My OpenAI"、"Production DeepSeek"）</summary>
    public string Name { get; set; } = "";

    /// <summary>API 基础地址（不含路径后缀，如 "https://api.openai.com"）</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>API 密钥</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>默认模型名称</summary>
    public string Model { get; set; } = "";

    /// <summary>Provider 类型</summary>
    public AIProviderType ProviderType { get; set; } = AIProviderType.OpenAI;

    /// <summary>
    /// 工厂方法：从配置创建 ITokenProvider
    /// </summary>
    public ITokenProvider CreateProvider()
    {
        switch (ProviderType)
        {
            case AIProviderType.Anthropic:
                return new AnthropicTokenProvider(
                    new AnthropicOptions
                    {
                        Identifier = Name,
                        ApiKey = ApiKey,
                        BaseUrl = BaseUrl,
                        Model = Model
                    },
                    null);

            case AIProviderType.Google:
                return new GoogleTokenProvider(
                    new GoogleOptions
                    {
                        Identifier = Name,
                        ApiKey = ApiKey,
                        BaseUrl = BaseUrl,
                        Model = Model
                    },
                    null);

            case AIProviderType.OpenAIResponses:
                return new OpenAIResponsesProvider(
                    new OpenAIOptions
                    {
                        Identifier = Name,
                        ApiKey = ApiKey,
                        BaseUrl = BaseUrl,
                        Model = Model,
                        ApiVersion = OpenAIApiVersion.Responses
                    },
                    null);

            default:
                return new OpenAITokenProvider(
                    new OpenAIOptions
                    {
                        Identifier = Name,
                        ApiKey = ApiKey,
                        BaseUrl = BaseUrl,
                        Model = Model
                    },
                    null);
        }
    }

    /// <summary>
    /// 工厂方法：创建 AIChatAdapter
    /// </summary>
    public AIChatAdapter CreateAdapter() => new AIChatAdapter(CreateProvider(), Model);

    /// <summary>
    /// URL 规范化：将旧的完整 endpoint URL 转换为 base URL
    /// 旧格式：https://api.openai.com/v1/chat/completions
    /// 新格式：https://api.openai.com
    /// </summary>
    public static string NormalizeBaseUrl(string fullUrl, AIProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(fullUrl))
            return fullUrl;

        // 去除尾部斜杠
        var url = fullUrl.TrimEnd('/');

        // 常见的路径后缀
        var suffixesToRemove = new[]
        {
            "/v1/chat/completions",
            "/v1/messages",
            "/chat/completions",
            "/anthropic/v1/messages",
            "/api/v3/chat/completions",
            "/api/paas/v4/chat/completions",
            "/compatible-mode/v1/chat/completions",
            "/v1beta/models/{model}:generateContent"
        };

        foreach (var suffix in suffixesToRemove)
        {
            if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - suffix.Length);
                break;
            }
        }

        return url;
    }

    public override string ToString() => Name ?? "未设置名称";
}