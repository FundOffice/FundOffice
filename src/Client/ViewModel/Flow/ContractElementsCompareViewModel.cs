using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.Collections.ObjectModel;
using System.Text;

namespace FMO;

/// <summary>
/// 合同要素对比 ViewModel — 每个字段一个命名 CompareField&lt;T&gt;，XAML 直接绑定
/// </summary>
public partial class ContractElementsCompareViewModel : ObservableObject
{
    public ObservableCollection<string> Warnings { get; }
    public bool HasWarnings => Warnings.Count > 0;


    public ShareClass[] ShareClasses { get; set; }

    // ==================== 基本要素 ====================
    public CompareField<string?> FullNameField { get; } = new("基金全称");
    public CompareField<string?> ShortNameField { get; } = new("基金简称");
    public CompareField<SecurityFundType?> SecurityFundTypeField { get; } = new("证券基金类型");
    public CompareField<FundModeInfo?> FundModeInfoField { get; } = new("运作方式");
    public CompareField<SealingRule?> SealingRuleField { get; } = new("封闭期");
    public CompareField<RiskLevel?> RiskLevelField { get; } = new("风险等级");
    public CompareField<FundDuration?> DurationInMonthsField { get; } = new("存续期");
    public CompareField<DateOnly?> ExpirationDateField { get; } = new("到期日");
    public CompareField<decimal?> StopLineField { get; } = new("止损线");
    public CompareField<decimal?> WarningLineField { get; } = new("预警线");
    public CompareField<SealingRule?[]> LockingRuleField { get; } = new("锁定期");
    public CompareField<StructureInfo?> StructureInfoField { get; } = new("结构化信息");

    // ==================== 投资相关 ====================
    public CompareField<string?> InvestmentObjectiveField { get; } = new("投资目标");
    public CompareField<string?> InvestmentScopeField { get; } = new("投资范围");
    public CompareField<string?> InvestmentStrategyField { get; } = new("投资策略");
    public CompareField<FundInvestmentManager[]?> InvestmentManagersField { get; } = new("投资经理");
    public CompareField<string?> InvestmentManagerField { get; } = new("投资经理(单人)");
    public CompareField<PerformanceBenchmark?> PerformanceBenchmarkField { get; } = new("业绩比较基准");

    // ==================== 费用相关 ====================
    public CompareField<FundFeeInfo?[]> ManageFeeField { get; } = new("管理费");
    public CompareField<FeePayInfo?> ManageFeePayField { get; } = new("管理费支付方式");
    public CompareField<FundPurchaseRule?[]> SubscriptionRuleField { get; } = new("认购规则");
    public CompareField<FundPurchaseRule?[]> PurchasRuleField { get; } = new("申购规则");
    public CompareField<RedemptionFeeInfo?[]> RedemptionFeeField { get; } = new("赎回费");
    public CompareField<AgencyInfo?> TrusteeInfoField { get; } = new("托管机构");
    public CompareField<AgencyInfo?> OutsourcingInfoField { get; } = new("外包机构");

    // ==================== 业绩报酬 ====================
    public CompareField<string?[]> PerformanceFeeStatementField { get; } = new("业绩报酬说明");
    public CompareField<PerformanceFeeStandard?[]> PerformanceFeeStandardField { get; } = new("业绩报酬标准");
    public CompareField<PerformanceFeeRule?> PerformanceFeeRuleField { get; } = new("业绩报酬规则");

    // ==================== 申赎相关 ====================
    public CompareField<OpenRule[]?[]> FundOpenRuleField { get; } = new("开放日规则");
    public CompareField<TemporarilyOpenInfo?[]> TemporarilyOpenInfoField { get; } = new("临时开放");
    public CompareField<HugeRedemptionRule?> HugeRedemptionField { get; } = new("巨额赎回");
    public CompareField<CoolingPeriodInfo?> CoolingPeriodField { get; } = new("冷静期");
    public CompareField<CallbackInfo?> CallbackField { get; } = new("回访");

    // ==================== 账户信息 ====================
    public CompareField<BankAccount?> CollectionAccountField { get; } = new("募集账户");

