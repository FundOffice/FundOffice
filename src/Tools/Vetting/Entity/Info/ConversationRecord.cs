namespace Vetting.Models.Entities;

/// <summary>
/// AI 会话记录 — 保存到数据库，支持断点续传
/// </summary>
public class ConversationRecord
{
    public int Id { get; set; }

    /// <summary>关联文件路径</summary>
    public string? SourcePath { get; set; }

    /// <summary>文件 hash</summary>
    public string? FileHash { get; set; }

    /// <summary>输出模板路径</summary>
    public string? OutputPath { get; set; }

    /// <summary>会话消息 (JSON 序列化的 ChatMessage 列表)</summary>
    public string? MessagesJson { get; set; }

    /// <summary>已完成的轮数</summary>
    public int CompletedTurns { get; set; }

    /// <summary>累计输入 token</summary>
    public int TotalInput { get; set; }

    /// <summary>累计输出 token</summary>
    public int TotalOutput { get; set; }

    /// <summary>状态: running/completed/failed</summary>
    public string? Status { get; set; }

    /// <summary>已生成的占位符 (JSON)</summary>
    public string? PlaceholdersJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
