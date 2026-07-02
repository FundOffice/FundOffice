namespace FundOffice.Copilot.Models;

/// <summary>
/// 消息内容的基类，使用 C# record 实现的可辨识联合（Discriminated Union）。
///
/// 一条消息的 Content 可以包含多个 ContentPart，例如：
/// - 一条助手消息可能同时包含 TextContent（文字回复）和 ToolCallContent（工具调用请求）
/// - 一条用户消息（Anthropic 场景）可能包含多个 ToolResultContent
///
/// 设计决策：ArgumentsJson 和 Result 都用原始 JSON 字符串，
/// 避免耦合具体的序列化方案，同时与两个 API 的原始格式直接对应。
/// </summary>
public abstract record ContentPart;

/// <summary>
/// 纯文本内容。
/// 对应 OpenAI 的 text content part 和 Anthropic 的 text content block。
/// </summary>
public sealed record TextContent(string Text) : ContentPart;

/// <summary>
/// 工具调用请求，由模型在 Assistant 消息中返回。
///
/// 对应 OpenAI 的 tool_calls[].function（id + function.name + function.arguments）
/// 对应 Anthropic 的 tool_use content block（id + name + input）
///
/// 参数说明：
///   Id            - 工具调用的唯一标识，用于关联 ToolResultContent.ToolCallId
///   FunctionName  - 要调用的函数名，对应 ToolDefinition.Name
///   ArgumentsJson - 函数参数的原始 JSON 字符串，由流式传输中增量累积而成
/// </summary>
public sealed record ToolCallContent(
    string Id,
    string FunctionName,
    string ArgumentsJson
) : ContentPart;

/// <summary>
/// 工具执行结果，由调用方构造后发回给模型。
///
/// 对应 OpenAI 的 role:"tool" 消息（tool_call_id + content）
/// 对应 Anthropic 的 user 消息中的 tool_result content block（tool_use_id + content）
///
/// 参数说明：
///   ToolCallId - 关联的 ToolCallContent.Id
///   Result     - 工具执行结果的字符串（通常是 JSON）
///   IsError    - 标记工具执行是否出错（Anthropic 原生支持，OpenAI 忽略此字段）
/// </summary>
public sealed record ToolResultContent(
    string ToolCallId,
    string Result,
    bool IsError = false
) : ContentPart;
