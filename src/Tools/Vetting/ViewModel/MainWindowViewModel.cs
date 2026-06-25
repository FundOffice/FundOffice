using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vetting.Entity;

namespace Vetting.ViewModel;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] public partial VettingReportViewModel? SelectedVetting { get; set; }
    [ObservableProperty] public partial string? SearchText { get; set; }
    public ObservableCollection<VettingReportViewModel> VettingList { get; } = [];
    public CollectionViewSource VettingView { get; }

    public MainWindowViewModel()
    {
        VettingView = new CollectionViewSource { Source = VettingList };
        VettingView.Filter += (_, e) =>
        {
            if (e.Item is VettingReportViewModel vm && !string.IsNullOrWhiteSpace(SearchText))
                e.Accepted = vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };
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
        var vm = new VettingReportViewModel(new VettingReport(id, "新建尽调", DateTime.Now));
        VettingList.Add(vm);
        SelectedVetting = vm;
    }

    [RelayCommand]
    private void DeleteVetting(VettingReportViewModel vm)
    {
        if (HandyControl.Controls.MessageBox.Show($"确认删除 \"{vm.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            VettingList.Remove(vm);
    }
}
