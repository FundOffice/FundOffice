using FMO.Models;

namespace FMO;

public partial class SelfInspectionWorkEventViewModel : WorkEventViewModel
{
    public SelfInspectionWorkEventViewModel()
    {
    }

    public SelfInspectionWorkEventViewModel(SelfInspectionWorkEvent workEvent)
    {
        FillFrom(workEvent);
    }

    public override WorkEvent Build()
    {
        var e = new SelfInspectionWorkEvent();
        CopyTo(e);
        return e;
    }
}
