namespace FundOffice.Copilot.Models;

/// <summary>
/// 统一的消息角色枚举。
///
/// 映射关系：
///   OpenAI:     system / user / assistant / tool
///   Anthropic:  system 单独提取到顶层 system 字段；tool 角色映射为 user 消息中的 tool_result 内容块
///
/// 注意：Anthropic 没有独立的 "tool" 角色，Tool 角色的消息在发送时会被
/// AnthropicRequestBuilder.ProcessMessages() 合并到 user 消息中。
/// </summary>
public enum MessageRole
{
    /// <summary>系统提示词。OpenAI 作为普通消息发送；Anthropic 提取到顶层 system 字段。</summary>
    System,

    /// <summary>用户消息。</summary>
    User,

    /// <summary>助手回复，可能包含文本和/或工具调用请求。</summary>
    Assistant,

    /// <summary>工具执行结果。OpenAI 独立发送；Anthropic 合并到 user 消息中。</summary>
    Tool
}
