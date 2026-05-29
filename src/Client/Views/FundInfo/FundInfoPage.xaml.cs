using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.IO.AMAC;

using FMO.Models;
using FMO.Shared;
using FMO.Todo;
using FMO.TPL;
using FMO.Trustee;
using FMO.Utilities;
using LiteDB;
using Microsoft.Playwright;
using Microsoft.Win32;
using MoT;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;
/// <summary>
/// FundInfoPage.xaml 的交互逻辑
/// </summary>
public partial class FundInfoPage : UserControl
{
    public FundInfoPage()
    {
        InitializeComponent();
    }

}


public partial class FundInfoPageViewModel : ObservableRecipient, IRecipient<FundDailyUpdateMessage>,
    IRecipient<FundStrategyChangedMessage>, IRecipient<FundAccountChangedMessage>, IRecipient<EntityChangedMessage<Fund, DateOnly>>,
    IRecipient<EntityChangedMessage<Fund, FundStatus>>, IRecipient<TrusteeStatus>
{
    public Fund Fund { get; init; }

    public int FundId { get; private set; }

    //private bool _initialized;

    private ITrustee? _api;

    //FileSystemWatcher sheetFolderWatcher;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
    [SetsRequiredMembers]
    public FundInfoPageViewModel(Fund fund)
    {
        this.Fund = fund;

        FundId = fund.Id;
        FundName = fund.Name;
        FundShortName = fund.ShortName;
        TrusteeName = fund.Trustee;
        SetupDate = fund.SetupDate;
        RegistDate = fund.AuditDate;
        ClearDate = fund.ClearDate;
        InitiateDate = fund.InitiateDate == default ? null : fund.InitiateDate;
        FundCode = fund.Code;
        FundStatus = fund.Status;
        AmacId = fund.AmacID;


        _api = TrusteeGallay.Find(fund.Id);


        using var db = DbHelper.Base();
        var ele = db.QueryFactor(FundId);
        var shares = ele.ShareClasses.Current!;
        var openRules = ele.FundOpenRule.Current;


        //AllowSetTemporaryOpen = ele.TemporarilyOpenInfo.Current?.IsAllowed ?? false;

        OpenAndOrderContext = new(FundId, shares, ele.TemporarilyOpenInfo.Current);
        //if (shares is not null && openRules.Length == shares?.Length)
        //{
        //    ShareOpenInfos = shares?.Index().Select(x => new ShareOpenInfo(x.Item, OpenRule.ApplyMany(DateTime.Now.Year, openRules[x.Index]))).ToArray() ?? [];

        //    SelectedShareOpenDays = ShareOpenInfos[0].DateInfo.Where(x => x.Date.Month == DateTime.Now.Month);
        //}
        //else
        //{
        //    Toast.Error($"{FundName} 未设置开放日规则");
        //    ShareOpenInfos = shares?.Index().Select(x => new ShareOpenInfo(x.Item, OpenRule.ApplyMany(DateTime.Now.Year, null))).ToArray() ?? [];
        //    SelectedShareOpenDays = ShareOpenInfos[0].DateInfo.Where(x => x.Date.Month == DateTime.Now.Month);
        //}

        InitFlows(fund);



        // 如果已定稿
        if (Flows?.Any(x => x is ContractFinalizeFlowViewModel f && f.IsReadOnly) ?? false)
            CheckAndTodo(ele);


        CollectionAccount = ele.CollectionAccount;
        CustodyAccount = ele.CustodyAccount;



        RiskLevel = ele.RiskLevel;


        App.Current.Dispatcher.BeginInvoke(() =>
        {
            DailySource.Source = Array.Empty<DailyValue>();
            DailySource.View.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(DailyValue.Date), System.ComponentModel.ListSortDirection.Descending));
            // DailySource.View.Refresh();
        });


        debouncerDaily = new(() => App.Current.Dispatcher.BeginInvoke(() => DailySource.View.Refresh()));



        IsActive = true;
        //_initialized = true;
    }

    public void CheckAndTodo(FundFactors ele)
    {
        var missingList = new List<string>();

        // 统一格式：if (!ele.XXX.HasValue)
        if (!ele.FullName.HasValue)
        {
            missingList.Add("名称");
        }
        if (!ele.ShortName.HasValue)
        {
            missingList.Add("简称");
        }
        if (!ele.SecurityFundType.HasValue)
        {
            missingList.Add("基金类型");
        }
        if (!ele.FundModeInfo.HasValue)
        {
            missingList.Add("运作方式");
        }

        if (ele.FundModeInfo.Current?.Mode == FundMode.Open)
        {
            if (!ele.SealingRule.HasValue)
            {
                missingList.Add("封闭期");
            }

            if (!ele.OpenDayInfo.HasValue)
            {
                missingList.Add("开放日规则");
            }
            if (!ele.FundOpenRule.HasValue)
            {
                missingList.Add("开放规则");
            }
        }

        if (!ele.RiskLevel.HasValue)
        {
            missingList.Add("风险等级");
        }
        if (!ele.DurationInMonths.HasValue)
        {
            missingList.Add("存续期");
        }
        if (!ele.ExpirationDate.HasValue)
        {
            missingList.Add("结束日期");
        }
        if (!ele.CollectionAccount.HasValue)
        {
            missingList.Add("募集账户");
        }
        if (!ele.CustodyAccount.HasValue)
        {
            missingList.Add("托管账户");
        }

        //if (!ele.StopLine.HasValue)
        //{
        //    missingList.Add("止损线");
        //}
        //if (!ele.WarningLine.HasValue)
        //{
        //    missingList.Add("预警线");
        //}

        if (!ele.TrusteeInfo.HasValue)
        {
            missingList.Add("托管机构");
        }
        if (!ele.TrusteeFee.HasValue)
        {
            missingList.Add("托管费");
        }
        if (!ele.OutsourcingInfo.HasValue)
        {
            missingList.Add("外包机构");
        }
        if (!ele.OutsourcingFee.HasValue)
        {
            missingList.Add("外包费");
        }
        //if (!ele.InvestmentManagers.HasValue)
        //{
        //    missingList.Add("投资管理人");
        //}
        if (!ele.InvestmentManager.HasValue)
        {
            missingList.Add("投资经理");
        }
        //if (!ele.PerformanceBenchmark.HasValue)
        //{
        //    missingList.Add("业绩比较基准");
        //}
        if (!ele.InvestmentObjective.HasValue)
        {
            missingList.Add("投资目标");
        }
        if (!ele.InvestmentScope.HasValue)
        {
            missingList.Add("投资范围");
        }
        if (!ele.InvestmentStrategy.HasValue)
        {
            missingList.Add("投资策略");
        }
        //if (!ele.TemporarilyOpenInfo.HasValue)
        //{
        //    missingList.Add("临时开放信息");
        //}
        if (!ele.HugeRedemption.HasValue)
        {
            missingList.Add("巨额赎回");
        }
        if (!ele.CoolingPeriod.HasValue)
        {
            missingList.Add("冷静期");
        }
        if (!ele.Callback.HasValue)
        {
            missingList.Add("回访");
        }
        if (!ele.LockingRule.HasValue)
        {
            missingList.Add("锁定期");
        }
        if (!ele.ManageFee.HasValue)
        {
            missingList.Add("管理费");
        }
        if (!ele.ManageFeePay.HasValue)
        {
            missingList.Add("管理费支付方式");
        }
        if (!ele.SubscriptionRule.HasValue)
        {
            missingList.Add("认购规则");
        }
        if (!ele.PurchasRule.HasValue)
        {
            missingList.Add("申购规则");
        }
        if (!ele.RedemptionFee.HasValue)
        {
            missingList.Add("赎回费");
        }
        if (!ele.PerformanceFeeStatement.HasValue)
        {
            missingList.Add("业绩报酬");
        }

        if (missingList.Count > 0)
        {
            TodoService.Register(new FundElementFillTodo
            {
                FundId = ele.Id,
                FundName = ele.FullName.Current ?? string.Empty,
                FundCode = string.Empty,
                Missing = missingList
            });
        }
    }

