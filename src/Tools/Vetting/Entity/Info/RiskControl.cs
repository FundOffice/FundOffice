using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 风控体系
/// </summary>
public partial class RiskControl : ObservableObject
{
    public int Id { get; set; } = 1;
    [ObservableProperty]
    private string? _systemIntro;

    [ObservableProperty]
    private string? _decisionMechanism;

    [ObservableProperty]
    private string? _riskMgmtCommittee;

    [ObservableProperty]
    private string? _drawdownControl;

    [ObservableProperty]
    private string? _systemicRiskResponse;

    [ObservableProperty]
    private string? _tradingMonitoring;

    [ObservableProperty]
    private string? _riskMeasures;

    [ObservableProperty]
    private string? _manualVsSystem;

    [ObservableProperty]
    private string? _riskMeasurement;

    [ObservableProperty]
    private string? _maxDrawdownTolerance;

    [ObservableProperty]
    private string? _tailRisk;

    [ObservableProperty]
    private string? _riskReserve;

    [ObservableProperty]
    private string? _liquidityMgmt;

    [ObservableProperty]
    private string? _insiderTradingPrevention;

    [ObservableProperty]
    private string? _employeeTradingMonitor;

    [ObservableProperty]
    private string? _productFairness;
}
