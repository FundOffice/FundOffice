using System.Net;

namespace FundOffice.Copilot.Providers;

/// <summary>
/// API 错误类型
/// </summary>
public enum TokenProviderErrorKind
{
    /// <summary>未知错误</summary>
    Unknown,

    /// <summary>API Key 无效或无权限</summary>
    Authentication,

    /// <summary>模型名称不存在或无权限访问</summary>
    InvalidModel,

    /// <summary>请求频率超限 (429)</summary>
    RateLimited,

    /// <summary>请求参数错误 (400)</summary>
    BadRequest,

    /// <summary>资源不存在 (404)</summary>
    NotFound,

    /// <summary>服务端错误 (5xx)</summary>
    ServerError,

    /// <summary>网络连接失败</summary>
    NetworkError,

    /// <summary>请求体或响应体 JSON 解析失败</summary>
    JsonError,

    /// <summary>内容被安全过滤</summary>
    ContentFiltered
}

/// <summary>
/// 统一的 TokenProvider 异常，包含结构化的错误信息
/// </summary>
public sealed class TokenProviderException : Exception
{
    /// <summary>结构化错误类型</summary>
    public TokenProviderErrorKind Kind { get; }

    /// <summary>HTTP 状态码（网络错误时为 null）</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>API 返回的原始错误响应体</summary>
    public string? ResponseBody { get; }

    /// <summary>API 返回的错误 code（如 "invalid_api_key", "model_not_found"）</summary>
    public string? ErrorCode { get; }

    /// <summary>是否值得重试（限流、服务端错误）</summary>
    public bool IsRetryable => Kind is TokenProviderErrorKind.RateLimited or TokenProviderErrorKind.ServerError;

    public TokenProviderException(
        TokenProviderErrorKind kind,
        string message,
        HttpStatusCode? statusCode = null,
        string? responseBody = null,
        string? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ErrorCode = errorCode;
    }
}
