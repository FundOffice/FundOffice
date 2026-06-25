using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 投资策略
/// </summary>
public partial class Strategy : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _manager;

    [ObservableProperty]
    private string? _scale;

    [ObservableProperty]
    private string? _type;

    [ObservableProperty]
    private string? _capacity;

    [ObservableProperty]
    private string? _sameStrategyCount;

    [ObservableProperty]
    private string? _factorPool;

    [ObservableProperty]
    private string? _capacityAndRisk;

    [ObservableProperty]
    private string? _replicated;

    [ObservableProperty]
    private string? _styleExposure;

    [ObservableProperty]
    private string? _turnover;

    [ObservableProperty]
    private string? _holdingPeriod;

    [ObservableProperty]
    private string? _weightAllocation;

    [ObservableProperty]
    private string? _warningStoploss;

}
