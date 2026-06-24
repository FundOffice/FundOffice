using FMO.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FMO.AI;

/// <summary>
/// 将 AiParsedFundInfo（DTO）转换为 ReadonlyFundInfo
/// 通过创建 FundFactor[] 调用 ReadonlyFundInfo.FillBy()
/// </summary>
internal static class AiParsedFundInfoConverter
{
    private const int DefaultFundId = 0;
    private const int DefaultFlowId = 1;

    public static ReadonlyFundInfo ToReadonlyFundInfo(AiParsedFundInfo dto)
    {
        var info = new ReadonlyFundInfo
        {
            ManagerProfile = dto.ManagerProfile,
            AuditDate = dto.AuditDate,
        };

        var factors = new List<IFundFactor>();

        // ===== 份额类别（必须先添加，FactorItem 依赖它）=====
        var shareClasses = BuildShareClasses(dto.ShareClassNames);
        factors.Add(MakeSingleton<ShareClass[]>(FactorFields.ShareClasses, shareClasses));

        // ===== 全局属性 =====
        AddSingleton(factors, dto.FullName, FactorFields.FullName);
        AddSingleton(factors, dto.ShortName, FactorFields.ShortName);

        if (dto.SecurityFundType != null)
            factors.Add(MakeSingleton<global::FMO.Models.SecurityFundType>(FactorFields.SecurityFundType, ParseSecurityFundType(dto.SecurityFundType)));

        if (dto.FundMode != null)
            factors.Add(MakeSingleton<FundModeInfo>(FactorFields.FundModeInfo, ParseFundModeInfo(dto.FundMode)));

        if (dto.SealingRule != null)
            factors.Add(MakeSingleton<global::FMO.Models.SealingRule>(FactorFields.SealingRule, ParseSealingRule(dto.SealingRule)));

        if (dto.RiskLevel != null)
            factors.Add(MakeSingleton<global::FMO.Models.RiskLevel>(FactorFields.RiskLevel, ParseRiskLevel(dto.RiskLevel)));

        if (dto.DurationInfinity.HasValue || dto.DurationMonths.HasValue)
            factors.Add(MakeSingleton<FundDuration>(FactorFields.DurationInMonths, new FundDuration
            {
                Infinity = dto.DurationInfinity ?? false,
                Month = dto.DurationMonths ?? 0
            }));

        if (dto.ExpirationDate != null && DateOnly.TryParse(dto.ExpirationDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expDate))
            factors.Add(MakeSingleton<DateOnly>(FactorFields.ExpirationDate, expDate));

        if (dto.StopLine.HasValue)
            factors.Add(MakeSingleton<decimal>(FactorFields.StopLine, dto.StopLine.Value));

        if (dto.WarningLine.HasValue)
            factors.Add(MakeSingleton<decimal>(FactorFields.WarningLine, dto.WarningLine.Value));

        if (dto.HugeRedemption != null)
            factors.Add(MakeSingleton<HugeRedemptionRule>(FactorFields.HugeRedemption, ParseHugeRedemptionRule(dto.HugeRedemption)));

        if (dto.CollectionAccount != null)
        {
            var account = BankAccount.FromString(dto.CollectionAccount);
            if (account != null) factors.Add(MakeSingleton<BankAccount>(FactorFields.CollectionAccount, account));
        }

        if (dto.CustodyAccount != null)
        {
            var account = BankAccount.FromString(dto.CustodyAccount);
            if (account != null) factors.Add(MakeSingleton<BankAccount>(FactorFields.CustodyAccount, account));
        }

        if (dto.TrusteeName != null)
            factors.Add(MakeSingleton<AgencyInfo>(FactorFields.TrusteeInfo, new AgencyInfo
            {
                HasAgency = !string.IsNullOrWhiteSpace(dto.TrusteeName),
                Name = dto.TrusteeName,
                HasFee = dto.TrusteeFee != null && dto.TrusteeFee != "无",
                Fee = 0,
                FeeType = FundFeeType.Ratio
            }));

        if (dto.TrusteeFee != null)
            factors.Add(MakeSingleton<FundFeeInfo>(FactorFields.TrusteeFee, ParseFundFeeInfo(dto.TrusteeFee)));

        if (dto.OutsourcingName != null)
            factors.Add(MakeSingleton<AgencyInfo>(FactorFields.OutsourcingInfo, new AgencyInfo
            {
                HasAgency = !string.IsNullOrWhiteSpace(dto.OutsourcingName),
                Name = dto.OutsourcingName,
                HasFee = dto.OutsourcingFee != null && dto.OutsourcingFee != "无",
                Fee = 0,
                FeeType = FundFeeType.Ratio
            }));

        if (dto.OutsourcingFee != null)
            factors.Add(MakeSingleton<FundFeeInfo>(FactorFields.OutsourcingFee, ParseFundFeeInfo(dto.OutsourcingFee)));

        if (dto.ManageFeePay != null)
            factors.Add(MakeSingleton<FeePayInfo>(FactorFields.ManageFeePay, ParseFeePayInfo(dto.ManageFeePay)));

        if (dto.InvestmentManager != null)
            AddSingleton(factors, dto.InvestmentManager, FactorFields.InvestmentManager);

        if (dto.PerformanceBenchmark != null)
            factors.Add(MakeSingleton<PerformanceBenchmark>(FactorFields.PerformanceBenchmark, new PerformanceBenchmark
            {
                Has = !string.IsNullOrWhiteSpace(dto.PerformanceBenchmark) && dto.PerformanceBenchmark != "无",
                Benchmark = dto.PerformanceBenchmark
            }));

        if (dto.InvestmentObjective != null)
            AddSingleton(factors, dto.InvestmentObjective, FactorFields.InvestmentObjective);

        if (dto.InvestmentScope != null)
            AddSingleton(factors, dto.InvestmentScope, FactorFields.InvestmentScope);

        if (dto.InvestmentStrategy != null)
            AddSingleton(factors, dto.InvestmentStrategy, FactorFields.InvestmentStrategy);

        if (dto.CoolingPeriod != null)
            factors.Add(MakeSingleton<CoolingPeriodInfo>(FactorFields.CoolingPeriod, ParseCoolingPeriodInfo(dto.CoolingPeriod)));

        if (dto.Callback != null)
            factors.Add(MakeSingleton<CallbackInfo>(FactorFields.Callback, new CallbackInfo
            {
                IsRequired = dto.Callback.Contains("需要") || dto.Callback.Contains("是")
            }));

        if (dto.TemporarilyOpenInfo != null)
            factors.Add(MakeSingleton<TemporarilyOpenInfo>(FactorFields.TemporarilyOpenInfo, ParseTemporarilyOpenInfo(dto.TemporarilyOpenInfo)));

        // ===== 份额相关（FactorItem）=====
        if (dto.LockingRule != null)
            AddFactorItems(factors, dto.LockingRule, FactorFields.LockingRule, shareClasses, ParseSealingRule);

        if (dto.ManageFee != null)
            AddFactorItems(factors, dto.ManageFee, FactorFields.ManageFee, shareClasses, ParseFundFeeInfo);

        if (dto.SubscriptionRule != null)
            AddFactorItems(factors, dto.SubscriptionRule, FactorFields.SubscriptionRule, shareClasses, ParseFundPurchaseRule);

        if (dto.PurchaseRule != null)
            AddFactorItems(factors, dto.PurchaseRule, FactorFields.PurchasRule, shareClasses, ParseFundPurchaseRule);

        if (dto.RedemptionFee != null)
            AddFactorItems(factors, dto.RedemptionFee, FactorFields.RedemptionFee, shareClasses, ParseRedemptionFeeInfo);

        if (dto.PerformanceFeeStatement != null)
            AddFactorItems(factors, dto.PerformanceFeeStatement, FactorFields.PerformanceFeeStatement, shareClasses, s => s);

        // 调用 FillBy
        info.FillBy(factors.ToArray());
        return info;
    }

