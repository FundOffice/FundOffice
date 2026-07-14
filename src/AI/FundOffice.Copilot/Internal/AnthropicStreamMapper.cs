using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// Anthropic 流式事件 -> StreamingToken 映射器。
///
/// 重要：这是实例类（非静态），因为需要跟踪当前 content block 的状态。
/// 每个请求必须创建独立实例，并发请求不能共享同一个 mapper。
///
/// Anthropic 流式事件时序（完整的一次请求）：
///
///   event: message_start
///   data: {"type":"message_start","message":{"usage":{"input_tokens":10}}}
///     -> 发出 UsageUpdate(promptTokens=10)
///
///   event: content_block_start
///   data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}
///     -> 记录当前块类型为 "text"
///
///   event: content_block_delta
///   data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"你"}}
///     -> 发出 TextDelta("你")
///
///   event: content_block_stop
///   data: {"type":"content_block_stop","index":0}
///     -> 清除当前块状态
///
///   --- 如果模型请求工具调用 ---
///   event: content_block_start
///   data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_123","name":"get_weather"}}
///     -> 记录当前块类型为 "tool_use"，发出 ToolCallDelta(id, name, "")
///
///   event: content_block_delta
///   data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"lo"}}
///     -> 发出 ToolCallDelta(id, null, "{\"lo")  // 参数片段
///
///   event: content_block_stop
///   data: {"type":"content_block_stop","index":1}
///     -> 清除当前块状态
///
///   event: message_delta
///   data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":20}}
///     -> 发出 StreamComplete("tool_calls") + UsageUpdate(completionTokens=20)
///
///   event: message_stop
///   data: {"type":"message_stop"}
///     -> 发出 StreamComplete("stop")
///
/// 状态跟踪说明：
///   _currentBlockType  - 当前正在接收的 content block 类型（"text" 或 "tool_use"）
///   _currentBlockId    - 当前工具调用的 ID（仅 tool_use 块有效）
///   _currentBlockName  - 当前工具调用的函数名（仅 tool_use 块有效）
///
///   content_block_start 时设置状态，content_block_stop 时清除。
///   content_block_delta 中 input_json_delta 需要 _currentBlockId 来关联正确的工具调用。
/// </summary>
internal sealed class AnthropicStreamMapper
{
    private string? _currentBlockType;
    private string? _currentBlockId;
    private string? _currentBlockName;
    private bool _hasFinishReason;

    /// <summary>
    /// 将一个 Anthropic SSE 事件映射为零个或多个 StreamingToken。
    /// eventType 来自 SSE 的 event: 行，data 是 JSON 字符串。
    /// </summary>
    public IEnumerable<StreamingToken> MapEvent(string? eventType, string data)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            var root = doc.RootElement;
            // 优先使用 JSON body 中的 type 字段，其次用 SSE event type
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : eventType;

            switch (type)
            {
                case "message_start":
                    // message_start 包含初始 usage（input_tokens）
                    if (root.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("usage", out var usage))
                    {
                        int? promptTokens = null;
                        if (usage.TryGetProperty("input_tokens", out var pt))
                            promptTokens = pt.GetInt32();
                        if (promptTokens.HasValue)
                            yield return new UsageUpdate(promptTokens, null);
                    }
                    break;

                case "content_block_start":
                    // 一个新的内容块开始，记录类型和 ID
                    if (root.TryGetProperty("content_block", out var block))
                    {
                        _currentBlockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : null;
                        if (_currentBlockType == "tool_use")
                        {
                            // 工具调用块：记录 ID 和函数名，发出第一个 delta（空参数）
                            _currentBlockId = block.TryGetProperty("id", out var bid) ? bid.GetString() : null;
                            _currentBlockName = block.TryGetProperty("name", out var bname) ? bname.GetString() : null;
                            if (_currentBlockId is not null)
                            {
                                yield return new ToolCallDelta(
                                    _currentBlockId,
                                    _currentBlockName,
                                    "");
                            }
                        }
                    }
                    break;

                case "content_block_delta":
                    // 内容增量
                    if (root.TryGetProperty("delta", out var delta))
                    {
                        var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;
                        switch (deltaType)
                        {
                            case "text_delta":
                                // 文本增量
                                var text = delta.TryGetProperty("text", out var dtxt) ? dtxt.GetString() ?? "" : "";
                                if (text.Length > 0)
                                    yield return new TextDelta(text);
                                break;

                            case "input_json_delta":
                                // 工具参数 JSON 境量，需要关联到当前工具调用
                                var partialJson = delta.TryGetProperty("partial_json", out var pj) ? pj.GetString() ?? "" : "";
                                if (_currentBlockId is not null)
                                {
                                    yield return new ToolCallDelta(
                                        _currentBlockId,
                                        null,   // FunctionName 仅在第一个 delta 中非 null
                                        partialJson);
                                }
                                break;
                        }
                    }
                    break;

                case "content_block_stop":
                    // 内容块结束，清除状态
                    _currentBlockType = null;
                    _currentBlockId = null;
                    _currentBlockName = null;
                    break;

                case "message_delta":
                    // 消息级别增量：stop_reason 和最终 usage
                    if (root.TryGetProperty("delta", out var msgDelta))
                    {
                        var stopReason = msgDelta.TryGetProperty("stop_reason", out var sr)
                            ? sr.GetString()
                            : null;
                        if (stopReason is not null)
                        {
                            _hasFinishReason = true;
                            yield return new StreamComplete(
                                AnthropicRequestBuilder.NormalizeFinishReason(stopReason));
                        }
                    }
                    if (root.TryGetProperty("usage", out var msgUsage))
                    {
                        int? completionTokens = null;
                        if (msgUsage.TryGetProperty("output_tokens", out var ct))
                            completionTokens = ct.GetInt32();
                        if (completionTokens.HasValue)
                            yield return new UsageUpdate(null, completionTokens);
                    }
                    break;

                case "message_stop":
                    // 消息完成信号。不重复发送 StreamComplete，
                    // 因为 message_delta 已经发送了含真实 stop_reason 的 StreamComplete。
                    // 如果 message_delta 没有 stop_reason（异常情况），此处兜底。
                    if (!_hasFinishReason)
                        yield return new StreamComplete("stop");
                    break;

                case "ping":
                    // 心跳事件，忽略
                    break;
            }
        }
    }
}
