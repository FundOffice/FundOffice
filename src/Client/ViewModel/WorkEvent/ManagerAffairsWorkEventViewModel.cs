using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public partial class ManagerAffairsWorkEventViewModel : WorkEventViewModel
{
    public ManagerAffairsWorkEventViewModel()
    {
    }

    public ManagerAffairsWorkEventViewModel(ManagerAffairsWorkEvent workEvent)
    {
        FillFrom(workEvent);
        AffairsType = workEvent.AffairsType;
        Handler = workEvent.Handler;
    }

    [ObservableProperty]
    public partial ManagerAffairsType AffairsType { get; set; }

    [ObservableProperty]
    public partial string? Handler { get; set; }

    public string AffairsTypeDisplay => AffairsType switch
    {
        ManagerAffairsType.Registration => "登记备案",
        ManagerAffairsType.Qualification => "资质申请",
        ManagerAffairsType.InformationChange => "信息变更",
        ManagerAffairsType.Other => "其他",
        _ => AffairsType.ToString(),
    };

    public override WorkEvent Build()
    {
        var e = new ManagerAffairsWorkEvent();
        CopyTo(e);
        e.AffairsType = AffairsType;
        e.Handler = Handler;
        return e;
    }
}
