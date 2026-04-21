using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace FMO.Schedule;

public partial class GatherDailyFromMailViewModel : MissionViewModel<GatherDailyFromMailMission>
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial string? MailName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial string? MailPassword { get; set; }

    [ObservableProperty]
    public partial string? MailPop3 { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyAccountCommand))]
    public partial bool IsServerAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsAccountVerified { get; set; }

    [ObservableProperty]
    public partial int Interval { get; set; }

    [ObservableProperty]
    public partial string? _log { get; set; }


    public GatherDailyFromMailViewModel(GatherDailyFromMailMission m) : base(m)
    {
        Title = "净值更新";
        IsServerAvailable = CheckPop3();
        IsAccountVerified = m.IsAccountVerified;

        MailName = m.MailName;
        MailPassword = m.MailPassword;
        MailPop3 = m.MailPop3;
        Interval = m.Interval;
    }

    partial void OnMailPop3Changed(string? value)
    {
        IsServerAvailable = CheckPop3();

        if (value?.Trim() != Mission.MailPop3)
        {
            Mission.MailPop3 = value?.Trim();
            MissionSchedule.SaveChanges(Mission);
        }
    }

    partial void OnIntervalChanged(int value)
    {
        if (value != Mission.Interval)
        {
            Mission.Interval = value;
            MissionSchedule.SaveChanges(Mission);
        }
    }

    partial void OnMailNameChanged(string? value)
    {
        if (value?.Trim() != Mission.MailName)
        {
            Mission.MailName = value?.Trim();
            MissionSchedule.SaveChanges(Mission);
        }
    }


    partial void OnMailPasswordChanged(string? value)
    {
        if (value?.Trim() != Mission.MailPassword)
        {
            Mission.MailPassword = value?.Trim();
            MissionSchedule.SaveChanges(Mission);
        }
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

    bool CanVerify() => IsServerAvailable && !string.IsNullOrWhiteSpace(MailName) && !string.IsNullOrWhiteSpace(MailPassword);

    [RelayCommand(CanExecute = nameof(CanVerify))]
    public void VerifyAccount()
    {
        IsAccountVerified = Verify();

        if (IsAccountVerified)
        {
            Mission.MailName = MailName?.Trim();
            Mission.MailPassword = MailPassword?.Trim();
            Mission.IsAccountVerified = true;
            MissionSchedule.SaveChanges(Mission);
        }
    }

    bool Verify()
    {
        try
        {
            var pop3Client = new MailKit.Net.Pop3.Pop3Client();
            pop3Client.Connect(MailPop3, 995, true);

            if (!pop3Client.IsConnected)
                return false;

            pop3Client.Authenticate(MailName, MailPassword);

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
        Task.Run(() =>
        {
            Mission.IgnoreCache = true;
            Mission.Work();
            Mission.IgnoreCache = false;
        });
    }

    public void Receive(MissionMailCredentialMessage message)
    {
        if (message.Id != Id) return;

        if (!message.IsSuccessed)
            IsAccountVerified = false;
    }
     
}
