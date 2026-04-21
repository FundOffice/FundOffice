using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FMO.Trustee;

public partial class CMSViewModel : TrusteeViewModelBase<CMS>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? CompanyId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial int? ServerType { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? LicenceKey { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial string? UserNo { get; set; }


    public byte[]? PFX { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCertificateCommand))]
    public partial string? CertPath { get; set; }

    [ObservableProperty]
    public partial string? CertFile { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCertificateCommand))]
    public partial string? Password { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    public partial bool CertIsValid { get; set; } = false;

    public bool CanCheckCertificate => !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(CertPath) && File.Exists(CertPath);

    public CMSViewModel()
    {
        CompanyId = Assist.CompanyId;
        ServerType = Assist.ServerType;
        UserNo = Assist.UserNo;
        Password = Assist.Password;
        PFX = Assist.PFX;
        LicenceKey = Assist.LicenceKey;
        CertIsValid = PFX is not null && !string.IsNullOrWhiteSpace(Password);
    }

    protected override bool CanSaveOverride()
    {
        return !string.IsNullOrWhiteSpace(CompanyId) && !string.IsNullOrWhiteSpace(UserNo) && ServerType is not null && CertIsValid;
    }

    protected override void SaveConfigOverride()
    {
        Assist.CompanyId = CompanyId?.Trim();
        Assist.LicenceKey = LicenceKey?.Trim();
        Assist.UserNo = UserNo?.Trim(); 
        Assist.Password = Password?.Trim();
        Assist.ServerType = ServerType;
        Assist.PFX = PFX;
        Assist.InitCertificate();
        Assist.SaveConfig();
    }

    [RelayCommand]
    public void ChooseCertificateFile()
    {
        var fd = new OpenFileDialog();
        fd.Filter = "证书|*.PFX";
        var r = fd.ShowDialog();
        if (r switch { true => true, _ => false })
        {
            CertPath = fd.FileName;
            CertFile = fd.SafeFileName;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckCertificate))]
    public void CheckCertificate()
    {
        try
        {
            X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(CertPath!, Password);

            // 获取私钥
            using var rsa = certificate.GetRSAPrivateKey();

            CertIsValid = rsa is not null;
            if (CertIsValid)
            {
                using var sr = new FileStream(CertPath!, FileMode.Open);
                PFX = new byte[sr.Length];
                sr.ReadExactly(PFX, 0, (int)sr.Length);
            }
        }
        catch (Exception e)
        {
            CertIsValid = false;
            Log.Error($"CheckCertificate {e}");
        }
    }
}