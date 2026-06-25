using System.Net;
using System.Text.Json;
using FundOffice.Copilot.Providers;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// HTTP 错误响应和网络异常 -> TokenProviderException 的统一转换器。
///
/// 两个 Provider（OpenAiTokenProvider、AnthropicTokenProvider）共享此逻辑，
/// 避免在每个 Provider 中重复错误处理代码。
///
/// 错误分类策略：
///
///   HTTP 状态码         TokenProviderErrorKind     说明
///   ─────────────────────────────────────────────────────────
///   401 Unauthorized    Authentication             API Key 无效
///   403 Forbidden       Authentication 或           需进一步细分：
///                       InvalidModel               "no access to model" -> InvalidModel
///   404 Not Found       InvalidModel               模型端点不存在
///   400 Bad Request     BadRequest 或              需进一步细分：
///                       InvalidModel               "Not supported model" -> InvalidModel
///   422 Unprocessable   BadRequest                 请求参数格式错误
///   429 Too Many Reqs   RateLimited                频率限制，可重试
///   500/502/503/504     ServerError                服务端问题，可重试
///   其他 4xx            BadRequest
///   其他 5xx            ServerError
///
/// 响应体格式兼容：
///   OpenAI:    {"error": {"message": "...", "code": "...", "type": "..."}}
///   Anthropic: {"error": {"message": "...", "code": "...", "type": "..."}}
///   部分代理:  {"error": {"code": "", "message": "...", "type": "..."}}
///   简单格式:  {"error": "message string"}
/// </summary>
internal static class ErrorMapper
{
    /// <summary>
    /// 检查 HTTP 响应状态，失败时读取响应体并抛出 TokenProviderException。
    /// 成功时静默返回。
    /// </summary>
    public static async Task ThrowIfErrorAsync(HttpResponseMessage response, string providerName)
    {
        if (response.IsSuccessStatusCode) return;

        var statusCode = response.StatusCode;
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            // 响应体读取失败时用空字符串，后续仅依赖状态码分类
            body = "";
        }

        var (kind, apiCode, apiMessage) = Classify(statusCode, body);

        // 构建人类可读的错误消息
        var message = $"[{providerName}] {kind} ({(int)statusCode})";
        if (apiMessage is not null)
            message += $": {apiMessage}";
        else if (body.Length > 0 && body.Length < 500)
            message += $": {body}";

