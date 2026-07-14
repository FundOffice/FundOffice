using CommunityToolkit.Mvvm.ComponentModel;

namespace Vetting.ViewModel;
public partial class SelectableProvider(AIProviderItemViewModel provider) : ObservableObject
{
    public AIProviderItemViewModel Provider { get; } = provider;
    public string Name => Provider.Name;
    [ObservableProperty] public partial bool IsSelected { get; set; }
}
