using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO.Trustee;

/// <summary>
/// 中信建投
/// </summary>
public partial class CSCViewModel : TrusteeViewModelBase<CSC>
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? APIKey { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? APISecret { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? EncryptKey { get; set; }


    public CSCViewModel()
    {
        APIKey = Assist.APIKey;
        APISecret = Assist.APISecret;
        EncryptKey = Assist.EncryptKey;

    }

    protected override void SaveConfigOverride()
    {
        Assist.APIKey = APIKey;
        Assist.APISecret = APISecret;
        Assist.EncryptKey = EncryptKey;
        Assist.SaveConfig();
    }

    protected override bool CanSaveOverride()
    {
        return !string.IsNullOrWhiteSpace(APIKey) && !string.IsNullOrWhiteSpace(APISecret) && !string.IsNullOrWhiteSpace(EncryptKey);
    }
}