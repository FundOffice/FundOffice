
using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.ComponentModel;

namespace FMO.Settings;



public class UnitViewModel : ObservableObject
{

}


[AutoViewModel(typeof(SwitchUnit))]
public partial class SwitchUnitViewModel : UnitViewModel
{
    
}


[AutoViewModel(typeof(AbilityUnit))]
public partial class AbilityUnitViewModel : ObservableObject
{

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsEnabled) && !string.IsNullOrWhiteSpace(Name))
        {
            if (IsEnabled)
                SettingService.EnableAbility($"{Section}.{Name}");
            else
                SettingService.DisableAbility($"{Section}.{Name}");
        }
    }

}
