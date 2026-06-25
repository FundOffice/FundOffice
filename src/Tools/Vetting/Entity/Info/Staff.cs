using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 人员信息 (高管/投研/风控/投资经理通用)
/// </summary>
public partial class Staff : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _education;

    [ObservableProperty]
    private string? _profile;

    [ObservableProperty]
    private string? _idNumber;

    [ObservableProperty]
    private string? _years;

    [ObservableProperty]
    private string? _age;

    [ObservableProperty]
    private string? _birthDate;

    [ObservableProperty]
    private string? _undergraduate;

    [ObservableProperty]
    private string? _masters;

    [ObservableProperty]
    private string? _doctoral;

    [ObservableProperty]
    private string? _specialty;

    [ObservableProperty]
    private string? _researchFocus;

    [ObservableProperty]
    private string? _mobilePhone;

    [ObservableProperty]
    private string? _telephone;

    [ObservableProperty]
    private string? _email;

    /// <summary>
    /// 角色: Executive(高管) / Researcher(投研) / RiskCtrl(风控) / PM(投资经理)
    /// </summary>
    [ObservableProperty]
    private string? _role;
}