#pragma warning restore CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    //private void WatchSheetFolder(Fund fund, FundElements ele)
    //{
    //    /// 监控估值表文件夹
    //    sheetFolderWatcher = new FileSystemWatcher(FundHelper.GetFolder(fund.Id, "Sheet"));
    //    sheetFolderWatcher.EnableRaisingEvents = true;
    //    sheetFolderWatcher.Created += (s, e) =>
    //    {
    //        try
    //        {
    //            using var fs = new FileStream(e.FullPath, FileMode.Open);
    //            var v = ValuationSheetHelper.ParseExcel(fs);
    //            if (v.dy is null)
    //            {
    //                Logg.Warning($"解析估值表 {e.Name} 出错");
    //                return;
    //            }

    //            if (v.code == FundCode || ele.FullName!.HasValue(v.fn))
    //            {
    //                var db = DbHelper.Base();
    //                db.GetDailyCollection(fund.Id).Upsert(v.dy);
    //            }
    //        }
    //        catch (Exception er)
    //        {
    //            Logg.Warning($"解析估值表 {e.Name} 出错 {er.Message}");
    //        }
    //    };
    //}

    private void InitFlows(Fund fund)
    {
        using var db = DbHelper.Base();
        var flows = db.GetCollection<FundFlow>().Find(x => x.FundId == fund.Id).OrderBy(x => x.Id).ToList();
        if (!flows.Any(x => x is InitiateFlow))
        {
            var f = new InitiateFlow { FundId = fund.Id, ElementFiles = new() { Label = "基金要素" }, ContractFiles = new() { Label = "基金合同" }, CustomFiles = new() };
            flows.Insert(0, f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        if (!flows.Any(x => x is ContractFinalizeFlow))
        {
            var f = new ContractFinalizeFlow { FundId = fund.Id, CustomFiles = new() };
            flows.Insert(1, f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        if (fund.Status >= FundStatus.Setup && !flows.Any(x => x is SetupFlow))
        {
            var f = new SetupFlow { FundId = fund.Id, Date = fund.SetupDate, CustomFiles = new() };
            flows.Insert(2, f);
            db.GetCollection<FundFlow>().Insert(f);
        }


        if (fund.Status >= FundStatus.Registration && !flows.Any(x => x is RegistrationFlow))
        {
            var f = new RegistrationFlow { FundId = fund.Id, Date = fund.AuditDate, CustomFiles = new() };
            flows.Add(f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        if (fund.Status >= FundStatus.StartLiquidation && !flows.Any(x => x is LiquidationFlow))
        {
            var f = new LiquidationFlow { FundId = fund.Id, CustomFiles = new() };
            flows.Add(f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        db.Dispose();

        Flows = new ObservableCollection<FlowViewModel>();
        Flows.CollectionChanged += Flows_CollectionChanged;

        foreach (var f in flows)
        {
            switch (f)
            {
                case InitiateFlow d:
                    Flows.Add(new InitiateFlowViewModel(d));
                    break;

                case ContractModifyFlow d:
                    Flows.Add(new ContractModifyFlowViewModel(d));
                    if (d.RegistrationLetter?.File?.Exists ?? false)
                        RegistrationLetter.Meta = d.RegistrationLetter.File;
                    break;

                case ContractFinalizeFlow d:
                    Flows.Add(new ContractFinalizeFlowViewModel(d));
                    break;

                case SetupFlow d:
                    Flows.Add(new SetupFlowViewModel(d));
                    break;

                case RegistrationFlow d:
                    Flows.Add(new RegistrationFlowViewModel(d));
                    if (d.RegistrationLetter is not null)
                        RegistrationLetter.Meta = d.RegistrationLetter.File;
                    break;

                case LiquidationFlow d:
                    Flows.Add(new LiquidationFlowViewModel(d));
                    //DataTracker.OnEntityChanged(d);
                    break;

                case DividendFlow d:
                    Flows.Add(new DividendFlowViewModel(d));
                    break;

                default:
                    break;
            }
        }


        FlowsSource = new CollectionViewSource { Source = Flows };
        FlowsSource.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(FlowViewModel.FlowId), System.ComponentModel.ListSortDirection.Descending));
        FlowsSource.Filter += FlowsSource_Filter;


        ElementsViewDataContext = new ElementsViewModel { FundId = Fund.Id };

        SelectedFlowInElements = Flows.LastOrDefault(x => x is IElementChangable);

    }

    private void Flows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            foreach (FlowViewModel flow in e.NewItems!)
            {
                switch (flow)
                {
                    case RegistrationFlowViewModel a:
                        a.RegistrationLetter!.FileChanged += f => RegistrationLetter.Meta = f?.File;
                        break;

                    case ContractModifyFlowViewModel a:
                        a.RegistrationLetter!.FileChanged += f => RegistrationLetter.Meta = f?.File;
                        break;
                    default:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 更新最新备案函
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    //private void RegistrationLetter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    //{
    //    if (Flows?.Select(x => x switch { RegistrationFlowViewModel a => a.RegistrationLetter, ContractModifyFlowViewModel b => b.RegistrationLetter, _ => null }).LastOrDefault(x => x is not null && x.File is not null)?.File?.FullName is string s)
    //        RegistrationLetter?.File = new FileStorageInfo(s, "", default);
    //    //RegistrationLetter = new LatestFileViewModel { Name = "备案函", File = Flows?.Select(x => x switch { RegistrationFlowViewModel a => a.RegistrationLetter, ContractModifyFlowViewModel b => b.RegistrationLetter, _ => null }).Where(x => x is not null && x.File is not null).LastOrDefault()?.File };
    //}



    private void FlowsSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item switch { ContractFinalizeFlowViewModel or ContractModifyFlowViewModel => true, _ => false };
    }

    #region Property

    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    [ObservableProperty]
    public partial bool IsEditable { get; set; }

    [ObservableProperty]
    public partial string? FundName { get; set; }


    [ObservableProperty]
    public partial string? FundShortName { get; set; }


    [ObservableProperty]
    public partial string? TrusteeName { get; set; }

    [ObservableProperty]
    public partial string? FundCode { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCleared))]
    public partial FundStatus FundStatus { get; set; }
    public string? AmacId { get; }

    public bool IsCleared => FundStatus > FundStatus.StartLiquidation;






    [ObservableProperty]
    public partial RiskLevel? RiskLevel { get; set; }



    [ObservableProperty]
    public partial DateOnly? SetupDate { get; set; }



    [ObservableProperty]
    public partial DateOnly? RegistDate { get; set; }


    [ObservableProperty]
    public partial DateOnly? ClearDate { get; set; }


    [ObservableProperty]
    public partial DateOnly? InitiateDate { get; set; }

    /// <summary>
    /// 投资范围
    /// </summary>
    [ObservableProperty]
    public partial string? InvestmentScope { get; set; }


    /// <summary>
    /// 募集账户
    /// </summary>
    [ObservableProperty]
    public partial BankAccount? CollectionAccount { get; set; }


    [ObservableProperty]
    public partial BankAccount? CustodyAccount { get; set; }


    /// <summary>
    /// 初始要素文件
    /// </summary>
    [ObservableProperty]
    public partial FileInfo? InitiateElementFile { get; set; }


    /// <summary>
    /// 初始基金合同
    /// </summary>
    [ObservableProperty]
    public partial FileInfo? InitiateFundContractFile { get; set; }

    /// <summary>
    /// 流程
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<FlowViewModel> Flows { get; set; }

    /// <summary>
    /// 流程，在要素页中
    /// </summary>
    public CollectionViewSource FlowsSource { get; set; }

    /// <summary>
    /// 选中的要素对应流程
    /// </summary>
    [ObservableProperty]
    public partial FlowViewModel? SelectedFlowInElements { get; set; }


    public SimpleFileViewModel RegistrationLetter { get; } = new();

    [ObservableProperty]
    public partial ObservableCollection<DailyValue> DailyValues { get; set; }

    private Debouncer debouncerDaily;

    public CollectionViewSource DailySource { get; } = new();


    /// <summary>
    /// 要素 上下文
    /// </summary>
    [ObservableProperty]
    public partial ElementsViewModel ElementsViewDataContext { get; set; }


    [ObservableProperty]
    public partial DailyValueCurveViewModel CurveViewDataContext { get; set; }

    [ObservableProperty]
    public partial FundStrategyViewModel StrategyDataContext { get; set; }

    [ObservableProperty]
    public partial FundAccountsViewModel AccountsDataContext { get; set; }


    [ObservableProperty]
    public partial FundTAViewModel TADataContext { get; set; }


    [ObservableProperty]
    public partial FundDisclosureViewModel AnnouncementContext { get; set; }

    [ObservableProperty]
    public partial OpenAndOrderViewModel OpenAndOrderContext { get; set; }
    #endregion




    partial void OnSelectedTabChanged(int value)
    {
        switch (value)
        {
            case 1: // TA
                if (TADataContext is null)
                    TADataContext = new FundTAViewModel(FundId) { IsTrusteeApiAvaliable = _api?.IsValid ?? false };
                break;

            case 2:
                if (DailyValues?.Count is null or 0)
                {
                    using var db = DbHelper.Base();
                    IEnumerable<DailyValue> collection = db.GetDailyCollection(Fund.Id).FindAll().OrderByDescending(x => x.Date).IntersectBy(Days.AllTradeDays, x => x.Date);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        DailyValues = new ObservableCollection<DailyValue>(collection);
                        DailySource.Source = DailyValues;
                        DailySource.View.Refresh();
                    });
                }
                break;

            case 3: // 曲线
                if (CurveViewDataContext is null)
                {
                    using var db = DbHelper.Base();
                    var strategies = db.GetCollection<FundStrategy>().Find(x => x.FundId == FundId).ToList();

                    CurveViewDataContext = new DailyValueCurveViewModel
                    {
                        FundId = Fund.Id,
                        FundName = Fund.ShortName,
                        Data = DailyValues.OrderBy(x => x.Date).ToList(),
                        SetupDate = Fund.SetupDate,
                        StartDate = DailyValues.LastOrDefault()?.Date,
                        EndDate = DailyValues.FirstOrDefault()?.Date,
                        Strategies = strategies
                    };
                }
                break;

            case 4: // 要素
                if (ElementsViewDataContext is null)
                {
                    ElementsViewDataContext = new ElementsViewModel { FundId = Fund.Id };
                    // 同步当前已选中的 Flow
                    if (SelectedFlowInElements != null)
                        ElementsViewDataContext.FlowId = SelectedFlowInElements.FlowId;
                }
                break;

            case 5: // 策略
                if (StrategyDataContext is null)
                    StrategyDataContext = new FundStrategyViewModel(FundId, Fund.SetupDate);
                break;

            case 6: // 账户
                if (AccountsDataContext is null)
                    AccountsDataContext = new FundAccountsViewModel(FundId, FundCode!);
                break;

            case 7: // 信披
                if (AnnouncementContext is null)
                    AnnouncementContext = new FundDisclosureViewModel(FundId);
                break;

            default:
                break;
        }
    }





    /// <summary>
    /// 打开基金公示
    /// </summary>
    [RelayCommand]
    public void NavigateToAmac()
    {
        try { if (Fund.Url is not null) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Fund.Url) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = FundHelper.GetFolder(FundId).FullName,
            UseShellExecute = true
        });
    }


    [RelayCommand]
    public void SetupFund()
    {
        using var db = DbHelper.Base();
        if (!Flows.Any(x => x is SetupFlowViewModel))
        {
            var flow = new SetupFlow { FundId = Fund.Id };
            db.GetCollection<FundFlow>().Insert(flow);
            var ele = db.GetCollection<FundElements>().FindById(Fund.Id);
            db.Dispose();
            Flows.Add(new SetupFlowViewModel(flow));
        }
        if (!Flows.Any(x => x is RegistrationFlowViewModel))
        {
            var flow = new RegistrationFlow { FundId = Fund.Id };
            db.GetCollection<FundFlow>().Insert(flow);
            var ele = db.GetCollection<FundElements>().FindById(Fund.Id);
            db.Dispose();
            Flows.Add(new RegistrationFlowViewModel(flow));
        }

    }

    /// <summary>
    /// 发起合同变更 
    /// </summary>
    [RelayCommand]
    public void CreateContractModify()
    {
        var flow = new ContractModifyFlow { FundId = Fund.Id };
        var db = DbHelper.Base();
        db.GetCollection<FundFlow>().Insert(flow);
        db.Dispose();
        Flows.Add(new ContractModifyFlowViewModel(flow));
    }

    [RelayCommand]
    public void CreateModifyByAnnounce()
    {
        var flow = new ModifyByAnnounceFlow { FundId = Fund.Id };
        using var db = DbHelper.Base();
        db.GetCollection<FundFlow>().Insert(flow);
        Flows.Add(new ModifyByAnnounceFlowViewModel(flow));
    }


    [RelayCommand]
    public void CreateDividendFlow()
    {
        // 分红日期检查
        var last = Flows.OfType<DividendFlowViewModel>().Select(x => x.Date).OfType<DateTime>().LastOrDefault();
        if (last != default && (DateTime.Now - last).TotalDays < 180)
            if (HandyControl.Controls.MessageBox.Show($"距上次分红不足6个月，本次分红将不会收取业绩报酬！\n\n建议 {last.AddDays(180):yyyy/M/d} 后分红\n\n是否继续", "警告", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

        var flow = new DividendFlow { FundId = Fund.Id };
        using var db = DbHelper.Base();
        db.GetCollection<FundFlow>().Insert(flow);
        Flows.Add(new DividendFlowViewModel(flow));
    }

    [RelayCommand]
    public void CreateClearFlow()
    {
        //if (FundStatus >= FundStatus.StartLiquidation) return;

        if (Flows.Any(x => x is LiquidationFlowViewModel)) return;

        var flow = new LiquidationFlow { FundId = Fund.Id };
        var db = DbHelper.Base();
        db.GetCollection<FundFlow>().Insert(flow);
        var fund = db.GetCollection<Fund>().FindById(Fund.Id);
        if (fund.Status < FundStatus.StartLiquidation)
            fund.Status = FundStatus.StartLiquidation;
        db.GetCollection<Fund>().Update(fund);
        db.Dispose();
        Flows.Add(new LiquidationFlowViewModel(flow));

        DataHub.Push(flow);
        //VerifyRules.OnEntityArrival([flow]);
        WeakReferenceMessenger.Default.Send(new EntityChangedMessage<Fund, FundStatus>(fund, nameof(Fund.Status), fund.Status));
        //WeakReferenceMessenger.Default.Send(new FundStatusChangedMessage(default, default) { FundId = fund.Id, Status = fund.Status });
    }


    /// <summary>
    /// 废除流程
    /// </summary>
    /// <param name="flow"></param>
    [RelayCommand]
    public void DeleteFlow(FlowViewModel flow)
    {
        if (flow is ContractRelatedFlowViewModel && HandyControl.Controls.MessageBox.Show("删除后不可恢复，同时会删除关联的要素", "确认删除", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.No)
            return;
        else if (HandyControl.Controls.MessageBox.Show("删除后不可恢复!!，确认删除？", "确认删除", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.No)
            return;

        using var db = DbHelper.Base();
        db.GetCollection<IFundFactor>().DeleteMany(x => x.FundId == FundId && x.FlowId == flow.FlowId);


        db.GetCollection<FundFlow>().Delete(flow.FlowId);
        if (flow is LiquidationFlowViewModel && FundStatus <= FundStatus.StartLiquidation)
        {
            FundStatus = FundStatus.Normal;

            var fund = db.GetCollection<Fund>().FindById(Fund.Id);
            fund.Status = FundStatus.Normal;
            db.GetCollection<Fund>().Update(fund);

            WeakReferenceMessenger.Default.Send(new EntityChangedMessage<Fund, FundStatus>(fund, nameof(Fund.Status), fund.Status));
            //WeakReferenceMessenger.Default.Send(new FundStatusChangedMessage(default, default) { FundId = fund.Id, Status = fund.Status });
        }
        Flows.Remove(flow);

        if (flow is LiquidationFlowViewModel)
            DataHub.Push(new EntityRemoved<FundFlow, int>(flow.FlowId));
        //VerifyRules.OnEntityArrival([new FundEntityRemoved<int>(typeof(LiquidationFlow), flow.FlowId, flow.FundId)]);
    }




    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshNetValuesCommand))]
    public partial bool CanRefreshNetValues { get; set; } = true;

    #region Nv List

    [RelayCommand(CanExecute = nameof(CanRefreshNetValues))]
    public void RefreshNetValues()
    {
        CanRefreshNetValues = false;
        Task.Run(() =>
        {
            var fd = FundHelper.GetFolder(Fund.Id, "Sheet");
            var di = new DirectoryInfo(fd);
            if (!di.Exists)
            {
                HandyControl.Controls.Growl.Info("未发现本基金的估值表");
                App.Current.Dispatcher.BeginInvoke(() => CanRefreshNetValues = true);
                return;
            }

            ConcurrentBag<(string? name, string? code, DailyValue? daily)> bag = new();

            try
            {
                Parallel.ForEach(di.GetFiles(), f =>
                {
                    using var fs = f.OpenRead();
                    var item = ValuationSheetHelper.ParseExcel(fs);
                    if (item.dy is not null)
                        item.dy.SheetPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), f.FullName);
                    else Logg.Error($"解析{f.Name}出错");

                    bag.Add(item);
                });


                List<(string? name, string? code, DailyValue? daily)> err = new();
                List<(string? name, string? code, DailyValue? daily)> ava = new(bag.Count);

                foreach (var x in bag)
                {
                    if (x.code != Fund.Code && x.name != Fund.Name)
                    {
                        err.Add(x);
                    }
                    else
                    {
                        x.daily?.FundId = Fund.Id;
                        ava.Add(x);
                    }
                }

                var data = bag.OrderBy(x => x.daily?.Date).ToArray();

                // 从属验证 
                if (err.Count != 0)
                {
                    Logg.Error($"{FundName} 解析全部估值表出错 发现{err.Count}个文件不属于本基金\n{string.Join('\n', err.Select(x => x.name))}))");
                    HandyControl.Controls.Growl.Info($"发现{err.Count}个文件不属于本基金\n{string.Join('\n', err.Select(x => x.name))}))");
                }

                if (ava.Count == 0) return;

                DataTracker.OnDailyValue(ava.Select(x => x.daily!));
            }
            catch (Exception e)
            {
                Logg.Error($"{FundName} 解析全部估值表出错 {e.Message}");
            }

            App.Current.Dispatcher.BeginInvoke(() => CanRefreshNetValues = true);
        });
    }


    [RelayCommand]
    public void ExportNetValues()
    {
        var last = DailyValues.FirstOrDefault(x => x.NetValue > 0);
        if (last is null) return;

        // 找模板
        using var db = DbHelper.Base();
        var exporters = db.GetCollection<TemplateInfo>().FindAll().Where(x => x.Suit.HasFlag(ExportTypeFlag.SingleFundNetValueList)).ToList();
        if (exporters is null || exporters.Count == 0)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "未找到基金净值列表导出模板"));
            return;
        }


        var wnd = new ExporterWindow
        {
            DataContext = new ExporterWindowViewModel(exporters, FundId),
            Owner = App.Current.MainWindow
        };
        wnd.ShowDialog();

    }
    #endregion


    [RelayCommand]
    public void ViewSheet(DailyValue daily)
    {
        string? path = daily?.SheetPath;

        if (path is null)
        {
            var di = new DirectoryInfo(Path.Combine(FundHelper.GetFolder(FundId, "Sheet")));
            var fis = di.GetFiles().Where(x => x.Name.Contains(daily!.Date.ToString("yyyyMMdd")));
            if (fis.Count() == 1)
                path = fis.First().FullName;
        }

        if (path is not null)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void GenerateAttach()
    {
        // 找模板
        try
        {
            var di = new DirectoryInfo(@"files\tpl\fund_attach");
            if (!di.Exists)
            {
                HandyControl.Controls.Growl.Warning("未找到附件模板");
                return;
            }


            using var db = DbHelper.Base();
            var m = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
            var e = db.GetCollection<FundElements>().FindById(FundId);
            var obj = new
            {
                ManagerName = m!.Name,
                FundName = Fund.Name,
                RiskLevel = RiskLevel ?? null,
                StopLine = e.StopLine.Value switch { 0 => "-", var n => n.ToString() }
            };

            var folder = FundHelper.GetFolder(FundId, "Contract\\Attach");
            var files = di.GetFiles("*.docx");
            foreach (var file in files)
            {
                var tar = Path.Combine(folder, file.Name);
                Tpl.Generate(tar, file.FullName, obj);
            }
        }
        catch (Exception e)
        {
            Logg.Error($"生成基金合同附件失败{e}");
            HandyControl.Controls.Growl.Warning("生成基金合同附件失败");
        }
    }

    [RelayCommand]
    public async Task UpdateFromApi()
    {
        if (SetupDate is null || SetupDate == default(DateOnly))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "请先设置基金的成立日期"));
            return;
        }
        if (FundCode is not null)
        {
            await TrusteeGallay.Worker.QueryNetValueOnce(Fund.Id, FundCode, SetupDate.Value, DateOnly.FromDateTime(DateTime.Today));
            await App.Current.Dispatcher.BeginInvoke(() =>
            {
                using var db = DbHelper.Base();
                var ll = new List<DailyValue>(db.GetDailyCollection(Fund.Id).FindAll().OrderByDescending(x => x.Date).IntersectBy(Days.AllTradeDays, x => x.Date).ToList());

                DailySource.Source = ll;
                DailySource.View.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(DailyValue.Date), System.ComponentModel.ListSortDirection.Descending));
                DailySource.View.Refresh();
            });
        }
    }


    [RelayCommand]
    public async Task<bool> SyncRegisterLeterFromAmac()
    {
        if (string.IsNullOrWhiteSpace(FundCode))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "没有备案号，无法更新"));
            return false;
        }

        AmacAccount acc = null!;
        using (var db = DbHelper.Base())
        {
            acc = db.GetCollection<AmacAccount>().FindById("ambers");
            if (string.IsNullOrWhiteSpace(acc?.Name) || string.IsNullOrWhiteSpace(acc?.Password))
            {
                Logg.Error("AMAC账号信息不完整，请检查数据库");
                return false;
            }
        }


        var (pw, browser, page) = await AmbersAssist.Prepare(true);

        try
        {
            // 检查登录
            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);


            // 登录
            var loginResult = await AmbersAssist.IsLogin(page);
            if (!loginResult)
                loginResult = await AmbersAssist.Login(page, acc.Name, acc.Password);

            if (!loginResult)
            {
                Logg.Error("AMAC登录失败，请检查账号信息");
                return false;
            }

            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // 下载 
            try
            {
                var downloadResult = await AmbersAssist.DownloadRegisterLetterCore(page, FundCode);
                if (string.IsNullOrWhiteSpace(downloadResult) || !File.Exists(downloadResult))
                {
                    WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "AMAC下载备案函失败，可能是网络问题或者AMAC页面结构发生变化"));
                    return false;
                }


                // 更新到flow中
                foreach (var flow in Flows.OrderByDescending(x => x.Date))
                {
                    if (flow is RegistrationFlowViewModel r)
                    {
                        r.RegistrationLetter.Normal.SetFile(downloadResult);
                        RegistrationLetter.Meta = r.RegistrationLetter.Normal.Meta;
                        break;
                    }
                    else if (flow is ContractModifyFlowViewModel c && c.ModifyName)
                    {
                        c.RegistrationLetter.Normal.SetFile(downloadResult);
                        RegistrationLetter.Meta = c.RegistrationLetter.Normal.Meta;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Logg.Error(e);
                WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "AMAC下载备案函失败，请查看log"));
                return false;
            }


            return true;

        }
        catch (Exception e)
        {
            Logg.Error(e);
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "AMAC下载备案函失败，请查看log"));
            return false;
        }
        finally
        {
            await browser.CloseAsync();
            pw.Dispose();
        }
    }


    partial void OnInitiateFundContractFileChanged(FileInfo? oldValue, FileInfo? newValue)
    {
        var dir = Fund.Folder();

        if (!dir.Exists)
            dir.Create();

        if (!dir.Exists)
        {
            Logg.Error($"[{FundName}]存储文件夹无法创建,{dir}");
            HandyControl.Controls.Growl.Error($"[{FundName}]存储文件夹无法创建");
            return;
        }









    }

    /// <summary>
    /// 当flow变动时
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnSelectedFlowInElementsChanged(FlowViewModel? oldValue, FlowViewModel? newValue)
    {
        if (newValue is not null)
            ElementsViewDataContext.FlowId = newValue.FlowId;
        else if (Flows.FirstOrDefault(x => x is IElementChangable) is FlowViewModel f)
            ElementsViewDataContext.FlowId = f.FlowId;
    }





    public void Receive(FundDailyUpdateMessage message)
    {
        if (message.FundId == Fund.Id && message.Daily.Class == null && Days.IsTradingDay(message.Daily.Date))
        {


            App.Current.Dispatcher.BeginInvoke(() =>
            {
                var old = DailyValues.FirstOrDefault(x => x.Id == message.Daily.Id && x.Class == message.Daily.Class);
                if (old is not null)
                    DailyValues.Remove(old);

                DailyValues.Add(message.Daily);

                if (CurveViewDataContext is not null)
                    CurveViewDataContext.Data = DailyValues.OrderBy(x => x.Date).ToList();

                debouncerDaily.Invoke();
            });


        }
    }

    public void Receive(FundStrategyChangedMessage message)
    {
        if (message.FundId == FundId && CurveViewDataContext is not null)
        {
            using var db = DbHelper.Base();
            var strategies = db.GetCollection<FundStrategy>().Find(x => x.FundId == FundId).ToList();
            CurveViewDataContext.Strategies = strategies.ToList();
        }
    }

    public void Receive(FundAccountChangedMessage message)
    {
        switch (message.Type)
        {
            case FundAccountType.None:
                break;
            case FundAccountType.Collection:
                {
                    using var db = DbHelper.Base();
                    CollectionAccount = db.QueryFundFactor<BankAccount>(FundId, FactorFields.CollectionAccount).FirstOrDefault()?.Data;
                }
                break;
            case FundAccountType.Custody:
                {
                    using var db = DbHelper.Base();
                    CustodyAccount = db.QueryFundFactor<BankAccount>(FundId, FactorFields.CustodyAccount).FirstOrDefault()?.Data;
                }
                break;
            default:
                break;
        }
    }

    public void Receive(EntityChangedMessage<Fund, DateOnly> message)
    {
        if (message.Entity.Id != FundId) return;

        switch (message.PropertyName)
        {
            case nameof(Fund.ClearDate):
                ClearDate = message.Value;
                break;
            default:
                break;
        }
    }

    public void Receive(EntityChangedMessage<Fund, FundStatus> message)
    {
        FundStatus = message.Value;
    }

    public void Receive(TrusteeStatus message)
    {
        if (message.Id == _api?.Identifier && TADataContext is not null)
            TADataContext.IsTrusteeApiAvaliable = message.Status;
    }
}


/// <summary>
/// 最新的文件版本视图
/// </summary>
public partial class LatestFileViewModel : ObservableObject
{
    public required string Name { get; set; }

    [ObservableProperty]
    public partial FileInfo? File { get; set; }


    [RelayCommand]
    public void View()
    {
        if (File?.Exists ?? false)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(File.FullName) { UseShellExecute = true }); } catch { }
    }



    [RelayCommand]
    public void Print()
    {
        if (File is null || !File.Exists) return;


        PrintDialog printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            // 获取默认打印机名称
            string printerName = printDialog.PrintQueue.Name;

            // 使用系统默认的PDF阅读器打印PDF文档
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = File.FullName;
            process.StartInfo.Verb = "print";
            process.Start();

            // 等待打印任务完成
            process.WaitForExit();
        }
    }


    [RelayCommand]
    public void SaveAs()
    {
        if (File is null || !File.Exists) return;

        try
        {
            var d = new SaveFileDialog();
            d.FileName = File.Name;
            if (d.ShowDialog() == true)
                System.IO.File.Copy(File.FullName, d.FileName);
        }
        catch (Exception ex)
        {
            Logg.Error($"文件另存为失败: {ex.Message}");
        }
    }
}


