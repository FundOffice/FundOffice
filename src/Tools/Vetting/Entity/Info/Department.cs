using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 部门信息
/// </summary>
public partial class Department : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _headcount;

    [ObservableProperty]
    private string? _mainFunction;

    [ObservableProperty]
    private string? _head;

    [ObservableProperty]
    private string? _recruitmentPlan;

    [ObservableProperty]
    private string? _hasPartTime;
}
