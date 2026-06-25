using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 投资流程
/// </summary>
public partial class InvestmentProcess : ObservableObject
{
    public int Id { get; set; } = 1;
    [ObservableProperty]
    private string? _research;

    [ObservableProperty]
    private string? _decision;

    [ObservableProperty]
    private string? _trading;

    [ObservableProperty]
    private string? _evaluation;

    [ObservableProperty]
    private string? _riskControl;

    [ObservableProperty]
    private string? _portfolioAdjust;

    [ObservableProperty]
    private string? _positionBuilding;

    [ObservableProperty]
    private string? _committeeRole;

    [ObservableProperty]
    private string? _researchAuthority;

    [ObservableProperty]
    private string? _systemAndData;

    [ObservableProperty]
    private string? _dataStorage;

    [ObservableProperty]
    private string? _tradingControl;

    [ObservableProperty]
    private string? _tradingErrorFix;

    [ObservableProperty]
    private string? _abnormalTrading;

    [ObservableProperty]
    private string? _accountFairness;
}
