using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Vetting.Data;
using Vetting.Entity;
using Vetting.View;

namespace Vetting.ViewModel;
public partial class MainWindowViewModel : ObservableObject, IDisposable, IRecipient<AIProviderChanged>
{
    [ObservableProperty] public partial VettingReportViewModel? SelectedVetting { get; set; }
    [ObservableProperty] public partial string? SearchText { get; set; }
    public ObservableCollection<VettingReportViewModel> VettingList { get; } = [];
    public CollectionViewSource VettingView { get; }

    public static ObservableCollection<AIProviderItemViewModel> GlobalProviders { get; } = [];

    private readonly VettingDbContext _db = new();

    public MainWindowViewModel()
    {
        VettingView = new CollectionViewSource { Source = VettingList };
        VettingView.Filter += (_, e) =>
        {
            if (e.Item is VettingReportViewModel vm && !string.IsNullOrWhiteSpace(SearchText))
                e.Accepted = vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
        WeakReferenceMessenger.Default.Register<AIProviderChanged>(this);
        LoadProviders();
        LoadFromDb();
    }

    private void LoadProviders()
    {
        var selected = GlobalProviders.Where(p => p.IsSelected).Select(p => p.Id).ToHashSet();
        GlobalProviders.Clear();
        foreach (var config in _db.AIProviderConfigs.FindAll())
        {
            var vm = new AIProviderItemViewModel(config);
            if (selected.Contains(config.Id)) vm.IsSelected = true;
            GlobalProviders.Add(vm);
        }
    }

    private void LoadFromDb()
    {
        foreach (var report in _db.Reports.FindAll())
        {
            var vm = CreateVm(report);
            VettingList.Add(vm);
        }
    }

    private VettingReportViewModel CreateVm(VettingReport report) => new(report);

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
        var vm = CreateVm(report);
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
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                        GlobalProviders.Add(new AIProviderItemViewModel(config));
                }
                break;
            case ChangedType.Update:
                using (var db = new VettingDbContext())
                {
                    if (db.AIProviderConfigs.FindById(message.Id) is AIProviderConfig config)
                    {
                        var idx = GlobalProviders.IndexOf(GlobalProviders.FirstOrDefault(p => p.Id == message.Id)!);
                        if (idx >= 0) GlobalProviders[idx] = new AIProviderItemViewModel(config);
                    }
                }
                break;
            case ChangedType.Delete:
                if (GlobalProviders.FirstOrDefault(p => p.Id == message.Id) is { } toDelete)
                {
                    GlobalProviders.Remove(toDelete);
                }
                break;
            default:
                break;
        }
    }
}
