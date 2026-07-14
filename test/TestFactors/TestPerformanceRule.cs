using FMO.Models;

namespace TestFactors;

[TestClass]
public sealed class TestPerformanceRule
{
    [TestMethod]
    public void TestPerformanceFeeRuleToString()
    {
        // 1. 整体高水位法
        var hwm = new PerformanceFeeRule
        {
            Method = PerformanceFeeMethod.HighWaterMark,
            DeductionType = PerformanceFeeDeductionType.NavDeduction,
            Trigger = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution | PerformanceFeeTrigger.Liquidation,
        };
        Console.WriteLine(hwm.ToString());
        Assert.AreEqual("整体高水位法，赎回/分红/清盘时提取", hwm.ToString());

        // 2. 单人高水位法 - 扣份额
        var perInvestor = new PerformanceFeeRule
        {
            Method = PerformanceFeeMethod.HighWaterMarkPerInvestor,
            DeductionType = PerformanceFeeDeductionType.ShareDeduction,
            Trigger = PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(perInvestor.ToString());
        Assert.AreEqual("单人高水位法，赎回时提取，扣份额", perInvestor.ToString());

        // 3. 整体收益法
        var overallReturn = new PerformanceFeeRule
        {
            Method = PerformanceFeeMethod.OverallReturn,
            Trigger = PerformanceFeeTrigger.Liquidation,
        };
        Console.WriteLine(overallReturn.ToString());
        Assert.AreEqual("整体收益法，清盘时提取", overallReturn.ToString());

        // 4. 特殊计提法
        var special = new PerformanceFeeRule
        {
            Method = PerformanceFeeMethod.Special,
            Trigger = PerformanceFeeTrigger.Redemption | PerformanceFeeTrigger.Distribution,
            SpecialMethod = "按合同约定的特殊方式计提",
            Remark = "补充说明",
        };
        Console.WriteLine(special.ToString());
        Assert.AreEqual("特殊计提法，按合同约定的特殊方式计提，赎回/分红时提取，补充说明", special.ToString());

        // 5. 开放日提取
        var openDay = new PerformanceFeeRule
        {
            Method = PerformanceFeeMethod.HighWaterMark,
            Trigger = PerformanceFeeTrigger.OpenDay | PerformanceFeeTrigger.Redemption,
        };
        Console.WriteLine(openDay.ToString());
        Assert.AreEqual("整体高水位法，赎回/开放日时提取", openDay.ToString());
    }

    [TestMethod]
    public void TestPerformanceFeeStandardToString()
    {
        // 1. 不计提
        var noFee = new PerformanceFeeStandard { Has = false };
        Console.WriteLine(noFee.ToString());
        Assert.AreEqual("不计提", noFee.ToString());

        // 2. 单一比例计提（实际收益率）
        var singleRate = new PerformanceFeeStandard
        {
            Has = true,
            ReturnType = PerformanceFeeReturnType.Actual,
            Tiers = [new() { UpperBound = null, Rate = 20 }],
        };
        Console.WriteLine(singleRate.ToString());
        Assert.AreEqual("计提：20%", singleRate.ToString());

        // 3. 分级计提（年化收益率R）
        var tieredAnnualized = new PerformanceFeeStandard
        {
            Has = true,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Tiers =
            [
                new() { UpperBound = 10, Include = false, Rate = 20 },
                new() { UpperBound = 20, Include = false, Rate = 25 },
                new() { UpperBound = null, Rate = 30 },
            ],
        };
        Console.WriteLine(tieredAnnualized.ToString());
        Assert.AreEqual("分级计提（年化收益率R）：0%≤R<10%：20%；10%≤R<20%：25%；R≥20%：30%", tieredAnnualized.ToString());

        // 4. 分级计提（实际收益率R）+ Include=true
        var tieredInclude = new PerformanceFeeStandard
        {
            Has = true,
            ReturnType = PerformanceFeeReturnType.Actual,
            Tiers =
            [
                new() { UpperBound = 5, Include = true, Rate = 10 },
                new() { UpperBound = 10, Include = true, Rate = 15 },
                new() { UpperBound = null, Rate = 20 },
            ],
        };
        Console.WriteLine(tieredInclude.ToString());
        Assert.AreEqual("分级计提（实际收益率R）：0%≤R≤5%：10%；5%≤R≤10%：15%；R≥10%：20%", tieredInclude.ToString());

        // 5. 两档分级计提
        var twoTier = new PerformanceFeeStandard
        {
            Has = true,
            ReturnType = PerformanceFeeReturnType.Annualized,
            Tiers =
            [
                new() { UpperBound = 8, Include = false, Rate = 15 },
                new() { UpperBound = null, Rate = 25 },
            ],
        };
        Console.WriteLine(twoTier.ToString());
        Assert.AreEqual("分级计提（年化收益率R）：0%≤R<8%：15%；R≥8%：25%", twoTier.ToString());
    }
}
