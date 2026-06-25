namespace FundOffice.Copilot.Models;

/// <summary>
/// 非流式聊天完成的返回结果。
///
/// Messages  - 模型的回复消息列表（通常只有一条 Assistant 消息）
///             消息的 Content 可能包含 TextContent（文字回复）
///             和/或 ToolCallContent（工具调用请求）
///
/// FinishReason 统一后的结束原因：
///   "stop"         - 模型正常结束
///   "tool_calls"   - 模型请求调用工具，需要执行工具后再次请求
///   "max_tokens"   - 达到最大 token 限制
///   "content_filter" - 内容被安全过滤（OpenAI）
///
/// Provider 原始值的映射：
///   OpenAI:    "stop" -> "stop", "tool_calls" -> "tool_calls", "length" -> "max_tokens"
///   Anthropic: "end_turn" -> "stop", "tool_use" -> "tool_calls", "max_tokens" -> "max_tokens"
/// </summary>
public sealed record ChatResult
{
    /// <summary>模型回复消息列表</summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>输入 token 数（提示词 + 历史消息）</summary>
    public int? PromptTokens { get; init; }

    /// <summary>输出 token 数（模型回复）</summary>
    public int? CompletionTokens { get; init; }

    /// <summary>归一化后的结束原因</summary>
    public string? FinishReason { get; init; }
}
