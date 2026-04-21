using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;

namespace FMO.Trustee;

public abstract partial class TrusteeViewModelBase : ObservableObject, IRecipient<TrusteeStatus>
{
    protected TrusteeViewModelBase()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public string Idenitifier { get; set; } = "";

    public string? Title { get; protected set; }

    [ObservableProperty]
    public partial bool IsAvaliable { get; set; }


    [ObservableProperty]
    public partial bool ShowConfigSetting { get; set; }



    public bool CanSave
    {
        get
        {
            try { return CanSaveOverride(); } catch { return false; }
        }
    }


    protected abstract bool CanSaveOverride();

    protected abstract void SaveConfigOverride();




    public void Receive(TrusteeStatus message)
    {
        if (message.Id == Idenitifier)
            IsAvaliable = message.Status;
    }
}

public record TrusteeStatus(string Id, bool Status);

public interface ITrusteeViewModel
{
    ITrustee Assist { get; }
}

public abstract partial class TrusteeViewModelBase<T> : TrusteeViewModelBase, ITrusteeViewModel where T : TrusteeApiBase, ITrustee, new()
{
    protected TrusteeViewModelBase()
    {
        Assist = new T();
        Assist.LoadConfig();
        IsAvaliable = Assist.IsValid;

        Idenitifier = Assist.Identifier;
        Title = Assist.Title;
    }


    public T Assist { get; }

    ITrustee ITrusteeViewModel.Assist => Assist;


    [RelayCommand(CanExecute = nameof(CanSave))]
    public void SaveConfig()
    {
        SaveConfigOverride();
        ShowConfigSetting = false;

        Task.Run(async () => { var r = await Assist.VerifyConfig(); WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"{Assist.Title}配置校验{(r ? "成功" : "失败")}")); });
    }
}