        throw new TokenProviderException(kind, message, statusCode, body, apiCode);
    }

    /// <summary>
    /// 将 HttpRequestException（网络层错误，如 DNS 失败、连接超时、连接被拒绝）包装为 TokenProviderException。
    /// 此时没有 HTTP 响应，StatusCode 为 null。
    /// </summary>
    public static TokenProviderException WrapNetworkError(Exception ex, string providerName)
    {
        return new TokenProviderException(
            TokenProviderErrorKind.NetworkError,
            $"[{providerName}] 网络错误: {ex.Message}",
            innerException: ex);
    }

    /// <summary>
    /// 将 JsonException（请求体构建或响应体解析时的 JSON 错误）包装为 TokenProviderException。
    /// 通常是代码 bug 或 API 返回了非预期的格式。
    /// </summary>
    public static TokenProviderException WrapJsonError(Exception ex, string providerName)
    {
        return new TokenProviderException(
            TokenProviderErrorKind.JsonError,
            $"[{providerName}] JSON 解析失败: {ex.Message}",
            innerException: ex);
    }

    /// <summary>
    /// 根据 HTTP 状态码和响应体 JSON 进行错误分类。
    ///
    /// 逻辑：
    ///   1. 先尝试从响应体提取 API 错误信息（message, code, type）
    ///   2. 根据状态码做初步分类
    ///   3. 403 和 400 需要进一步细分（通过响应体内容判断是模型问题还是认证问题）
    /// </summary>
    private static (TokenProviderErrorKind Kind, string? Code, string? Message) Classify(
        HttpStatusCode statusCode, string body)
    {
        // 第一步：从响应体提取 API 错误信息
        string? apiCode = null;
        string? apiMessage = null;
        string? apiParam = null;

        if (body.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    // 标准格式: {"error": {"message": "...", "code": "...", "type": "..."}}
                    if (error.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        apiMessage = msg.GetString();
                    if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                        apiCode = code.GetString();
                    if (error.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
                        apiCode ??= type.GetString();
                    // 兼容厂商格式: {"error": {"message": "Param Incorrect", "param": "Not supported model xxx"}}
                    // 模型相关的错误信息在 param 字段中
                    if (error.TryGetProperty("param", out var param) && param.ValueKind == JsonValueKind.String)
                        apiParam = param.GetString();
                    // 简单格式: {"error": "message string"}
                    if (error.ValueKind == JsonValueKind.String)
                        apiMessage = error.GetString();
                }
            }
            catch
            {
                // JSON 解析失败，仅依赖状态码
            }
        }

        // 第二步：根据状态码分类
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized => TokenProviderErrorKind.Authentication,            // 401
            HttpStatusCode.Forbidden => ClassifyForbidden(apiCode, apiMessage),              // 403 需细分
            HttpStatusCode.NotFound => TokenProviderErrorKind.NotFound,                     // 404
            HttpStatusCode.TooManyRequests => TokenProviderErrorKind.RateLimited,            // 429
            HttpStatusCode.BadRequest => ClassifyBadRequest(apiCode, apiMessage, apiParam),  // 400 需细分
            HttpStatusCode.UnprocessableEntity => TokenProviderErrorKind.BadRequest,         // 422
            HttpStatusCode.InternalServerError => TokenProviderErrorKind.ServerError,        // 500
            HttpStatusCode.BadGateway => TokenProviderErrorKind.ServerError,                 // 502
            HttpStatusCode.ServiceUnavailable => TokenProviderErrorKind.ServerError,         // 503
            HttpStatusCode.GatewayTimeout => TokenProviderErrorKind.ServerError,             // 504
            _ when (int)statusCode >= 500 => TokenProviderErrorKind.ServerError,
            _ when (int)statusCode >= 400 => TokenProviderErrorKind.BadRequest,
            _ => TokenProviderErrorKind.Unknown
        };

        return (kind, apiCode, apiMessage);
    }

    /// <summary>
    /// 403 Forbidden 细分。
    ///
    /// 403 可能是两种情况：
    ///   1. API Key 无效或过期 -> Authentication
    ///   2. API Key 有效但无权访问指定模型 -> InvalidModel
    ///
    /// 通过响应体关键词判断：
    ///   "no access to model" / "model_not_found" -> InvalidModel
    ///   其他 -> Authentication
    /// </summary>
    private static TokenProviderErrorKind ClassifyForbidden(string? code, string? message)
    {
        if (message is not null &&
            (message.Contains("no access to model", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)))
        {
            return TokenProviderErrorKind.InvalidModel;
        }
        return TokenProviderErrorKind.Authentication;
    }

    /// <summary>
    /// 400 Bad Request 细分。
    ///
    /// 400 可能是两种情况：
    ///   1. 模型名称不存在或不支持 -> InvalidModel
    ///   2. 其他请求参数错误 -> BadRequest
    ///
    /// 判断依据（按优先级）：
    ///   message 中: "Not supported model" (Anthropic 代理)
    ///   message 中: "does not exist" (OpenAI)
    ///   message 中: "model_not_found"
    ///   param  中: "Not supported model" (兼容厂商，错误详情在 param 字段)
    ///   -> InvalidModel
    /// </summary>
    private static TokenProviderErrorKind ClassifyBadRequest(string? code, string? message, string? param)
    {
        // 兼容各厂商的错误信息格式，message 和 param 都检查
        var text = $"{message} {param}";

        if (text.Contains("not supported model", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("model_not_found", StringComparison.OrdinalIgnoreCase))
        {
            return TokenProviderErrorKind.InvalidModel;
        }
        return TokenProviderErrorKind.BadRequest;
    }
}
