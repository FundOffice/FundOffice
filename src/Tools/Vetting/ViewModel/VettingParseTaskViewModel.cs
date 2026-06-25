using CommunityToolkit.Mvvm.ComponentModel;
using FundOffice.Copilot.Providers;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Vetting.ViewModel;

public partial class VettingParseTaskViewModel : ObservableObject
{
    public ITokenProvider? Provider { get; set; }
    [ObservableProperty] public partial string TaskName { get; set; } = "";
    [ObservableProperty] public partial TaskStatus Status { get; set; } = TaskStatus.Pending;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(UsageText))] public partial int Usage { get; set; }
    public string UsageText => Usage >= 1000 ? $"{Usage / 1000.0:F1}k tokens" : Usage > 0 ? $"{Usage} tokens" : "";
    [ObservableProperty] public partial string Elapsed { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    public ObservableCollection<string> Output { get; } = [];
    private readonly Stopwatch _sw = new();

    public void Start()
    {
        Status = TaskStatus.Running;
        _sw.Restart();
        // TODO: 启动Elapsed定时刷新
    }

    public void Complete()
    {
        _sw.Stop();
        Status = TaskStatus.Done;
        Elapsed = FormatElapsed(_sw.Elapsed);
    }

    public void Fail(string message)
    {
        _sw.Stop();
        Status = TaskStatus.Error;
        ErrorMessage = message;
        Elapsed = FormatElapsed(_sw.Elapsed);
    }

    private static string FormatElapsed(TimeSpan ts)
        => ts.TotalMinutes >= 1 ? $"{ts.Minutes}m{ts.Seconds:D2}s" : $"{ts.TotalSeconds:F1}s";
}
