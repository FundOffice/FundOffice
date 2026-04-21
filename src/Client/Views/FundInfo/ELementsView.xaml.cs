using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// ELementsView.xaml 的交互逻辑
/// </summary>
public partial class ElementsView : UserControl
{
    public ElementsView()
    {
        InitializeComponent();
    }
}



public partial class ElementsViewModel : EditableControlViewModelBase<FundElements>, IRecipient<ElementChangedBackgroundMessage>
{
    public ElementsViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public static RiskLevel[] RiskLevels { get; } = [Models.RiskLevel.R1, Models.RiskLevel.R2, Models.RiskLevel.R3, Models.RiskLevel.R4, Models.RiskLevel.R5];

    public static FundMode[] FundModes { get; } = [Models.FundMode.Open, Models.FundMode.Close, Models.FundMode.Other];

    public static FundFeeType[] FundFeeTypes { get; } = [FundFeeType.Ratio, FundFeeType.Fix, FundFeeType.Other];

    public static FundFeeType[] RedemptionFeeTypes { get; } = [FundFeeType.Ratio, FundFeeType.ByTime, FundFeeType.Fix, FundFeeType.Other];


    public static FundFeePayType[] FundFeePayTypes { get; } = [FundFeePayType.Extra, FundFeePayType.Out, FundFeePayType.Other];

    public static FeePayFrequency[] FeePayFrequencies { get; } = [FeePayFrequency.Month, FeePayFrequency.Quarter, FeePayFrequency.Other];


    public static CoolingPeriodType[] CoolingPeriodTypes { get; } = [CoolingPeriodType.OneDay, CoolingPeriodType.Other];


    public static string[] TrusteeNames { get; } = ["中国工商银行股份有限公司",
"中国农业银行股份有限公司",
"中国银行股份有限公司",
"中国建设银行股份有限公司",
"交通银行股份有限公司",
"华夏银行股份有限公司",
"中国光大银行股份有限公司",
"招商银行股份有限公司",
"中信银行股份有限公司",
"中国民生银行股份有限公司",
"兴业银行股份有限公司",
"上海浦东发展银行股份有限公司",
"北京银行股份有限公司",
"平安银行股份有限公司",
"广发银行股份有限公司",
"中国邮政储蓄银行股份有限公司",
"上海银行股份有限公司",
"渤海银行股份有限公司",
"宁波银行股份有限公司",
"浙商银行股份有限公司",
"海通证券股份有限公司",
"国信证券股份有限公司",
"徽商银行股份有限公司",
"广州农村商业银行股份有限公司",
"招商证券股份有限公司",
"中国证券登记结算有限责任公司",
"财通证券股份有限公司",
"恒丰银行股份有限公司",
"杭州银行股份有限公司",
"南京银行股份有限公司",
"广发证券股份有限公司",
"国泰君安证券股份有限公司",
"江苏银行股份有限公司",
"中国银河证券股份有限公司",
"华泰证券股份有限公司",
"中信证券股份有限公司",
"兴业证券股份有限公司",
"中国证券金融股份有限公司",
"中信建投证券股份有限公司",
"中国国际金融股份有限公司",
"恒泰证券股份有限公司",
"中泰证券股份有限公司",
"光大证券股份有限公司",
"安信证券股份有限公司",
"东方证券股份有限公司",
"申万宏源证券有限公司",
"华鑫证券有限责任公司",
"华福证券有限责任公司",
"万联证券股份有限公司",
"华安证券股份有限公司",
"国元证券股份有限公司",
"国金证券股份有限公司",
"长城证券股份有限公司",
"长江证券股份有限公司",
"浙商证券股份有限公司",
"苏州银行股份有限公司",
"南京证券股份有限公司",
"东方财富证券股份有限公司",
"青岛银行股份有限公司",
"成都银行股份有限公司",
"长沙银行股份有限公司",
"第一创业证券股份有限公司",
"上海农村商业银行股份有限公司",
];

    public static SecurityFundType[] SecurityFundTypes = Enum.GetValues<SecurityFundType>();

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial int FundId { get; set; }


    [ObservableProperty]
    public partial int FlowId { get; set; }


    public DateOnly SetupDate { get; set; }

