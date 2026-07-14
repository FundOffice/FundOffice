using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// OpenAI 流式响应 SSE data 行 -> StreamingToken 映射器。
///
/// OpenAI 流式 JSON 结构（每个 SSE data 行一个 JSON 对象）：
///
/// 文本增量：
///   {"choices":[{"delta":{"content":"你"},"index":0}]}
///
/// 工具调用增量（参数分片到达）：
///   {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_123","function":{"name":"fn","arguments":""}}]},"index":0}]}
///   {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"lo"}}]},"index":0}]}
///   {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"cation\":\"BJ\"}"}}]},"index":0}]}
///
/// token 用量（需要 stream_options.include_usage=true 才会出现）：
///   {"usage":{"prompt_tokens":10,"completion_tokens":20}}
///
/// 结束：
///   data: [DONE]
///
/// 注意：一个 SSE data 行可能同时包含文本增量和工具调用增量，
/// 所以 MapLine 返回 IEnumerable 而非单个 token。
/// </summary>
internal static class OpenAIStreamMapper
{
    /// <summary>
    /// 将一条 SSE data 行解析为零个或多个 StreamingToken。
    /// JSON 解析失败时静默跳过（返回空序列）。
    /// </summary>
    public static IEnumerable<StreamingToken> MapLine(string data)
    {
        // OpenAI 特有的流结束标记
        if (data == "[DONE]")
        {
            // FinishReason 为 null 表示这是 [DONE] 信号而非正常的 finish_reason
            // 调用方应以 StreamComplete 作为流结束的标志
            yield return new StreamComplete(null);
            yield break;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            // 无法解析的行静默跳过
            yield break;
        }

        using (doc)
        {
            var root = doc.RootElement;

            // token 用量信息（通常在流的最后一个非 [DONE] 消息中）
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                int? promptTokens = null;
                int? completionTokens = null;
                if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                    promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number)
                    completionTokens = ct.GetInt32();
                if (promptTokens.HasValue || completionTokens.HasValue)
                    yield return new UsageUpdate(promptTokens, completionTokens);
            }

            // choices 数组（可能为空，如 usage-only 消息）
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                yield break;

            var choice = choices[0];

            // 结束原因（最后一个有 choices 的消息中可能出现）
            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
            {
                var finishReason = OpenAIRequestBuilder.NormalizeFinishReason(fr.GetString());
                if (finishReason is not null)
                    yield return new StreamComplete(finishReason);
            }

            // delta 对象包含增量内容
            if (!choice.TryGetProperty("delta", out var delta))
                yield break;

            // 文本内容增量
            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new TextDelta(text);
            }

            // 推理内容增量（DeepSeek 等推理模型的思维链）
            if (delta.TryGetProperty("reasoning_content", out var reasoning) && reasoning.ValueKind == JsonValueKind.String)
            {
                var text = reasoning.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new ReasoningDelta(text);
            }

            // 工具调用增量
            if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    // index: 工具调用在本次响应中的序号（从 0 开始）
                    var index = tc.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : 0;

                    // id: 仅在该工具调用的第一个 delta 中出现（如 "call_abc123"）
                    var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                    string? funcName = null;
                    string argsDelta = "";

                    if (tc.TryGetProperty("function", out var func))
                    {
                        // function.name: 仅在第一个 delta 中出现
                        if (func.TryGetProperty("name", out var nameEl))
                            funcName = nameEl.GetString();
                        // function.arguments: 每个 delta 都有，是参数 JSON 的一个片段
                        if (func.TryGetProperty("arguments", out var argsEl))
                            argsDelta = argsEl.GetString() ?? "";
                    }

                    // 稳定 ID：优先使用 API 返回的 id，没有时用 index 作为后备标识
                    // TokenProviderBase 用此 ID 累积参数片段
                    var stableId = id ?? index.ToString();
                    yield return new ToolCallDelta(stableId, funcName, argsDelta);
                }
            }
        }
    }
}
