using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace FMO.Schedule;

[MissionInfo("邮件缓存")]
public partial class MailCacheViewModel : MissionViewModel<MailCacheMission>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial string? MailName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial string? MailPassword { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    public partial string? MailPop3 { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    public partial int MailPort { get; set; } = 995;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial bool IsServerAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsAccountVerified { get; set; }

    [ObservableProperty]
    public partial int Interval { get; set; }

    [ObservableProperty]
    public partial string? _log { get; set; }


    public override bool IsAvailable => IsAccountVerified && IsServerAvailable;
     

    public MailCacheViewModel(MailCacheMission m) : base(m)
    {
        Title = "邮件缓存";

        IsAccountVerified = m.IsAccountVerified;

        MailName = m.MailName;
        MailPassword = m.MailPassword;
        MailPop3 = m.MailPop3;
        MailPort = m.MailPort;
        Interval = m.Interval;

        _initialized = true;
        _ = InitializeServerCheckAsync();
    }


    partial void OnMailPop3Changed(string? value)
    {
        if (!_initialized) return;

        IsServerAvailable = CheckPop3();
         
    }



    public bool CheckPop3()
    {
        if (MailPop3 is null || !Regex.IsMatch(MailPop3, @"(\w+\.)+\w+"))
            return false;

        if (!NetworkInterface.GetIsNetworkAvailable())
            return false;


        try
        {
            Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(MailPop3.Trim());
            return reply.Status == IPStatus.Success;
        }
        catch { return false; }
    }


    public async Task<bool> CheckPop3Async()
    {
        if (string.IsNullOrWhiteSpace(MailPop3) || !Regex.IsMatch(MailPop3, @"(\w+\.)+\w+"))
            return false;

        if (!NetworkInterface.GetIsNetworkAvailable())
            return false;

        try
        {
            using var ping = new Ping();
            // 设置 2 秒超时，避免长时间阻塞
            var reply = await ping.SendPingAsync(MailPop3.Trim(), 2000);
            return reply.Status == IPStatus.Success;
        }
        catch { return false; }
    }
     
    private async Task InitializeServerCheckAsync()
    {
        IsServerAvailable = await CheckPop3Async();
    }

    bool CanVerify() => IsServerAvailable && !string.IsNullOrWhiteSpace(MailName) && !string.IsNullOrWhiteSpace(MailPassword);

    [RelayCommand(CanExecute = nameof(CanVerify))]
    public async Task VerifyAccount()
    {
        IsAccountVerified = await Verify();

        if (IsAccountVerified)
        {
            Mission.MailName = MailName?.Trim();
            Mission.MailPassword = MailPassword?.Trim();
            Mission.IsAccountVerified = true;
            MissionSchedule.SaveChanges(Mission);
        }
    }

    async Task<bool> Verify()
    {
        try
        {
            var pop3Client = new MailKit.Net.Pop3.Pop3Client();
            await pop3Client.ConnectAsync(MailPop3!, MailPort, true);

            if (!pop3Client.IsConnected)
                return false;

            await pop3Client.AuthenticateAsync(MailName!, MailPassword!);

            return pop3Client.IsAuthenticated;
        }
        catch
        {
            return false;
        }
    }


    [RelayCommand]
    public void RebuildData()
    {
        Task.Run(async () =>
        {
            Mission.IgnoreCache = true;
            await Mission.Work();
            Mission.IgnoreCache = false;
        });
    }

    internal void Receive(MissionMailCredentialMessage message)
    {
        if (message.Id != Id) return;

        if (!message.IsSuccessed)
            IsAccountVerified = false;
    }
}