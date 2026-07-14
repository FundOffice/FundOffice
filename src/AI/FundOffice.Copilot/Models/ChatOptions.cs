namespace FundOffice.Copilot.Models;

/// <summary>
/// 聊天请求参数接口。
///
/// 设计为接口而非具体类，方便调用方用不同方式实现（record、class、匿名对象等）。
/// ChatOptions 提供了默认的 record 实现。
///
/// AdditionalProperties 用于传递 provider 特有的参数：
///   OpenAI:    frequency_penalty, presence_penalty, parallel_tool_calls, response_format 等
///   Anthropic: top_k 等
/// </summary>
public interface IChatOptions
{
    /// <summary>模型名称，覆盖 Provider 构造时的默认值</summary>
    string? Model { get; }

    /// <summary>采样温度 (0.0~2.0)，越高输出越随机。null 使用模型默认值。</summary>
    float? Temperature { get; }

    /// <summary>最大输出 token 数。OpenAI 映射到 max_tokens（不传则不限制）；Anthropic 为必填字段，默认 16384。</summary>
    int? MaxTokens { get; }

    /// <summary>核采样参数 (0.0~1.0)，与 Temperature 配合使用。</summary>
    float? TopP { get; }

    /// <summary>停止序列列表，模型生成这些字符串时停止输出。</summary>
    IReadOnlyList<string>? StopSequences { get; }

    /// <summary>
    /// Provider 特有参数的键值对。
    /// 各 Provider 的 Builder 会识别已知 key 并映射到对应的 SDK 参数。
    /// </summary>
    IDictionary<string, object>? AdditionalProperties { get; }
}

/// <summary>
/// IChatOptions 的默认 record 实现，支持 init 语法。
///
/// 使用示例：
/// <code>
/// var options = new ChatOptions
/// {
///     Model = "gpt-4o",
///     Temperature = 0.7f,
///     MaxTokens = 2048,
///     AdditionalProperties = new Dictionary&lt;string, object&gt;
///     {
///         ["frequency_penalty"] = 0.5f
///     }
/// };
/// </code>
/// </summary>
public sealed record ChatOptions : IChatOptions
{
    public string? Model { get; init; }
    public float? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public float? TopP { get; init; }
    public IReadOnlyList<string>? StopSequences { get; init; }
    public IDictionary<string, object>? AdditionalProperties { get; init; }
}
