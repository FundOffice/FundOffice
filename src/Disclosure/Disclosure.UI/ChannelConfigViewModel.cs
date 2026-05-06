using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Utilities;
using System.Windows;

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
    public async Task Save(Window window)
    {
        Error = "";

        IsAvailable = await VerifyOverride();

        using var db = DbHelper.Base();
        db.GetCollection<DisclosureChannelConfig>().Upsert(BuildOverride());

        if (IsAvailable)
            window.Close();
    }

    protected abstract DisclosureChannelConfig BuildOverride();

    protected abstract Task<bool> VerifyOverride();
}
