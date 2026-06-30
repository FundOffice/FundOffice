using System.Text;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// TokenProvider 抽象基类。
///
/// 子类只需实现 ChatCompletionStreamAsync（流式），
/// ChatCompletionAsync（非流式）有默认实现：调用流式方法并聚合结果。
///
/// 聚合逻辑：
///   - TextDelta -> StringBuilder 拼接 -> 最终一个 TextContent
///   - ToolCallDelta -> 按 Id 分组用 StringBuilder 累积参数 -> 多个 ToolCallContent
///   - UsageUpdate / StreamComplete -> 提取最终值
///
/// 子类如果 SDK 有原生的非流式 API，可以覆盖 ChatCompletionAsync 以获得更好的性能。
/// </summary>
public abstract class TokenProviderBase : ITokenProvider
{
    public abstract string Identifier { get; }



    /// <summary>子类必须实现的流式方法</summary>
    public abstract IAsyncEnumerable<StreamingToken> ChatCompletionStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 默认的非流式实现：消费流式结果并聚合为完整的 ChatResult。
    /// 子类可覆盖此方法以使用 SDK 原生的非流式 API。
    /// </summary>
    public virtual async Task<ChatResult> ChatCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 累积文本回复
        var textBuilder = new StringBuilder();

        // 按工具调用 ID 累积参数。Key = ToolCallContent.Id（如 "call_abc123"）
        var toolCalls = new Dictionary<string, ToolCallAccumulator>();

        int? promptTokens = null;
        int? completionTokens = null;
        string? finishReason = null;

        // 消费所有流式事件
        await foreach (var token in ChatCompletionStreamAsync(messages, tools, options, cancellationToken))
        {
            switch (token)
            {
                case TextDelta td:
                    textBuilder.Append(td.Text);
                    break;

                case ReasoningDelta:
                    // 推理内容不进入结果（ChatResult 无此字段），忽略
                    break;

                case ToolCallDelta tcd:
                    // 首次见到此 Id 时创建累积器
                    if (!toolCalls.TryGetValue(tcd.Id, out var acc))
                    {
                        acc = new ToolCallAccumulator
                        {
                            Id = tcd.Id,
                            FunctionName = tcd.FunctionName ?? ""
                        };
                        toolCalls[tcd.Id] = acc;
                    }
                    // FunctionName 仅在第一个 delta 中非 null
                    if (tcd.FunctionName is not null)
                        acc.FunctionName = tcd.FunctionName;
                    // 参数片段逐步拼接
                    acc.ArgumentsBuilder.Append(tcd.ArgumentsDelta);
                    break;

                case UsageUpdate u:
                    // 可能多次更新，取最后一次
                    promptTokens = u.PromptTokens;
                    completionTokens = u.CompletionTokens;
                    break;

                case StreamComplete sc:
                    finishReason = sc.FinishReason;
                    break;
            }
        }

        // 组装最终的 ContentPart 列表
        var contentParts = new List<ContentPart>();

        if (textBuilder.Length > 0)
            contentParts.Add(new TextContent(textBuilder.ToString()));

        foreach (var tc in toolCalls.Values)
        {
            contentParts.Add(new ToolCallContent(
                tc.Id,
                tc.FunctionName,
                tc.ArgumentsBuilder.ToString()));
        }

        return new ChatResult
        {
            Messages = [new ChatMessage { Role = MessageRole.Assistant, Content = contentParts }],
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = finishReason
        };
    }

    /// <summary>
    /// 测试连通性：发送一次最小请求（"hi", max_tokens=1），不重试。
    /// 子类可覆盖以使用更轻量的方式（如 OpenAI 的 GET /v1/models）。
    /// </summary>
    public virtual async Task TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { ChatMessage.User("hi") };
        var options = new ChatOptions { MaxTokens = 1 };
        await ChatCompletionAsync(messages, options: options, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取模型列表。默认返回空列表，子类覆盖以调用各自的 /v1/models 端点。
    /// </summary>
    public virtual Task<IReadOnlyList<ModelInfo>> GetModelsAsync(
        string? modelUrl = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>([]);
    }

    /// <summary>单个工具调用的参数累积器</summary>
    private sealed class ToolCallAccumulator
    {
        /// <summary>工具调用 ID（如 "call_abc123"）</summary>
        public required string Id { get; init; }

        /// <summary>函数名（如 "get_weather"），可能在后续 delta 中更新</summary>
        public required string FunctionName { get; set; }

        /// <summary>参数 JSON 片段的累积器</summary>
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}