    // ===== 辅助方法 =====

    private static void AddSingleton<T>(List<IFundFactor> factors, T? value, string factorId) where T : class
    {
        if (value != null)
            factors.Add(MakeSingleton<T>(factorId, value));
    }

    private static FundFactor<T> MakeSingleton<T>(string factorId, T data)
        => new(factorId, DefaultFundId, DefaultFlowId, ShareClass.Singleton, data);

    private static FundFactor<T> MakeFactor<T>(string factorId, int shareId, T data)
        => new(factorId, DefaultFundId, DefaultFlowId, shareId, data);

    private static ShareClass[] BuildShareClasses(string[]? names)
    {
        if (names == null || names.Length == 0)
            return [new ShareClass { Id = ShareClass.MakeId(DefaultFlowId, 0), Name = "默认", FundName = "", Code = "" }];

        return names.Select((name, i) => new ShareClass
        {
            Id = ShareClass.MakeId(DefaultFlowId, i),
            Name = name,
            FundName = "",
            Code = "",
            Inherit = i > 0 ? ShareClass.MakeId(DefaultFlowId, i - 1) : ShareClass.Singleton
        }).ToArray();
    }

    private static void AddFactorItems<T>(
        List<IFundFactor> factors,
        string[] values,
        string factorId,
        ShareClass[] shareClasses,
        Func<string, T> parser) where T : class
    {
        if (values.Length == 0) return;

        // 如果只有一个值或所有值相同，使用 Singleton ShareId（FillBy 会自动处理）
        if (values.Length == 1)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, parser(values[0])));
            return;
        }

        // 检查所有值是否相同
        var firstParsed = parser(values[0]);
        var allSame = values.All(v => EqualityComparer<T>.Default.Equals(parser(v), firstParsed));
        if (allSame)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, firstParsed));
            return;
        }

        // 不同值，按份额添加
        for (int i = 0; i < values.Length && i < shareClasses.Length; i++)
        {
            if (values[i] != null)
                factors.Add(MakeFactor(factorId, shareClasses[i].Id, parser(values[i])));
        }
    }

    // ===== 字符串解析方法 =====

    private static global::FMO.Models.SecurityFundType ParseSecurityFundType(string s)
        => s.Contains("固定收益") ? global::FMO.Models.SecurityFundType.FixedIncome
         : s.Contains("权益") ? global::FMO.Models.SecurityFundType.Equity
         : s.Contains("衍生") || s.Contains("期货") ? global::FMO.Models.SecurityFundType.CommodityAndDerivatives
         : s.Contains("混合") ? global::FMO.Models.SecurityFundType.Hybrid
         : global::FMO.Models.SecurityFundType.Unk;

    private static FundModeInfo ParseFundModeInfo(string s)
        => s.Contains("开放") ? new FundModeInfo { Mode = FundMode.Open }
         : s.Contains("封闭") ? new FundModeInfo { Mode = FundMode.Close }
         : new FundModeInfo { Mode = FundMode.Other, Other = s };

    private static SealingRule ParseSealingRule(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无")
            return new SealingRule { Type = SealingType.No };

        var m = Regex.Match(s, @"(\d+)\s*[个]?月");
        if (m.Success)
            return new SealingRule { Type = SealingType.Has, Month = int.Parse(m.Groups[1].Value) };

        return new SealingRule { Type = SealingType.Other, Extra = s };
    }

    private static global::FMO.Models.RiskLevel ParseRiskLevel(string s)
        => s.ToUpperInvariant() switch
        {
            "R1" => global::FMO.Models.RiskLevel.R1,
            "R2" => global::FMO.Models.RiskLevel.R2,
            "R3" => global::FMO.Models.RiskLevel.R3,
            "R4" => global::FMO.Models.RiskLevel.R4,
            "R5" => global::FMO.Models.RiskLevel.R5,
            _ => global::FMO.Models.RiskLevel.Unk
        };

    private static HugeRedemptionRule ParseHugeRedemptionRule(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无")
            return new HugeRedemptionRule { Has = false };

        var m = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%");
        if (m.Success)
        {
            var ratio = decimal.Parse(m.Groups[1].Value) / 100m;
            return new HugeRedemptionRule { Has = true, Ratio = ratio };
        }

        return new HugeRedemptionRule { Has = false };
    }

    private static FundFeeInfo ParseFundFeeInfo(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无")
            return new FundFeeInfo { HasFee = false, Type = FundFeeType.Other };

        // "固定X元/年" 或 "固定X元"
        var fixMatch = Regex.Match(s, @"固定\s*(\d+(?:\.\d+)?)\s*元");
        if (fixMatch.Success)
        {
            var fee = decimal.Parse(fixMatch.Groups[1].Value);
            return new FundFeeInfo { HasFee = true, Type = FundFeeType.Fix, Fee = fee };
        }

        // "X%/年" 或 "X%"
        var ratioMatch = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%");
        if (ratioMatch.Success)
        {
            var fee = decimal.Parse(ratioMatch.Groups[1].Value);

            // 检查保底费
            decimal guaranteed = 0;
            var guaranteeMatch = Regex.Match(s, @"保底\s*(\d+(?:\.\d+)?)");
            if (guaranteeMatch.Success)
                guaranteed = decimal.Parse(guaranteeMatch.Groups[1].Value);

            return new FundFeeInfo
            {
                HasFee = true,
                Type = FundFeeType.Ratio,
                Fee = fee,
                HasGuaranteedFee = guaranteed > 0,
                GuaranteedFee = guaranteed
            };
        }

        return new FundFeeInfo { HasFee = true, Type = FundFeeType.Other, Other = s };
    }

    private static RedemptionFeeInfo ParseRedemptionFeeInfo(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无")
            return new RedemptionFeeInfo { HasFee = false, Type = FundFeeType.Other };

        var result = new RedemptionFeeInfo { HasFee = true };

        // 按时间分段格式："持有<6月,1.5%；6月≤持有<12月,0.5%；持有≥12月,0%"
        var segments = s.Split(['；', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1)
        {
            result.Type = FundFeeType.ByTime;
            result.Parts = [];

            foreach (var seg in segments)
            {
                var part = ParsePartRedemptionFee(seg.Trim());
                if (part != null)
                    result.Parts.Add(part);
            }

            if (result.Parts.Count == 0)
                result.Parts = null;
        }
        else
        {
            // 单一费率
            var m = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%");
            if (m.Success)
            {
                result.Type = FundFeeType.Ratio;
                result.Fee = decimal.Parse(m.Groups[1].Value);
            }
            else
            {
                result.Type = FundFeeType.Other;
                result.Other = s;
            }
        }

        return result;
    }

    private static PartRedemptionFee? ParsePartRedemptionFee(string s)
    {
        // 匹配 "持有<6月,1.5%" 或 "T<6月,1.5%" 等
        var m = Regex.Match(s, @"(\d+)\s*月[^%]*?(\d+(?:\.\d+)?)\s*%");
        if (!m.Success) return null;

        var month = int.Parse(m.Groups[1].Value);
        var fee = decimal.Parse(m.Groups[2].Value);

        // 判断是否包含等号
        var include = s.Contains('≤') || s.Contains("≥") || s.Contains(">=") || s.Contains("<=");

        return new PartRedemptionFee { Month = month, Fee = fee, Include = include };
    }

    private static FundPurchaseRule ParseFundPurchaseRule(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无")
            return new FundPurchaseRule { MinDeposit = 0, HasFee = false };

        var rule = new FundPurchaseRule();

        // "100万起投" 或 "100万元起投"
        var minMatch = Regex.Match(s, @"(\d+)\s*(?:万)?元?\s*起投");
        if (minMatch.Success)
        {
            var amount = int.Parse(minMatch.Groups[1].Value);
            rule.MinDeposit = s.Contains("万") ? amount * 10000 : amount;
        }

        // "追加10万起" 或 "追加10万元起"
        var addMatch = Regex.Match(s, @"追加\s*(\d+)\s*(?:万)?元?");
        if (addMatch.Success)
        {
            var amount = int.Parse(addMatch.Groups[1].Value);
            rule.AdditionalDeposit = s.Contains("万") ? amount * 10000 : amount;
        }

        // 费用描述
        var feeMatch = Regex.Match(s, @"认购费\s*(\d+(?:\.\d+)?)\s*%|申购费\s*(\d+(?:\.\d+)?)\s*%|费用\s*(\d+(?:\.\d+)?)\s*%");
        if (feeMatch.Success)
        {
            rule.HasFee = true;
            rule.Type = FundFeeType.Ratio;
            var fee = feeMatch.Groups[1].Success ? feeMatch.Groups[1].Value
                      : feeMatch.Groups[2].Success ? feeMatch.Groups[2].Value
                      : feeMatch.Groups[3].Value;
            rule.Fee = decimal.Parse(fee);

            if (s.Contains("价外")) rule.PayMethod = FundFeePayType.Out;
            else if (s.Contains("额外")) rule.PayMethod = FundFeePayType.Extra;
        }

        if (s.Contains("无认购费") || s.Contains("无申购费") || s.Contains("无费用"))
            rule.HasFee = false;

        return rule;
    }

    private static CoolingPeriodInfo ParseCoolingPeriodInfo(string s)
        => s.Contains("24小时") || s.Contains("24 小时") || s.Contains("一天") || s.Contains("1天")
            ? new CoolingPeriodInfo { Type = CoolingPeriodType.OneDay }
            : new CoolingPeriodInfo { Type = CoolingPeriodType.Other, Other = s };

    private static FeePayInfo ParseFeePayInfo(string s)
        => s.Contains("月") ? new FeePayInfo { Type = FeePayFrequency.Month }
         : s.Contains("季") ? new FeePayInfo { Type = FeePayFrequency.Quarter }
         : new FeePayInfo { Type = FeePayFrequency.Other, Other = s };

    private static TemporarilyOpenInfo ParseTemporarilyOpenInfo(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "无" || s.Contains("不允许"))
            return new TemporarilyOpenInfo { IsAllowed = false };

        return new TemporarilyOpenInfo
        {
            IsAllowed = true,
            IsLimited = s.Contains("合同变更") || s.Contains("法规"),
            AllowPurchase = s.Contains("申购"),
            AllowRedemption = s.Contains("赎回"),
        };
    }
}
