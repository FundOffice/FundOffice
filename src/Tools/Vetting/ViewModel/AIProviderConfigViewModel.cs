using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows;
using Vetting.Data;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class AIProviderConfigViewModel : ObservableObject
{
    public ObservableCollection<AIProviderItemViewModel> Providers { get; } = [];
    [ObservableProperty] public partial AIProviderItemViewModel? SelectedProvider { get; set; }

    public AIProviderConfigViewModel()
    {
        using var db = new VettingDbContext();
        foreach (var config in db.AIProviderConfigs.FindAll())
            Providers.Add(new AIProviderItemViewModel(config));
        SelectedProvider = Providers.FirstOrDefault();
    }

    [RelayCommand]
    private void DeleteProvider(AIProviderItemViewModel vm)
    {
        if (HandyControl.Controls.MessageBox.Show($"确认删除 \"{vm.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        using var db = new VettingDbContext();
        db.AIProviderConfigs.Delete(vm.Id);
        Providers.Remove(vm);
        if (SelectedProvider == vm) SelectedProvider = Providers.FirstOrDefault();

        WeakReferenceMessenger.Default.Send(new AIProviderChanged(vm.Id, ChangedType.Delete));
    }

    [RelayCommand]
    private void AddProvider()
    {
        AIProviderItemViewModel vm = new();
        Providers.Add(vm);
        SelectedProvider = vm;
    }
}
