using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 财务报表 (按年份)
/// </summary>
public partial class FinancialStatement : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _year;

    [ObservableProperty]
    private string? _totalAssets;

    [ObservableProperty]
    private string? _totalLiabilities;

    [ObservableProperty]
    private string? _ownersEquity;

    [ObservableProperty]
    private string? _revenue;

    [ObservableProperty]
    private string? _cost;

    [ObservableProperty]
    private string? _netProfit;
}
