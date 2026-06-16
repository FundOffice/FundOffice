using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// WorkEventPage.xaml 的交互逻辑
/// </summary>
public partial class WorkEventPage : UserControl
{
    public WorkEventPage()
    {
        InitializeComponent();
    }
}

public partial class WorkEventPageViewModel : ObservableObject
{
    public WorkEventPageViewModel()
    {
        using var db = DbHelper.Base();
        var items = db.GetCollection<WorkEvent>().Query().OrderByDescending(x => x.CreateTime).ToList();
        Events = [.. items.Select(WorkEventViewModel.Create)];

        EventSource.Source = Events;
        EventSource.Filter += (s, e) => e.Accepted = Filter(e.Item as WorkEventViewModel);

        // 收集所有标签和关联项
        AllTags = [.. Events.SelectMany(x => x.Tags ?? []).Distinct().OrderBy(x => x)];
        AllLinks = [.. Events.Where(x => !string.IsNullOrWhiteSpace(x.LinkName))
                             .Select(x => new WorkEventLinkItem(x.LinkType ?? string.Empty, x.LinkId, x.LinkName!))
                             .Distinct()
                             .OrderBy(x => x.Name)];

        SelectedStatus = StatusOptions[0];
    }

    public ObservableCollection<WorkEventViewModel> Events { get; } = [];

    public CollectionViewSource EventSource { get; } = new();

    public ObservableCollection<string> AllTags { get; } = [];

    public ObservableCollection<WorkEventLinkItem> AllLinks { get; } = [];

    [ObservableProperty]
    public partial DateTime? StartTime { get; set; }

    [ObservableProperty]
    public partial DateTime? EndTime { get; set; }

    [ObservableProperty]
    public partial string? SelectedTag { get; set; }

    [ObservableProperty]
    public partial WorkEventLinkItem? SelectedLink { get; set; }

    [ObservableProperty]
    public partial StatusOption? SelectedStatus { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    public StatusOption[] StatusOptions { get; } =
    [
        new StatusOption(null, "全部"),
        new StatusOption(WorkEventStatus.Pending, "待处理"),
        new StatusOption(WorkEventStatus.InProgress, "进行中"),
        new StatusOption(WorkEventStatus.Completed, "已完成"),
        new StatusOption(WorkEventStatus.Cancelled, "已取消"),
    ];

    partial void OnStartTimeChanged(DateTime? value)
    {
        if (value is not null && EndTime is not null && value.Value.Date > EndTime.Value.Date)
            EndTime = value.Value.Date;

        EventSource.View?.Refresh();
    }

    partial void OnEndTimeChanged(DateTime? value)
    {
        if (value is not null && StartTime is not null && value.Value.Date < StartTime.Value.Date)
            StartTime = value.Value.Date;

        EventSource.View?.Refresh();
    }

    partial void OnSelectedTagChanged(string? value) => EventSource.View?.Refresh();

    partial void OnSelectedLinkChanged(WorkEventLinkItem? value) => EventSource.View?.Refresh();

    partial void OnSelectedStatusChanged(StatusOption? value) => EventSource.View?.Refresh();

    partial void OnSearchTextChanged(string? value) => EventSource.View?.Refresh();

    private bool Filter(WorkEventViewModel? vm)
    {
        if (vm is null) return false;

        // 仅当起止日期都选择了才按时间过滤
        if (StartTime is not null && EndTime is not null)
        {
            var start = StartTime.Value.Date;
            var end = EndTime.Value.Date.AddDays(1).AddTicks(-1);
            if (vm.CreateTime < start || vm.CreateTime > end) return false;
        }

        if (!string.IsNullOrWhiteSpace(SelectedTag) && (vm.Tags is null || !vm.Tags.Contains(SelectedTag!))) return false;

        if (SelectedLink is not null && (vm.LinkType != SelectedLink.Type || vm.LinkId != SelectedLink.Id)) return false;

        if (SelectedStatus?.Status is not null && vm.Status != SelectedStatus.Status.Value) return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText!.Trim();
            if (!vm.Title.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                (vm.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) != true) &&
                (vm.LinkName?.Contains(text, StringComparison.OrdinalIgnoreCase) != true))
                return false;
        }

        return true;
    }

    [RelayCommand]
    public void Refresh()
    {
        using var db = DbHelper.Base();
        var items = db.GetCollection<WorkEvent>().Query().OrderByDescending(x => x.CreateTime).ToList();

        Events.Clear();
        foreach (var item in items.Select(WorkEventViewModel.Create))
            Events.Add(item);

        AllTags.Clear();
        foreach (var tag in Events.SelectMany(x => x.Tags ?? []).Distinct().OrderBy(x => x))
            AllTags.Add(tag);

        AllLinks.Clear();
        foreach (var link in Events.Where(x => !string.IsNullOrWhiteSpace(x.LinkName))
                                    .Select(x => new WorkEventLinkItem(x.LinkType ?? string.Empty, x.LinkId, x.LinkName!))
                                    .Distinct()
                                    .OrderBy(x => x.Name))
            AllLinks.Add(link);

        EventSource.View?.Refresh();
    }

    [RelayCommand]
    public void ClearFilter()
    {
        StartTime = null;
        EndTime = null;
        SelectedTag = null;
        SelectedLink = null;
        SelectedStatus = null;
        SearchText = null;
        EventSource.View?.Refresh();
    }

    [RelayCommand]
    public void AddEvent()
    {
        // TODO: 打开新增工作事项窗口
        var vm = new CustomWorkEventViewModel
        {
            Title = "新建工作事项",
            CreateTime = DateTime.Now,
            Status = WorkEventStatus.Pending,
        };
        Events.Insert(0, vm);
    }

    [RelayCommand]
    public void DeleteEvent(WorkEventViewModel vm)
    {
        if (vm is null) return;
        vm.DeleteCommand.Execute(null);
        Events.Remove(vm);
    }
}

public record WorkEventLinkItem(string Type, int Id, string Name)
{
    public override string ToString() => $"[{Type}] {Name}";
}

public record StatusOption(WorkEventStatus? Status, string Display);
