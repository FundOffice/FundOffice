using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Utilities;

namespace FMO.Disclosure;

public abstract partial class ChannelConfigViewModel : ObservableObject
{
    public abstract string ChannelCode { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }


    [ObservableProperty]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [RelayCommand]
    public void Save()
    {
        Error = "";

        IsAvailable = VerifyOverride();

        using var db = DbHelper.Base();
        db.GetCollection<DisclosureChannelConfig>().Upsert(BuildOverride());

    }

    protected abstract DisclosureChannelConfig BuildOverride();

    protected abstract bool VerifyOverride();
}
