namespace FundOffice.Copilot.Models;

/// <summary>
/// 统一的聊天消息模型。
///
/// 一条消息由 Role（角色）和 Content（内容块列表）组成。
/// Content 使用 IReadOnlyList&lt;ContentPart&gt; 支持多模态内容，
/// 例如一条助手消息可能同时包含文字回复和工具调用请求。
///
/// 提供静态工厂方法简化常见场景的构造：
///   ChatMessage.System("...")     - 系统提示词
///   ChatMessage.User("...")       - 用户纯文本消息
///   ChatMessage.Assistant("...")  - 助手纯文本回复
///   ChatMessage.ToolResult(...)   - 工具执行结果
///
/// 较少见的场景（如用户发送多模态内容、助手含多个工具调用）直接用 record 初始化语法。
/// </summary>
public sealed record ChatMessage
{
    /// <summary>消息角色</summary>
    public required MessageRole Role { get; init; }

    /// <summary>消息内容块列表。一条消息可包含多个 ContentPart（如文字 + 工具调用）。</summary>
    public required IReadOnlyList<ContentPart> Content { get; init; }

    /// <summary>构造系统提示词消息</summary>
    public static ChatMessage System(string text) =>
        new() { Role = MessageRole.System, Content = [new TextContent(text)] };

    /// <summary>构造用户纯文本消息</summary>
    public static ChatMessage User(string text) =>
        new() { Role = MessageRole.User, Content = [new TextContent(text)] };

    /// <summary>构造用户多模态消息（可包含多个内容块）</summary>
    public static ChatMessage User(IReadOnlyList<ContentPart> parts) =>
        new() { Role = MessageRole.User, Content = parts };

    /// <summary>构造助手纯文本回复</summary>
    public static ChatMessage Assistant(string text) =>
        new() { Role = MessageRole.Assistant, Content = [new TextContent(text)] };

    /// <summary>构造助手复合回复（文字 + 工具调用）。通常不需要手动构造，由 Provider 解析响应自动生成。</summary>
    public static ChatMessage Assistant(IReadOnlyList<ContentPart> parts) =>
        new() { Role = MessageRole.Assistant, Content = parts };

    /// <summary>
    /// 构造工具执行结果消息。
    /// toolCallId 必须与对应的 ToolCallContent.Id 一致，模型据此关联调用与结果。
    /// isError=true 时 Anthropic 会标记为错误结果，OpenAI 会忽略此标志。
    /// </summary>
    public static ChatMessage ToolResult(string toolCallId, string result, bool isError = false) =>
        new()
        {
            Role = MessageRole.Tool,
            Content = [new ToolResultContent(toolCallId, result, isError)]
        };
}