    public ContractElementsCompareViewModel(ReadonlyFundInfo newInfo, ReadonlyFundInfo? oldInfo, IReadOnlyList<string>? warnings = null)
    {
        Warnings = new ObservableCollection<string>(warnings ?? []);
        ShareClasses = newInfo.ShareClasses ?? [];

        // 基本要素
        Populate(nameof(ReadonlyFundInfo.FullName), newInfo.FullName, oldInfo?.FullName, FullNameField);
        Populate(nameof(ReadonlyFundInfo.ShortName), newInfo.ShortName, oldInfo?.ShortName, ShortNameField);
        Populate(nameof(ReadonlyFundInfo.SecurityFundType), newInfo.SecurityFundType, oldInfo?.SecurityFundType, SecurityFundTypeField);
        Populate(nameof(ReadonlyFundInfo.FundModeInfo), newInfo.FundModeInfo, oldInfo?.FundModeInfo, FundModeInfoField);
        Populate(nameof(ReadonlyFundInfo.SealingRule), newInfo.SealingRule, oldInfo?.SealingRule, SealingRuleField);
        Populate(nameof(ReadonlyFundInfo.RiskLevel), newInfo.RiskLevel, oldInfo?.RiskLevel, RiskLevelField);
        Populate(nameof(ReadonlyFundInfo.DurationInMonths), newInfo.DurationInMonths, oldInfo?.DurationInMonths, DurationInMonthsField);
        Populate(nameof(ReadonlyFundInfo.ExpirationDate), newInfo.ExpirationDate, oldInfo?.ExpirationDate, ExpirationDateField);
        Populate(nameof(ReadonlyFundInfo.StopLine), newInfo.StopLine, oldInfo?.StopLine, StopLineField);
        Populate(nameof(ReadonlyFundInfo.WarningLine), newInfo.WarningLine, oldInfo?.WarningLine, WarningLineField);
        Populate(nameof(ReadonlyFundInfo.LockingRule), newInfo.LockingRule, oldInfo?.LockingRule, LockingRuleField);
        Populate(nameof(ReadonlyFundInfo.StructureInfo), newInfo.StructureInfo, oldInfo?.StructureInfo, StructureInfoField);

        // 投资相关
        Populate(nameof(ReadonlyFundInfo.InvestmentObjective), newInfo.InvestmentObjective, oldInfo?.InvestmentObjective, InvestmentObjectiveField);
        Populate(nameof(ReadonlyFundInfo.InvestmentScope), newInfo.InvestmentScope, oldInfo?.InvestmentScope, InvestmentScopeField);
        Populate(nameof(ReadonlyFundInfo.InvestmentStrategy), newInfo.InvestmentStrategy, oldInfo?.InvestmentStrategy, InvestmentStrategyField);
        Populate(nameof(ReadonlyFundInfo.InvestmentManagers), newInfo.InvestmentManagers, oldInfo?.InvestmentManagers, InvestmentManagersField);
        Populate(nameof(ReadonlyFundInfo.InvestmentManager), newInfo.InvestmentManager, oldInfo?.InvestmentManager, InvestmentManagerField);
        Populate(nameof(ReadonlyFundInfo.PerformanceBenchmark), newInfo.PerformanceBenchmark, oldInfo?.PerformanceBenchmark, PerformanceBenchmarkField);

        // 费用相关
        Populate(nameof(ReadonlyFundInfo.ManageFee), newInfo.ManageFee, oldInfo?.ManageFee, ManageFeeField);
        Populate(nameof(ReadonlyFundInfo.ManageFeePay), newInfo.ManageFeePay, oldInfo?.ManageFeePay, ManageFeePayField);
        Populate(nameof(ReadonlyFundInfo.SubscriptionRule), newInfo.SubscriptionRule, oldInfo?.SubscriptionRule, SubscriptionRuleField);
        Populate(nameof(ReadonlyFundInfo.PurchasRule), newInfo.PurchasRule, oldInfo?.PurchasRule, PurchasRuleField);
        Populate(nameof(ReadonlyFundInfo.RedemptionFee), newInfo.RedemptionFee, oldInfo?.RedemptionFee, RedemptionFeeField);
        Populate(nameof(ReadonlyFundInfo.TrusteeInfo), newInfo.TrusteeInfo, oldInfo?.TrusteeInfo, TrusteeInfoField);
        Populate(nameof(ReadonlyFundInfo.OutsourcingInfo), newInfo.OutsourcingInfo, oldInfo?.OutsourcingInfo, OutsourcingInfoField);

        // 业绩报酬
        Populate(nameof(ReadonlyFundInfo.PerformanceFeeStatement), newInfo.PerformanceFeeStatement, oldInfo?.PerformanceFeeStatement, PerformanceFeeStatementField);
        Populate(nameof(ReadonlyFundInfo.PerformanceFeeStandard), newInfo.PerformanceFeeStandard, oldInfo?.PerformanceFeeStandard, PerformanceFeeStandardField);
        Populate(nameof(ReadonlyFundInfo.PerformanceFeeRule), newInfo.PerformanceFeeRule, oldInfo?.PerformanceFeeRule, PerformanceFeeRuleField);

        // 申赎相关
        Populate(nameof(ReadonlyFundInfo.FundOpenRule), newInfo.FundOpenRule, oldInfo?.FundOpenRule, FundOpenRuleField);
        Populate(nameof(ReadonlyFundInfo.TemporarilyOpenInfo), newInfo.TemporarilyOpenInfo, oldInfo?.TemporarilyOpenInfo, TemporarilyOpenInfoField);
        Populate(nameof(ReadonlyFundInfo.HugeRedemption), newInfo.HugeRedemption, oldInfo?.HugeRedemption, HugeRedemptionField);
        Populate(nameof(ReadonlyFundInfo.CoolingPeriod), newInfo.CoolingPeriod, oldInfo?.CoolingPeriod, CoolingPeriodField);
        Populate(nameof(ReadonlyFundInfo.Callback), newInfo.Callback, oldInfo?.Callback, CallbackField);

        // 账户信息
        Populate(nameof(ReadonlyFundInfo.CollectionAccount), newInfo.CollectionAccount, oldInfo?.CollectionAccount, CollectionAccountField);
    }

