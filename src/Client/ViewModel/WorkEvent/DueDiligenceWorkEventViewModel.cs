using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public partial class DueDiligenceWorkEventViewModel : WorkEventViewModel
{
    public DueDiligenceWorkEventViewModel()
    {
    }

    public DueDiligenceWorkEventViewModel(DueDiligenceWorkEvent workEvent)
    {
        FillFrom(workEvent);
        DueDiligenceType = workEvent.DueDiligenceType;
        Result = workEvent.Result;
        Target = workEvent.Target;
    }

    [ObservableProperty]
    public partial DueDiligenceType DueDiligenceType { get; set; }

    [ObservableProperty]
    public partial string? Result { get; set; }

    [ObservableProperty]
    public partial string? Target { get; set; }

    public string DueDiligenceTypeDisplay => DueDiligenceType switch
    {
        DueDiligenceType.Initial => "首次尽调",
        DueDiligenceType.Regular => "常规尽调",
        DueDiligenceType.AdHoc => "临时尽调",
        _ => DueDiligenceType.ToString(),
    };

    public override WorkEvent Build()
    {
        var e = new DueDiligenceWorkEvent();
        CopyTo(e);
        e.DueDiligenceType = DueDiligenceType;
        e.Result = Result;
        e.Target = Target;
        return e;
    }
}
