using FMO.Models;

namespace TestFactors;

[TestClass]
public sealed class TestPerformanceRule
{
    [TestMethod]
    public void TestPerformanceFeeRuleToString()
    {
        // 1. 不收取业绩报酬
        var noFee = new PerformanceFeeRule { Has = false };
        Console.WriteLine(noFee.ToString());
        Assert.AreEqual("不收取业绩报酬", noFee.ToString());

        // 2. 整体高水位法（无基准）
        var aggregate = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            Tiers = [new() { UpperBound = null, Rate = 20 }],
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
        };
        Console.WriteLine(aggregate.ToString());
        Assert.AreEqual("整体高水位法，计提：20%，赎回/分红时提取", aggregate.ToString());

        // 3. 整体高水位法 + 高于8%计提 - 扣份额
        var aggregateWithHurdle = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            Tiers = [new() { UpperBound = null, Rate = 25 }],
            DeductionType = PerformanceFeeDeductionType.ShareDeduction,
            Benchmark = "8%",
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
        };
        Console.WriteLine(aggregateWithHurdle.ToString());
        Assert.AreEqual("整体高水位法，高于8%的部分，计提：25%，扣份额，赎回/分红时提取", aggregateWithHurdle.ToString());

        // 4. 单人高水位法 + 业绩比较基准P：沪深300 - 超额部分分级计提（年化收益率R）
        var perInvestorWithBenchmark = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.PerInvestor,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Benchmark = "沪深300",
            Triggers = PerformanceFeeTrigger.Redemption,
            Tiers =
            [
                new() { UpperBound = 10, Include = false, Rate = 20 },
                new() { UpperBound = 20, Include = false, Rate = 25 },
                new() { UpperBound = null, Rate = 30 },
            ],
        };
        Console.WriteLine(perInvestorWithBenchmark.ToString());
        Assert.AreEqual("单人高水位法，业绩比较基准P：沪深300，超额部分分级计提（年化收益率R）：P≤R<10%：20%；10%≤R<20%：25%；R≥20%：30%，赎回时提取", perInvestorWithBenchmark.ToString());

        // 5. 仅高于6%计提（无高水位）
        var hurdleOnly = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            Tiers = [new() { UpperBound = null, Rate = 30 }],
            Benchmark = "6%",
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
        };
        Console.WriteLine(hurdleOnly.ToString());
        Assert.AreEqual("高于6%的部分，计提：30%，赎回/分红时提取", hurdleOnly.ToString());

        // 6. 仅业绩比较基准P（无高水位）
        var benchmarkOnly = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            Tiers = [new() { UpperBound = null, Rate = 20 }],
            Benchmark = "中证500",
            Triggers = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(benchmarkOnly.ToString());
        Assert.AreEqual("业绩比较基准P：中证500，超额部分计提：20%，赎回时提取", benchmarkOnly.ToString());

        // 7. 整体高水位法（赎回补提）
        var supplementary = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.AggregateWithSupplementary,
            Tiers = [new() { UpperBound = null, Rate = 20 }],
            Triggers = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(supplementary.ToString());
        Assert.AreEqual("整体高水位法（赎回补提），计提：20%，赎回时提取", supplementary.ToString());

        // 8. 兜底：无高水位 + 无基准 - 使用 SpecialMethod
        var special = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            Tiers = [new() { UpperBound = null, Rate = 15 }],
            SpecialMethod = "按合同约定的特殊方式计提",
            Remark = "补充说明",
        };
        Console.WriteLine(special.ToString());
        Assert.AreEqual("按合同约定的特殊方式计提，计提：15%，补充说明", special.ToString());

        // 9. 分级计提（实际收益率R）+ Include=true（上限包含）
        var includeTier = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            ReturnType = PerformanceFeeReturnType.Actual,
            Tiers =
            [
                new() { UpperBound = 5, Include = true, Rate = 10 },
                new() { UpperBound = 10, Include = true, Rate = 15 },
                new() { UpperBound = null, Rate = 20 },
            ],
        };
        Console.WriteLine(includeTier.ToString());
        Assert.AreEqual("整体高水位法，分级计提（实际收益率R）：0%≤R≤5%：10%；5%≤R≤10%：15%；R≥10%：20%", includeTier.ToString());
        // 10. 门槛收益 + 年化收益率
        var hurdleAnnualized = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Tiers = [new() { UpperBound = null, Rate = 30 }],
            Benchmark = "8%",
            Triggers = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(hurdleAnnualized.ToString());
        Assert.AreEqual("年化收益高于8%的部分，计提：30%，赎回时提取", hurdleAnnualized.ToString());

        // 11. 指数基准 + 单档 + 年化超额部分计提
        var indexAnnualized = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Tiers = [new() { UpperBound = null, Rate = 20 }],
            Benchmark = "中证500",
            Triggers = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(indexAnnualized.ToString());
        Assert.AreEqual("业绩比较基准P：中证500，年化超额部分计提：20%，赎回时提取", indexAnnualized.ToString());

        // 12. 指数基准 + 分级 + 年化
        var indexTierAnnualized = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Benchmark = "沪深300",
            Triggers = PerformanceFeeTrigger.Redemption,
            Tiers =
            [
                new() { UpperBound = 5, Include = false, Rate = 10 },
                new() { UpperBound = null, Rate = 20 },
            ],
        };
        Console.WriteLine(indexTierAnnualized.ToString());
        Assert.AreEqual("整体高水位法，业绩比较基准P：沪深300，超额部分分级计提（年化收益率R）：P≤R<5%：10%；R≥5%：20%，赎回时提取", indexTierAnnualized.ToString());

        // 13. 门槛 + 整体高水位 + 年化
        var hurdleAggregateAnnualized = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Tiers = [new() { UpperBound = null, Rate = 25 }],
            Benchmark = "6%",
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
        };
        Console.WriteLine(hurdleAggregateAnnualized.ToString());
        Assert.AreEqual("整体高水位法，年化收益高于6%的部分，计提：25%，赎回/分红时提取", hurdleAggregateAnnualized.ToString());

        // 14. 指数基准 + 分级 + 实际收益率
        var indexTierActual = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.PerInvestor,
            ReturnType = PerformanceFeeReturnType.Actual,
            Benchmark = "中证1000",
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Liquidation,
            Tiers =
            [
                new() { UpperBound = null, Rate = 20 },
            ],
        };
        Console.WriteLine(indexTierActual.ToString());
        Assert.AreEqual("单人高水位法，业绩比较基准P：中证1000，超额部分计提：20%，赎回/清算时提取", indexTierActual.ToString());

        // 15. 门槛收益 + 实际收益率（无高水位）
        var hurdleActual = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.None,
            ReturnType = PerformanceFeeReturnType.Actual,
            Tiers = [new() { UpperBound = null, Rate = 30 }],
            Benchmark = "8%",
            Triggers = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(hurdleActual.ToString());
        Assert.AreEqual("高于8%的部分，计提：30%，赎回时提取", hurdleActual.ToString());

        // 16. 门槛 + 整体高水位 + 实际收益率
        var hurdleAggregateActual = new PerformanceFeeRule
        {
            Has = true,
            HighWaterMark = HighWaterMarkType.Aggregate,
            ReturnType = PerformanceFeeReturnType.Actual,
            Tiers = [new() { UpperBound = null, Rate = 25 }],
            Benchmark = "6%",
            Triggers = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
        };
        Console.WriteLine(hurdleAggregateActual.ToString());
        Assert.AreEqual("整体高水位法，高于6%的部分，计提：25%，赎回/分红时提取", hurdleAggregateActual.ToString());
    }
}

