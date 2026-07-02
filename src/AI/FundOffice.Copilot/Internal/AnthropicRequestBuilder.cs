using System.Buffers;
using System.Text;
using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// Anthropic 请求体构建器 + 响应体解析器。
///
/// Anthropic /v1/messages 请求体结构：
/// <code>
/// {
///   "model": "claude-sonnet-4-20250514",
///   "max_tokens": 16384,                             // 必填
///   "system": "系统提示词",                            // 独立字段，不在 messages 中
///   "messages": [
///     {"role": "user", "content": "你好"},
///     {"role": "assistant", "content": "你好！"},
///     {"role": "user", "content": [                   // 工具结果在 user 消息中
///       {"type": "tool_result", "tool_use_id": "...", "content": "..."}
///     ]},
///     {"role": "assistant", "content": [              // 工具调用在 assistant 消息中
///       {"type": "tool_use", "id": "...", "name": "...", "input": {...}}
///     ]}
///   ],
///   "tools": [{"name": "...", "description": "...", "input_schema": {...}}],
///   "stream": true
/// }
/// </code>
///
/// 与 OpenAI 的关键差异总结：
///   1. system 独立字段（不在 messages 数组中）
///   2. 没有 role:"tool"，工具结果放在 role:"user" 消息的 tool_result 内容块中
///   3. messages 必须 user/assistant 交替（ProcessMessages 保证）
///   4. max_tokens 是必填字段
///   5. 工具定义用 input_schema（不是 parameters）
///   6. 工具调用在 assistant 消息的 content 数组中（不是独立的 tool_calls 字段）
/// </summary>
internal static class AnthropicRequestBuilder
{
    /// <summary>构建请求体 JSON 字节数组</summary>
    public static byte[] BuildRequestBody(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        IChatOptions? options,
        string defaultModel,
        bool stream)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        // model 优先使用请求级选项，其次使用 Provider 构造时的默认值
        writer.WriteString("model", options?.Model ?? defaultModel);

        // max_tokens（Anthropic 必填，默认 16384）
        writer.WriteNumber("max_tokens", options?.MaxTokens ?? 16384);