    private void Populate<T>(string propName, T? newVal, T? oldVal, CompareField<T> field)
    {
        field.NewValue = newVal;
        field.OldValue = oldVal;
        field.NewDisplay = FormatPropertyValue(propName, newVal, ShareClasses);
        field.OldDisplay = oldVal is not null ? FormatPropertyValue(propName, oldVal, ShareClasses) : "";
        field.HasChanged = oldVal is not null
            && field.OldDisplay is not "" and not "-"
            && !string.Equals(field.OldDisplay, field.NewDisplay, System.StringComparison.Ordinal);
    }

    

    #region ShareClass names

    /// <summary>
    /// 读取份额名称（兼容 ShareClasses / ShareClass 属性名，以及 string[] / ShareClass[] / 任意带 Name 属性的集合）
    /// </summary>
    private static string[] GetShareClassNames(ReadonlyFundInfo info)
    {
        var prop = typeof(ReadonlyFundInfo).GetProperty("ShareClasses")
                ?? typeof(ReadonlyFundInfo).GetProperty("ShareClass");
        if (prop is null) return [];

        var value = prop.GetValue(info);
        if (value is null) return [];

        if (value is string[] names)
            return names.Select(NormalizeShareName).ToArray();

        if (value is System.Collections.IEnumerable enumerable)
            return enumerable.Cast<object?>().Select(NormalizeShareName).ToArray();

        return [];
    }

    private static string NormalizeShareName(object? item)
    {
        if (item is null) return "未命名";
        if (item is string s) return string.IsNullOrWhiteSpace(s) ? "未命名" : s;

        var name = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString()
                ?? item.GetType().GetProperty("ClassName")?.GetValue(item)?.ToString()
                ?? item.ToString();
        return string.IsNullOrWhiteSpace(name) ? "未命名" : name;
    }

    #endregion

    #region Formatting

