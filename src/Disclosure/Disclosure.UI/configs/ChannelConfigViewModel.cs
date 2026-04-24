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


public class ChannelConfigFactory
{
    public static ChannelConfigViewModel? CreateConfig(string channelCode)
    {
        return channelCode switch
        {
            DisclosureChannelCode.Email => new EmailChannelConfigViewModel(),
            DisclosureChannelCode.AMAC => new AMACChannelConfigViewModel(),
            DisclosureChannelCode.Pfid => new PfidChannelConfigViewModel(),
            DisclosureChannelCode.MeiShi => new MeiShiChannelConfigViewModel(),
            _ => null
        };
    }

    internal static ChannelConfigViewModel? CreateConfig(IDisclosureChannelConfig config)
    {
        return config switch
        {
            // DisclosureChannelCode.Email => new EmailChannelConfig(config),
            EmailChannelConfig c => new EmailChannelConfigViewModel(c),
            AMACChannelConfig c => new AMACChannelConfigViewModel(c),
            PfidChannelConfig c => new PfidChannelConfigViewModel(c),
            MeiShiChannelConfig c=> new MeiShiChannelConfigViewModel(c),
            _ => null
        };
    }
}