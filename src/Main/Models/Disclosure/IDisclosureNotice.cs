using FMO.Models;
using System.ComponentModel;

namespace FMO.Disclosure;

/// <summary>
/// 通用信批公告
/// </summary>
public interface IDisclosureNotice
{
    long Id { get; }

    DisclosureType Type { get; }


    /// <summary>
    /// 期望发布日期
    /// </summary>
    DateOnly PublishDate { get; set; }

    TimeOnly PublishTime { get; set; }


    /// <summary>
    /// 报告名称
    /// </summary>
    string Name { get; }

}

/// <summary>
/// 基金信批公告
/// </summary>
public interface IFundDisclosureNotice : IDisclosureNotice
{
    public int FundId { get; }

    public string FundName { get; }

    public string FundCode { get; }

}





public interface IFundPeriodicalDisclosure : IFundDisclosureNotice
{
    DateOnly ReportDate { get; }
}


/// <summary>
/// 定期报告
/// 月报、季报、半年报、年报等
/// </summary>
public class PeriodicalDisclosureNotice : IFundPeriodicalDisclosure
{
    public long Id => ((long)ReportDate.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type { get; set; }

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public required string Name { get; set; }


    public DateOnly ReportDate { get; set; }


    public SimpleFile? Word { get; set; }

    public SimpleFile? Excel { get; set; }

    public SimpleFile? Xbrl { get; set; }

    public SimpleFile? Pdf { get; set; }

    public SimpleFile? Sealed { get; set; }

}

/// <summary>
/// 季度更新
/// </summary>
public class QuarterlyUpdate : IFundPeriodicalDisclosure
{
    public long Id => ((long)ReportDate.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public required int FundId { get; set; }

    public required string FundCode { get; set; }

    public DisclosureType Type => DisclosureType.QuarterlyUpdate;

    public required string FundName { get; set; }


    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public required string Name { get; set; }


    /// <summary>
    /// 定期报告的最后一天
    /// </summary>

    public DateOnly ReportDate { get; set; }

    /// <summary>
    /// 投资者
    /// </summary>
    public SimpleFile? Investor { get; set; }

    /// <summary>
    /// 运行信息
    /// </summary>
    public SimpleFile? Operation { get; set; }



}


public interface ITemporaryDisclosureNotice
{
    SimpleFile? Word { get; set; }

    SimpleFile? Pdf { get; set; }

}

/// <summary>
/// 临时报告
/// </summary>
public class TemporaryDisclosureNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id { get; init; }

    public DisclosureType Type => DisclosureType.OtherFundNotice;

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public required string Name { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }

    //public SimpleFile? File => Pdf;

    public TemporaryDisclosureNotice()
    {
        Id = ((DateTime.Now.Ticks - 621355968000000000L) & 0x1FFFFFFFFFFFF) << 10 | ((long)Type);
    }

}

/// <summary>
/// 临时开放公告
/// </summary>
public class TemporaryOpenNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id => ((long)OpenDay.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type => DisclosureType.TemporaryOpen;

    public int FundId { get; set; }
    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public string Name => $"{FundName} 临时开放公告";

    public DateOnly OpenDay { get; set; }

    public bool AllowPurchase { get; set; }

    public bool AllowRedemption { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }
}


/// <summary>
/// 巨额赎回公告
/// </summary>
public class HugeRedemptionNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id => ((long)OpenDay.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type => DisclosureType.HugeRedemption;

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public string Name => $"{FundName} 巨额赎回公告";

    public DateOnly OpenDay { get; set; }

    /// <summary>
    /// 赎回比例
    /// </summary>
    public decimal RealRatio { get; set; }

    /// <summary>
    /// 合同约定的赎回比例
    /// </summary>
    public decimal DefinedRatio { get; set; }

    /// <summary>
    /// 是否全部兑付
    /// </summary>
    public bool IsFullyPaied { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }


}

/// <summary>
/// 产品成立公告
/// </summary>
public class FundSetupNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id => ((long)SetupDay.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type => DisclosureType.FundSetup;

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public string Name => $"{FundName} 产品成立公告";

    public DateOnly SetupDay { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }
}


/// <summary>
/// 基金分红
/// </summary>
public class FundDivdendNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id => ((long)DividendDay.DayNumber) << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type => DisclosureType.FundDivdend;

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public string Name => $"{FundName} 产品分红公告";

    public DividendType DividendType { get; set; }

    public decimal Target { get; set; }

    public DividendMethod Method { get; set; }

    public DateTime DividendReferenceDate { get; set; }
    public DateTime RecordDate { get; set; }
    public DateTime ExDividendDate { get; set; }
    public DateTime CashPaymentDate { get; set; }



    public DateOnly DividendDay { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }
}



/// <summary>
/// 基金规模预警类型
/// 最大32种，否则需要调整ID生成逻辑，增加预留位
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum ScaleWarningType
{
    [Description("未选择")]None,

    /// <summary>
    /// 基金年度日均基金资产净值低于1000万元规模预警
    /// </summary>
    [Description("年度日均净资产净值低于1000万元")] AnnualAverageNetAssetBelow1000W = 1,

    /// <summary>
    /// 基金日均资产规模低于500万元停止申购通知
    /// </summary>
    [Description("年度日均净资产净值低于500万元")] DailyAverageAssetBelow500W = 2,

    /// <summary>
    /// 基金连续60个交易日基金资产低于500万元停止申购通知
    /// </summary>
    [Description("连续60个交易日净资产低于500万元")] Continuous60TradeDaysAssetBelow500W = 3,

}

public class FundSacleWarningNotice : IFundDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id => (long)WarningType << 58 | (long)TouchDate.DayNumber << 32 | (long)FundId << 10 | ((long)Type);

    public DisclosureType Type => DisclosureType.FundScaleWarning;

    public int FundId { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }

    public ScaleWarningType WarningType { get; set; }

    /// <summary>
    /// 触发日期
    /// </summary>
    public DateOnly TouchDate { get; set; }

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public string Name => $"{FundName} {WarningType switch
    {
        ScaleWarningType.AnnualAverageNetAssetBelow1000W => "年度日均净资产净值低于1000万元预警",
        ScaleWarningType.DailyAverageAssetBelow500W => "年度日均净资产净值低于500万元停止申购通知",
        ScaleWarningType.Continuous60TradeDaysAssetBelow500W => "连续60个交易日净资产低于500万元停止申购通知",
        _ => ""
    }}";

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }
}





/// <summary>
/// 管理人公告
/// </summary>
public class ManagerDisclosureNotice : IDisclosureNotice, ITemporaryDisclosureNotice
{
    public long Id { get; init; }

    public DisclosureType Type => DisclosureType.OtherManagerNotice;

    public DateOnly PublishDate { get; set; }

    public TimeOnly PublishTime { get; set; }

    public required string Name { get; set; }

    public SimpleFile? Word { get; set; }

    public SimpleFile? Pdf { get; set; }

    public ManagerDisclosureNotice()
    {
        Id = ((DateTime.Now.Ticks - 621355968000000000L) & 0x1FFFFFFFFFFFF) << 10 | ((long)Type);
    }

}