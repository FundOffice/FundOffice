using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// OpenAI Responses API 流式响应 SSE 事件 → StreamingToken 映射器。
///
/// 与 <see cref="OpenAiStreamMapper"/> 对称，但处理 Responses API 的 SSE 事件格式。
///
/// Responses API 的 SSE 事件通过 event: 行指定事件类型，data: 行包含 JSON 载荷。
/// 事件类型（type 字段）决定如何解析 data JSON：
///
/// 文本输出增量：
///   event: response.output_text.delta
///   data: {"type":"response.output_text.delta","output_index":0,"content_index":0,"delta":"你"}
///
/// 工具调用增量（参数分片到达）：
///   event: response.function_call_arguments.delta
///   data: {"type":"response.function_call_arguments.delta","output_index":0,"call_id":"call_123","delta":"{\"lo"}
///
/// 工具调用参数完成：
///   event: response.function_call_arguments.done
///   data: {"type":"response.function_call_arguments.done","output_index":0,"call_id":"call_123","arguments":"{\"location\":\"BJ\"}"}
///
/// 推理增量（o 系列模型思维链）：
///   event: response.reasoning_text.delta
///   data: {"type":"response.reasoning_text.delta","output_index":0,"content_index":0,"delta":"让我想想..."}
///
/// 输出项添加（function_call 开始时获取 id 和 name）：
///   event: response.output_item.added
///   data: {"type":"response.output_item.added","output_index":0,"item":{"type":"function_call","id":"fc_...","call_id":"call_...","name":"fn","arguments":""}}
///
/// 响应完成（含 usage 和 status）：
///   event: response.completed
///   data: {"type":"response.completed","response":{"id":"resp_...","status":"completed","usage":{"input_tokens":10,"output_tokens":20}}}
///
/// 错误：
///   event: error
///   data: {"type":"error","message":"...","code":"..."}
///
/// 注意：SseParser 已经解析了 event: 行，通过 eventType 参数传入。
/// </summary>
internal static class OpenAiResponsesStreamMapper
{
    /// <summary>
    /// 将一条 SSE 事件解析为零个或多个 StreamingToken。
    ///
    /// eventType 来自 SSE 的 event: 行，data 来自 data: 行。
    /// JSON 解析失败时静默跳过（返回空序列）。
    /// </summary>
    public static IEnumerable<StreamingToken> MapEvent(string? eventType, string data)
    {
        // 尝试解析 JSON（大部分事件类型需要）
        JsonDocument? doc = null;
        if (!string.IsNullOrEmpty(data) && data != "[DONE]")
        {
            try
            {
                doc = JsonDocument.Parse(data);
            }
            catch (JsonException)
            {
                yield break;
            }
        }

        // [DONE] 标记 — Responses API 通常不用此标记，但做防御性处理
        if (data == "[DONE]")
        {
            yield return new StreamComplete(null);
            yield break;
        }

        if (doc is null)
            yield break;

        using (doc)
        {
            var root = doc.RootElement;

            switch (eventType)
            {
                // ── 文本输出增量 ──
                case "response.output_text.delta":
                {
                    var delta = ExtractString(root, "delta");
                    if (delta is not null)
                        yield return new TextDelta(delta);
                    break;
                }

                // ── 推理增量（o 系列模型思维链） ──
                case "response.reasoning_text.delta":
                {
                    var delta = ExtractString(root, "delta");
                    if (delta is not null)
                        yield return new ReasoningDelta(delta);
                    break;
                }

                // ── 工具调用增量（参数分片） ──
                case "response.function_call_arguments.delta":
                {
                    var callId = ExtractString(root, "call_id") ?? "";
                    var delta = ExtractString(root, "delta") ?? "";
                    yield return new ToolCallDelta(callId, null, delta);
                    break;
                }

                // ── 工具调用参数完成 ──
                // 参数已完整到达，不需要额外处理（调用方已通过 ToolCallDelta 累积）
                // 但可用于校验或触发回调，此处不产生 token
                case "response.function_call_arguments.done":
                    break;

                // ── 输出项添加 ──
                // function_call 项开始时，携带 id/call_id/name
                // 产生一个 ToolCallDelta 让调用方知道有新工具调用开始
                case "response.output_item.added":
                {
                    if (root.TryGetProperty("item", out var item))
                    {
                        var itemType = ExtractString(item, "type");
                        if (itemType == "function_call")
                        {
                            var callId = ExtractString(item, "call_id")
                                         ?? ExtractString(item, "id")
                                         ?? "";
                            var funcName = ExtractString(item, "name") ?? "";
                            // 发送初始 ToolCallDelta，携带 FunctionName
                            yield return new ToolCallDelta(callId, funcName, "");
                        }
                    }
                    break;
                }

                // ── 输出项完成 ──
                case "response.output_item.done":
                    break;

                // ── 响应完成 ──
                case "response.completed":
                {
                    // 从 response 对象中提取 usage 和 status
                    if (root.TryGetProperty("response", out var response))
                    {
                        // status → FinishReason
                        var status = ExtractString(response, "status");
                        var finishReason = OpenAiResponsesRequestBuilder.NormalizeStatus(status);

                        // 如果有 function_call 输出项，finishReason 应为 "tool_calls"
                        if (response.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in output.EnumerateArray())
                            {
                                var t = ExtractString(item, "type");
                                if (t == "function_call")
                                {
                                    finishReason = "tool_calls";
                                    break;
                                }
                            }
                        }

                        yield return new StreamComplete(finishReason);

                        // usage
                        if (response.TryGetProperty("usage", out var usage))
                        {
                            int? promptTokens = null;
                            int? completionTokens = null;
                            if (usage.TryGetProperty("input_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                                promptTokens = pt.GetInt32();
                            if (usage.TryGetProperty("output_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number)
                                completionTokens = ct.GetInt32();
                            if (promptTokens.HasValue || completionTokens.HasValue)
                                yield return new UsageUpdate(promptTokens, completionTokens);
                        }
                    }
                    else
                    {
                        yield return new StreamComplete("stop");
                    }
                    break;
                }

                // ── 响应失败 ──
                case "response.failed":
                    yield return new StreamComplete("content_filter");
                    break;

                // ── 响应不完整 ──
                case "response.incomplete":
                    yield return new StreamComplete("max_tokens");
                    break;

                // ── 错误事件 ──
                case "error":
                    // 错误事件不产生 token，由 ErrorMapper 处理
                    // 此处静默跳过，让调用方通过 HTTP 状态码或超时检测错误
                    break;

                // ── 其他事件（忽略） ──
                // 包括：
                //   response.created, response.in_progress
                //   response.output_item.done, response.content_part.added, response.content_part.done
                //   response.output_text.done, response.refusal.delta, response.refusal.done
                //   response.reasoning_text.done, response.reasoning_summary_part.added/done
                //   response.reasoning_summary_text.delta/done
                //   response.file_search_call.*, response.code_interpreter_call.*
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// 从 JsonElement 中提取字符串值。字段不存在或非字符串时返回 null。
    /// </summary>
    private static string? ExtractString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
