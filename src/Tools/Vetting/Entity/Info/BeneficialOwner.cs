using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 实控人/穿透股东
/// </summary>
public partial class BeneficialOwner : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _penetration;

    [ObservableProperty]
    private string? _intro;
}
