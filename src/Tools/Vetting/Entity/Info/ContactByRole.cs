using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 按职能联系人
/// </summary>
public partial class ContactByRole : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty]
    private string? _role;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phone;
}
