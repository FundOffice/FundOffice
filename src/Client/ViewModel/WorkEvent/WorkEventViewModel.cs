using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;

namespace FMO;

/// <summary>
/// 工作事项 ViewModel 基类
/// </summary>
public abstract partial class WorkEventViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial WorkEventType Type { get; set; }

    [ObservableProperty]
    public partial DateTime CreateTime { get; set; }

    [ObservableProperty]
    public partial DateTime? UpdateTime { get; set; }

    [ObservableProperty]
    public partial DateTime? DueTime { get; set; }

    [ObservableProperty]
    public partial WorkEventStatus Status { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> Tags { get; set; } = [];

    [ObservableProperty]
    public partial string? LinkType { get; set; }

    [ObservableProperty]
    public partial int LinkId { get; set; }

    [ObservableProperty]
    public partial string? LinkName { get; set; }

    [ObservableProperty]
    public partial bool IsManagerLinked { get; set; }

    [ObservableProperty]
    public partial bool IsFundLinked { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<int> LinkedFundIds { get; set; } = [];

    [ObservableProperty]
    public partial bool IsAccountLinked { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<int> LinkedAccountIds { get; set; } = [];

    public string TagDisplay => Tags is null || Tags.Count == 0 ? string.Empty : string.Join(", ", Tags);

    [ObservableProperty]
    public partial string NewTagInput { get; set; } = string.Empty;

    [RelayCommand]
    public void AddTag(string? text)
    {
        if (Tags is null) return;
        var input = text ?? NewTagInput;
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Split([',', '，', ';'], StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim())
                         .Where(x => !string.IsNullOrWhiteSpace(x));
        bool changed = false;
        foreach (var t in parts)
        {
            if (!Tags.Contains(t, StringComparer.OrdinalIgnoreCase))
            {
                Tags.Add(t);
                changed = true;
            }
        }
        if (changed) OnPropertyChanged(nameof(TagDisplay));
        NewTagInput = string.Empty;
    }

    [RelayCommand]
    public void RemoveTag(string? tag)
    {
        if (Tags is null || string.IsNullOrWhiteSpace(tag)) return;
        if (Tags.Remove(tag))
            OnPropertyChanged(nameof(TagDisplay));
    }

    public string StatusDisplay => Status switch
    {
        WorkEventStatus.Pending => "待处理",
        WorkEventStatus.InProgress => "进行中",
        WorkEventStatus.Completed => "已完成",
        WorkEventStatus.Cancelled => "已取消",
        _ => Status.ToString(),
    };

    public string TypeDisplay => Type switch
    {
        WorkEventType.AccountOpening => "开户",
        WorkEventType.AccountCancellation => "销户",
        WorkEventType.DueDiligence => "尽调",
        WorkEventType.SelfInspection => "自查",
        WorkEventType.ManagerAffairs => "管理人事务",
        WorkEventType.AccountInfoChange => "账户资料变更",
        WorkEventType.Custom => "自定义",
        _ => Type.ToString(),
    };

    protected void FillFrom(WorkEvent workEvent)
    {
        Id = workEvent.Id;
        Title = workEvent.Title;
        Type = workEvent.Type;
        CreateTime = workEvent.CreateTime;
        UpdateTime = workEvent.UpdateTime;
        DueTime = workEvent.DueTime;
        Status = workEvent.Status;
        Description = workEvent.Description;
        Tags = workEvent.Tags is null ? [] : new ObservableCollection<string>(workEvent.Tags);
        LinkType = workEvent.LinkType;
        LinkId = workEvent.LinkId;
        LinkName = workEvent.LinkName;
        IsManagerLinked = workEvent.IsManagerLinked;
        IsFundLinked = workEvent.IsFundLinked;
        LinkedFundIds = workEvent.LinkedFundIds is null ? [] : new ObservableCollection<int>(workEvent.LinkedFundIds);
        IsAccountLinked = workEvent.IsAccountLinked;
        LinkedAccountIds = workEvent.LinkedAccountIds is null ? [] : new ObservableCollection<int>(workEvent.LinkedAccountIds);
    }

    protected void CopyTo(WorkEvent workEvent)
    {
        workEvent.Id = Id;
        workEvent.Title = Title;
        workEvent.Type = Type;
        workEvent.CreateTime = CreateTime;
        workEvent.UpdateTime = UpdateTime;
        workEvent.DueTime = DueTime;
        workEvent.Status = Status;
        workEvent.Description = Description;
        workEvent.Tags = Tags?.ToList() ?? [];
        workEvent.LinkType = LinkType;
        workEvent.LinkId = LinkId;
        workEvent.LinkName = LinkName;
        workEvent.IsManagerLinked = IsManagerLinked;
        workEvent.IsFundLinked = IsFundLinked;
        workEvent.LinkedFundIds = LinkedFundIds?.ToList() ?? [];
        workEvent.IsAccountLinked = IsAccountLinked;
        workEvent.LinkedAccountIds = LinkedAccountIds?.ToList() ?? [];
    }

    [RelayCommand]
    public void Save()
    {
        using var db = DbHelper.Base();
        var obj = Build();
        db.GetCollection<WorkEvent>().Upsert(obj);
        Id = obj.Id;
    }

    [RelayCommand]
    public void Delete()
    {
        if (Id == 0) return;
        using var db = DbHelper.Base();
        db.GetCollection<WorkEvent>().Delete(Id);
    }

    public abstract WorkEvent Build();

    public static WorkEventViewModel Create(WorkEvent workEvent)
    {
        return workEvent switch
        {
            AccountOpeningWorkEvent e => new AccountOpeningWorkEventViewModel(e),
            AccountCancellationWorkEvent e => new AccountCancellationWorkEventViewModel(e),
            DueDiligenceWorkEvent e => new DueDiligenceWorkEventViewModel(e),
            SelfInspectionWorkEvent e => new SelfInspectionWorkEventViewModel(e),
            ManagerAffairsWorkEvent e => new ManagerAffairsWorkEventViewModel(e),
            AccountInfoChangeWorkEvent e => new AccountInfoChangeWorkEventViewModel(e),
            CustomWorkEvent e => new CustomWorkEventViewModel(e),
            _ => throw new NotSupportedException($"未知的工作事项类型: {workEvent.GetType().Name}"),
        };
    }
}
