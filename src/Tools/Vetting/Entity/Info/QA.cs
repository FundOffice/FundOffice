using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 散装问答 (模板中 {{a1}} {{a2}} 等占位符)
/// Source: 0=种子数据, 其他=VettingReport.Id
/// </summary>
public partial class QA : ObservableObject
{
    public int Id { get; set; }

    /// <summary>来源: 0=种子数据, 其他=VettingReport.Id</summary>
    [ObservableProperty]
    private int _source;

    [ObservableProperty]
    private string? _question;

    [ObservableProperty]
    private string? _answer;
}
