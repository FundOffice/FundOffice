using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;

namespace FMO;

public partial class CustomWorkEventViewModel : WorkEventViewModel
{
    public CustomWorkEventViewModel()
    {
    }

    public CustomWorkEventViewModel(CustomWorkEvent workEvent)
    {
        FillFrom(workEvent);
        Category = workEvent.Category;
    }

    [ObservableProperty]
    public partial string? Category { get; set; }

    public override WorkEvent Build()
    {
        var e = new CustomWorkEvent();
        CopyTo(e);
        e.Category = Category;
        return e;
    }
}
