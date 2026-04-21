using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;

namespace FMO.ESigning;

public abstract partial class ESignViewModelBase : ObservableObject, IRecipient<ESigningStatus>
{
    public abstract string Id { get; }

    public abstract ISigning Signing { get; }

    //protected ISigningConfig Config { get; set; }

    public abstract string Title { get; }

    [ObservableProperty]
    public partial bool? IsEnable { get; set; }



    [ObservableProperty]
    public partial bool IsValid { get; set; }



    [ObservableProperty]
    public partial bool ShowConfigSetting { get; set; }

    public bool CanSave => CanSaveOverride();

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task Save()
    {
        var config = BuildConfig();
        config.IsEnable = IsEnable ?? false;
        config.IsValid = await VerifyOverride();
        IsValid = config.IsValid;

        using var db = DbHelper.Platform();
        db.GetCollection<ISigningConfig>().Upsert(config);
        Signing.OnConfig(config);

        ShowConfigSetting = false;

        WeakReferenceMessenger.Default.Send(new ESigningStatus(Id, IsValid));
        WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"{Title}配置校验{(config.IsValid ? "成功" : "失败")}"));
    }

    protected abstract ISigningConfig BuildConfig();


    public abstract bool CanSaveOverride();

    public abstract Task<bool> VerifyOverride();


    public abstract void Load();

    partial void OnIsEnableChanged(bool? oldValue, bool? newValue)
    { 
        if (oldValue is null || newValue is null) return;

        using var db = DbHelper.Platform();
        var config = db.GetCollection<ISigningConfig>().FindById(Id) ?? BuildConfig();
        config.IsEnable = newValue.Value;

        db.GetCollection<ISigningConfig>().Upsert(config);
        Signing.OnConfig(config);
    }

    public void Receive(ESigningStatus message)
    {
        if (message.Id == Id)
            IsValid = message.IsValid;
    }
}