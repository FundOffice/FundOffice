using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 诚信合规情况
/// </summary>
public partial class CreditStanding : ObservableObject
{
    public int Id { get; set; } = 1;
    [ObservableProperty]
    private string? _adminPenalty;

    [ObservableProperty]
    private string? _businessException;

    [ObservableProperty]
    private string? _seriousIllegal;

    [ObservableProperty]
    private string? _executionInfo;

    [ObservableProperty]
    private string? _securitiesDishonesty;

    [ObservableProperty]
    private string? _corePersonDishonesty;

    [ObservableProperty]
    private string? _fundAssocCreditReport;

    [ObservableProperty]
    private string? _aICQuery;

    [ObservableProperty]
    private string? _cSRCQuery;

    [ObservableProperty]
    private string? _associationQuery;

    [ObservableProperty]
    private string? _judicialQuery;

    [ObservableProperty]
    private string? _regPenalty3Y;

    [ObservableProperty]
    private string? _adminPenalty3Y;

    [ObservableProperty]
    private string? _moneyLaundering5Y;

    [ObservableProperty]
    private string? _falseMaterials3Y;

    [ObservableProperty]
    private string? _majorChange;

    [ObservableProperty]
    private string? _majorOperationalRisk;

    [ObservableProperty]
    private string? _pendingInvestigation;

    [ObservableProperty]
    private string? _negativeReports;

    [ObservableProperty]
    private string? _execViolation;

    [ObservableProperty]
    private string? _otherNegative;

    [ObservableProperty]
    private string? _antiMoneyLaundering;
}
