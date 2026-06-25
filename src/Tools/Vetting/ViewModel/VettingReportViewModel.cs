using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class VettingReportViewModel : ObservableObject
{
    public VettingReport Report { get; }
    public string Id => Report.Id;
    public string FolderPath => Path.Combine("files", "vetting", Id);
    public DateTime CreateTime => Report.CreateTime;
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string NameEdit { get; set; }
    public ObservableCollection<ReportFileViewModel> OriginalFiles { get; } = [];

    private FileSystemWatcher? _watcher;

    public VettingReportViewModel(VettingReport report)
    {
        Report = report;
        Name = report.Name;
        NameEdit = report.Name;
    }

    public void StartWatching()
    {
        Directory.CreateDirectory(FolderPath);
        ReloadFiles();
        _watcher = new FileSystemWatcher(FolderPath) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size };
        _watcher.Created += (_, _) => ReloadFiles();
        _watcher.Deleted += (_, _) => ReloadFiles();
        _watcher.Renamed += (_, _) => ReloadFiles();
        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher is { } w) { w.EnableRaisingEvents = false; w.Dispose(); _watcher = null; }
    }

    private void ReloadFiles()
    {
        var files = new DirectoryInfo(FolderPath).GetFiles();
        App.Current.Dispatcher.Invoke(() =>
        {
            OriginalFiles.Clear();
            foreach (var f in files) OriginalFiles.Add(new ReportFileViewModel(f));
        });
    }

    [RelayCommand]
    private void SaveName()
    {
        Name = NameEdit;
        SaveToDb();
    }

    public void SaveToDb()
    {
        using var db = new VettingDbContext();
        db.Reports.Upsert(new VettingReport(Id, Name, CreateTime));
    }
}

public partial class ReportFileViewModel : ObservableObject, IRecipient<AIProviderChanged>
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }



    [ObservableProperty] public partial ObservableCollection<VettingParseTaskViewModel> Tasks { get; set; } = [];


    public ObservableCollection<AIProviderItemViewModel> Providers { get; } = [];

    [SetsRequiredMembers]
    public ReportFileViewModel(FileInfo fileInfo)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        using var db = new VettingDbContext();
        foreach (var config in db.AIProviderConfigs.FindAll())
            Providers.Add(new AIProviderItemViewModel(config));
    }

    [RelayCommand]
    private async Task GenerateTemplatesAsync()
    {
        var sel = Providers.Where(x => x.IsSelected).ToArray();

        if (sel.Length == 0) return;

        IsExpanded = true;

        Tasks = [.. sel.Select(provider => new VettingParseTaskViewModel { TaskName = provider.Name, Provider = CreateProvider(provider) })];

        var tasks = Tasks.Select(RunSingleTaskAsync).ToArray();

        await Task.WhenAll(tasks);
    }

    private static async Task RunSingleTaskAsync(VettingParseTaskViewModel task)
    {
        task.Start();
        try
        {
            // TODO: 实际工作函数等会再写
            await Task.Delay(100000);
            task.Complete();
        }
        catch (Exception ex) { task.Fail(ex.Message); }
    }

    private static FundOffice.Copilot.Providers.ITokenProvider CreateProvider(AIProviderItemViewModel vm) => vm.ProviderType switch
    {
        "Anthropic" => new FundOffice.Copilot.Providers.AnthropicTokenProvider(
            new FundOffice.Copilot.Configuration.AnthropicOptions { ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl }),
        _ => new FundOffice.Copilot.Providers.OpenAITokenProvider(
            new FundOffice.Copilot.Configuration.OpenAIOptions { ApiKey = vm.ApiKey, BaseUrl = vm.BaseUrl }),
    };

    public void Receive(AIProviderChanged message)
    {
        switch (message.Type)
        {
            case ChangedType.Add:
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                        Providers.Add(new AIProviderItemViewModel(config));
                }
                break;
            case ChangedType.Update:
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                    {
                        var idx = Providers.IndexOf(Providers.FirstOrDefault(p => p.Id == message.Id)!);
                        if (idx >= 0) Providers[idx] = new AIProviderItemViewModel(config);
                    }
                }
                break;
            case ChangedType.Delete:
                if (Providers.FirstOrDefault(p => p.Id == message.Id) is { } toDelete)
                {
                    Providers.Remove(toDelete);
                }
                break;
            default:
                break;
        }

    }
}
