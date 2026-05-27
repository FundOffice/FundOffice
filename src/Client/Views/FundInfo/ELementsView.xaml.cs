using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using MoT;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Utilities;

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



public partial class ElementsViewModel : ObservableObject, IRecipient<ElementChangedBackgroundMessage>
{
    public ElementsViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    #region 组
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

    #endregion
    public int Id => FundId;

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    /// <summary>
    /// 
    /// </summary> 
    public int FundId { get; init; }


    [ObservableProperty]
    public partial int FlowId { get; set; }


    public DateOnly SetupDate { get; set; }

    [ObservableProperty]
    public partial bool IsDividingShare { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<ShareClassViewModel> Shares { get; set; } = null!;

    [ObservableProperty]
    public partial bool IsSharesInherited { get; set; }

    public OpenRule? OpenRule { get; set; }

    #region 要素

    //[ObservableProperty]
    //public partial FactorModifiableViewModel<string>? FullName { get; set; }





    [ObservableProperty]
    public partial ShareFactorViewModel<FundFeeInfo?, FundFeeInfoViewModel>? ManageFee { get; set; } = null!;


     

    [ObservableProperty]
    public partial FactorModifiableViewModel<DateOnly?, FundExpireDateViewModel> ExpirationDate { get; private set; } = null!;


    [ObservableProperty]
    public partial ShareFactorViewModel<RedemptionFeeInfo?, RedemptionFeeInfoViewMdoel>? RedemptionFee { get; set; } = null!;

    [ObservableProperty]
    public partial ShareFactorViewModel<OpenRule[]?, FundOpenRuleViewModel>? FundOpenRule { get; set; } = null!;






    //[ObservableProperty]
    //public partial ElementItemViewModelSealing? LockingRule { get; set; }

    [ObservableProperty]
    public partial SealingType[]? SealingTypes { get; set; } = [SealingType.No, SealingType.Has, SealingType.Other];


    public bool IsSealingFund => FundModeInfo.OldValue?.Mode == FundMode.Close;







    //[ObservableProperty]
    //public partial ElementRefrenceWithBooleanViewModel<string>? PerformanceBenchmarks { get; set; }







    /// <summary>
    /// 单一份额
    /// </summary>
    [ObservableProperty]
    public partial bool OnlyOneShare { get; set; }


    #endregion

    //private void FillBy(FundFactors factors, int flowId)
    //{
    //    var sc = factors.ShareClasses[flowId];
    //    var classIds = sc.Select(x => x.Id).ToArray();

    //    // singleton 示例
    //    FullName = new()
    //    {
    //        FlowId = flowId,
    //        ShareId = -1,
    //        FactorId = FactorFields.FullName,
    //        FundId = FundId,
    //        NewValue = CloneHelper.CloneValue(factors.FullName[flowId]),
    //        OldValue = CloneHelper.CloneValue(factors.FullName[flowId]),
    //        FallbackValue = CloneHelper.CloneValue(factors.FullName[flowId - 1]),
    //    };

    //    FullName.Changed += (s, e) =>
    //    {
    //        if (e.Kind is ValueChangeKind.Added or ValueChangeKind.Modified)
    //            SaveChange(e.FundId, FactorFields.FullName, e.FlowId, e.ShareId, e.NewValue);
    //        else if (e.Kind is ValueChangeKind.Deleted)
    //            RemoveFact(e.FundId, FactorFields.FullName, e.FlowId, e.ShareId);
    //    };


    //    // 其它示例
    //    var mfi = factors.ManageFee.GetInheritValues(flowId, classIds);
    //    ManageFee = new ShareFactorViewModel<FundFeeInfo, FundFeeInfoViewModel>(FundId, FlowId, FactorFields.ManageFee, sc, [.. mfi.Select(x => (new FundFeeInfoViewModel(x.Old), new FundFeeInfoViewModel(x.New)))]);


    //    // 写在外面 
    //    void SaveChange<T>(int fundId, string factId, int flowId, int shareId, T data)
    //    {
    //        using var db = DbHelper.Base();
    //        db.GetCollection<IFundFactor>().Upsert(new FundFactor<T>(factId, FundId, FlowId, shareId, data));
    //    }
    //    void RemoveFact(int fundId, string factId, int flowId, int shareId)
    //    {
    //        using var db = DbHelper.Base();
    //        db.GetCollection<IFundFactor>().Delete($"{fundId}.{flowId}.{shareId}.{factId}");
    //    }
    //}

    partial void OnFlowIdChanged(int oldValue, int newValue)
    {
        using var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(FundId);
        var flow = db.GetCollection<FundFlow>().FindById(newValue);
        bool isori = flow is ContractFinalizeFlow;
        var elements = db.GetCollection<FundElements>().FindById(FundId);

        if (elements is null)
            elements = new FundElements { Id = FundId };



        //IFundFactor[] factories = db.GetCollection<IFundFactor>().Query().Where(x => x.FundId == FundId).Where(LiteDB.Query.In(nameof(IFundFactor.FlowId), flowIds.Select(x=> new LiteDB.BsonValue(x)))).ToArray();
        FundFactors facts = db.QueryFactor(Id);

        FillBy(facts, newValue);

        IsSharesInherited = !facts.ShareClasses[newValue].Any(x=> ShareClass.GetFlow(x.Id) == newValue);

        var type = GetType();
        SetupDate = fund.SetupDate;
        var cinfo = elements.ShareClasses.GetValue(newValue);
     
        var sc = cinfo.Value ?? [ShareClass.DefaultShare];


        OnlyOneShare = Shares.Count <= 1;


        //OpenRule = elements.FundOpenRule.GetValue(newValue).Value;




        if (isori)
        {
            if (FullName.NewValue == default)
                FullName.NewValue = fund.Name;

            if (ShortName.NewValue == default)
                ShortName.NewValue = fund.ShortName;
        }

        /// 最大999，认为是永续
        DurationInMonths.Changed += (e) =>
        {
            if (e.NewValue?.Infinity ?? false)
            {
                ExpirationDate?.NewValue = new FundExpireDateViewModel(new(2099, 12, 31));
            }
            else if (e.NewValue?.Month is int d && d > 0)
            {
                ExpirationDate?.NewValue = new FundExpireDateViewModel(SetupDate.AddMonths(d).AddDays(-1));
            }
        };

        ExpirationDate.Changed += e =>
            DataHub.Push(new EntityChanged<FundElements, DateOnly, int>(Id, nameof(FundElements.ExpirationDate), e.OldValue ?? default, e.NewValue ?? default));

        // 开放/封闭切换
        FundModeInfo.Changed += e => OnPropertyChanged(nameof(IsSealingFund));

 

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
    public void SetBankFromClipboard(ModifiableViewModel<BankAccount?, BankAccountInfoViewModel> v)
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
        try
        {
            var wnd = new ModifyShareClassWindow();
            wnd.DataContext = new ModifyShareClassWindowViewModel(FundId, FlowId, Shares.Select(x => new ShareClassViewModel(FlowId, x.Build())).ToArray());
            wnd.Owner = App.Current.MainWindow;
            Window window = Window.GetWindow(panel);
            Point point = panel.TransformToAncestor(window).Transform(new Point(panel.ActualWidth / 2, panel.ActualHeight / 2));

            wnd.Left = window.Left + point.X - wnd.Width / 2;
            wnd.Top = window.Top + point.Y + panel.ActualHeight;

            var r = wnd.ShowDialog();

            if (r ?? false) OnFlowIdChanged(FlowId, FlowId);
        }
        catch (Exception e)
        {
            Logg.Error(e);
            Toast.Warning("出错了");
        }
    }


    [RelayCommand]
    public void ModifyInherit()
    {
        try
        {
            var wnd = new ModifyInheritWindow();
            var context = new ModifyInheritWindowViewModel(FundId);
            wnd.DataContext = context;
            wnd.Owner = App.Current.MainWindow;

            var r = wnd.ShowDialog();

            if(context.Changed)
            {
                using var db = DbHelper.Base();
            }

            if (r ?? false) OnFlowIdChanged(FlowId, FlowId);
        }
        catch (Exception e)
        { 
            Logg.Error(e);
            Toast.Warning("出错了");
        }
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

    //public void InitShare(Mutable<ShareClass[]>? shareClass = null)
    //{
    //    if (shareClass is null)
    //    {
    //        using var db = DbHelper.Base();
    //        shareClass = db.GetCollection<FundElements>().FindById(FundId)?.ShareClasses;
    //    }

    //    if (shareClass is not null && shareClass.GetValue(FlowId).Value is ShareClass[] shares)
    //        Shares = new ObservableCollection<ShareClassViewModel>(shares.Select(x => new ShareClassViewModel { Id = x.Id, Name = x.Name }));
    //    else
    //        throw new Exception(); //Shares = new ObservableCollection<ShareClassViewModel>([new ShareClassViewModel { Id = IdGenerator.GetNextId(nameof(ShareClass)), Name = FundElements.SingleShareKey }]);

    //}

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

    private T? ValueFormat<T>(T d) where T : struct
    {
        return default(T).Equals(d) ? null : d;
    }
    //private DateOnly? ValueFormat(DateOnly d)
    //{
    //    return d == default ? null : d;
    //}



    [RelayCommand]
    protected void Save()
    {
        var ps = GetType().GetProperties();
        foreach (var p in ps)
        {
            if (p.PropertyType.IsAssignableTo(typeof(IValueModifier)) && p.GetValue(this) is IValueModifier v && v.CanConfirm)
                v.Apply();

            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ShareFactorViewModel<,>))
            {
                var obj = p.GetValue(this);
                if (obj is not null)
                {
                    var pi = obj.GetType().GetProperty("Data");
                    if (pi!.GetValue(obj, null) is IEnumerable<IValueModifier> e)
                        foreach (var item in e)
                            item.Apply();
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

    public class Modifier { }
}
