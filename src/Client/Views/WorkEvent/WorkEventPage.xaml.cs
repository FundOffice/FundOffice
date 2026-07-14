using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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

    private void TagInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not TextBox tb) return;
        if (DataContext is not WorkEventPageViewModel vm) return;
        if (vm.SelectedEvent is null) return;

        vm.SelectedEvent.AddTagCommand.Execute(tb.Text);
        tb.Clear();
        e.Handled = true;
    }

    private void OriginalDrop(object sender, DragEventArgs e)
    {
        if (DataContext is WorkEventPageViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            vm.DropFiles("原始文件", files);
    }

    private void SealDrop(object sender, DragEventArgs e)
    {
        if (DataContext is WorkEventPageViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            vm.DropFiles("用印文件", files);
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

        // 加载所有基金
        var funds = db.GetCollection<Fund>().Query().OrderBy(x => x.Name).ToList();
        foreach (var fund in funds)
        {
            var display = string.IsNullOrWhiteSpace(fund.ShortName)
                ? $"[{fund.Code}] {fund.Name}"
                : $"[{fund.Code}] {fund.Name} ({fund.ShortName})";
            var selectable = new SelectableFund(fund.Id, fund.ShortName ?? string.Empty, display);
            selectable.PropertyChanged += OnFundSelectionChanged;
            AllFunds.Add(selectable);
        }

        FundSource.Source = AllFunds;
        FundSource.Filter += (s, e) => e.Accepted = FilterFund(e.Item as SelectableFund);

        var fundShortNameMap = funds.ToDictionary(f => f.Id, f => f.ShortName ?? f.Name);

        // 加载所有交易账户
        var accounts = db.GetCollection<TradingAccoutOfFund>().Query().ToList();
        foreach (var account in accounts)
        {
            var fundName = fundShortNameMap.GetValueOrDefault(account.FundId, "未知基金");
            var accountNo = account switch
            {
                StockAccount sa => sa.Common?.Account,
                FutureAccount fa => fa.Common?.Account,
                _ => null,
            };
            var noText = string.IsNullOrWhiteSpace(accountNo) ? "无账号" : accountNo;
            var display = $"[{GetAccountTypeName(account)}] {noText} / {fundName}";
            var selectable = new SelectableAccount(account.Id, account.FundId, display);
            selectable.PropertyChanged += OnAccountSelectionChanged;
            AllAccounts.Add(selectable);
        }

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

    public ObservableCollection<SelectableFund> AllFunds { get; } = [];

    public CollectionViewSource FundSource { get; } = new();

    public ObservableCollection<SelectableAccount> AllAccounts { get; } = [];

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

    [ObservableProperty]
    public partial WorkEventViewModel? SelectedEvent { get; set; }

    [ObservableProperty]
    public partial WorkEventType SelectedEventType { get; set; }

    [ObservableProperty]
    public partial string? FundFilterText { get; set; }

    [ObservableProperty]
    public partial bool IsFundPopupOpen { get; set; }

    [ObservableProperty]
    public partial bool IsAccountPopupOpen { get; set; }

    public string FundButtonText => $"已选({SelectedEvent?.LinkedFundIds.Count ?? 0})";

    public string FundButtonTooltip
    {
        get
        {
            if (SelectedEvent is null) return "";
            if (SelectedEvent.LinkedFundIds.Count == 0) return "未选择基金";
            var selected = AllFunds.Where(f => SelectedEvent.LinkedFundIds.Contains(f.Id)).Select(f => f.Display);
            return string.Join("\n", selected);
        }
    }

    public string AccountButtonText => $"已选({SelectedEvent?.LinkedAccountIds.Count ?? 0})";

    public string AccountButtonTooltip
    {
        get
        {
            if (SelectedEvent?.LinkedAccountIds.Count == 0) return "未选择账户";
            var selected = FilteredAccounts.Where(a => SelectedEvent!.LinkedAccountIds.Contains(a.Id)).Select(a => a.Display);
            return string.Join("\n", selected);
        }
    }

    public StatusOption[] StatusOptions { get; } =
    [
        new StatusOption(null, "全部"),
        new StatusOption(WorkEventStatus.Pending, "待处理"),
        new StatusOption(WorkEventStatus.InProgress, "进行中"),
        new StatusOption(WorkEventStatus.Completed, "已完成"),
        new StatusOption(WorkEventStatus.Cancelled, "已取消"),
    ];

    public WorkEventType[] TypeOptions { get; } =
    [
        WorkEventType.Custom,
        WorkEventType.AccountOpening,
        WorkEventType.DueDiligence,
        WorkEventType.ManagerAffairs,
        WorkEventType.AccountInfoChange,
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

    partial void OnSelectedEventChanged(WorkEventViewModel? value)
    {
        SelectedEventType = value?.Type ?? WorkEventType.Custom;
        OnPropertyChanged(nameof(IsDetailEnabled));
        SyncLinkSelections();
    }

    partial void OnSelectedEventTypeChanged(WorkEventType value)
    {
        OnPropertyChanged(nameof(IsDetailEnabled));
    }

    partial void OnFundFilterTextChanged(string? value)
    {
        FundSource.View?.Refresh();
    }

    /// <summary>
    /// 仅当类型下拉框与当前事件类型一致时，下方编辑区域才可用
    /// </summary>
    public bool IsDetailEnabled => SelectedEvent is not null && SelectedEventType == SelectedEvent.Type;

    private bool FilterFund(SelectableFund? fund)
    {
        if (fund is null) return false;
        if (string.IsNullOrWhiteSpace(FundFilterText)) return true;
        return fund.Display.Contains(FundFilterText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSelectedTagChanged(string? value) => EventSource.View?.Refresh();

    partial void OnSelectedLinkChanged(WorkEventLinkItem? value) => EventSource.View?.Refresh();

    partial void OnSelectedStatusChanged(StatusOption? value) => EventSource.View?.Refresh();

    partial void OnSearchTextChanged(string? value) => EventSource.View?.Refresh();

    private void SyncLinkSelections()
    {
        foreach (var fund in AllFunds)
            fund.IsSelected = SelectedEvent?.LinkedFundIds.Contains(fund.Id) == true;

        foreach (var account in AllAccounts)
            account.IsSelected = SelectedEvent?.LinkedAccountIds.Contains(account.Id) == true;

        OnPropertyChanged(nameof(FundButtonText));
        OnPropertyChanged(nameof(FundButtonTooltip));
        RefreshFilteredAccounts();
    }

    private void OnFundSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableFund.IsSelected) || sender is not SelectableFund fund) return;
        if (SelectedEvent is null) return;

        if (fund.IsSelected && !SelectedEvent.LinkedFundIds.Contains(fund.Id))
            SelectedEvent.LinkedFundIds.Add(fund.Id);
        else if (!fund.IsSelected && SelectedEvent.LinkedFundIds.Contains(fund.Id))
            SelectedEvent.LinkedFundIds.Remove(fund.Id);

        OnPropertyChanged(nameof(FundButtonText));
        OnPropertyChanged(nameof(FundButtonTooltip));
        RefreshFilteredAccounts();
    }

    private void OnAccountSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableAccount.IsSelected) || sender is not SelectableAccount account) return;
        if (SelectedEvent is null) return;

        if (account.IsSelected && !SelectedEvent.LinkedAccountIds.Contains(account.Id))
            SelectedEvent.LinkedAccountIds.Add(account.Id);
        else if (!account.IsSelected && SelectedEvent.LinkedAccountIds.Contains(account.Id))
            SelectedEvent.LinkedAccountIds.Remove(account.Id);

        OnPropertyChanged(nameof(AccountButtonText));
        OnPropertyChanged(nameof(AccountButtonTooltip));
    }

    private static string GetAccountTypeName(TradingAccoutOfFund account)
    {
        return account switch
        {
            StockAccount => "股票户",
            FutureAccount => "期货户",
            _ => account.GetType().Name,
        };
    }

    [ObservableProperty]
    public partial ObservableCollection<SelectableAccount> FilteredAccounts { get; set; } = [];

    partial void OnFilteredAccountsChanged(ObservableCollection<SelectableAccount> value)
    {
        // 切换筛选集合时保持选中状态与当前事件一致
        if (SelectedEvent is null) return;
        foreach (var account in value)
            account.IsSelected = SelectedEvent.LinkedAccountIds.Contains(account.Id);

        OnPropertyChanged(nameof(AccountButtonText));
        OnPropertyChanged(nameof(AccountButtonTooltip));
    }

    private void RefreshFilteredAccounts()
    {
        var selectedFundIds = SelectedEvent?.LinkedFundIds.ToHashSet() ?? [];
        var filtered = AllAccounts.Where(a => selectedFundIds.Contains(a.FundId)).ToList();

        // 保留当前选中状态
        var oldSelections = FilteredAccounts.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet();
        FilteredAccounts = [.. filtered];
        if (SelectedEvent is not null)
        {
            foreach (var account in FilteredAccounts)
                account.IsSelected = SelectedEvent.LinkedAccountIds.Contains(account.Id) || oldSelections.Contains(account.Id);
        }
    }

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

        // 刷新基金和账户列表
        var funds = db.GetCollection<Fund>().Query().OrderBy(x => x.Name).ToList();
        AllFunds.Clear();
        foreach (var fund in funds)
        {
            var display = string.IsNullOrWhiteSpace(fund.ShortName)
                ? $"[{fund.Code}] {fund.Name}"
                : $"[{fund.Code}] {fund.ShortName}";
            var selectable = new SelectableFund(fund.Id, fund.ShortName ?? string.Empty, display);
            selectable.PropertyChanged += OnFundSelectionChanged;
            AllFunds.Add(selectable);
        }
        FundSource.View?.Refresh();

        var fundShortNameMap = funds.ToDictionary(f => f.Id, f => f.ShortName ?? f.Name);

        var accounts = db.GetCollection<TradingAccoutOfFund>().Query().ToList();
        AllAccounts.Clear();
        foreach (var account in accounts)
        {
            var fundName = fundShortNameMap.GetValueOrDefault(account.FundId, "未知基金");
            var accountNo = account switch
            {
                StockAccount sa => sa.Common?.Account,
                FutureAccount fa => fa.Common?.Account,
                _ => null,
            };
            var noText = string.IsNullOrWhiteSpace(accountNo) ? "无账号" : accountNo;
            var display = $"[{GetAccountTypeName(account)}] {noText} / {fundName}";
            var selectable = new SelectableAccount(account.Id, account.FundId, display);
            selectable.PropertyChanged += OnAccountSelectionChanged;
            AllAccounts.Add(selectable);
        }

        SyncLinkSelections();
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
        AddTypedEvent(WorkEventType.Custom);
    }

    [RelayCommand]
    public void AddAccountOpeningEvent() => AddTypedEvent(WorkEventType.AccountOpening);

    [RelayCommand]
    public void AddAccountCancellationEvent() => AddTypedEvent(WorkEventType.AccountCancellation);

    [RelayCommand]
    public void AddDueDiligenceEvent() => AddTypedEvent(WorkEventType.DueDiligence);

    [RelayCommand]
    public void AddSelfInspectionEvent() => AddTypedEvent(WorkEventType.SelfInspection);

    [RelayCommand]
    public void AddManagerAffairsEvent() => AddTypedEvent(WorkEventType.ManagerAffairs);

    [RelayCommand]
    public void AddAccountInfoChangeEvent() => AddTypedEvent(WorkEventType.AccountInfoChange);

    [RelayCommand]
    public void AddAccountOtherEvent() => AddTypedEvent(WorkEventType.AccountOther);

    private void AddTypedEvent(WorkEventType type)
    {
        WorkEvent workEvent = type switch
        {
            WorkEventType.AccountOpening => new AccountOpeningWorkEvent(),
            WorkEventType.AccountCancellation => new AccountCancellationWorkEvent(),
            WorkEventType.DueDiligence => new DueDiligenceWorkEvent(),
            WorkEventType.SelfInspection => new SelfInspectionWorkEvent(),
            WorkEventType.ManagerAffairs => new ManagerAffairsWorkEvent(),
            WorkEventType.AccountInfoChange => new AccountInfoChangeWorkEvent(),
            WorkEventType.AccountOther => new AccountOtherWorkEvent(),
            _ => new CustomWorkEvent(),
        };
        workEvent.Title = type switch
        {
            WorkEventType.AccountOpening => "新建开户",
            WorkEventType.AccountCancellation => "新建销户",
            WorkEventType.DueDiligence => "新建尽调",
            WorkEventType.SelfInspection => "新建自查",
            WorkEventType.ManagerAffairs => "新建管理人变更",
            WorkEventType.AccountInfoChange => "新建账户变更",
            WorkEventType.AccountOther => "新建账户其它",
            _ => "新建工作事项",
        };
        workEvent.CreateTime = DateTime.Now;
        workEvent.Status = WorkEventStatus.Pending;

        // 账户相关类型默认勾选关联基金；变更/销户再加勾关联账户
        workEvent.IsFundLinked = type is WorkEventType.AccountOpening
            or WorkEventType.AccountInfoChange
            or WorkEventType.AccountCancellation
            or WorkEventType.AccountOther;
        workEvent.IsAccountLinked = type is WorkEventType.AccountInfoChange
            or WorkEventType.AccountCancellation
            or WorkEventType.AccountOther;

        var vm = WorkEventViewModel.Create(workEvent);
        Events.Insert(0, vm);
        SelectedEvent = vm;
    }

    [RelayCommand]
    public void DeleteEvent(WorkEventViewModel vm)
    {
        if (vm is null) return;
        vm.DeleteCommand.Execute(null);
        Events.Remove(vm);
        if (SelectedEvent == vm) SelectedEvent = null;
    }

    [RelayCommand]
    public void SwitchType()
    {
        if (SelectedEvent is null || SelectedEvent.Type == SelectedEventType) return;

        var newVm = CreateWithType(SelectedEvent, SelectedEventType);
        var index = Events.IndexOf(SelectedEvent);
        if (index >= 0)
            Events[index] = newVm;
        else
            Events.Add(newVm);

        SelectedEvent = newVm;
    }

    [RelayCommand]
    public void OpenFundPopup() => IsFundPopupOpen = true;

    [RelayCommand]
    public void OpenAccountPopup() => IsAccountPopupOpen = true;

    [RelayCommand]
    public void OpenOriginalFolder() => OpenEventFolder("原始文件");

    [RelayCommand]
    public void OpenSealFolder() => OpenEventFolder("用印文件");

    private void OpenEventFolder(string sub)
    {
        if (SelectedEvent is null) return;
        if (SelectedEvent.Id == 0)
        {
            MessageBox.Show("请先保存事项后再打开附件文件夹。");
            return;
        }
        var folder = GetEventFolder(sub);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }

    public void DropFiles(string sub, string[] files)
    {
        if (SelectedEvent is null || files is null || files.Length == 0) return;
        if (SelectedEvent.Id == 0)
        {
            MessageBox.Show("请先保存事项后再拖入文件。");
            return;
        }
        var folder = GetEventFolder(sub);
        int saved = 0;
        foreach (var file in files)
        {
            if (Directory.Exists(file)) continue;
            if (!File.Exists(file)) continue;
            try
            {
                var fi = new FileInfo(file);
                var (_, full) = FileHelper.GenerateUniqueFileName(folder, fi.Name);
                File.Copy(file, full);
                saved++;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件失败：{file}\n{ex.Message}");
            }
        }
        if (saved > 0)
            MessageBox.Show($"已保存 {saved} 个文件到 {folder}");
    }

    private string GetEventFolder(string sub)
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "events", SelectedEvent!.Id.ToString(), sub);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        return folder;
    }

    private static WorkEventViewModel CreateWithType(WorkEventViewModel source, WorkEventType type)
    {
        WorkEvent target = type switch
        {
            WorkEventType.AccountOpening => new AccountOpeningWorkEvent(),
            WorkEventType.AccountCancellation => new AccountCancellationWorkEvent(),
            WorkEventType.DueDiligence => new DueDiligenceWorkEvent(),
            WorkEventType.SelfInspection => new SelfInspectionWorkEvent(),
            WorkEventType.ManagerAffairs => new ManagerAffairsWorkEvent(),
            WorkEventType.AccountInfoChange => new AccountInfoChangeWorkEvent(),
            _ => new CustomWorkEvent(),
        };

        target.Id = source.Id;
        target.Title = source.Title;
        target.Type = type;
        target.CreateTime = source.CreateTime;
        target.UpdateTime = DateTime.Now;
        target.DueTime = source.DueTime;
        target.Status = source.Status;
        target.Description = source.Description;
        target.Tags = source.Tags?.ToList() ?? [];
        target.LinkType = source.LinkType;
        target.LinkId = source.LinkId;
        target.LinkName = source.LinkName;
        target.IsManagerLinked = source.IsManagerLinked;
        target.IsFundLinked = source.IsFundLinked;
        target.LinkedFundIds = source.LinkedFundIds?.ToList() ?? [];
        target.IsAccountLinked = source.IsAccountLinked;
        target.LinkedAccountIds = source.LinkedAccountIds?.ToList() ?? [];

        return WorkEventViewModel.Create(target);
    }
}

public partial class SelectableFund : ObservableObject
{
    public int Id { get; }
    public string ShortName { get; }
    public string Display { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public SelectableFund(int id, string shortName, string display)
    {
        Id = id;
        ShortName = shortName;
        Display = display;
    }
}

public partial class SelectableAccount : ObservableObject
{
    public int Id { get; }
    public int FundId { get; }
    public string Display { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public SelectableAccount(int id, int fundId, string display)
    {
        Id = id;
        FundId = fundId;
        Display = display;
    }
}

public record WorkEventLinkItem(string Type, int Id, string Name)
{
    public override string ToString() => $"[{Type}] {Name}";
}

public record StatusOption(WorkEventStatus? Status, string Display);
