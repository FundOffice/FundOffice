using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// Google Gemini API 实现。
///
/// API 端点：POST {_baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}
/// 认证方式：查询参数 key={apiKey}
///
/// Gemini 请求体结构：
/// <code>
/// {
///   "contents": [
///     {
///       "role": "user",
///       "parts": [{"text": "..."}]
///     }
///   ],
///   "generationConfig": {
///     "temperature": 0.7,
///     "maxOutputTokens": 16384
///   }
/// }
/// </code>
///
/// Gemini 响应体结构：
/// <code>
/// {
///   "candidates": [
///     {
///       "content": {"role": "model", "parts": [{"text": "..."}]},
///       "finishReason": "STOP"
///     }
///   ],
///   "usageMetadata": {"promptTokenCount": 10, "candidatesTokenCount": 20}
/// }
/// </code>
///
/// Gemini 支持多模态输入（inline_data），支持工具调用（functionCall/functionResponse）。
/// </summary>
public sealed class GoogleTokenProvider : TokenProviderBase
{
    private const string ProviderName = "Google";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _idf;

    public override string Identifier => _idf;

    public GoogleTokenProvider(GoogleOptions options, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("ApiKey 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new ArgumentException("BaseUrl 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Model))
            throw new ArgumentException("Model 不能为空", nameof(options));

