using System.Buffers;
using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// OpenAI Responses API 请求体构建器 + 响应体解析器。
///
/// 与 <see cref="OpenAIRequestBuilder"/> 对称，但面向 Responses API（POST /v1/responses）。
///
/// 全部使用 Utf8JsonWriter 手写 JSON，不依赖匿名对象或反射序列化。
///
/// Responses API 请求体结构：
/// <code>
/// {
///   "model": "gpt-4o",
///   "input": [
///     {"role": "user", "content": "..."},
///     {"role": "assistant", "content": [{"type": "output_text", "text": "..."}]},
///     {"type": "function_call", "id": "fc_...", "call_id": "call_...", "name": "...", "arguments": "..."},
///     {"type": "function_call_output", "call_id": "call_...", "output": "..."}
///   ],
///   "instructions": "system prompt...",
///   "tools": [{"type": "function", "name": "...", "description": "...", "parameters": {...}}],
///   "temperature": 0.7,
///   "max_output_tokens": 16384,
///   "stream": true
/// }
/// </code>
///
/// Responses API 非流式响应体结构：
/// <code>
/// {
///   "id": "resp_...",
///   "output": [
///     {"type": "message", "role": "assistant", "content": [{"type": "output_text", "text": "..."}]},
///     {"type": "function_call", "id": "fc_...", "call_id": "call_...", "name": "...", "arguments": "..."}
///   ],
///   "usage": {"input_tokens": 10, "output_tokens": 20, "total_tokens": 30},
///   "status": "completed"
/// }
/// </code>
///
/// 与 Chat Completions API 的主要差异：
///   - messages → input
///   - system message → instructions 字段
///   - max_tokens → max_output_tokens
///   - 工具调用：assistant 的 tool_calls → 独立的 function_call 输入项
///   - 工具结果：role:"tool" → type:"function_call_output" 输入项
///   - 工具定义格式有变化（name/description/parameters 在顶层，不在嵌套的 function 对象中）
///   - 响应中 choices → output，message → output items
///   - finish_reason → status（"completed"/"incomplete"）
/// </summary>
internal static class OpenAIResponsesRequestBuilder
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// 构建 Responses API 请求体 JSON 字节数组。
    ///
    /// 关键映射：
    ///   - System 角色消息 → instructions 字段（只取第一条，后续忽略）
    ///   - User 消息 → input 中的 user 项
    ///   - Assistant 消息 → input 中的 assistant 项 + 独立的 function_call 项
    ///   - Tool 消息 → input 中的 function_call_output 项
    ///   - max_tokens → max_output_tokens
    ///   - stream=true 时添加 "stream": true
    /// </summary>
    public static byte[] BuildRequestBody(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        IChatOptions? options,
        string defaultModel,
        bool stream)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();

        // model 优先使用请求级选项，其次使用 Provider 构造时的默认值
        var model = options?.Model ?? defaultModel;
        writer.WriteString("model", model);

        // 提取 system 消息作为 instructions，其余消息写入 input 数组
        string? instructions = null;
        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.System)
            {
                foreach (var part in msg.Content)
                {
                    if (part is TextContent tc)
                    {
                        instructions = tc.Text;
                        break;
                    }
                }
                break; // 只取第一条 system 消息
            }
        }

        if (instructions is not null)
            writer.WriteString("instructions", instructions);

        // input 数组（非 System 消息）
        writer.WritePropertyName("input");
        WriteInput(writer, messages);

        // tools 数组（仅在有工具定义时写入）
        if (tools is { Count: > 0 })
        {
            writer.WritePropertyName("tools");
            WriteTools(writer, tools);
        }

        // 可选参数
        if (options?.Temperature is { } temp)
            writer.WriteNumber("temperature", temp);
        if (options?.MaxTokens is { } maxTokens)
            writer.WriteNumber("max_output_tokens", maxTokens);
        if (options?.TopP is { } topP)
            writer.WriteNumber("top_p", topP);
        // Responses API 不支持 stop 参数，静默忽略 StopSequences

        // Provider 特有参数（previous_response_id 等）
        if (options?.AdditionalProperties is not null)
        {
            foreach (var (key, value) in options.AdditionalProperties)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value, s_jsonOptions);
            }
        }

        // 流式标记
        writer.WriteBoolean("stream", stream);

        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 解析非流式响应 JSON 为 ChatResult。
    ///
    /// Responses API 响应中 status 的值：
    ///   "completed"     - 正常结束（对应 Chat Completions 的 "stop"）
    ///   "incomplete"    - 未完成（达到 max_output_tokens 等，对应 "max_tokens"）
    ///   "failed"        - 失败
    ///
    /// output 数组中的项类型：
    ///   "message"       - 文本消息，含 content 数组
    ///   "function_call" - 工具调用，含 id/call_id/name/arguments
    /// </summary>
    public static ChatResult ParseResponse(JsonElement root)
    {
        var contentParts = new List<ContentPart>();
        string? finishReason = null;

        // 解析 output 数组
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                switch (type)
                {
                    // 文本消息
                    case "message":
                        if (item.TryGetProperty("content", out var msgContent) && msgContent.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var c in msgContent.EnumerateArray())
                            {
                                var contentType = c.TryGetProperty("type", out var ct) ? ct.GetString() : null;
                                if (contentType == "output_text" || contentType == "text")
                                {
                                    var text = c.TryGetProperty("text", out var t) ? t.GetString() : "";
                                    if (!string.IsNullOrEmpty(text))
                                        contentParts.Add(new TextContent(text));
                                }
                            }
                        }
                        break;

                    // 工具调用
                    case "function_call":
                        // call_id 是 Responses API 的新字段，用于关联 function_call_output
                        // 兼容处理：优先用 call_id，否则用 id
                        var callId = item.TryGetProperty("call_id", out var cid) ? cid.GetString() : null;
                        if (callId is null)
                            callId = item.TryGetProperty("id", out var fid) ? fid.GetString() : "";
                        var funcName = item.TryGetProperty("name", out var fn) ? fn.GetString() : "";
                        var funcArgs = item.TryGetProperty("arguments", out var fa) ? fa.GetString() : "{}";
                        contentParts.Add(new ToolCallContent(callId ?? "", funcName ?? "", funcArgs ?? "{}"));
                        break;
                }
            }
        }

        // 解析 status → FinishReason
        if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
            finishReason = NormalizeStatus(status.GetString());

        // 如果有 function_call 输出项，finishReason 应为 "tool_calls"
        if (contentParts.Any(p => p is ToolCallContent) && finishReason == "stop")
            finishReason = "tool_calls";

        // 解析 token 用量
        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            if (usage.TryGetProperty("input_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number)
                completionTokens = ct.GetInt32();
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
    /// 将 Responses API 的 status 映射为统一的结束原因。
    /// </summary>
    public static string? NormalizeStatus(string? status) => status switch
    {
        "completed" => "stop",
        "incomplete" => "max_tokens",
        "failed" => "content_filter",
        _ => status
    };

    /// <summary>
    /// 将消息列表写为 Responses API 的 input JSON 数组。
    ///
    /// 与 Chat Completions 的 messages 格式不同：
    ///   - System 消息被提取为 instructions，不出现在 input 中
    ///   - Assistant 消息的文本部分 → assistant 输入项
    ///   - Assistant 消息的 tool_calls → 独立的 function_call 输入项
    ///   - Tool 消息 → function_call_output 输入项
    ///
    /// 各角色的映射：
    ///   User      → {"role": "user", "content": "..."}
    ///   Assistant → {"role": "assistant", "content": [...]} + 独立 function_call 项
    ///   Tool      → {"type": "function_call_output", "call_id": "...", "output": "..."}
    /// </summary>
    private static void WriteInput(Utf8JsonWriter writer, IReadOnlyList<ChatMessage> messages)
    {
        writer.WriteStartArray();

        foreach (var msg in messages)
        {
            // System 消息已提取为 instructions，跳过
            if (msg.Role == MessageRole.System)
                continue;

            switch (msg.Role)
            {
                case MessageRole.User:
                    writer.WriteStartObject();
                    writer.WriteString("role", "user");
                    WriteUserContent(writer, msg.Content);
                    writer.WriteEndObject();
                    break;

                case MessageRole.Assistant:
                    WriteAssistantInputItems(writer, msg.Content);
                    break;

                case MessageRole.Tool:
                    // Responses API：工具结果为 function_call_output 输入项
                    foreach (var part in msg.Content)
                    {
                        if (part is ToolResultContent tr)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("type", "function_call_output");
                            writer.WriteString("call_id", tr.ToolCallId);
                            writer.WriteString("output", tr.Result);
                            writer.WriteEndObject();
                        }
                    }
                    break;
            }
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写入 User 消息的 content。
    /// 单个 TextContent 时直接写字符串，多个时写 content part 数组。
    /// </summary>
    private static void WriteUserContent(Utf8JsonWriter writer, IReadOnlyList<ContentPart> parts)
    {
        if (parts.Count == 1 && parts[0] is TextContent tc)
        {
            writer.WriteString("content", tc.Text);
            return;
        }

        writer.WritePropertyName("content");
        writer.WriteStartArray();
        foreach (var part in parts)
        {
            if (part is TextContent text)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "input_text");
                writer.WriteString("text", text.Text);
                writer.WriteEndObject();
            }
        }
        writer.WriteEndArray();
    }

    /// <summary>
    /// 写入 Assistant 消息对应的 input 项。
    ///
    /// Responses API 中，assistant 消息的文本和工具调用是分开的 input 项：
    ///   1. {"role": "assistant", "content": [{"type": "output_text", "text": "..."}]}
    ///   2. {"type": "function_call", "call_id": "...", "name": "...", "arguments": "..."}
    ///
    /// 如果 assistant 只有工具调用没有文本，仍然需要写一个 content 为空的 assistant 项。
    /// </summary>
    private static void WriteAssistantInputItems(Utf8JsonWriter writer, IReadOnlyList<ContentPart> parts)
    {
        var textParts = new List<string>();
        var toolCallParts = new List<ToolCallContent>();

        foreach (var part in parts)
        {
            switch (part)
            {
                case TextContent tc:
                    textParts.Add(tc.Text);
                    break;
                case ToolCallContent tcc:
                    toolCallParts.Add(tcc);
                    break;
            }
        }

        // assistant 消息项（文本内容）
        writer.WriteStartObject();
        writer.WriteString("role", "assistant");
        if (textParts.Count > 0)
        {
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var text in textParts)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "output_text");
                writer.WriteString("text", text);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull("content");
        }
        writer.WriteEndObject();

        // 独立的 function_call 输入项
        foreach (var tc in toolCallParts)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "function_call");
            writer.WriteString("call_id", tc.Id);
            writer.WriteString("name", tc.FunctionName);
            writer.WriteString("arguments", tc.ArgumentsJson);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// 写入 tools 数组。
    ///
    /// Responses API 的工具定义格式与 Chat Completions 有差异：
    /// <code>
    /// {"type": "function", "name": "...", "description": "...", "parameters": {...}}
    /// </code>
    /// 注意：name/description/parameters 在顶层，不在嵌套的 function 对象中。
    /// </summary>
    private static void WriteTools(Utf8JsonWriter writer, IReadOnlyList<ToolDefinition> tools)
    {
        writer.WriteStartArray();
        foreach (var tool in tools)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WritePropertyName("parameters");
            tool.ParametersSchema.WriteTo(writer);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
