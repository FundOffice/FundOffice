
using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO.Trustee;

/// <summary>
/// ÖÐÐÅ
/// </summary>
public partial class CITISCViewModel : TrusteeViewModelBase<CITICS>
{
    [ObservableProperty]

    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? CustomerAuth { get; set; }

    public string? Token { get; set; }

    public CITISCViewModel()
    {
        CustomerAuth = Assist.CustomerAuth;
    }

    protected override void SaveConfigOverride()
    {
        Assist.CustomerAuth = CustomerAuth;

        Assist.SaveConfig();
    }

    protected override bool CanSaveOverride() => !string.IsNullOrWhiteSpace(CustomerAuth);
}