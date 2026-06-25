using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 资产管理规模 (按年份)
/// </summary>
public partial class AUM : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _year;

    [ObservableProperty]
    private string? _scale;
}
