using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 证照资质
/// </summary>
public partial class License : ObservableObject
{
    public int Id { get; set; } = 1;


    /// <summary>
    /// 非会员/观察会员/正式会员
    /// </summary>
    [ObservableProperty]
    private string? _fundAssociationMember;

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    private bool _investmentAdvisor;
}
