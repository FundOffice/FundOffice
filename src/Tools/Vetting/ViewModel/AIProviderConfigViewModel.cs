using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
}
