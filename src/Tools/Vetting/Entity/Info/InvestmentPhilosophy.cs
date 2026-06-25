using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 投资理念/策略概述
/// </summary>
public partial class InvestmentPhilosophy : ObservableObject
{
    public int Id { get; set; } = 1;
    [ObservableProperty]
    private string? _target;

    [ObservableProperty]
    private string? _philosophy;

 
}