        _httpClient = httpClient ?? new HttpClient();
        _apiKey = options.ApiKey;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _model = options.Model;
        _idf = options.Identifier;
    }

    public override async IAsyncEnumerable<StreamingToken> ChatCompletionStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Gemini 目前不支持真正的 SSE 流式响应，使用非流式 API
        var result = await ChatCompletionAsync(messages, tools, options, cancellationToken);

        // 将结果模拟为流式输出
        var firstMessage = result.Messages?.FirstOrDefault();
        if (firstMessage != null)
        {
            foreach (var part in firstMessage.Content ?? [])
            {
                switch (part)
                {
                    case TextContent tc:
                        yield return new TextDelta(tc.Text);
                        break;
                    case ToolCallContent tcc:
                        yield return new ToolCallDelta(tcc.Id, tcc.FunctionName, tcc.ArgumentsJson);
                        break;
                }
            }
        }

        yield return new UsageUpdate(result.PromptTokens, result.CompletionTokens);
        yield return new StreamComplete(result.FinishReason);
    }

    public override async Task<ChatResult> ChatCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = options?.Model ?? _model;
            var url = $"{_baseUrl}/v1beta/models/{model}:generateContent?key={_apiKey}";

            var body = BuildRequestBody(messages, tools, options);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return ParseResponse(doc.RootElement);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }
    }

    public override async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(
        string? modelUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(modelUrl)
            ? $"{_baseUrl}/v1beta/models?key={_apiKey}"
            : modelUrl;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var modelsArr) && modelsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modelsArr.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (name is null) continue;

                    // Google 返回 "models/gemini-2.5-pro" 格式，去掉前缀
                    if (name.StartsWith("models/"))
                        name = name.Substring("models/".Length);

                    models.Add(new ModelInfo { Id = name });
                }
            }
            return models;
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }
    }

    private byte[] BuildRequestBody(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        IChatOptions? options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();

        // contents 数组
        writer.WritePropertyName("contents");
        writer.WriteStartArray();

        // 收集 system 消息内容，合并到第一个 user 消息
        string? systemContent = null;
        var systemMessages = messages.Where(m => m.Role == MessageRole.System).ToList();
        if (systemMessages.Count > 0)
        {
            systemContent = string.Join("\n\n", systemMessages
                .SelectMany(m => m.Content)
                .OfType<TextContent>()
                .Select(t => t.Text));
        }

        bool systemApplied = false;
        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.System)
                continue;

            writer.WriteStartObject();

            // Gemini 用 "user" 和 "model" 作为 role
            writer.WriteString("role", msg.Role == MessageRole.Assistant ? "model" : "user");

            writer.WritePropertyName("parts");
            writer.WriteStartArray();

            foreach (var part in msg.Content)
            {
                if (part is TextContent tc)
                {
                    writer.WriteStartObject();
                    // 如果是第一个 user 消息且有 system 内容，合并
                    if (!systemApplied && msg.Role == MessageRole.User && systemContent != null)
                    {
                        writer.WriteString("text", systemContent + "\n\n" + tc.Text);
                        systemApplied = true;
                    }
                    else
                    {
                        writer.WriteString("text", tc.Text);
                    }
                    writer.WriteEndObject();
                }
                else if (part is DocumentContent doc)
                {
                    // Gemini inline_data 格式
                    writer.WriteStartObject();
                    writer.WritePropertyName("inline_data");
                    writer.WriteStartObject();
                    writer.WriteString("mime_type", doc.MediaType);
                    writer.WriteString("data", doc.Data);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                else if (part is ToolResultContent trc)
                {
                    // Gemini functionResponse 格式
                    writer.WriteStartObject();
                    writer.WritePropertyName("functionResponse");
                    writer.WriteStartObject();
                    writer.WriteString("name", trc.ToolCallId);
                    writer.WritePropertyName("response");
                    // 尝试将 Result 作为 JSON 解析，否则作为字符串
                    try
                    {
                        using var respDoc = JsonDocument.Parse(trc.Result);
                        respDoc.RootElement.WriteTo(writer);
                    }
                    catch
                    {
                        writer.WriteStringValue(trc.Result);
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // generationConfig
        writer.WritePropertyName("generationConfig");
        writer.WriteStartObject();
        if (options?.Temperature is { } temp)
            writer.WriteNumber("temperature", temp);
        if (options?.MaxTokens is { } maxTokens)
            writer.WriteNumber("maxOutputTokens", maxTokens);
        else
            writer.WriteNumber("maxOutputTokens", 16384);
        writer.WriteEndObject();

        // safetySettings (可选)
        // Gemini 默认的安全设置，避免内容被过滤
        writer.WritePropertyName("safetySettings");
        writer.WriteStartArray();
        foreach (var category in new[] { "HARM_CATEGORY_HARASSMENT", "HARM_CATEGORY_HATE_SPEECH", "HARM_CATEGORY_SEXUALLY_EXPLICIT", "HARM_CATEGORY_DANGEROUS_CONTENT" })
        {
            writer.WriteStartObject();
            writer.WriteString("category", category);
            writer.WriteString("threshold", "BLOCK_NONE");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // tools（functionDeclarations）
        if (tools is { Count: > 0 })
        {
            writer.WritePropertyName("tools");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WritePropertyName("functionDeclarations");
            writer.WriteStartArray();
            foreach (var tool in tools)
            {
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WritePropertyName("parameters");
                tool.ParametersSchema.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static ChatResult ParseResponse(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
            return new ChatResult { Messages = [], FinishReason = "error" };

        var candidate = candidates[0];

        if (!candidate.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
            return new ChatResult { Messages = [], FinishReason = "error" };

        var contentParts = new List<ContentPart>();
        foreach (var part in parts.EnumerateArray())
        {
            // 文本内容
            if (part.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrEmpty(text))
                    contentParts.Add(new TextContent(text));
            }
            // 工具调用（Gemini functionCall 格式）
            else if (part.TryGetProperty("functionCall", out var fc))
            {
                var funcName = fc.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                var argsJson = fc.TryGetProperty("args", out var argsEl) ? argsEl.GetRawText() : "{}";
                // Gemini 没有 call id，用函数名 + 时间戳生成稳定 ID
                contentParts.Add(new ToolCallContent($"call_{funcName}", funcName, argsJson));
            }
        }

        var finishReason = candidate.TryGetProperty("finishReason", out var frEl)
            ? NormalizeFinishReason(frEl.GetString())
            : "stop";

        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("promptTokenCount", out var pt))
                promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("candidatesTokenCount", out var ct))
                completionTokens = ct.GetInt32();
        }

        // 如果包含 functionCall，覆盖 finishReason
        if (contentParts.Any(p => p is ToolCallContent) && finishReason == "stop")
            finishReason = "tool_calls";

        return new ChatResult
        {
            Messages = [new ChatMessage { Role = MessageRole.Assistant, Content = contentParts }],
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = finishReason
        };
    }

    private static string NormalizeFinishReason(string? reason) => reason?.ToUpperInvariant() switch
    {
        "STOP" => "stop",
        "MAX_TOKENS" => "max_tokens",
        "SAFETY" => "content_filter",
        "RECITATION" => "content_filter",
        _ => reason?.ToLowerInvariant() ?? "stop"
    };
}