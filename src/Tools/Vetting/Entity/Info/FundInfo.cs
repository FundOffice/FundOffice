using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 基金/产品信息 (合并产品要素 + 运作指标 + 业绩列表项)
/// </summary>
public partial class FundInfo : ObservableObject
{
    public int Id { get; set; }

    /// <summary>是否推荐</summary>
    [ObservableProperty]
    private bool _recommend;

    // ═══════════════════════════════════════════════
    // 产品要素
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private string? _duration;

    [ObservableProperty]
    private string? _type;

    [ObservableProperty]
    private string? _minSubscription;

    [ObservableProperty]
    private string? _frequency;

    [ObservableProperty]
    private string? _custodian;

    [ObservableProperty]
    private string? _riskLevel;

    [ObservableProperty]
    private string? _buySellFee;

    [ObservableProperty]
    private string? _mgmtFee;

    [ObservableProperty]
    private string? _custodyFee;

    [ObservableProperty]
    private string? _scope;

    [ObservableProperty]
    private string? _restriction;

    [ObservableProperty]
    private string? _warningStoploss;

    [ObservableProperty]
    private string? _performanceFee;

    [ObservableProperty]
    private string? _dividend;

    [ObservableProperty]
    private string? _other;

    [ObservableProperty]
    private string? _establishmentDate;

    [ObservableProperty]
    private string? _lockupPeriod;

    [ObservableProperty]
    private string? _openingDay;

    [ObservableProperty]
    private string? _filingOrRegistration;

    // ═══════════════════════════════════════════════
    // 运作指标 (原 ProductPerformance)
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private string? _strategyType;

    [ObservableProperty]
    private string? _navDate;

    [ObservableProperty]
    private string? _scale;

    [ObservableProperty]
    private string? _issueScale;

    [ObservableProperty]
    private string? _currentScale;

    [ObservableProperty]
    private string? _unitNav;

    [ObservableProperty]
    private string? _cumulativeNav;

    [ObservableProperty]
    private string? _annualReturn;

    [ObservableProperty]
    private string? _maxDrawdown;

    [ObservableProperty]
    private string? _volatility;

    [ObservableProperty]
    private string? _sharpe;

    [ObservableProperty]
    private string? _calmar;

    [ObservableProperty]
    private string? _cumulativeReturn;

    [ObservableProperty]
    private string? _return6M;

    [ObservableProperty]
    private string? _return1Y;

    [ObservableProperty]
    private string? _return1M;
}
