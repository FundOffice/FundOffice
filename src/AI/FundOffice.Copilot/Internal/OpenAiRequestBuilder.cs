using System.Buffers;
using System.Text;
using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// OpenAI 请求体构建器 + 响应体解析器。
///
/// 全部使用 Utf8JsonWriter 手写 JSON，不依赖匿名对象或反射序列化。
/// 这样做是为了：
///   1. 零额外依赖（不需要任何 JSON 序列化库）
///   2. 精确控制输出格式（避免属性名大小写、null 处理等问题）
///   3. 高性能（直接写入 ArrayBufferWriter，无中间 string 分配）
///
/// OpenAI /v1/chat/completions 请求体结构：
/// <code>
/// {
///   "model": "gpt-4o",
///   "messages": [
///     {"role": "system", "content": "..."},
///     {"role": "user", "content": "..."},
///     {"role": "assistant", "content": "...", "tool_calls": [...]},
///     {"role": "tool", "tool_call_id": "...", "content": "..."}
///   ],
///   "tools": [{"type": "function", "function": {"name": "...", "description": "...", "parameters": {...}}}],
///   "temperature": 0.7,
///   "max_tokens": 16384,
///   "stream": true,
///   "stream_options": {"include_usage": true}
/// }
/// </code>
///
/// OpenAI 响应体结构（非流式）：
/// <code>
/// {
///   "choices": [{"message": {"role": "assistant", "content": "...", "tool_calls": [...]}, "finish_reason": "stop"}],
///   "usage": {"prompt_tokens": 10, "completion_tokens": 20}
/// }
/// </code>
/// </summary>
internal static class OpenAiRequestBuilder
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// 构建请求体 JSON 字节数组。
    ///
    /// stream=true 时：
    ///   - 添加 "stream": true
    ///   - 添加 "stream_options": {"include_usage": true} 让 API 在流中返回 token 用量
    ///   - API 返回 SSE 流而非 JSON
    ///
    /// stream=false 时：
    ///   - API 返回完整的 JSON 响应
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

        // messages 数组
        writer.WritePropertyName("messages");
        WriteMessages(writer, messages);

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
            writer.WriteNumber("max_tokens", maxTokens);
        if (options?.TopP is { } topP)
            writer.WriteNumber("top_p", topP);
        if (options?.StopSequences is { Count: > 0 } stops)
        {
            writer.WritePropertyName("stop");
            writer.WriteStartArray();
            foreach (var s in stops)
                writer.WriteStringValue(s);
            writer.WriteEndArray();
        }

        // Provider 特有参数（frequency_penalty, presence_penalty, parallel_tool_calls 等）
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
        if (stream)
        {
            // stream_options 让 API 在最后一个 SSE 消息中返回 usage 统计
            writer.WritePropertyName("stream_options");
            writer.WriteStartObject();
            writer.WriteBoolean("include_usage", true);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 解析非流式响应 JSON 为 ChatResult。
    ///
    /// OpenAI 响应中 finish_reason 的值：
    ///   "stop"          - 正常结束
    ///   "tool_calls"    - 模型请求调用工具
    ///   "length"        - 达到 max_tokens
    ///   "content_filter" - 内容被过滤
    /// </summary>
    public static ChatResult ParseCompletion(JsonElement root)
    {
        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var role = message.GetProperty("role").GetString()!;
        var finishReason = choice.GetProperty("finish_reason").GetString();

        var contentParts = new List<ContentPart>();

        // 文本内容（可能为 null，如纯工具调用响应）
        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            if (!string.IsNullOrEmpty(text))
                contentParts.Add(new TextContent(text));
        }

        // 工具调用列表
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in toolCalls.EnumerateArray())
            {
                var id = tc.GetProperty("id").GetString()!;
                var func = tc.GetProperty("function");
                var funcName = func.GetProperty("name").GetString()!;
                var funcArgs = func.GetProperty("arguments").GetString()!;
                contentParts.Add(new ToolCallContent(id, funcName, funcArgs));
            }
        }

        // token 用量
        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt))
                promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct))
                completionTokens = ct.GetInt32();
        }

        return new ChatResult
        {
            Messages = [new ChatMessage { Role = MessageRole.Assistant, Content = contentParts }],
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = NormalizeFinishReason(finishReason)
        };
    }

    /// <summary>
    /// 将 OpenAI 的 finish_reason 映射为统一的结束原因。
    /// OpenAI "length" 映射为 "max_tokens"，与其他 Provider 统一。
    /// </summary>
    public static string NormalizeFinishReason(string? reason) => reason switch
    {
        "stop" => "stop",
        "tool_calls" => "tool_calls",
        "length" => "max_tokens",
        "content_filter" => "content_filter",
        _ => reason ?? "stop"
    };

    /// <summary>
    /// 将统一的 ChatMessage 列表写为 OpenAI messages JSON 数组。
    ///
    /// 各角色的映射：
    ///   System    -> {"role":"system", "content":"..."}
    ///   User      -> {"role":"user", "content":"..."} 或 content 数组
    ///   Assistant -> {"role":"assistant", "content":"...", "tool_calls":[...]}
    ///   Tool      -> {"role":"tool", "tool_call_id":"...", "content":"..."}
    /// </summary>
    private static void WriteMessages(Utf8JsonWriter writer, IReadOnlyList<ChatMessage> messages)
    {
        writer.WriteStartArray();

        foreach (var msg in messages)
        {
            writer.WriteStartObject();

            switch (msg.Role)
            {
                case MessageRole.System:
                    writer.WriteString("role", "system");
                    WriteContentText(writer, msg.Content);
                    break;

                case MessageRole.User:
                    writer.WriteString("role", "user");
                    WriteContentParts(writer, msg.Content);
                    break;

                case MessageRole.Assistant:
                    writer.WriteString("role", "assistant");
                    WriteAssistantContent(writer, msg.Content);
                    break;

                case MessageRole.Tool:
                    // OpenAI 工具结果是独立的 role:"tool" 消息
                    writer.WriteString("role", "tool");
                    foreach (var part in msg.Content)
                    {
                        if (part is ToolResultContent tr)
                        {
                            writer.WriteString("tool_call_id", tr.ToolCallId);
                            writer.WriteString("content", tr.Result);
                            break;
                        }
                    }
                    break;
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>提取第一个 TextContent 写入 content 字段（System/User 简单场景）</summary>
    private static void WriteContentText(Utf8JsonWriter writer, IReadOnlyList<ContentPart> parts)
    {
        foreach (var part in parts)
        {
            if (part is TextContent tc)
            {
                writer.WriteString("content", tc.Text);
                return;
            }
        }
        writer.WriteString("content", "");
    }

    /// <summary>
    /// 写入 User 消息的 content。
    /// 单个 TextContent 时直接写字符串，多个时写 content part 数组。
    /// </summary>
    private static void WriteContentParts(Utf8JsonWriter writer, IReadOnlyList<ContentPart> parts)
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
                writer.WriteString("type", "text");
                writer.WriteString("text", text.Text);
                writer.WriteEndObject();
            }
        }
        writer.WriteEndArray();
    }

    /// <summary>
    /// 写入 Assistant 消息，包含 content（文本）和 tool_calls（工具调用）。
    ///
    /// OpenAI 格式中，assistant 消息的 tool_calls 是独立的顶层字段（不在 content 中）：
    /// <code>
    /// {
    ///   "role": "assistant",
    ///   "content": "让我查一下...",
    ///   "tool_calls": [
    ///     {"id": "call_123", "type": "function", "function": {"name": "get_weather", "arguments": "{...}"}}
    ///   ]
    /// }
    /// </code>
    /// </summary>
    private static void WriteAssistantContent(Utf8JsonWriter writer, IReadOnlyList<ContentPart> parts)
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

        // content 字段：有文本写文本，无文本写 null
        if (textParts.Count > 0)
            writer.WriteString("content", string.Join("", textParts));
        else
            writer.WriteNull("content");

        // tool_calls 数组
        if (toolCallParts.Count > 0)
        {
            writer.WritePropertyName("tool_calls");
            writer.WriteStartArray();
            foreach (var tc in toolCallParts)
            {
                writer.WriteStartObject();
                writer.WriteString("id", tc.Id);
                writer.WriteString("type", "function");
                writer.WritePropertyName("function");
                writer.WriteStartObject();
                writer.WriteString("name", tc.FunctionName);
                writer.WriteString("arguments", tc.ArgumentsJson);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// 写入 tools 数组。
    ///
    /// OpenAI 格式：每个工具包装在 {"type":"function", "function":{...}} 中。
    /// parameters 直接从 ToolDefinition.ParametersSchema (JsonElement) 零拷贝写入。
    /// </summary>
    private static void WriteTools(Utf8JsonWriter writer, IReadOnlyList<ToolDefinition> tools)
    {
        writer.WriteStartArray();
        foreach (var tool in tools)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WritePropertyName("function");
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WritePropertyName("parameters");
            tool.ParametersSchema.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