    [ObservableProperty]
    public partial bool IsDividingShare { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<ShareClassViewModel>? Shares { get; set; }

    [ObservableProperty]
    public partial bool IsSharesInherited { get; set; }

    public OpenRule? OpenRule { get; set; }

    #region 要素

    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? FullName { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? ShortName { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, SecurityFundType>? SecurityFundType { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, RiskLevel>? RiskLevel { get; set; }





    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, int?>? DurationInMonths { get; set; }

    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, DateOnly?>? ExpirationDate { get; set; }



    [ObservableProperty]
    public partial BankChangeableViewModel<FundElements>? CollectionAccount { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, BankAccountInfoViewModel>? CustodyAccount { get; set; }




    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, decimal?>? StopLine { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, decimal?>? WarningLine { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, decimal?>? HugeRedemptionRatio { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSealingFund))]
    public partial ChangeableViewModel<FundElements, DataExtraViewModel<FundMode>>? FundModeInfo { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, SealingInfoViewModel>? SealingRule { get; set; }


    //[ObservableProperty]
    //public partial ElementItemViewModelSealing? LockingRule { get; set; }

    [ObservableProperty]
    public partial SealingType[]? SealingTypes { get; set; } = [SealingType.No, SealingType.Has, SealingType.Other];

    [ObservableProperty]
    public partial bool IsSealingFund { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? OpenDayInfo { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, AgencyInfoViewModel>? TrusteeInfo { get; set; }

    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, AgencyInfoViewModel>? OutsourcingInfo { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, FundFeeInfoViewModel>? TrusteeFee { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, FundFeeInfoViewModel>? OutsourcingFee { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? InvestmentManagers { get; set; }



    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, TemporarilyOpenInfoViewModel>? TemporarilyOpenInfo { get; set; }



    //[ObservableProperty]
    //public partial ElementRefrenceWithBooleanViewModel<string>? PerformanceBenchmarks { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? InvestmentObjective { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? InvestmentScope { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, string>? InvestmentStrategy { get; set; }

    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, CoolingPeriodInfoViewModel>? CoolingPeriod { get; set; }

    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, CallbackInfoViewModel>? Callback { get; set; }



    [ObservableProperty]
    public partial ShareElementsViewModel<FundFeeInfo, FundFeeInfoViewModel>? ManageFee { get; set; }


    [ObservableProperty]
    public partial ChangeableViewModel<FundElements, FeePayInfoViewModel>? ManageFeePay { get; set; }



    [ObservableProperty]
    public partial ShareElementsViewModel<FundPurchaseRule, FundPurchaseRuleViewModel>? SubscriptionRule { get; set; }




    [ObservableProperty]
    public partial ShareElementsViewModel<FundPurchaseRule, FundPurchaseRuleViewModel>? PurchasRule { get; set; }



    [ObservableProperty]
    public partial ShareElementsViewModel<RedemptionFeeInfo, RedemptionFeeInfoViewMdoel>? RedemptionFee { get; set; }



    [ObservableProperty]
    public partial ShareElementsViewModel<string, string>? PerformanceFeeStatement { get; set; }


    /// <summary>
    /// 单一份额
    /// </summary>
    [ObservableProperty]
    public partial bool OnlyOneShare { get; set; }





    #endregion

    partial void OnFlowIdChanged(int oldValue, int newValue)
    {
        Id = FundId;
        using var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(FundId);
        var flow = db.GetCollection<FundFlow>().FindById(newValue);
        bool isori = flow is ContractFinalizeFlow;
        var elements = db.GetCollection<FundElements>().FindById(FundId);

        if (elements is null)
            elements = new FundElements { Id = FundId };

        var type = GetType();
        SetupDate = fund.SetupDate;
        var cinfo = elements.ShareClasses.GetValue(newValue);
        IsSharesInherited = cinfo.FlowId < newValue;
        var sc = cinfo.Value ?? [ShareClass.DefaultShare];
        Shares = new(sc.Select(x => new ShareClassViewModel { Id = x.Id, Name = x.Name, Requirement = x.Requirement }));
        OnlyOneShare = Shares.Count <= 1;
        OpenRule = elements.FundOpenRule.GetValue(newValue).Value;

        FullName = new ChangeableViewModel<FundElements, string>
        {
            Label = "基金全称",
            InitFunc = x => x.FullName.GetValue(newValue).Value,
            InheritedFunc = x => x.FullName.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.FullName.SetValue(y!, newValue),
            ClearFunc = x => x.FullName.RemoveValue(newValue)
        };
        FullName.Init(elements);

        ShortName = new ChangeableViewModel<FundElements, string>
        {
            Label = "基金简称",
            InitFunc = x => x.ShortName.GetValue(newValue).Value,
            InheritedFunc = x => x.ShortName.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.ShortName.SetValue(y!, newValue),
            ClearFunc = x => x.ShortName.RemoveValue(newValue)
        };
        ShortName.Init(elements);

        SecurityFundType = new()
        {
            Label = "基金类型",
            InitFunc = x => x.SecurityFundType.GetValue(newValue).Value,
            InheritedFunc = x => x.SecurityFundType.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.SecurityFundType.SetValue(y!, newValue),
            ClearFunc = x => x.SecurityFundType.RemoveValue(newValue)
        };
        SecurityFundType.Init(elements);

        if (isori)
        {
            if (FullName.NewValue == default)
                FullName.NewValue = fund.Name;

            if (ShortName.NewValue == default)
                ShortName.NewValue = fund.ShortName;
        }


        #region MyRegion

        FundModeInfo = new ChangeableViewModel<FundElements, DataExtraViewModel<FundMode>>
        {
            Label = "运作方式",
            InitFunc = x => new(x.FundModeInfo!.GetValue(newValue).Value),
            InheritedFunc = x => x.FundModeInfo.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.FundModeInfo!.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.FundModeInfo!.RemoveValue(newValue),
            DisplayFunc = x => x?.Data switch { FundMode.Open => "开放式", FundMode.Close => "封闭式", FundMode.Other => x.Other, _ => "-" }
        };
        FundModeInfo.Init(elements);


        SealingRule = new ChangeableViewModel<FundElements, SealingInfoViewModel>
        {
            Label = "封闭期",
            InitFunc = x => new(x.SealingRule!.GetValue(newValue).Value),
            InheritedFunc = x => x.SealingRule.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.SealingRule!.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.SealingRule!.RemoveValue(newValue),
            DisplayFunc = x => x?.Type switch { SealingType.No => "无", SealingType.Has => $"{x.Month}个月", SealingType.Other => x.Other, _ => "-" }
        };
        SealingRule.Init(elements);

        RiskLevel = new ChangeableViewModel<FundElements, RiskLevel>
        {
            Label = "风险等级",
            InitFunc = x => x.RiskLevel!.GetValue(newValue).Value,
            InheritedFunc = x => x.RiskLevel.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.RiskLevel!.SetValue(y, newValue),
            ClearFunc = x => x.RiskLevel!.RemoveValue(newValue),

        };
        RiskLevel.Init(elements);


        /// 最大999，认为是永续
        DurationInMonths = new ChangeableViewModel<FundElements, int?>
        {
            Label = "存续期",
            InitFunc = x => x.DurationInMonths!.GetValue(newValue).Value switch { 0 => null, var n => n },
            InheritedFunc = x => x.DurationInMonths.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) =>
            {
                if (y is not null)
                {
                    x.DurationInMonths!.SetValue(y.Value, newValue);

                    if (newValue >= 999)
                    {
                        ExpirationDate?.NewValue = new(2099, 12, 31);
                        //ExpirationDate.OldValue = new(2099, 12, 31);
                    }
                    else
                    {
                        ExpirationDate?.NewValue = SetupDate.AddMonths(y.Value).AddDays(-1);
                        //ExpirationDate.OldValue = ExpirationDate.NewValue;
                    }
                }
            },
            ClearFunc = x => x.DurationInMonths!.RemoveValue(newValue),
            DisplayFunc = x => x switch { >= 999 => "无固定期限", var m when m % 12 == 0 => $"{x / 12}年", > 0 => $"{x}个月", _ => "" }
        };
        DurationInMonths.Init(elements);

        ExpirationDate = new ChangeableViewModel<FundElements, DateOnly?>
        {
            Label = "到期日",
            InitFunc = x => ExpirationDateFormat(x.ExpirationDate!.GetValue(newValue).Value),
            InheritedFunc = x => x.ExpirationDate.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.ExpirationDate!.SetValue(y.Value, newValue); },
            ClearFunc = x => x.ExpirationDate!.RemoveValue(newValue),
        };
        ExpirationDate.Init(elements);



        CollectionAccount = new BankChangeableViewModel<FundElements>
        {
            Label = "募集账户",
            InitFunc = x => new(x.CollectionAccount.GetValue(newValue).Value),
            InheritedFunc = x => x.CollectionAccount.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.CollectionAccount.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.CollectionAccount.RemoveValue(newValue),
            DisplayFunc = x => BankString(x)
        };
        CollectionAccount.Init(elements);


        CustodyAccount = new ChangeableViewModel<FundElements, BankAccountInfoViewModel>
        {
            Label = "托管账户",
            InitFunc = x => new(x.CustodyAccount.GetValue(newValue).Value),
            InheritedFunc = x => x.CustodyAccount.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.CustodyAccount.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.CustodyAccount.RemoveValue(newValue),
            DisplayFunc = x => BankString(x)
        };
        CustodyAccount.Init(elements);


        StopLine = new ChangeableViewModel<FundElements, decimal?>
        {
            Label = "止损线",
            InitFunc = x => ValueFormat(x.StopLine.GetValue(newValue).Value),
            InheritedFunc = x => x.StopLine.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.StopLine!.SetValue(y.Value, newValue); },
            ClearFunc = x => x.StopLine.RemoveValue(newValue),
        };
        StopLine.Init(elements);

        WarningLine = new ChangeableViewModel<FundElements, decimal?>
        {
            Label = "预警线",
            InitFunc = x => ValueFormat(x.WarningLine.GetValue(newValue).Value),
            InheritedFunc = x => x.WarningLine.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.WarningLine!.SetValue(y.Value, newValue); },
            ClearFunc = x => x.WarningLine.RemoveValue(newValue),
        };
        WarningLine.Init(elements);


        HugeRedemptionRatio = new ChangeableViewModel<FundElements, decimal?>
        {
            Label = "巨额赎回比例",
            InitFunc = x => ValueFormat(x.HugeRedemptionRatio.GetValue(newValue).Value),
            InheritedFunc = x => x.HugeRedemptionRatio.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.HugeRedemptionRatio!.SetValue(y.Value, newValue); },
            ClearFunc = x => x.HugeRedemptionRatio.RemoveValue(newValue),
            DisplayFunc = x => x?.ToString("P")
        };
        HugeRedemptionRatio.Init(elements);
        //FundModeInfo = new ElementItemFundModeViewModel(elements, nameof(FundElements.FundModeInfo), FlowId, "运作方式");

        //SealingRule = new ElementItemViewModelSealing(elements, nameof(FundElements.SealingRule), FlowId, "封闭期");
        //// LockingRule = new ElementItemViewModelSealing(elements, nameof(FundElements.LockingRule), FlowId, "锁定期");


        //IsSealingFund = FundModeInfo.Data.New == Models.FundMode.Close;

        OpenDayInfo = new ChangeableViewModel<FundElements, string>
        {
            Label = "固定开放日",
            InitFunc = x => x.OpenDayInfo.GetValue(newValue).Value,
            InheritedFunc = x => x.OpenDayInfo.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.OpenDayInfo.SetValue(y!, newValue),
            ClearFunc = x => x.OpenDayInfo.RemoveValue(newValue)
        };
        OpenDayInfo.Init(elements);

        TrusteeFee = new ChangeableViewModel<FundElements, FundFeeInfoViewModel>
        {
            Label = "托管费",
            InitFunc = x => new(x.TrusteeFee.GetValue(newValue).Value),
            InheritedFunc = x => x.TrusteeFee.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.TrusteeFee.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.TrusteeFee.RemoveValue(newValue),
            DisplayFunc = x => x?.ToString() ?? "-"
        };
        TrusteeFee.Init(elements);


        OutsourcingFee = new ChangeableViewModel<FundElements, FundFeeInfoViewModel>
        {
            Label = "外包费",
            InitFunc = x => new(x.OutsourcingFee.GetValue(newValue).Value),
            InheritedFunc = x => x.OutsourcingFee.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.OutsourcingFee.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.OutsourcingFee.RemoveValue(newValue),
            DisplayFunc = x => x is null ? "-" : x.ToString()
        };
        OutsourcingFee.Init(elements);

        TrusteeInfo = new ChangeableViewModel<FundElements, AgencyInfoViewModel>
        {
            Label = "托管机构",
            InitFunc = x => new(x.TrusteeInfo.GetValue(newValue).Value),
            InheritedFunc = x => x.TrusteeInfo.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.TrusteeInfo!.SetValue(y.Build(), newValue); },
            ClearFunc = x => x.TrusteeInfo.RemoveValue(newValue),
        };
        TrusteeInfo.Init(elements);

        OutsourcingInfo = new ChangeableViewModel<FundElements, AgencyInfoViewModel>
        {
            Label = "外包机构",
            InitFunc = x => new(x.OutsourcingInfo.GetValue(newValue).Value),
            InheritedFunc = x => x.OutsourcingInfo.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.OutsourcingInfo!.SetValue(y.Build(), newValue); },
            ClearFunc = x => x.OutsourcingInfo.RemoveValue(newValue),
        };
        OutsourcingInfo.Init(elements);

        //PerformanceBenchmarks = new(elements, nameof(FundElements.PerformanceBenchmarks), FlowId, "业绩比较基准");
        //PerformanceBenchmarks.DisplayGenerator = (a, b) => a switch { true => b, false => "无", _ => ElementItemViewModel.UnsetValue };


        InvestmentObjective = new ChangeableViewModel<FundElements, string>
        {
            Label = "投资目标",
            InitFunc = x => x.InvestmentObjective.GetValue(newValue).Value,
            InheritedFunc = x => x.InvestmentObjective.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.InvestmentObjective!.SetValue(y, newValue); },
            ClearFunc = x => x.InvestmentObjective.RemoveValue(newValue),
        };
        InvestmentObjective.Init(elements);


        InvestmentScope = new ChangeableViewModel<FundElements, string>
        {
            Label = "投资范围",
            InitFunc = x => x.InvestmentScope.GetValue(newValue).Value,
            InheritedFunc = x => x.InvestmentScope.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.InvestmentScope!.SetValue(y, newValue); },
            ClearFunc = x => x.InvestmentScope.RemoveValue(newValue),
        };
        InvestmentScope.Init(elements);

        InvestmentStrategy = new ChangeableViewModel<FundElements, string>
        {
            Label = "投资策略",
            InitFunc = x => x.InvestmentStrategy.GetValue(newValue).Value,
            InheritedFunc = x => x.InvestmentStrategy.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.InvestmentStrategy!.SetValue(y, newValue); },
            ClearFunc = x => x.InvestmentStrategy.RemoveValue(newValue),
        };
        InvestmentStrategy.Init(elements);

        InvestmentManagers = new ChangeableViewModel<FundElements, string>
        {
            Label = "投资经理",
            InitFunc = x => x.InvestmentManager.GetValue(newValue).Value,
            InheritedFunc = x => x.InvestmentManager.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.InvestmentManager!.SetValue(y, newValue); },
            ClearFunc = x => x.InvestmentManager.RemoveValue(newValue),
        };
        InvestmentManagers.Init(elements);


        TemporarilyOpenInfo = new ChangeableViewModel<FundElements, TemporarilyOpenInfoViewModel>
        {
            Label = "临开规则",
            InitFunc = x => new(x.TemporarilyOpenInfo.GetValue(newValue).Value),
            InheritedFunc = x => x.TemporarilyOpenInfo.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => { if (y is not null) x.TemporarilyOpenInfo!.SetValue(y.Build(), newValue); },
            ClearFunc = x => x.TemporarilyOpenInfo.RemoveValue(newValue),
        };
        TemporarilyOpenInfo.Init(elements);

        #endregion

        //SubscriptionFee = new ShareElementsViewModel2<FundFeeInfo, FundFeeInfoViewModel>(FundId, FlowId, elements, sc, x => x.SubscriptionFee, x => new(x), x => x!.Build());
        SubscriptionRule = new ShareElementsViewModel<FundPurchaseRule, FundPurchaseRuleViewModel>(FundId, FlowId, elements, sc, x => x.SubscriptionRule, x => new(x) { FeeName = "认购费" }, x => x!.Build());
        foreach (var item in SubscriptionRule.Data)
        {
            if (item.NewValue!.MinDeposit == 0 || item.NewValue.MinDeposit is null)
                item.NewValue.MinDeposit = 1000000;
        }

        //PurchaseFee = new ShareElementsViewModel2<FundFeeInfo, FundFeeInfoViewModel>(FundId, FlowId, elements, sc, x => x.PurchaseFee, x => new(x), x => x!.Build());
        PurchasRule = new ShareElementsViewModel<FundPurchaseRule, FundPurchaseRuleViewModel>(FundId, FlowId, elements, sc, x => x.PurchasRule, x => new(x) { FeeName = "申购费" }, x => x!.Build());
        foreach (var item in PurchasRule.Data)
        {
            if (item.NewValue!.MinDeposit == 0 || item.NewValue.MinDeposit is null)
                item.NewValue.MinDeposit = 1000000;
        }

        RedemptionFee = new ShareElementsViewModel<RedemptionFeeInfo, RedemptionFeeInfoViewMdoel>(FundId, FlowId, elements, sc, x => x.RedemptionFee, x => new(x), x => x!.Build());
        PerformanceFeeStatement = new ShareElementsViewModel<string, string>(FundId, FlowId, elements, sc, x => x.PerformanceFeeStatement, x => x!, x => x!);

        ManageFee = new ShareElementsViewModel<FundFeeInfo, FundFeeInfoViewModel>(FundId, FlowId, elements, sc, x => x.ManageFee, x => new(x), x => x!.Build());
        ManageFeePay = new ChangeableViewModel<FundElements, FeePayInfoViewModel>
        {
            Label = "支付频率",
            InitFunc = x => new(x.ManageFeePay!.GetValue(newValue).Value),
            InheritedFunc = x => x.ManageFeePay.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.ManageFeePay!.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.ManageFeePay!.RemoveValue(newValue),
        };
        ManageFeePay.Init(elements);

        CoolingPeriod = new ChangeableViewModel<FundElements, CoolingPeriodInfoViewModel>
        {
            Label = "冷静期",
            InitFunc = x => new(x.CoolingPeriod!.GetValue(newValue).Value),
            InheritedFunc = x => x.CoolingPeriod.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.CoolingPeriod!.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.CoolingPeriod!.RemoveValue(newValue),
        };
        CoolingPeriod.Init(elements);

        Callback = new ChangeableViewModel<FundElements, CallbackInfoViewModel>
        {
            Label = "回访",
            InitFunc = x => new(x.Callback!.GetValue(newValue).Value),
            InheritedFunc = x => x.Callback.GetValue(newValue).FlowId switch { -1 => false, int i => i < newValue },
            UpdateFunc = (x, y) => x.Callback!.SetValue(y!.Build(), newValue),
            ClearFunc = x => x.Callback!.RemoveValue(newValue),
        };
        Callback.Init(elements);

        // InitElementsOfShare(elements);

    }

