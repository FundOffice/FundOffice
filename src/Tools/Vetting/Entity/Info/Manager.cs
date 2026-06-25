using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 管理人基本信息 (单值, collection 中仅一条)
/// </summary>
public partial class Manager : ObservableObject
{
    /// <summary>LiteDB 主键, 固定为 "default"</summary>
    public int Id { get; set; } = 1;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _registerNo;

    [ObservableProperty]
    private string? _artificialPerson;

    [ObservableProperty]
    private string? _registerCapital;

    [ObservableProperty]
    private string? _realCapital;

    [ObservableProperty]
    private string? _setupDate;

    [ObservableProperty]
    private string? _businessScope;

    [ObservableProperty]
    private string? _registerAddress;

    [ObservableProperty]
    private string? _officeAddress;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _telephone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _fax;

    [ObservableProperty]
    private string? _englishName;

    [ObservableProperty]
    private string? _webSite;

    [ObservableProperty]
    private string? _amacId;

    [ObservableProperty]
    private string? _memberType;

    [ObservableProperty]
    private string? _institutionType;

    [ObservableProperty]
    private string? _relatedCompany;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _historicalEvolution;

    [ObservableProperty]
    private string? _orgStructureIntro;

    [ObservableProperty]
    private string? _futureStrategicPlan;

    [ObservableProperty]
    private string? _governingSecuritiesBureau;

    [ObservableProperty]
    private string? _actualController;

    [ObservableProperty]
    private string? _contactName;

    [ObservableProperty]
    private string? _contactPhoneAndEmail;


}
