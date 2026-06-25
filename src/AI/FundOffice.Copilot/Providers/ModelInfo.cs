namespace FundOffice.Copilot.Providers;

/// <summary>
/// 模型信息
/// </summary>
public sealed record ModelInfo
{
    /// <summary>模型 ID（如 "gpt-4o"、"claude-sonnet-4-20250514"）</summary>
    public required string Id { get; init; }

    /// <summary>模型所属（如 "openai"、"anthropic"），某些 API 不返回此字段</summary>
    public string? OwnedBy { get; init; }
}
