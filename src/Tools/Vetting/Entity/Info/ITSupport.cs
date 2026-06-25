using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// IT/技术支持
/// </summary>
public partial class ITSupport : ObservableObject
{
    public int Id { get; set; } = 1;
    [ObservableProperty]
    private string? _teamDemand;

    [ObservableProperty]
    private string? _headcount;

    [ObservableProperty]
    private string? _supportScope;

    [ObservableProperty]
    private string? _selfDeveloped;

    [ObservableProperty]
    private string? _keyFeatures;

    [ObservableProperty]
    private string? _annualInvestment;

    [ObservableProperty]
    private string? _emergencyResponse;
}
