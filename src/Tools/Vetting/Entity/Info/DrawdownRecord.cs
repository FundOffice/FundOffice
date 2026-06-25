using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 回撤记录
/// </summary>
public partial class DrawdownRecord : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _productName;

    [ObservableProperty]
    private string? _date;

    [ObservableProperty]
    private string? _amplitude;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private string? _countermeasures;

    [ObservableProperty]
    private string? _recoveryDays;
}
