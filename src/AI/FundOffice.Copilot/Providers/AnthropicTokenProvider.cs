using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// Anthropic API 实现。也兼容所有 Anthropic 格式的第三方代理。
///
/// API 端点：POST {_baseUrl}/v1/messages
/// 认证方式：x-api-key: {_apiKey}，anthropic-version: {_apiVersion}
///
/// 与 OpenAI 的主要差异：
///   1. system 消息不放在 messages 数组中，而是顶层 system 字段
///   2. tool 角色不存在，工具结果放在 user 消息的 tool_result 内容块中
///   3. messages 必须 user/assistant 交替，连续同角色消息需合并
///   4. max_tokens 是必填字段（默认 16384）
///
/// 流式事件格式（SSE）：
///   event: message_start        -> 包含 input_tokens
///   event: content_block_start  -> 文本块或工具调用块开始
///   event: content_block_delta  -> text_delta 或 input_json_delta（工具参数片段）
///   event: content_block_stop   -> 内容块结束
///   event: message_delta        -> stop_reason + output_tokens
///   event: message_stop         -> 消息完成
///   event: ping                 -> 心跳，忽略
///
/// 注意：AnthropicStreamMapper 是实例类（非静态），
/// 因为它需要跟踪当前 content block 的状态（_currentBlockType/Id/Name），
/// 并发请求不能共享状态。
/// </summary>
public sealed class AnthropicTokenProvider : TokenProviderBase
{
    private const string ProviderName = "Anthropic";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _apiVersion;
    private readonly string _idf;

    public override string Identifier => _idf;


    /// <summary>
    /// 创建 Anthropic Provider。
    ///
    /// 参数在构造时一次性校验并存储为字段，之后不再持有 options 引用。
    /// 请求级选项（如切换模型、调温度）通过 ChatOptions 在每次调用时传入。
    /// </summary>
    /// <param name="options">Provider 配置：ApiKey（必填）、Model（必填）、BaseUrl（必填）、ApiVersion</param>
    /// <param name="httpClient">可选注入，不传则内部创建（无代理、默认超时）</param>
    public AnthropicTokenProvider(AnthropicOptions options, HttpClient? httpClient = null)
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
        _apiVersion = options.ApiVersion;
        _idf = options.Identifier;
    }

    /// <summary>流式调用。详见 OpenAiTokenProvider 同名方法的生命周期说明。</summary>
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
            var body = AnthropicRequestBuilder.BuildRequestBody(messages, tools, options, _model, stream: true);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", _apiVersion);

            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);
            stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }

        // 每次请求创建独立的 mapper 实例，避免并发状态冲突
        var mapper = new AnthropicStreamMapper();
        using var _ = response;
        await using (stream)
        {
            await foreach (var (eventType, data) in SseParser.ParseAsync(stream, cancellationToken))
            {
                foreach (var token in mapper.MapEvent(eventType, data))
                    yield return token;
            }
        }
    }

    /// <summary>非流式调用。覆盖基类默认实现以使用原生非流式 API。</summary>
    public override async Task<ChatResult> ChatCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        IChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = AnthropicRequestBuilder.BuildRequestBody(messages, tools, options, _model, stream: false);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", _apiVersion);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await ErrorMapper.ThrowIfErrorAsync(response, ProviderName);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return AnthropicRequestBuilder.ParseCompletion(doc.RootElement);
        }
        catch (HttpRequestException ex) { throw ErrorMapper.WrapNetworkError(ex, ProviderName); }
        catch (JsonException ex) { throw ErrorMapper.WrapJsonError(ex, ProviderName); }
    }

    /// <summary>
    /// 获取可用模型列表。调用 GET {baseUrl}/v1/models。
    ///
    /// Anthropic 标准 API 不提供模型列表端点，但部分兼容代理支持。
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
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", _apiVersion);
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

            // 兼容 OpenAI 格式: {"data": [{"id": "...", "owned_by": "..."}]}
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
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

            // 兼容直接数组格式: [{"id": "..."}]
            if (root.ValueKind == JsonValueKind.Array)
            {
                var models = new List<ModelInfo>();
                foreach (var item in root.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (id is null) continue;
                    models.Add(new ModelInfo { Id = id });
                }
                return models;
            }

            return [];
        }
    }
}
