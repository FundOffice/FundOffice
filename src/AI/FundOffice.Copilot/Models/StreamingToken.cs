namespace FundOffice.Copilot.Models;

/// <summary>
/// 流式输出的事件基类，使用 record 实现的可辨识联合。
///
/// 调用方通过 ChatCompletionStreamAsync() 获取 IAsyncEnumerable&lt;StreamingToken&gt;，
/// 然后用 pattern matching 逐个处理事件：
///
/// <code>
/// await foreach (var token in provider.ChatCompletionStreamAsync(messages))
/// {
///     switch (token)
///     {
///         case TextDelta td:       // 文本片段
///         case ToolCallDelta tcd:  // 工具调用增量
///         case UsageUpdate u:      // token 用量
///         case StreamComplete sc:  // 流结束
///     }
/// }
/// </code>
/// </summary>
public abstract record StreamingToken;

/// <summary>
/// 文本增量。将所有 TextDelta.Text 拼接即得到完整的文本回复。
/// </summary>
public sealed record TextDelta(string Text) : StreamingToken;

/// <summary>
/// 推理增量（o 系列模型的思维链）。Responses API 特有。
/// 将所有 ReasoningDelta.Text 拼接即得到完整的推理过程。
/// 非推理模型不会产生此事件，调用方可按需处理或忽略。
/// </summary>
public sealed record ReasoningDelta(string Text) : StreamingToken;

/// <summary>
/// 工具调用增量。
///
/// 流式传输中，一个工具调用的参数会分多个 delta 片段到达。
/// 调用方需要按 Id 分组，将 ArgumentsDelta 逐步拼接。
///
/// 参数说明：
///   Id              - 同一个工具调用的所有 delta 共享同一个 Id（稳定标识符）
///   FunctionName    - 仅在该工具调用的第一个 delta 中非 null，后续 delta 为 null
///   ArgumentsDelta  - 参数 JSON 的一个片段，需要拼接
///
/// 累积示例：
///   delta 1: Id="call_123", FunctionName="get_weather", ArgumentsDelta=""
///   delta 2: Id="call_123", FunctionName=null,          ArgumentsDelta="{\"lo"
///   delta 3: Id="call_123", FunctionName=null,          ArgumentsDelta="cation\":"
///   delta 4: Id="call_123", FunctionName=null,          ArgumentsDelta="\"Beijing\"}"
///   拼接后完整参数: {"location":"Beijing"}
/// </summary>
public sealed record ToolCallDelta(
    string Id,
    string? FunctionName,
    string ArgumentsDelta
) : StreamingToken;

/// <summary>
/// Token 用量更新。可能在流式中途或结束时到达。
/// 两个字段都是 null 时无意义（某些 API 不在流中报告用量）。
/// </summary>
public sealed record UsageUpdate(
    int? PromptTokens,
    int? CompletionTokens
) : StreamingToken;

/// <summary>
/// 流结束信号。FinishReason 已归一化（见 ChatResult.FinishReason 说明）。
/// 流中可能出现多次 StreamComplete（如 Anthropic 的 message_delta + message_stop），
/// 调用方应只处理第一个。
/// </summary>
public sealed record StreamComplete(
    string? FinishReason
) : StreamingToken;
