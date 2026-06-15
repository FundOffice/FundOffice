using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;

namespace FMO;

/// <summary>
/// 合同要素对比 ViewModel
/// </summary>
public partial class ContractElementsCompareViewModel : ObservableObject
{
    public ObservableCollection<FactorCompareItem> Items { get; }

    /// <summary>
    /// 解析警告信息
    /// </summary>
    public ObservableCollection<string> Warnings { get; }

    /// <summary>
    /// 是否有警告
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    public ContractElementsCompareViewModel(ReadonlyFundInfo newInfo, ReadonlyFundInfo? oldInfo, IReadOnlyList<string>? warnings = null)
    {
        Items = BuildCompareItems(newInfo, oldInfo);
        Warnings = new ObservableCollection<string>(warnings ?? []);
    }

    private static ObservableCollection<FactorCompareItem> BuildCompareItems(ReadonlyFundInfo newInfo, ReadonlyFundInfo? oldInfo)
    {
        var items = new ObservableCollection<FactorCompareItem>();
        var props = typeof(ReadonlyFundInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.Name is not nameof(ReadonlyFundInfo.Id));

        var jsonOpt = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        foreach (var prop in props)
        {
            var newVal = prop.GetValue(newInfo);
            var oldVal = oldInfo is not null ? prop.GetValue(oldInfo) : null;

            string newDisplay = FormatValue(newVal, jsonOpt);
            string oldDisplay = FormatValue(oldVal, jsonOpt);

            bool hasOld = oldInfo is not null && oldVal is not null;
            bool changed = hasOld && !string.Equals(oldDisplay, newDisplay, StringComparison.Ordinal);

            string displayValue;
            if (changed)
                displayValue = $"{oldDisplay} → {newDisplay}";
            else
                displayValue = newDisplay;

            items.Add(new FactorCompareItem
            {
                Name = GetDisplayName(prop.Name),
                DisplayValue = displayValue,
                HasChanged = changed,
                IsEmpty = string.IsNullOrWhiteSpace(newDisplay) || newDisplay == "null"
            });
        }

        return items;
    }

    private static string FormatValue(object? value, JsonSerializerOptions jsonOpt)
    {
        if (value is null) return "null";
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? "null" : s;
        if (value is bool b) return b ? "是" : "否";
        if (value is DateOnly d) return d == default ? "null" : d.ToString("yyyy-MM-dd");
        if (value is DateTime dt) return dt == default ? "null" : dt.ToString("yyyy-MM-dd HH:mm");
        if (value is decimal dec) return dec.ToString();
        if (value is int i) return i.ToString();
        if (value is double dbl) return dbl.ToString();
        if (value is Enum e) return e.ToString();

        // 数组类型
        if (value is System.Collections.IEnumerable arr and not string)
        {
            var list = new List<string>();
            foreach (var item in arr)
                list.Add(item is null ? "null" : (item is string str ? str : JsonSerializer.Serialize(item, item.GetType(), jsonOpt)));
            return list.Count == 0 ? "null" : string.Join(" | ", list);
        }

        // 复杂对象用 JSON
        return JsonSerializer.Serialize(value, value.GetType(), jsonOpt);
    }

    private static string GetDisplayName(string propName) => propName switch
    {
        nameof(ReadonlyFundInfo.FullName) => "基金全称",
        nameof(ReadonlyFundInfo.ShortName) => "基金简称",
        nameof(ReadonlyFundInfo.SecurityFundType) => "证券基金类型",
        nameof(ReadonlyFundInfo.FundModeInfo) => "运作方式",
        nameof(ReadonlyFundInfo.SealingRule) => "封闭期",
        nameof(ReadonlyFundInfo.RiskLevel) => "风险等级",
        nameof(ReadonlyFundInfo.DurationInMonths) => "存续期",
        nameof(ReadonlyFundInfo.ExpirationDate) => "结束日期",
        nameof(ReadonlyFundInfo.StopLine) => "止损线",
        nameof(ReadonlyFundInfo.WarningLine) => "预警线",
        nameof(ReadonlyFundInfo.HugeRedemption) => "巨额赎回",
        nameof(ReadonlyFundInfo.FundOpenRule) => "开放日规则",
        nameof(ReadonlyFundInfo.TemporarilyOpenInfo) => "临时开放",
        nameof(ReadonlyFundInfo.CollectionAccount) => "募集账户",
        nameof(ReadonlyFundInfo.CustodyAccount) => "托管账户",
        nameof(ReadonlyFundInfo.TrusteeInfo) => "托管机构",
        nameof(ReadonlyFundInfo.OutsourcingInfo) => "外包机构",
        nameof(ReadonlyFundInfo.ManageFeePay) => "管理费支付方式",
        nameof(ReadonlyFundInfo.InvestmentManagers) => "基金经理列表",
        nameof(ReadonlyFundInfo.InvestmentManager) => "基金经理",
        nameof(ReadonlyFundInfo.PerformanceBenchmark) => "业绩比较基准",
        nameof(ReadonlyFundInfo.InvestmentObjective) => "投资目标",
        nameof(ReadonlyFundInfo.InvestmentScope) => "投资范围",
        nameof(ReadonlyFundInfo.InvestmentStrategy) => "投资策略",
        nameof(ReadonlyFundInfo.CoolingPeriod) => "冷静期",
        nameof(ReadonlyFundInfo.Callback) => "回访",
        nameof(ReadonlyFundInfo.PerformanceFeeRule) => "业绩报酬规则",
        nameof(ReadonlyFundInfo.LockingRule) => "锁定期",
        nameof(ReadonlyFundInfo.ManageFee) => "管理费",
        nameof(ReadonlyFundInfo.SubscriptionRule) => "认购规则",
        nameof(ReadonlyFundInfo.PurchasRule) => "申购规则",
        nameof(ReadonlyFundInfo.RedemptionFee) => "赎回费",
        nameof(ReadonlyFundInfo.PerformanceFeeStatement) => "业绩报酬说明",
        nameof(ReadonlyFundInfo.PerformanceFeeStandard) => "业绩报酬标准",
        nameof(ReadonlyFundInfo.StructureInfo) => "结构化信息",
        nameof(ReadonlyFundInfo.SetupDate) => "成立日期",
        nameof(ReadonlyFundInfo.AuditDate) => "备案日期",
        nameof(ReadonlyFundInfo.Code) => "备案号",
        nameof(ReadonlyFundInfo.Url) => "公示网址",
        nameof(ReadonlyFundInfo.Status) => "状态",
        nameof(ReadonlyFundInfo.ManagerProfile) => "管理人简介",
        nameof(ReadonlyFundInfo.ClearDate) => "清算日期",
        nameof(ReadonlyFundInfo.AmacID) => "协会ID",
        nameof(ReadonlyFundInfo.AsAdvisor) => "投资顾问",
        _ => propName
    };
}

/// <summary>
/// 要素对比项
/// </summary>
public class FactorCompareItem
{
    public string Name { get; set; } = "";
    public string DisplayValue { get; set; } = "";
    public bool HasChanged { get; set; }
    public bool IsEmpty { get; set; }
}