        // system 提示词：从所有 System 角色消息中提取，合并为一个字符串
        // Anthropic 不允许 system 消息出现在 messages 数组中
        var systemParts = new List<string>();
        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.System)
            {
                foreach (var part in msg.Content)
                {
                    if (part is TextContent tc)
                        systemParts.Add(tc.Text);
                }
            }
        }
        if (systemParts.Count > 0)
        {
            writer.WriteString("system", string.Join("\n\n", systemParts));
        }

        // 可选参数
        if (options?.Temperature is { } temp)
            writer.WriteNumber("temperature", temp);
        if (options?.TopP is { } topP)
            writer.WriteNumber("top_p", topP);

        // Anthropic 特有参数（如 top_k）
        if (options?.AdditionalProperties is not null)
        {
            foreach (var (key, value) in options.AdditionalProperties)
            {
                if (key == "top_k" && value is int topK)
                    writer.WriteNumber("top_k", topK);
                else if (key == "top_k" && value is double topKd)
                    writer.WriteNumber("top_k", topKd);
            }
        }

        // 停止序列（Anthropic 用 stop_sequences，OpenAI 用 stop）
        if (options?.StopSequences is { Count: > 0 } stops)
        {
            writer.WritePropertyName("stop_sequences");
            writer.WriteStartArray();
            foreach (var s in stops)
                writer.WriteStringValue(s);
            writer.WriteEndArray();
        }

        // 工具定义（Anthropic 用 input_schema，OpenAI 用 parameters）
        if (tools is { Count: > 0 })
        {
            writer.WritePropertyName("tools");
            writer.WriteStartArray();
            foreach (var tool in tools)
            {
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WritePropertyName("input_schema");
                tool.ParametersSchema.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        // messages（System 已提取到 system 字段，此处跳过）
        writer.WritePropertyName("messages");
        WriteMessages(writer, messages);

        writer.WriteBoolean("stream", stream);

        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// 写入 messages 数组。
    ///
    /// 关键处理逻辑：
    ///   1. 跳过 System 消息（已在 system 字段中）
    ///   2. Tool 消息映射为 user 消息 + tool_result 内容块
    ///   3. 连续同角色消息合并（保证 user/assistant 交替）
    /// </summary>
    private static void WriteMessages(Utf8JsonWriter writer, IReadOnlyList<ChatMessage> messages)
    {
        writer.WriteStartArray();

        // 预处理：合并连续同角色消息，保证 Anthropic 的交替要求
        var processed = ProcessMessages(messages);

        foreach (var (role, parts) in processed)
        {
            writer.WriteStartObject();
            writer.WriteString("role", role);

            writer.WritePropertyName("content");

            // 单个 TextContent 优化：直接写字符串，省略 content block 包装
            if (parts.Count == 1 && parts[0] is TextContent tc)
            {
                writer.WriteStringValue(tc.Text);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var part in parts)
                {
                    WriteContentBlock(writer, part);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 写入单个内容块。
    ///
    /// Anthropic 内容块类型：
    ///   text        - 文本内容
    ///   tool_use    - 工具调用请求（assistant 消息中）
    ///   tool_result - 工具执行结果（user 消息中）
    ///
    /// 注意：tool_use 的 input 字段是 JSON 对象（不是字符串），
    /// 需要从 ArgumentsJson 字符串重新解析为 JsonDocument 再写入。
    /// </summary>
    private static void WriteContentBlock(Utf8JsonWriter writer, ContentPart part)
    {
        switch (part)
        {
            case TextContent tc:
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", tc.Text);
                writer.WriteEndObject();
                break;

            case DocumentContent doc:
                // Anthropic document 格式：https://docs.anthropic.com/en/docs/build-with-claude/pdf-support
                writer.WriteStartObject();
                writer.WriteString("type", "document");
                writer.WritePropertyName("source");
                writer.WriteStartObject();
                writer.WriteString("type", "base64");
                writer.WriteString("media_type", doc.MediaType);
                writer.WriteString("data", doc.Data);
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;

            case ToolCallContent tcc:
                writer.WriteStartObject();
                writer.WriteString("type", "tool_use");
                writer.WriteString("id", tcc.Id);
                writer.WriteString("name", tcc.FunctionName);
                writer.WritePropertyName("input");
                // ArgumentsJson 是原始 JSON 字符串，解析后写入以保证格式正确
                using (var doc = JsonDocument.Parse(tcc.ArgumentsJson))
                {
                    doc.RootElement.WriteTo(writer);
                }
                writer.WriteEndObject();
                break;

            case ToolResultContent trc:
                writer.WriteStartObject();
                writer.WriteString("type", "tool_result");
                writer.WriteString("tool_use_id", trc.ToolCallId);
                writer.WriteString("content", trc.Result);
                if (trc.IsError)
                    writer.WriteBoolean("is_error", true);
                writer.WriteEndObject();
                break;
        }
    }

    /// <summary>
    /// 消息预处理：确保符合 Anthropic 的 user/assistant 交替要求。
    ///
    /// 处理规则：
    ///   1. System 消息跳过（已在 system 字段中）
    ///   2. Tool 消息映射为 user 消息（工具结果在 user 消息中）
    ///   3. 连续同 effective role 的消息合并（ContentPart 合并到同一个列表）
    ///
    /// 示例：
    ///   输入: [User, Assistant, Tool, Tool, User, Assistant]
    ///   输出: [("user", [...]), ("assistant", [...]), ("user", [tool_result, tool_result, ...]), ("assistant", [...])]
    ///   注意两个 Tool 和后面的 User 被合并为一个 user 消息
    /// </summary>
    private static List<(string Role, List<ContentPart> Parts)> ProcessMessages(
        IReadOnlyList<ChatMessage> messages)
    {
        var result = new List<(string Role, List<ContentPart> Parts)>();

        foreach (var msg in messages)
        {
            // System 消息已在 system 字段中处理
            if (msg.Role == MessageRole.System)
                continue;

            // 确定 effective role
            string effectiveRole = msg.Role switch
            {
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "user",   // 关键：Tool 结果映射为 user 消息
                _ => "user"
            };

            // 如果与上一条消息同 role，合并 ContentPart 列表
            if (result.Count > 0 && result[^1].Role == effectiveRole)
            {
                result[^1].Parts.AddRange(msg.Content);
            }
            else
            {
                result.Add((effectiveRole, new List<ContentPart>(msg.Content)));
            }
        }

        return result;
    }

    /// <summary>
    /// 解析 Anthropic 非流式响应 JSON 为 ChatResult。
    ///
    /// Anthropic 响应结构：
    /// <code>
    /// {
    ///   "content": [
    ///     {"type": "text", "text": "你好！"},
    ///     {"type": "tool_use", "id": "toolu_123", "name": "get_weather", "input": {"location": "Beijing"}}
    ///   ],
    ///   "stop_reason": "end_turn",    // end_turn | tool_use | max_tokens | stop_sequence
    ///   "usage": {"input_tokens": 10, "output_tokens": 20}
    /// }
    /// </code>
    /// </summary>
    public static ChatResult ParseCompletion(JsonElement root)
    {
        var contentParts = new List<ContentPart>();

        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                switch (type)
                {
                    case "text":
                        var text = block.GetProperty("text").GetString() ?? "";
                        if (text.Length > 0)
                            contentParts.Add(new TextContent(text));
                        break;

                    case "tool_use":
                        var id = block.GetProperty("id").GetString()!;
                        var name = block.GetProperty("name").GetString()!;
                        var input = block.GetProperty("input");
                        // GetRawText() 获取原始 JSON 字符串，与 ToolCallContent.ArgumentsJson 格式一致
                        var argsJson = input.GetRawText();
                        contentParts.Add(new ToolCallContent(id, name, argsJson));
                        break;
                }
            }
        }

        // Anthropic 的 token 字段名：input_tokens / output_tokens
        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var pt))
                promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ct))
                completionTokens = ct.GetInt32();
        }

        var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;

        return new ChatResult
        {
            Messages = [new ChatMessage { Role = MessageRole.Assistant, Content = contentParts }],
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = NormalizeFinishReason(stopReason)
        };
    }

    /// <summary>
    /// 将 Anthropic 的 stop_reason 映射为统一的结束原因。
    ///
    /// Anthropic          统一值
    /// end_turn       ->  stop          （模型主动结束对话轮次）
    /// tool_use       ->  tool_calls    （模型请求调用工具）
    /// max_tokens     ->  max_tokens    （达到最大 token 限制）
    /// stop_sequence  ->  stop          （匹配到停止序列）
    /// </summary>
    public static string NormalizeFinishReason(string? reason) => reason switch
    {
        "end_turn" => "stop",
        "tool_use" => "tool_calls",
        "max_tokens" => "max_tokens",
        "stop_sequence" => "stop",
        _ => reason ?? "stop"
    };
}
