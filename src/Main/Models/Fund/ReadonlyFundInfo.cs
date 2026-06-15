#nullable enable
using System.Collections.Immutable;

namespace FMO.Models;

/// <summary>
/// 包含全部基金信息的聚合类
/// </summary>
public partial class ReadonlyFundInfo
{
    public int Id { get; set; }

    /// <summary>
    /// 管理人名称
    /// </summary>
    //public required string ManagerName { get; set; }

    /// <summary>
    /// 管理人名称
    /// </summary>
    //public string? ManagerEnglishName { get; set; }

    /// <summary>
    /// 管理人备案号
    /// </summary>
    //public required string ManagerAmacCode { get; set; }

    /// <summary>
    /// 管理人简介
    /// </summary>
    public string? ManagerProfile { get; set; }

    /// <summary>
    /// 成立日期 yyyy-MM-dd
    /// </summary>
    public DateOnly SetupDate { get; set; }

    /// <summary>
    /// 备案日期  yyyy-MM-dd
    /// </summary>
    public DateOnly AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 最新更新日期
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// 清算日期
    /// </summary>
    public DateOnly ClearDate { get; set; }

    /// <summary>
    /// 在协会的id
    /// </summary>
    public string? AmacID { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FundStatus Status { get; set; }

    /// <summary>
    /// 是否作为投资顾问
    /// </summary>
    public bool AsAdvisor { get; set; }

    /// <summary>
    /// 公示信息同步时间
    /// </summary>
    public DateTime PublicDisclosureSynchronizeTime { get; set; }

    /// <summary>
    /// 备案系统同步时间
    /// </summary>
    public DateTime AmbersSynchronizeTime { get; set; }

    // ──────────────────── 以下属性来自 FundFactors.Property.cs，任何修改需同步修改本文件 ────────────────────

    public string? FullName { get; set; }
    public string? ShortName { get; set; }

    public ShareClass[]? ShareClasses { get; set; }

    public SecurityFundType? SecurityFundType { get; set; }
    public FundModeInfo? FundModeInfo { get; set; }
    public SealingRule? SealingRule { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public FundDuration? DurationInMonths { get; set; }
    public global::System.DateOnly? ExpirationDate { get; set; }
    public StructureInfo? StructureInfo { get; set; }
    public BankAccount? CollectionAccount { get; set; }
    public BankAccount? CustodyAccount { get; set; }
    public decimal? StopLine { get; set; }
    public decimal? WarningLine { get; set; }
    public HugeRedemptionRule? HugeRedemption { get; set; }
    /// <summary>FundOpenRule（无数据返回空数组）</summary>
    public OpenRule[]?[] FundOpenRule { get; set; }
    /// <summary>TemporarilyOpenInfo（无数据返回空数组）</summary>
    public TemporarilyOpenInfo?[] TemporarilyOpenInfo { get; set; }
    public CoolingPeriodInfo? CoolingPeriod { get; set; }
    public CallbackInfo? Callback { get; set; }
    /// <summary>LockingRule（无数据返回空数组）</summary>
    public SealingRule?[] LockingRule { get; set; }
    /// <summary>SubscriptionRule（无数据返回空数组）</summary>
    public FundPurchaseRule?[] SubscriptionRule { get; set; }
    /// <summary>PurchasRule（无数据返回空数组）</summary>
    public FundPurchaseRule?[] PurchasRule { get; set; }
    public AgencyInfo? TrusteeInfo { get; set; }
    public AgencyInfo? OutsourcingInfo { get; set; }
    public FeePayInfo? ManageFeePay { get; set; }
    /// <summary>ManageFee（无数据返回空数组）</summary>
    public FundFeeInfo?[] ManageFee { get; set; }
    /// <summary>RedemptionFee（无数据返回空数组）</summary>
    public RedemptionFeeInfo?[] RedemptionFee { get; set; }
    /// <summary>PerformanceFeeStatement（无数据返回空数组）</summary>
    public string?[] PerformanceFeeStatement { get; set; }
    public PerformanceFeeRule? PerformanceFeeRule { get; set; }
    /// <summary>PerformanceFeeStandard（无数据返回空数组）</summary>
    public PerformanceFeeStandard?[] PerformanceFeeStandard { get; set; }
    public FundInvestmentManager[]? InvestmentManagers { get; set; }
    public string? InvestmentManager { get; set; }
    public PerformanceBenchmark? PerformanceBenchmark { get; set; }
    public string? InvestmentObjective { get; set; }
    public string? InvestmentScope { get; set; }
    public string? InvestmentStrategy { get; set; }

    public void FillBy(IFundFactor[] val)
    {
        if (val is null) return;

        var g = val
            .Where(x => x.FactorId is not null)
            .OrderByDescending(x => x.FlowId)
            .ThenBy(x => x.ShareId)
            .GroupBy(x => x.FactorId)
            .ToDictionary(x => x.Key, x => x.AsEnumerable());

        if (g.TryGetValue(FactorFields.FullName, out var _s0))
        {
            var _f = _s0.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                FullName = _f.Data;
        }

        if (g.TryGetValue(FactorFields.ShortName, out var _s1))
        {
            var _f = _s1.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                ShortName = _f.Data;
        }

        if (g.TryGetValue(FactorFields.ShareClasses, out var _sc))
        {
            var _f = _sc.OfType<FundFactor<ShareClass[]>>().FirstOrDefault();
            if (_f != null)
                ShareClasses = _f.Data;
        }

        if (g.TryGetValue(FactorFields.SecurityFundType, out var _s2))
        {
            var _f = _s2.OfType<FundFactor<SecurityFundType>>().FirstOrDefault();
            if (_f != null)
                SecurityFundType = _f.Data;
        }

        if (g.TryGetValue(FactorFields.FundModeInfo, out var _s3))
        {
            var _f = _s3.OfType<FundFactor<FundModeInfo>>().FirstOrDefault();
            if (_f != null)
                FundModeInfo = _f.Data;
        }

        if (g.TryGetValue(FactorFields.SealingRule, out var _s4))
        {
            var _f = _s4.OfType<FundFactor<SealingRule>>().FirstOrDefault();
            if (_f != null)
                SealingRule = _f.Data;
        }

        if (g.TryGetValue(FactorFields.RiskLevel, out var _s5))
        {
            var _f = _s5.OfType<FundFactor<RiskLevel>>().FirstOrDefault();
            if (_f != null)
                RiskLevel = _f.Data;
        }

        if (g.TryGetValue(FactorFields.DurationInMonths, out var _s6))
        {
            var _f = _s6.OfType<FundFactor<FundDuration>>().FirstOrDefault();
            if (_f != null)
                DurationInMonths = _f.Data;
        }

        if (g.TryGetValue(FactorFields.ExpirationDate, out var _s7))
        {
            var _f = _s7.OfType<FundFactor<global::System.DateOnly>>().FirstOrDefault();
            if (_f != null)
                ExpirationDate = _f.Data;
        }

        if (g.TryGetValue(FactorFields.StructureInfo, out var _s8))
        {
            var _f = _s8.OfType<FundFactor<StructureInfo>>().FirstOrDefault();
            if (_f != null)
                StructureInfo = _f.Data;
        }

        if (g.TryGetValue(FactorFields.CollectionAccount, out var _s9))
        {
            var _f = _s9.OfType<FundFactor<BankAccount>>().FirstOrDefault();
            if (_f != null)
                CollectionAccount = _f.Data;
        }

        if (g.TryGetValue(FactorFields.CustodyAccount, out var _s10))
        {
            var _f = _s10.OfType<FundFactor<BankAccount>>().FirstOrDefault();
            if (_f != null)
                CustodyAccount = _f.Data;
        }

        if (g.TryGetValue(FactorFields.StopLine, out var _s11))
        {
            var _f = _s11.OfType<FundFactor<decimal>>().FirstOrDefault();
            if (_f != null)
                StopLine = _f.Data;
        }

        if (g.TryGetValue(FactorFields.WarningLine, out var _s12))
        {
            var _f = _s12.OfType<FundFactor<decimal>>().FirstOrDefault();
            if (_f != null)
                WarningLine = _f.Data;
        }

        if (g.TryGetValue(FactorFields.HugeRedemption, out var _s13))
        {
            var _f = _s13.OfType<FundFactor<HugeRedemptionRule>>().FirstOrDefault();
            if (_f != null)
                HugeRedemption = _f.Data;
        }

        if (g.TryGetValue(FactorFields.CoolingPeriod, out var _s14))
        {
            var _f = _s14.OfType<FundFactor<CoolingPeriodInfo>>().FirstOrDefault();
            if (_f != null)
                CoolingPeriod = _f.Data;
        }

        if (g.TryGetValue(FactorFields.Callback, out var _s15))
        {
            var _f = _s15.OfType<FundFactor<CallbackInfo>>().FirstOrDefault();
            if (_f != null)
                Callback = _f.Data;
        }

        if (g.TryGetValue(FactorFields.TrusteeInfo, out var _s16))
        {
            var _f = _s16.OfType<FundFactor<AgencyInfo>>().FirstOrDefault();
            if (_f != null)
                TrusteeInfo = _f.Data;
        }

        if (g.TryGetValue(FactorFields.OutsourcingInfo, out var _s17))
        {
            var _f = _s17.OfType<FundFactor<AgencyInfo>>().FirstOrDefault();
            if (_f != null)
                OutsourcingInfo = _f.Data;
        }

        if (g.TryGetValue(FactorFields.ManageFeePay, out var _s18))
        {
            var _f = _s18.OfType<FundFactor<FeePayInfo>>().FirstOrDefault();
            if (_f != null)
                ManageFeePay = _f.Data;
        }

        if (g.TryGetValue(FactorFields.PerformanceFeeRule, out var _s19))
        {
            var _f = _s19.OfType<FundFactor<PerformanceFeeRule>>().FirstOrDefault();
            if (_f != null)
                PerformanceFeeRule = _f.Data;
        }

        if (g.TryGetValue(FactorFields.InvestmentManagers, out var _s20))
        {
            var _f = _s20.OfType<FundFactor<FundInvestmentManager[]>>().FirstOrDefault();
            if (_f != null)
                InvestmentManagers = _f.Data;
        }

        if (g.TryGetValue(FactorFields.InvestmentManager, out var _s21))
        {
            var _f = _s21.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                InvestmentManager = _f.Data;
        }

        if (g.TryGetValue(FactorFields.PerformanceBenchmark, out var _s22))
        {
            var _f = _s22.OfType<FundFactor<PerformanceBenchmark>>().FirstOrDefault();
            if (_f != null)
                PerformanceBenchmark = _f.Data;
        }

        if (g.TryGetValue(FactorFields.InvestmentObjective, out var _s23))
        {
            var _f = _s23.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                InvestmentObjective = _f.Data;
        }

        if (g.TryGetValue(FactorFields.InvestmentScope, out var _s24))
        {
            var _f = _s24.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                InvestmentScope = _f.Data;
        }

        if (g.TryGetValue(FactorFields.InvestmentStrategy, out var _s25))
        {
            var _f = _s25.OfType<FundFactor<string>>().FirstOrDefault();
            if (_f != null)
                InvestmentStrategy = _f.Data;
        }

        // FactorItem: 默认空数组
        FundOpenRule = [];
        TemporarilyOpenInfo = [];
        LockingRule = [];
        SubscriptionRule = [];
        PurchasRule = [];
        ManageFee = [];
        RedemptionFee = [];
        PerformanceFeeStatement = [];
        PerformanceFeeStandard = [];

        if (g.TryGetValue(FactorFields.ShareClasses, out var _scData))
        {
            var _scArr = _scData.OfType<FundFactor<ShareClass[]>>().ToArray();
            if (_scArr.Length > 0)
            {
                var _sci = new ShareClassFactorItem(_scArr);
                var _shares = _sci.GetShares();
                var _cfg = __BuildInheritedShareConfigMap(_shares);

                if (g.TryGetValue(FactorFields.FundOpenRule, out var _f0))
                {
                    var _item = new FactorItem<OpenRule[]>(_f0.OfType<FundFactor<OpenRule[]>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            FundOpenRule = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<OpenRule[]>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.TemporarilyOpenInfo, out var _f1))
                {
                    var _item = new FactorItem<TemporarilyOpenInfo>(_f1.OfType<FundFactor<TemporarilyOpenInfo>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            TemporarilyOpenInfo = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<TemporarilyOpenInfo>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.LockingRule, out var _f2))
                {
                    var _item = new FactorItem<SealingRule>(_f2.OfType<FundFactor<SealingRule>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            LockingRule = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<SealingRule>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.SubscriptionRule, out var _f3))
                {
                    var _item = new FactorItem<FundPurchaseRule>(_f3.OfType<FundFactor<FundPurchaseRule>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            SubscriptionRule = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<FundPurchaseRule>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.PurchasRule, out var _f4))
                {
                    var _item = new FactorItem<FundPurchaseRule>(_f4.OfType<FundFactor<FundPurchaseRule>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            PurchasRule = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<FundPurchaseRule>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.ManageFee, out var _f5))
                {
                    var _item = new FactorItem<FundFeeInfo>(_f5.OfType<FundFactor<FundFeeInfo>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            ManageFee = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<FundFeeInfo>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.RedemptionFee, out var _f6))
                {
                    var _item = new FactorItem<RedemptionFeeInfo>(_f6.OfType<FundFactor<RedemptionFeeInfo>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            RedemptionFee = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<RedemptionFeeInfo>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.PerformanceFeeStatement, out var _f7))
                {
                    var _item = new FactorItem<string>(_f7.OfType<FundFactor<string>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            PerformanceFeeStatement = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

                if (g.TryGetValue(FactorFields.PerformanceFeeStandard, out var _f8))
                {
                    var _item = new FactorItem<PerformanceFeeStandard>(_f8.OfType<FundFactor<PerformanceFeeStandard>>(), _shares, _cfg);
                    if (_item.HasValue)
                    {
                        var _vals = _item.Current;
                        if (_vals.Length > 0)
                        {
                            PerformanceFeeStandard = _vals.Length == 1 || _vals.All(x => global::System.Collections.Generic.EqualityComparer<PerformanceFeeStandard>.Default.Equals(x, _vals[0])) ? [_vals[0]] : _vals;
                        }
                    }
                }

            }
        }
    }

    private static ImmutableDictionary<int, InheritMap> __BuildInheritedShareConfigMap((int FlowId, ShareClass[] Shares)[] rawShares)
    {
        if (rawShares == null || rawShares.Length == 0) return ImmutableDictionary<int, InheritMap>.Empty;

        var result = new System.Collections.Generic.Dictionary<int, InheritMap>();
        for (int i = 0; i < rawShares.Length; i++)
        {
            var item = rawShares[i];
            if (item.Shares == null || item.Shares.Length == 0) continue;
            foreach (var sc in item.Shares)
            {
                if (i > 1 && ShareClass.GetFlow(sc.Inherit) < rawShares[i - 1].FlowId && rawShares[i - 1].Shares.Length == 1)
                    sc.Inherit = rawShares[i - 1].Shares[0].Id;
                result[sc.Id] = new InheritMap(sc.Id, item.FlowId, sc.Inherit);
            }
        }
        return result.ToImmutableDictionary();
    }
}
