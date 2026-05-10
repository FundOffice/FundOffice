
using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.ComponentModel;

namespace FMO.Settings;



[AutoViewModel(typeof(VerifyRuleUnit))]
public partial class VerifyRuleUnitViewModel : ObservableObject
{

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if(e.PropertyName == nameof(IsEnabled) && !string.IsNullOrWhiteSpace(Name))
        {
            if (IsEnabled)
                SettingService.EnableVerify(Name);
            else 
                SettingService.DisableVerify(Name);
        }
    }
   
}
