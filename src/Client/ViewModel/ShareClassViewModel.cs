using CommunityToolkit.Mvvm.ComponentModel;

using FMO.Models;
using System.Diagnostics.CodeAnalysis;

namespace FMO;

public partial class ShareClassViewModel : ObservableObject, IViewModel<ShareClass>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInherited))]
    public partial int Id { get; set; }

    public required int FlowId { get; set; }

    [ObservableProperty]
    public required partial string Name { get; set; }


    [ObservableProperty]
    public partial string? FundName { get; set; }

    [ObservableProperty]
    public partial string? Code { get; set; }

    [ObservableProperty]
    public partial string? Requirement { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInherited))]
    public partial int Inherit { get; set; }

    [ObservableProperty]
    public partial int RealInherit { get; set; }

    public bool IsInherited => ShareClass.GetFlow(Id) != FlowId;

    public ShareClassViewModel() { }

    [SetsRequiredMembers]
    public ShareClassViewModel(int flowId, ShareClass s)
    {
        Id = s.Id;
        Name = s.Name;
        FundName = s.FundName;
        Code = s.Code;
        Inherit = s.Inherit;
        RealInherit = s.Inherit;
        Requirement = s.Requirement;
        FlowId = flowId;
    }

    public ShareClassViewModel(ShareClass s)
    {
        Id = s.Id;
        Name = s.Name;
        FundName = s.FundName;
        Code = s.Code;
        Inherit = s.Inherit;
        RealInherit = s.Inherit;
        Requirement = s.Requirement;
    }


    public ShareClass Build() => new ShareClass()
    {
        Name = Name,
        FundName = FundName,
        Id = Id,
        Code = Code,
        Inherit = RealInherit,
        Requirement = Requirement
    };


    public static int GetFlow(int id) => id / 1000;


    public bool IsInheritBrother() => GetFlow(Id) == GetFlow(Inherit);
}
