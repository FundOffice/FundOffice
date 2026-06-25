using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 股东信息
/// </summary>
public partial class Shareholder : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _ratio;

    [ObservableProperty]
    private string? _intro;

    [ObservableProperty]
    private string? _nature;

    [ObservableProperty]
    private string? _paidInAmount;

    [ObservableProperty]
    private string? _identityBrief;

    [ObservableProperty]
    private string? _companyRole;

    [ObservableProperty]
    private string? _isCoreResearch;

    [ObservableProperty]
    private string? _companyPosition;
}
