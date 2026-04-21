using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace FMO.Trustee;

public partial class XYZQViewModel : TrusteeViewModelBase<XYZQ>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? ClientId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? Password { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? ClientSecret { get; set; }


    public XYZQViewModel()
    {
        this.CloneFrom(Assist);
    }

    protected override bool CanSaveOverride()
    {
        return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    protected override void SaveConfigOverride()
    {
        Assist.CloneFrom(this);
        Assist.SaveConfig();
    }

}