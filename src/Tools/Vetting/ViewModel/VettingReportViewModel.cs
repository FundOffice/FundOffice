using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
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
    public ObservableCollection<TemplateFileViewModel> TemplateFiles { get; } = [];

    private FileSystemWatcher? _watcher;
    private FileSystemWatcher? _tplWatcher;

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

        Directory.CreateDirectory(TplPath);
        ReloadTemplateFiles();
        _tplWatcher = new FileSystemWatcher(TplPath) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size };
        _tplWatcher.Created += (_, _) => ReloadTemplateFiles();
        _tplWatcher.Deleted += (_, _) => ReloadTemplateFiles();
        _tplWatcher.Renamed += (_, _) => ReloadTemplateFiles();
        _tplWatcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher is { } w) { w.EnableRaisingEvents = false; w.Dispose(); _watcher = null; }
        if (_tplWatcher is { } tw) { tw.EnableRaisingEvents = false; tw.Dispose(); _tplWatcher = null; }
    }

    private void ReloadFiles()
    {
        var files = new DirectoryInfo(FolderPath).GetFiles()
            .Where(f => !f.Name.StartsWith("~$") && !f.Attributes.HasFlag(FileAttributes.Hidden))
            .ToArray();
        App.Current.Dispatcher.Invoke(() =>
        {
            OriginalFiles.Clear();
            foreach (var f in files) OriginalFiles.Add(new ReportFileViewModel(f, Id));
        });
    }

    private void ReloadTemplateFiles()
    {
        if (!Directory.Exists(TplPath)) return;
        var files = new DirectoryInfo(TplPath).GetFiles()
            .Where(f => !f.Name.StartsWith("~$")
                && !f.Attributes.HasFlag(FileAttributes.Hidden)
                && !f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        App.Current.Dispatcher.Invoke(() =>
        {
            TemplateFiles.Clear();
            foreach (var f in files) TemplateFiles.Add(new TemplateFileViewModel(f, Id));
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
