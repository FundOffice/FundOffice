using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// OpenAI Responses API 实现。
///
/// 与 <see cref="OpenAITokenProvider"/> 对称，但使用新一代 Responses API。
/// 也兼容所有支持 Responses API 的第三方代理。
///
/// API 端点：POST {_baseUrl}/v1/responses
/// 认证方式：Authorization: Bearer {_apiKey}
///
/// 流式响应格式（SSE）：
///   event: response.output_text.delta
///   data: {"type":"response.output_text.delta","output_index":0,"content_index":0,"delta":"你"}
///
///   event: response.function_call_arguments.delta
///   data: {"type":"response.function_call_arguments.delta","output_index":0,"call_id":"call_123","delta":"{\"lo"}
///
///   event: response.completed
///   data: {"type":"response.completed","response":{"status":"completed","usage":{...}}}
///
/// 非流式响应格式：
///   {"id":"resp_...","output":[{"type":"message",...},{"type":"function_call",...}],"status":"completed","usage":{...}}
///
/// 与 Chat Completions API 的主要差异：
///   - messages → input + instructions
///   - max_tokens → max_output_tokens
///   - 工具调用/结果格式不同
///   - 响应结构：choices → output, finish_reason → status
///   - 新增 previous_response_id 支持服务端对话状态
///
/// 错误处理：
///   - HTTP 错误 -> TokenProviderException（含结构化的 Kind、ResponseBody）
///   - 网络异常 (HttpRequestException) -> TokenProviderException(NetworkError)
///   - JSON 解析异常 -> TokenProviderException(JsonError)
/// </summary>
public sealed class OpenAIResponsesProvider : TokenProviderBase
{
    private const string ProviderName = "OpenAI Responses";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _idf;

    public override string Identifier => _idf;

    /// <summary>
    /// 创建 OpenAI Responses API Provider。
    ///
    /// 参数在构造时一次性校验并存储为字段，之后不再持有 options 引用。
    /// 请求级选项（如切换模型、调温度）通过 ChatOptions 在每次调用时传入。
    /// </summary>
    /// <param name="options">Provider 配置：ApiKey（必填）、Model（必填）、BaseUrl（必填）</param>
    /// <param name="httpClient">可选注入，不传则内部创建（无代理、默认超时）</param>
    public OpenAIResponsesProvider(OpenAIOptions options, HttpClient? httpClient = null)
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

    /// <summary>
    /// 流式调用。
    ///
    /// 生命周期说明：
    ///   response 和 stream 在 try 块中获取，成功后用 using/await using 管理释放。
    ///   yield return 发生在 using 块内，确保整个流式消费期间资源不被释放。
    ///   如果 try 块中抛出异常，response/stream 尚未赋值，不会泄漏。
    /// </summary>
    public override async IAsyncEnumerable<StreamingToken> ChatCompletionStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        Stream stream;
        try
        {
            var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, tools, options, _model, stream: true);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/responses");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // ResponseHeadersRead: 收到响应头就返回，不等整个 body 下载完
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);
            stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }

        using var _ = response;
        await using (stream)
        {
            await foreach (var (eventType, data) in SseParser.ParseAsync(stream, cancellationToken))
            {
                foreach (var token in OpenAIResponsesStreamMapper.MapEvent(eventType, data))
                    yield return token;
            }
        }
    }

    /// <summary>
    /// 非流式调用。覆盖基类默认实现（基类聚合流式结果），直接用非流式 API 更高效。
    /// </summary>
    public override async Task<ChatResult> ChatCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, tools, options, _model, stream: false);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/responses");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }
    }

    /// <summary>
    /// 获取可用模型列表。调用 GET {baseUrl}/v1/models。
    ///
    /// modelUrl 非空时直接用作完整 URL，为空时用 _baseUrl 拼接。
    /// 404 时静默返回空列表（端点不支持），其他错误抛 TokenProviderException。
    /// </summary>
    public override async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(
        string? modelUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(modelUrl) ? $"{_baseUrl}/v1/models" : modelUrl;

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }

        using var _ = response;
        await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return [];

            var models = new List<ModelInfo>();
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (id is null) continue;
                var ownedBy = item.TryGetProperty("owned_by", out var ob) ? ob.GetString() : null;
                models.Add(new ModelInfo { Id = id, OwnedBy = ownedBy });
            }
            return models;
        }
    }
}
