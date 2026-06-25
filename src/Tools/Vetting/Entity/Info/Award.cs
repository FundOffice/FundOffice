using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 奖项
/// </summary>
public partial class Award : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _time;

    [ObservableProperty]
    private string? _entity;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _evaluator;
}