    private DateOnly? ExpirationDateFormat(DateOnly value)
    {
        if (value == default)
        {
            if (DurationInMonths!.NewValue is not null && DurationInMonths.NewValue > 0)
                return SetupDate.AddMonths(DurationInMonths.NewValue.Value).AddDays(-1);
        }
        return value;
    }

    private string BankString(BankAccountInfoViewModel? x)
    {
        if (x is null) return "-";

        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(x.Name))
            builder.Append($"户名：{x.Name}\n");

        if (!string.IsNullOrWhiteSpace(x.Number))
            builder.Append($"账号：{x.Number}\n");

        if (!string.IsNullOrWhiteSpace(x.BankOfDeposit))
            builder.Append($"开户行：{x.BankOfDeposit}\n");

        if (!string.IsNullOrWhiteSpace(x.LargePayNo))
            builder.Append($"大额支付号：{x.LargePayNo}\n");

        if (!string.IsNullOrWhiteSpace(x.SwiftCode))
            builder.Append($"SWIFT：{x.SwiftCode}\n");

        return builder.ToString();
    }



    [RelayCommand]
    public void SetBankFromClipboard(ChangeableViewModel<FundElements, BankAccountInfoViewModel> v)
    {
        try
        {
            var text = Clipboard.GetText();

            if (BankAccount.FromString(text) is BankAccount account)
            {
                v.NewValue = new(account);
                if (!account.Name!.Contains("募集") && v.Label!.Contains("募集"))
                    HandyControl.Controls.Growl.Warning("请确认此账户是募集账户");
            }
            else
                HandyControl.Controls.Growl.Error("无法识别的银行信息格式");
        }
        catch { }
    }

    [RelayCommand]
    public void BeginChangedShare(FrameworkElement panel)
    {
        var wnd = new ModifyShareClassWindow();
        wnd.DataContext = new ModifyShareClassWindowViewModel(FundId, FlowId);
        wnd.Owner = App.Current.MainWindow;
        Window window = Window.GetWindow(panel);
        Point point = panel.TransformToAncestor(window).Transform(new Point(panel.ActualWidth / 2, panel.ActualHeight / 2));

        wnd.Left = window.Left + point.X - wnd.Width / 2;
        wnd.Top = window.Top + point.Y + panel.ActualHeight;

        var r = wnd.ShowDialog();

        if (r ?? false) OnFlowIdChanged(FlowId, FlowId);
    }


    [RelayCommand]
    public void SetOpenRule()
    {
        OpenRuleViewModel openRuleViewModel = new();
        if (OpenRule is not null) openRuleViewModel.Init(OpenRule);

        var wnd = new OpenRuleEditor
        {
            Height = 930,
            Width = 1200,
            DataContext = openRuleViewModel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = App.Current.MainWindow
        };
        if (wnd.ShowDialog() switch { true => true, _ => false })
        {
            using var db = DbHelper.Base();
            var e = db.GetCollection<FundElements>().FindById(Id);
            OpenRule = openRuleViewModel.Rule;
            e.FundOpenRule.SetValue(openRuleViewModel.Rule, FlowId);
            db.GetCollection<FundElements>().Update(e);
            if (string.IsNullOrWhiteSpace(OpenDayInfo!.NewValue))
                OpenDayInfo.NewValue = OpenRule.ToString();
        }
    }

    public void InitShare(Mutable<ShareClass[]>? shareClass = null)
    {
        if (shareClass is null)
        {
            using var db = DbHelper.Base();
            shareClass = db.GetCollection<FundElements>().FindById(FundId)?.ShareClasses;
        }

        if (shareClass is not null && shareClass.GetValue(FlowId).Value is ShareClass[] shares)
            Shares = new ObservableCollection<ShareClassViewModel>(shares.Select(x => new ShareClassViewModel { Id = x.Id, Name = x.Name }));
        else
            throw new Exception(); //Shares = new ObservableCollection<ShareClassViewModel>([new ShareClassViewModel { Id = IdGenerator.GetNextId(nameof(ShareClass)), Name = FundElements.SingleShareKey }]);

    }

    //public void Receive(FundShareChangedMessage message)
    //{
    //    if (message.FundId == FundId && message.FlowId <= FlowId)
    //    {
    //        using var db = DbHelper.Base();
    //        var elements = db.GetCollection<FundElements>().FindOne(x => x.FundId == FundId);
    //        //InitElementsOfShare(elements);
    //    }

    //    //  OnFlowIdChanged(0, FlowId);
    //}

    protected override FundElements InitNewEntity() => throw new NotImplementedException();

    private T? ValueFormat<T>(T d) where T : struct
    {
        return default(T).Equals(d) ? null : d;
    }
    //private DateOnly? ValueFormat(DateOnly d)
    //{
    //    return d == default ? null : d;
    //}



    protected override void ModifyOverride(IPropertyModifier unit)
    {
        base.ModifyOverride(unit);
        if (unit == CollectionAccount)
            WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Collection));
        else if (unit == CustodyAccount)
            WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Custody));
        else if (unit == ExpirationDate)
            DataTracker.OnEntityChanged(new EntityChanged<DateOnly>(typeof(FundElements), Id, nameof(FundElements.ExpirationDate), ExpirationDate.OldValue ?? default, ExpirationDate.NewValue ?? default));
    }


    protected override void DeleteOverride(IPropertyModifier unit)
    {
        base.DeleteOverride(unit);
        if (unit == ExpirationDate)
            DataTracker.OnEntityChanged(new EntityChanged<DateOnly>(typeof(FundElements), Id, nameof(FundElements.ExpirationDate), ExpirationDate.OldValue ?? default, ExpirationDate.NewValue ?? default));
    }

    protected override void SaveOverride()
    {
        var ps = GetType().GetProperties();
        foreach (var p in ps)
        {
            if (p.PropertyType.IsAssignableTo(typeof(IPropertyModifier)) && p.GetValue(this) is IPropertyModifier v && v.IsValueChanged)
                Modify(v);

            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ShareElementsViewModel<,>))
            {
                var obj = p.GetValue(this);
                if (obj is not null)
                {
                    var pi = obj.GetType().GetProperty("Data");
                    if (pi!.GetValue(obj, null) is IEnumerable<IPropertyModifier> e)
                        foreach (var item in e)
                            Modify(item);
                }
            }
        }
        WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Collection));
        WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Custody));
    }

    public void Receive(ElementChangedBackgroundMessage message)
    {
        //if (message.FundId == FundId && message.FlowId == FlowId)
        OnFlowIdChanged(FlowId, FlowId);
    }
}



