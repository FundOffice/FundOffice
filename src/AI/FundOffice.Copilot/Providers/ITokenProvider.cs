using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// AI 调用的核心抽象接口。
///
/// 设计原则：
///   - 只负责单次请求/响应，不管理对话历史
///   - 不执行工具调用循环（由调用方管理，见 README 中的工具调用流程）
///   - 不含重试逻辑（由调用方根据 TokenProviderException.IsRetryable 判断）
///   - 不含对话上下文管理
///
/// 两个方法的区别：
///   ChatCompletionStreamAsync - 流式，返回 IAsyncEnumerable，适合实时输出
///   ChatCompletionAsync       - 非流式，返回完整结果，默认实现基于流式聚合
///
/// 工具调用的典型流程（调用方负责）：
///   1. 调用 ChatCompletionAsync，传入 messages + tools
///   2. 检查 FinishReason == "tool_calls"
///   3. 从返回的 Assistant 消息中提取 ToolCallContent
///   4. 执行工具，构造 ToolResult 消息
///   5. 把 Assistant 消息 + ToolResult 消息追加到 messages
///   6. 再次调用 ChatCompletionAsync
///   7. 重复直到 FinishReason == "stop"
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// 流式聊天完成。逐块返回文本、工具调用、用量和结束信号。
    /// </summary>
    IAsyncEnumerable<StreamingToken> ChatCompletionStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 非流式聊天完成。返回完整的 ChatResult。
    /// 默认实现聚合流式结果，子类可覆盖以使用更高效的非流式 API。
    /// </summary>
    Task<ChatResult> ChatCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试连通性。发送一次最小请求验证 API Key 和模型是否可用。
    /// 不重试，失败直接抛出 TokenProviderException。
    /// </summary>
    Task TestConnectivityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用模型列表。
    ///
    /// OpenAI: 默认调用 GET {baseUrl}/v1/models
    /// Anthropic: 默认调用 GET {baseUrl}/v1/models（非标准，部分代理支持）
    ///
    /// modelUrl 不为空时直接用作完整 URL，为空时使用 Provider 的 BaseUrl 拼接。
    /// 如果 API 不支持模型列表端点，返回空列表（不抛异常）。
    /// </summary>
    /// <param name="modelUrl">完整 URL，不为空时直接使用；为空时用 Provider 默认地址拼接 /v1/models</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string? modelUrl = null, CancellationToken cancellationToken = default);
}