    private static string FormatPropertyValue(string propName, object? value, ShareClass[] shareClasses)
    {
        if (value is null) return "-";

        if (propName == nameof(ReadonlyFundInfo.FundOpenRule) && value is OpenRule[]?[] openRules)
            return FormatFundOpenRule(openRules, shareClasses);

        if (value is Array arr)
        {
            if (IsPerShareProperty(propName))
                return FormatShareArray(arr, shareClasses);

            // 非按份额数组：直接列出元素
            if (arr.Length == 0) return "-";
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FormatValue(arr.GetValue(i)));
            }
            return sb.ToString();
        }

        return FormatValue(value);
    }

    /// <summary>判断是否为按份额的数组属性</summary>
    private static bool IsPerShareProperty(string propName) => propName switch
    {
        nameof(ReadonlyFundInfo.TemporarilyOpenInfo) or
        nameof(ReadonlyFundInfo.LockingRule) or
        nameof(ReadonlyFundInfo.SubscriptionRule) or
        nameof(ReadonlyFundInfo.PurchasRule) or
        nameof(ReadonlyFundInfo.ManageFee) or
        nameof(ReadonlyFundInfo.RedemptionFee) or
        nameof(ReadonlyFundInfo.PerformanceFeeStatement) or
        nameof(ReadonlyFundInfo.PerformanceFeeStandard) => true,
        _ => false
    };

    /// <summary>
    /// 按份额数组格式化：所有份额相同 → 与单份额一样显示；不同 → 份额名: 值
    /// </summary>
    private static string FormatShareArray(Array values, ShareClass[] shareClasses)
    {
        if (values.Length == 0) return "-";

        var first = values.GetValue(0);
        bool allSame = true;
        for (int i = 1; i < values.Length; i++)
        {
            if (!Equals(values.GetValue(i), first))
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
            return FormatValue(first);

        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            string shareName = i < shareClasses.Length ? shareClasses[i].Name : $"份额{i + 1}";
            sb.Append(shareName).Append(": ").Append(FormatValue(values.GetValue(i)));
        }
        return sb.ToString();
    }

    /// <summary>FundOpenRule 是 OpenRule[]?[]，特殊处理</summary>
    private static string FormatFundOpenRule(OpenRule[]?[] values, ShareClass[] shareClasses)
    {
        if (values.Length == 0) return "-";

        // 如果所有非空内层数组都相同（或全空），按单份额显示
        OpenRule[]? first = values[0];
        bool allSame = true;
        for (int i = 1; i < values.Length; i++)
        {
            if (!OpenRuleArraysEqual(values[i], first))
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
            return FormatOpenRuleArray(first);

        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            string shareName = i < shareClasses.Length ? shareClasses[i].Name : $"份额{i + 1}";
            sb.Append(shareName).Append(':').Append('\n');
            sb.Append(FormatOpenRuleArray(values[i]));
        }
        while (sb.Length > 0 && sb[^1] == '\n') sb.Length--;
        return sb.ToString();
    }

    private static bool OpenRuleArraysEqual(OpenRule[]? a, OpenRule[]? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!Equals(a[i], b[i])) return false;
        return true;
    }

    private static string FormatOpenRuleArray(OpenRule[]? rules)
    {
        if (rules is null || rules.Length == 0) return "  -";
        var sb = new StringBuilder();
        for (int i = 0; i < rules.Length; i++)
        {
            sb.Append("  ").Append(FormatValue(rules[i]));
            if (i < rules.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return "-";
        if (value is Enum e) return GetEnumDescription(e);
        if (value is bool b) return b ? "是" : "否";
        if (value is DateOnly d) return d == default ? "-" : d.ToString("yyyy-MM-dd");
        if (value is DateTime dt) return dt == default ? "-" : dt.ToString("yyyy-MM-dd HH:mm");
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? "-" : s;
        if (value is decimal or int or double or float or long or byte or short)
            return value.ToString() ?? "-";

        if (value is Array arr)
        {
            if (arr.Length == 0) return "-";
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FormatValue(arr.GetValue(i)));
            }
            return sb.ToString();
        }

        return FormatModel(value);
    }

    private static string FormatModel(object? value)
    {
        if (value is null) return "-";
        var type = value.GetType();
        string ts = value.ToString() ?? "";
        if (!string.IsNullOrEmpty(ts) && ts != type.FullName && ts != type.ToString())
            return ts;

        var parts = new System.Collections.Generic.List<string>();
        foreach (var prop in type.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0 || !prop.CanRead) continue;
            try
            {
                var val = prop.GetValue(value);
                string display = FormatValue(val);
                if (display is not "-")
                    parts.Add($"{prop.Name}={display}");
            }
            catch { }
        }
        return parts.Count == 0 ? ts : string.Join(", ", parts);
    }

    private static string GetEnumDescription(Enum e)
    {
        var fi = e.GetType().GetField(e.ToString());
        var desc = fi is not null
            ? System.Attribute.GetCustomAttribute(fi, typeof(System.ComponentModel.DescriptionAttribute))
                as System.ComponentModel.DescriptionAttribute
            : null;
        return desc?.Description ?? e.ToString();
    }

    #endregion
}

/// <summary>单个对比字段 — 泛型，保留原始类型</summary>
public class CompareField<T> : ObservableObject
{
    public string Name { get; }

    private T? _newValue;
    public T? NewValue { get => _newValue; set => SetProperty(ref _newValue, value); }

    private T? _oldValue;
    public T? OldValue { get => _oldValue; set => SetProperty(ref _oldValue, value); }

    private string _newDisplay = "";
    public string NewDisplay { get => _newDisplay; internal set => SetProperty(ref _newDisplay, value); }

    private string _oldDisplay = "";
    public string OldDisplay { get => _oldDisplay; internal set => SetProperty(ref _oldDisplay, value); }

    private bool _hasChanged;
    public bool HasChanged { get => _hasChanged; internal set => SetProperty(ref _hasChanged, value); }

    public CompareField(string name) { Name = name; }
}
