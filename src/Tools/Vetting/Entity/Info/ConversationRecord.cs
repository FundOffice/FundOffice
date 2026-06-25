using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// AI 会话记录 — 保存到数据库，支持断点续传
/// </summary>
public partial class ConversationRecord : ObservableObject
{
    public int Id { get; set; }

    /// <summary>关联文件路径</summary>
    [ObservableProperty]
    private string? _sourcePath;

    /// <summary>文件 hash</summary>
    [ObservableProperty]
    private string? _fileHash;

    /// <summary>输出模板路径</summary>
    [ObservableProperty]
    private string? _outputPath;

    /// <summary>会话消息 (JSON 序列化的 ChatMessage 列表)</summary>
    [ObservableProperty]
    private string? _messagesJson;

    /// <summary>已完成的轮数</summary>
    [ObservableProperty]
    private int _completedTurns;

    /// <summary>累计输入 token</summary>
    [ObservableProperty]
    private int _totalInput;

    /// <summary>累计输出 token</summary>
    [ObservableProperty]
    private int _totalOutput;

    /// <summary>状态: running/completed/failed</summary>
    [ObservableProperty]
    private string? _status;

    /// <summary>已生成的占位符 (JSON)</summary>
    [ObservableProperty]
    private string? _placeholdersJson;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private DateTime _updatedAt;
}
