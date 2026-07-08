using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.AI;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using MoT;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// 正在 AI 解析合同要素
    /// </summary>
    [ObservableProperty]
    public partial bool IsParsingContract { get; set; }

    /// <summary>
    /// AI 解析进度状态文字
    /// </summary>
    [ObservableProperty]
    public partial string ParseStatus { get; set; } = "";

    /// <summary>
    /// AI 解析已接收的 token 数
    /// </summary>
    [ObservableProperty]
    public partial int ParsedTokenCount { get; set; }

    /// <summary>
    /// 当前合同文件关联的 AI 解析历史记录
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ContractParseHistory> ParseHistories { get; set; } = new();

    /// <summary>
    /// 是否有可显示的历史解析记录
    /// </summary>
    [ObservableProperty]
    public partial bool HasParseHistories { get; set; }

    /// <summary>
    /// 当前是否满足显示历史解析记录的条件（有历史且处于编辑模式）
    /// </summary>
    [ObservableProperty]
    public partial bool CanShowParseHistories { get; set; }

    /// <summary>
    /// 用户选中的历史解析记录
    /// </summary>
    [ObservableProperty]
    public partial ContractParseHistory? SelectedParseHistory { get; set; }

    /// <summary>
    /// 是否已选中一条历史解析记录
    /// </summary>
    [ObservableProperty]
    public partial bool HasSelectedParseHistory { get; set; }

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
    public partial FactorModifiableViewModel<PerformanceFeeRule?, PerformanceFeeRuleViewModel> PerformanceFeeRule { get; set; } = null!;

    [ObservableProperty]
    public partial ShareFactorViewModel<PerformanceFeeStandard?, PerformanceFeeStandardViewModel> PerformanceFeeStandard { get; set; } = null!;


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
    public string? FundCode { get; private set; }



    /// <summary>
    /// 是否有任何份额收取业绩报酬
    /// </summary>
    public bool HasPerformanceFeeStandard => PerformanceFeeStandard?.Data.Any(d => d.NewValue?.Has == true) ?? false;

    partial void OnPerformanceFeeStandardChanged(ShareFactorViewModel<PerformanceFeeStandard?, PerformanceFeeStandardViewModel> value)
    {
        if (value?.Data is not null)
        {
            value.Data.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                if (e.NewItems is not null)
                    foreach (FactorModifiableViewModel<PerformanceFeeStandard?, PerformanceFeeStandardViewModel> d in e.NewItems)
                    {
                        // 1. 订阅 NewValue 内部属性变化（Has）
                        if (d.NewValue is INotifyPropertyChanged npc)
                            npc.PropertyChanged += (s, e) =>
                            {
                                OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                            };
                        // 2. 订阅 NewValue 本身被替换的情况（如 Reset）
                        d.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(d.NewValue))
                            {
                                OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                                // NewValue 被替换，重新订阅新实例
                                if (d.NewValue is INotifyPropertyChanged newNpc)
                                    newNpc.PropertyChanged += (s, e) =>
                                    {
                                        OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                                    };
                            }
                        };
                    }
            };
            foreach (var d in value.Data)
            {
                if (d.NewValue is INotifyPropertyChanged npc)
                    npc.PropertyChanged += (s, e) =>
                    {
                        OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                    };
                d.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(d.NewValue))
                    {
                        OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                        // NewValue 被替换，重新订阅新实例
                        if (d.NewValue is INotifyPropertyChanged newNpc)
                            newNpc.PropertyChanged += (s, e) =>
                            {
                                OnPropertyChanged(nameof(HasPerformanceFeeStandard));
                            };
                    }
                };
            }
        }
    }
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
        FundCode = fund.Code;
        var flow = db.GetCollection<FundFlow>().FindById(newValue);
        bool isori = flow is ContractFinalizeFlow;

        var fileHash = (flow as ContractFlow)?.ContractFile?.File?.Hash;
        if (!string.IsNullOrWhiteSpace(fileHash))
            LoadParseHistories(fileHash);
        else
            ParseHistories.Clear();

        //IFundFactor[] factories = db.GetCollection<IFundFactor>().Query().Where(x => x.FundId == FundId).Where(LiteDB.Query.In(nameof(IFundFactor.FlowId), flowIds.Select(x=> new LiteDB.BsonValue(x)))).ToArray();
        FundFactors factors = db.QueryFactor(Id);

        // 检查份额
        ShareClass[] shares = factors.ShareClasses[newValue];

        FillBy(factors, newValue);

        IsSharesInherited = !shares.Any(x => ShareClass.GetFlow(x.Id) == newValue);

        var type = GetType();
        SetupDate = fund.SetupDate;


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
            wnd.DataContext = new ModifyShareClassWindowViewModel(FundId, FundCode ?? "SSSUNK", FlowId, Shares.Select(x => new ShareClassViewModel(FlowId, x.Build())).ToArray());
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
            if (string.IsNullOrWhiteSpace(FullName.OldValue))
            {
                Toast.Warning("请先设置基金全称");
                return;
            }

            var wnd = new ModifyInheritWindow();
            var context = new ModifyInheritWindowViewModel(FundId, FullName.OldValue, FlowId);
            wnd.DataContext = context;
            wnd.Owner = App.Current.MainWindow;

            var r = wnd.ShowDialog();

            if (context.Changed)
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
            //if (string.IsNullOrWhiteSpace(OpenDayInfo!.NewValue))
            //    OpenDayInfo.NewValue = OpenRule.ToString();
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


    [RelayCommand]
    protected void CancelAll()
    {
        var ps = GetType().GetProperties();
        foreach (var p in ps)
        {
            if (p.PropertyType.IsAssignableTo(typeof(IValueModifier)) && p.GetValue(this) is IValueModifier v && v.CanConfirm)
                v.Reset();

            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ShareFactorViewModel<,>))
            {
                var obj = p.GetValue(this);
                if (obj is not null)
                {
                    var pi = obj.GetType().GetProperty("Data");
                    if (pi!.GetValue(obj, null) is IEnumerable<IValueModifier> e)
                        foreach (var item in e)
                            item.Reset();
                }
            }
        }
        WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Collection));
        WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, FundAccountType.Custody));
    }

    [RelayCommand]
    public async Task ParseContractElements()
    {
        FileMeta? meta;
        using (var db = DbHelper.Base())
        {
            var flow = db.GetCollection<FundFlow>().FindById(FlowId) as ContractFlow;
            meta = flow?.ContractFile?.File;
        }

        if (meta?.Exists is not true)
        {
            Toast.Warning("无合同文件或文件已删除");
            return;
        }

        TokenProviderConfig? config;
        using (var db = DbHelper.Base())
        {
            config = db.GetCollection<TokenProviderConfig>().Query().ToEnumerable()
                .Where(x => !string.IsNullOrWhiteSpace(x.Name)
                    && !string.IsNullOrWhiteSpace(x.BaseUrl)
                    && !string.IsNullOrWhiteSpace(x.ApiKey)
                    && !string.IsNullOrWhiteSpace(x.Model))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();

            if (config is null)
            {
                Toast.Warning("没有可用的 AI 提供商，请在平台设置中配置完整的提供商（地址、密钥、模型）");
                return;
            }
        }

        Toast.Info($"正在 AI [{config.Name}] 解析合同要素...");
        IsParsingContract = true;
        ParsedTokenCount = 0;
        ParseStatus = "发送中...";

        var progress = new Progress<int>(count =>
        {
            ParsedTokenCount = count;
            ParseStatus = $"接收中... {count} tokens";
        });

        try
        {
            var parser = new FundDocxAIParser(config.CreateAdapter());
            ParseStatus = "等待响应...";
            var result = await parser.ParseAsync(meta.GetFullPath(), progress);
            ParseStatus = "解析完成";

            if (result is null) return;

            if (result.Factors.Length > 0)
            {
                using var db = DbHelper.Base();
                db.GetCollection<ContractParseHistory>().Insert(new ContractParseHistory
                {
                    FileHash = meta.Hash,
                    ParsedAt = DateTime.Now,
                    FundInfoJson = result.Json,
                    Provider = config.Name
                });
                LoadParseHistories(meta.Hash);
            }

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyParsedFactors(result.Factors);
                if (result.Warnings.Count > 0)
                    Toast.Warning($"以下字段解析可能不准确：\n{string.Join("\n", result.Warnings)}");
            });
        }
        catch (HttpRequestException ex)
        {
            Logg.Error($"AI 解析合同要素网络错误: {ex}");
            Toast.Error($"网络请求失败：{ex.Message}，请检查网络连接或 AI 服务地址");
        }
        catch (TaskCanceledException)
        {
            Logg.Error("AI 解析合同要素超时");
            Toast.Error("AI 请求超时（5分钟），请检查网络连接或稍后重试");
        }
        catch (System.IO.InvalidDataException ex)
        {
            Logg.Error(ex, $"AI 配置错误");
            Toast.Error($"AI 配置错误：{ex.Message}");
        }
        catch (Exception ex)
        {
            Logg.Error(ex, $"AI 解析合同要素失败");
            Toast.Error($"AI 解析失败: {ex.Message}");
        }
        finally
        {
            IsParsingContract = false;
            ParseStatus = "";
        }
    }

    private void LoadParseHistories(string fileHash)
    {
        using var db = DbHelper.Base();
        var list = db.GetCollection<ContractParseHistory>()
            .Query()
            .Where(x => x.FileHash == fileHash)
            .OrderByDescending(x => x.ParsedAt)
            .ToList();

        ParseHistories.Clear();
        foreach (var item in list)
            ParseHistories.Add(item);

        HasParseHistories = ParseHistories.Count > 0;
        CanShowParseHistories = HasParseHistories && !IsReadOnly;
    }

    partial void OnIsReadOnlyChanged(bool oldValue, bool newValue)
    {
        CanShowParseHistories = HasParseHistories && !newValue;
        if (newValue)
            SelectedParseHistory = null;
    }

    partial void OnSelectedParseHistoryChanged(ContractParseHistory? oldValue, ContractParseHistory? newValue)
    {
        HasSelectedParseHistory = newValue is not null;
    }

    [RelayCommand]
    private void ApplySelectedParseHistory()
    {
        if (SelectedParseHistory is null) return;

        var factors = JsonToFactors(SelectedParseHistory.FundInfoJson);
        if (factors is null || factors.Length == 0)
        {
            Toast.Warning("该历史记录无法解析出有效要素");
            return;
        }

        ApplyParsedFactors(factors);
        Toast.Success("已应用选中的历史解析结果");
    }

    [RelayCommand]
    private void ViewParseJson(ContractParseHistory? history)
    {
        if (history is null) return;
        var wnd = new ContractParseJsonViewWindow(history) { Owner = App.Current.MainWindow };
        wnd.Show();
    }

    private static IFundFactor[]? JsonToFactors(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<AiParsedFundInfo>(json, FundDocxAIParser.JsonOptions);
            if (dto is null) return null;
            return AiParsedFundInfoConverter.ToFactors(dto);
        }
        catch (Exception ex)
        {
            Logg.Warning($"反序列化历史解析记录失败: {ex.Message}");
            return null;
        }
    }

    private static void ApplyToSingleton<T>(FactorModifiableViewModel<T>? vm, IFundFactor? factor)
    {
        if (vm is null) return;
        try
        {
            if (factor is FundFactor<T> f) vm.NewValue = f.Data;
        }
        catch (Exception ex)
        {
            Logg.Warning($"写入 {vm.FactorId} 失败: {ex.Message}");
        }
    }

    private static void ApplyToSingleton<T, TVm>(FactorModifiableViewModel<T?, TVm>? vm, IFundFactor? factor)
        where TVm : IViewModel<T?, TVm>
    {
        if (vm is null) return;
        try
        {
            if (factor is FundFactor<T> f) vm.NewValue = TVm.Trans(f.Data);
        }
        catch (Exception ex)
        {
            Logg.Warning($"写入 {vm.FactorId} 失败: {ex.Message}");
        }
    }

    private static void ApplyShareFactors<T>(ShareFactorViewModel<T>? vm, IEnumerable<IFundFactor> factors)
    {
        if (vm is null) return;
        try
        {
            var list = factors.OfType<FundFactor<T>>().ToList();
            if (list.Count == 0) return;

            var data = vm.Data.ToList();
            if (list.Count == 1 && list[0].ShareId == ShareClass.Singleton)
            {
                foreach (var item in data)
                    item.NewValue = list[0].Data;
                return;
            }

            var used = new HashSet<int>();
            foreach (var item in data)
            {
                var match = list.FirstOrDefault(f =>
                    f.ShareId != ShareClass.Singleton &&
                    ShareNamesEqual(GetShareName(vm.Classes, f.ShareId), item.ShareName) &&
                    !used.Contains(f.GetHashCode()));
                if (match is not null)
                {
                    item.NewValue = match.Data;
                    used.Add(match.GetHashCode());
                }
            }
            for (int i = 0, j = 0; i < data.Count && j < list.Count; i++, j++)
            {
                if (!used.Contains(list[j].GetHashCode()))
                    data[i].NewValue = list[j].Data;
            }
        }
        catch (Exception ex)
        {
            Logg.Warning($"写入 {vm.FactorId} 失败: {ex.Message}");
        }
    }

    private static void ApplyShareFactors<T, TVm>(ShareFactorViewModel<T?, TVm>? vm, IEnumerable<IFundFactor> factors)
        where TVm : IViewModel<T?, TVm>
    {
        if (vm is null) return;
        try
        {
            var list = factors.OfType<FundFactor<T>>().ToList();
            if (list.Count == 0) return;

            var data = vm.Data.ToList();
            if (list.Count == 1 && list[0].ShareId == ShareClass.Singleton)
            {
                foreach (var item in data)
                    item.NewValue = TVm.Trans(list[0].Data);
                return;
            }

            var used = new HashSet<int>();
            foreach (var item in data)
            {
                var match = list.FirstOrDefault(f =>
                    f.ShareId != ShareClass.Singleton &&
                    ShareNamesEqual(GetShareName(vm.Classes, f.ShareId), item.ShareName) &&
                    !used.Contains(f.GetHashCode()));
                if (match is not null)
                {
                    item.NewValue = TVm.Trans(match.Data);
                    used.Add(match.GetHashCode());
                }
            }
            for (int i = 0, j = 0; i < data.Count && j < list.Count; i++, j++)
            {
                if (!used.Contains(list[j].GetHashCode()))
                    data[i].NewValue = TVm.Trans(list[j].Data);
            }
        }
        catch (Exception ex)
        {
            Logg.Warning($"写入 {vm.FactorId} 失败: {ex.Message}");
        }
    }

    private static string? GetShareName(ShareClass[] classes, int shareId)
        => classes.FirstOrDefault(c => c.Id == shareId)?.Name;

    private static bool ShareNamesEqual(string? name1, string? name2)
    {
        if (name1 is null || name2 is null) return false;
        var a = name1.Trim();
        var b = name2.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        // 兼容 "A" 与 "A类" / "A類"，仅去掉末尾的类/類 后缀
        if (a.EndsWith("类") || a.EndsWith("類"))
            a = a[..^1];
        if (b.EndsWith("类") || b.EndsWith("類"))
            b = b[..^1];
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }


    [RelayCommand]
    public void GenerateBrochure()
    {
        try
        {
            // 读取资源
            var frame = @"files\brochure\.frame";
            if (!File.Exists(frame))
            {
                Toast.Warning("框架资源丢失");
                return;
            }

            using var sr = new StreamReader(frame);
            var html = sr.ReadToEnd();

            // 写json
            using var db = DbHelper.Base();
            var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
            using var ms = new MemoryStream();
            if (db.FileStorage.Exists("icon.main"))
                db.FileStorage.Download("icon.main", ms);
            var logo = ms.ToArray();

            var fund = db.GetCollection<Fund>().FindById(FundId);
            var factors = db.QueryFactor(FundId);

            // 获取投资经理
            List<BrochureInvestManager> investManagers = [];
            var flowDate = db.GetCollection<FundFlow>().FindById(FlowId)?.Date ?? fund.SetupDate;
            var ims = db.GetCollection<FundInvestmentManager>().Query().Where(x => x.FundId == FundId && x.End.DayNumber >= flowDate.DayNumber).ToArray();
            foreach (var nn in ims)
            {
                using var ps = new MemoryStream();
                Participant participant = db.GetCollection<Participant>().FindById(nn.PersonId);

                if (participant is not null)
                {
                    if (string.IsNullOrWhiteSpace(nn.Profile))
                        nn.Profile = participant.Profile;

                    if (db.FileStorage.Exists($"Photo.Participant.{participant.Id}"))
                        db.FileStorage.Download($"Photo.Participant.{participant.Id}", ps);
                }
                var ddd = db.FileStorage.FindAll().ToArray();
                investManagers.Add(new BrochureInvestManager(nn.Name, nn.Profile ?? "", ps.ToArray()));
            }


            var bro = BrochureFactor.Create(manager, logo, [], [.. investManagers], fund, factors, FlowId);

            ///json
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }
            ;

            var json = JsonSerializer.Serialize(bro, jsonOptions);

            html = html.Replace("###DATA###", json);

            // 读取模板 <div>...</div>
            var templateFiles = new DirectoryInfo(@"files\brochure").GetFiles("*.html")
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // 获取所有模板文件（按文件名排序，保证索引稳定）
            var listItemHtmlSb = new StringBuilder();       // 左侧文件名列表HTML
            var templateContentList = new List<string>();   // 模板内容集合

            foreach (var file in templateFiles)
            {
                // 读取单个模板内容
                string tplContent = File.ReadAllText(file.FullName, Encoding.UTF8);
                templateContentList.Add(tplContent);

                // 生成左侧列表项：显示【文件名】 
                listItemHtmlSb.AppendLine($"<div class=\"list-item\">{Path.GetFileNameWithoutExtension(file.Name)}</div>");
            }

            // 替换占位符1：###LIST### 左侧模板名称列表
            html = html.Replace("###LIST###", listItemHtmlSb.ToString());

            // 4. 拼接 JS 数组字符串（重点：转义HTML，防止JS语法错误）
            string templateArrayJs = JsonSerializer.Serialize(templateContentList, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            html = html.Replace("###TEMPLATE_ARR###", templateArrayJs);



            var fileInfo = new FileInfo(@$"temp\{FundId}\brochure.html");
            if (!fileInfo.Directory!.Exists)
                fileInfo.Directory.Create();

            using var sw = new StreamWriter(fileInfo.FullName);
            sw.Write(html);
            sw.Flush();


            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = fileInfo.FullName, UseShellExecute = true });

        }
        catch (Exception e)
        {
            Logg.Error(e);
            Toast.Warning(e.Message);
        }
    }



    public void Receive(ElementChangedBackgroundMessage message)
    {
        //if (message.FundId == FundId && message.FlowId == FlowId)
        OnFlowIdChanged(FlowId, FlowId);
    }

}
