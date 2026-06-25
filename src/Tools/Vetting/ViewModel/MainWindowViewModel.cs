using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vetting.Data;
using Vetting.Entity;
using Vetting.View;

namespace Vetting.ViewModel;
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] public partial VettingReportViewModel? SelectedVetting { get; set; }
    [ObservableProperty] public partial string? SearchText { get; set; }
    public ObservableCollection<VettingReportViewModel> VettingList { get; } = [];
    public CollectionViewSource VettingView { get; }
    private readonly VettingDbContext _db = new();

    public MainWindowViewModel()
    {
        VettingView = new CollectionViewSource { Source = VettingList };
        VettingView.Filter += (_, e) =>
        {
            if (e.Item is VettingReportViewModel vm && !string.IsNullOrWhiteSpace(SearchText))
                e.Accepted = vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
        LoadFromDb();
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
}
