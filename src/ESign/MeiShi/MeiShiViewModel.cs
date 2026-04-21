using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Utilities;

namespace FMO.ESigning.MeiShi;





public partial class MeiShiViewModel : ESignViewModelBase
{
    public override string Id =>"meishi";

    public override string Title => "易私募";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Password { get; set; }


    private MeiShiAssit Assit { get; set; }

    public override ISigning Signing => Assit;

    public MeiShiViewModel()
    {
        Assit = new MeiShiAssit();

    }


    public override void Load()
    {
        using var db = DbHelper.Platform();
        MeiShiConfig config = db.GetCollection<ISigningConfig>().FindById("meishi") as MeiShiConfig ?? new MeiShiConfig();
 
        UserName = config.UserName;
        Password = config.Password;
        IsEnable = config.IsEnable;
        IsValid = config.IsValid;
    }


    public override bool CanSaveOverride()
    {
        return !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
    }
     


    public override async Task<bool> VerifyOverride()
    {
        return await Assit.Login();
    }

    protected override ISigningConfig BuildConfig()
    {
        return new MeiShiConfig() { UserName = UserName, Password = Password, IsValid = true };
    }
}