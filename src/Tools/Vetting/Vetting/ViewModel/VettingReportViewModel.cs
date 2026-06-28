using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class VettingReportViewModel : ObservableObject
{
    public VettingReport Report { get; }
    public string Id => Report.Id;
    public string FolderPath => Path.Combine("files", "vetting", Id);
    public string TplPath => Path.Combine("files", "vetting", Id, "tpl");
    public DateTime CreateTime => Report.CreateTime;
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string NameEdit { get; set; }
    public ObservableCollection<ReportFileViewModel> OriginalFiles { get; } = [];

    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 缓存 ReportFileViewModel，避免切换尽调时丢失 ProviderRunViewModel 状态
    /// Key: "{VettingId}_{FileName}"
    /// </summary>
    private static readonly Dictionary<string, ReportFileViewModel> _fileCache = new();

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
        var files = new DirectoryInfo(FolderPath).GetFiles()
            .Where(f => !f.Name.StartsWith("~$") && !f.Attributes.HasFlag(FileAttributes.Hidden))
            .ToArray();
        // 获取全局参数
        AIProviderItemViewModel[] selectedProviders;
        string answerMode, runMode;
        using (var db = new VettingAppDbContext())
        {
            var setting = db.GetSettings();
            answerMode = setting.AnswerMode;
            runMode = setting.RunMode;
            var selectedIds = setting.SelectedProviderIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
            selectedProviders = db.AIProviderConfigs.FindAll()
                .Where(c => selectedIds.Contains(c.Id))
                .Select(c => new AIProviderItemViewModel(c))
                .ToArray();
        }
        App.Current.Dispatcher.Invoke(() =>
        {
            // 缓存当前的 ReportFileViewModel
            foreach (var vm in OriginalFiles)
            {
                var cacheKey = $"{Id}_{vm.FileName}";
                _fileCache[cacheKey] = vm;
            }

            OriginalFiles.Clear();
            foreach (var f in files)
            {
                var cacheKey = $"{Id}_{f.Name}";
                if (_fileCache.TryGetValue(cacheKey, out var cached))
                {
                    // 从缓存恢复
                    OriginalFiles.Add(cached);
                }
                else
                {
                    // 创建新的
                    OriginalFiles.Add(new ReportFileViewModel(f, Id, selectedProviders, answerMode, runMode));
                }
            }
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
        using var db = new VettingAppDbContext();
        db.Reports.Upsert(new VettingReport(Id, Name, CreateTime));
    }
}
