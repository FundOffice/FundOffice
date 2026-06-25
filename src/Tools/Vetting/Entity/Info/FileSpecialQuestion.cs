using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 文件专属问题 — 模板中 {{a1}} {{a2}} 等散装占位符
/// 每个问题关联到具体文件(FileHash)和顺序(Index)
/// </summary>
public partial class FileSpecialQuestion : ObservableObject
{
    public int Id { get; set; }

    /// <summary>文件哈希，关联到具体模板文件</summary>
    [ObservableProperty]
    private string? _fileHash;

    /// <summary>占位符序号 (对应 {{a1}} 的 1)</summary>
    [ObservableProperty]
    private int _index;

    /// <summary>问题描述</summary>
    [ObservableProperty]
    private string? _question;

    /// <summary>AI 回答</summary>
    [ObservableProperty]
    private string? _answer;
}
