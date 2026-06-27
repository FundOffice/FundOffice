using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Vetting.Data;
using Vetting.Entity;
using Vetting.View;

namespace Vetting.ViewModel;
public partial class MainWindowViewModel : ObservableObject, IDisposable, IRecipient<AIProviderChanged>
{
    [ObservableProperty] public partial VettingReportViewModel? SelectedVetting { get; set; }
    [ObservableProperty] public partial string? SearchText { get; set; }

    public const string AnswerModeStrict = "精确";
    public const string AnswerModeFull = "完整";
    public string[] AnswerModes { get; } = [AnswerModeStrict, AnswerModeFull];
    [ObservableProperty] public partial string AnswerMode { get; set; } = AnswerModeStrict;

    public const string RunModeStep = "逐步";
    public const string RunModeAuto = "自动";
    public string[] RunModes { get; } = [RunModeStep, RunModeAuto];
    [ObservableProperty] public partial string RunMode { get; set; } = RunModeStep;

    public ObservableCollection<AIProviderItemViewModel> Providers { get; } = [];
    public ObservableCollection<VettingReportViewModel> VettingList { get; } = [];
    public CollectionViewSource VettingView { get; }

    private readonly VettingAppDbContext _db = new();

    public MainWindowViewModel()
    {
        VettingView = new CollectionViewSource { Source = VettingList };
        VettingView.Filter += (_, e) =>
        {
            if (e.Item is VettingReportViewModel vm && !string.IsNullOrWhiteSpace(SearchText))
                e.Accepted = vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
        WeakReferenceMessenger.Default.Register<AIProviderChanged>(this);

        // 从同一 _db 加载，不并发
        var setting = _db.GetSettings();
        AnswerMode = setting.AnswerMode;
        RunMode = setting.RunMode;
        var selectedIds = setting.SelectedProviderIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
        foreach (var config in _db.AIProviderConfigs.FindAll())
        {
            var vm = new AIProviderItemViewModel(config);
            if (selectedIds.Contains(config.Id)) vm.IsSelected = true;
            vm.IsSelectedChanged += () => SaveSelectedProviders();
            Providers.Add(vm);
        }

        foreach (var report in _db.Reports.FindAll())
            VettingList.Add(new VettingReportViewModel(report));
    }

    partial void OnAnswerModeChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var s = _db.GetSettings();
        s.AnswerMode = value;
        _db.AppSettings.Upsert(s);
    }

    partial void OnRunModeChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var s = _db.GetSettings();
        s.RunMode = value;
        _db.AppSettings.Upsert(s);
        WeakReferenceMessenger.Default.Send(new RunModeChanged(value));
    }

    private void SaveSelectedProviders()
    {
        var ids = string.Join(",", Providers.Where(p => p.IsSelected).Select(p => p.Id));
        var s = _db.GetSettings();
        s.SelectedProviderIds = ids;
        _db.AppSettings.Upsert(s);
    }

    partial void OnSearchTextChanged(string? value) => VettingView.View.Refresh();

    partial void OnSelectedVettingChanged(VettingReportViewModel? oldValue, VettingReportViewModel? newValue)
    {
        oldValue?.StopWatching();
        newValue?.StartWatching();
    }

    [RelayCommand]
    private void NewVetting()
    {
        var id = Guid.NewGuid().ToString("N");
        var report = new VettingReport(id, "新建尽调", DateTime.Now);
        _db.Reports.Insert(report);
        var vm = new VettingReportViewModel(report);
        VettingList.Add(vm);
        SelectedVetting = vm;
    }

    [RelayCommand]
    private void DeleteVetting(VettingReportViewModel vm)
    {
        if (HandyControl.Controls.MessageBox.Show($"确认删除 \"{vm.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            vm.StopWatching();
            _db.Reports.Delete(vm.Id);
            if (Directory.Exists(vm.FolderPath)) Directory.Delete(vm.FolderPath, true);
            VettingList.Remove(vm);
        }
    }

    public void Dispose() => _db.Dispose();

    [RelayCommand]
    private void SetAIConfig() => new AIProviderConfigWindow { Owner = Application.Current.MainWindow }.ShowDialog();

    [RelayCommand]
    private void OpenDataCenter() => new DataCenterWindow { Owner = Application.Current.MainWindow, DataContext = new DataCenterViewModel() }.Show();

    public void Receive(AIProviderChanged message)
    {
        switch (message.Type)
        {
            case ChangedType.Add:
                if (_db.AIProviderConfigs.FindById(message.Id) is { } addConfig)
                {
                    var addVm = new AIProviderItemViewModel(addConfig);
                    addVm.IsSelectedChanged += () => SaveSelectedProviders();
                    Providers.Add(addVm);
                }
                break;
            case ChangedType.Update:
                if (_db.AIProviderConfigs.FindById(message.Id) is { } updateConfig)
                {
                    var idx = Providers.IndexOf(Providers.FirstOrDefault(p => p.Id == message.Id)!);
                    if (idx >= 0)
                    {
                        var updateVm = new AIProviderItemViewModel(updateConfig);
                        updateVm.IsSelected = Providers[idx].IsSelected;
                        updateVm.IsSelectedChanged += () => SaveSelectedProviders();
                        Providers[idx] = updateVm;
                    }
                }
                break;
            case ChangedType.Delete:
                if (Providers.FirstOrDefault(p => p.Id == message.Id) is { } toDelete)
                    Providers.Remove(toDelete);
                break;
        }
    }
}
