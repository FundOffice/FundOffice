
using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO.Trustee;

/// <summary>
/// ÖÐÐÅ
/// </summary>
public partial class CITICSViewModel : TrusteeViewModelBase<CITICS>
{
    [ObservableProperty]

    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? CustomerAuth { get; set; }

    public string? Token { get; set; }

    public CITICSViewModel()
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