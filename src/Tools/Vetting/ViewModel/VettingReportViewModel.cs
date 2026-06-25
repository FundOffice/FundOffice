using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vetting.Entity;

namespace Vetting.ViewModel;
public partial class VettingReportViewModel(VettingReport report) : ObservableObject
{
    public VettingReport Report { get; } = report;
    public string Id => Report.Id;
    public string FolderPath => Path.Combine("files", "vetting", Id);
    public DateTime CreateTime => Report.CreateTime;
    [ObservableProperty] public partial string Name { get; set; } = report.Name;
    [ObservableProperty] public partial string NameEdit { get; set; } = report.Name;
    public ObservableCollection<FileInfo> OriginalFiles { get; } = [];

    private FileSystemWatcher? _watcher;

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
            foreach (var f in files) OriginalFiles.Add(f);
        });
    }

    [RelayCommand]
    private void SaveName() => Name = NameEdit;
}
