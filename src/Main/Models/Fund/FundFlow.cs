using System.ComponentModel;

namespace FMO.Models;


public interface IElementChangable
{

}

/// <summary>
/// 流程
/// </summary>
public abstract class FundFlow
{
    public int Id { get; set; }

    public required int FundId { get; set; }

    /// <summary>
    /// 日期
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public abstract string Name { get; set; }

    /// <summary>
    /// 自定义文件
    /// </summary>
    public MultiCustomFile? CustomFiles { get; set; }

    /// <summary>
    /// 流程结束
    /// </summary>
    public bool Finished { get; set; }


}



/// <summary>
/// 发起工作流
/// </summary>
public class InitiateFlow : FundFlow
{
    /// <summary>
    /// 要素文件
    /// </summary>
    public required MultiFile ElementFiles { get; set; }

    /// <summary>
    /// 定稿前历次合同文件
    /// </summary>
    public required MultiFile ContractFiles { get; set; }

    public override string Name { get => "项目发起"; set { } }
}




public abstract class ContractFlow : FundFlow
{
    /// <summary>
    /// 定稿合同文件
    /// </summary>
    public SimpleFile? ContractFile { get; set; }


    /// <summary>
    /// 风险揭示书
    /// </summary>
    public SimpleFile? RiskDisclosureDocument { get; set; }

    /// <summary>
    /// 募集账户函
    /// </summary>
    public SimpleFile? CollectionAccountFile { get; set; }

    /// <summary>
    /// 托管账户函
    /// </summary>
    public SimpleFile? CustodyAccountFile { get; set; }

}





/// <summary>
/// 合同定稿工作流
/// </summary>
public class ContractFinalizeFlow : ContractFlow
{



    //份额、要素

    public override string Name { get => "合同定稿"; set { } }
}

public struct DateRange
{
    public DateOnly Begin { get; set; }

    public DateOnly End { get; set; }

    public override string ToString()
    {
        return $"{Begin:yyyy-MM-dd} - {End:yyyy-MM-dd}";
    }

    public static bool operator ==(DateRange left, DateRange right)
    {
        return left.Begin == right.Begin && left.End == right.End;
    }

    public static bool operator !=(DateRange left, DateRange right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        return obj is DateRange other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Begin, End);
    }
}

/// <summary>
/// 基金成立工作流
/// </summary>
public class SetupFlow : FundFlow
{
    /// <summary>
    /// 募集开始日期
    /// </summary>
    public DateOnly RaisingStartDate { get; set; }

    /// <summary>
    /// 募集结束日期
    /// </summary>
    public DateOnly RaisingEndDate { get; set; }

    /// <summary>
    /// 募集期
    /// </summary>
    public DateRange RasingPeriod { get; set; }


    public decimal InitialAsset { get; set; }


    /// <summary>
    /// 实缴出资证明
    /// </summary>
    public SimpleFile? PaidInCapitalProof { get; set; }

    /// <summary>
    /// 成立公告
    /// </summary>
    public DualFile? EstablishmentAnnouncement { get; set; }



    public override string Name { get => "基金成立"; set { } }
}

/// <summary>
/// 备案工作流
/// </summary>
public class RegistrationFlow : FundFlow
{

    /// <summary>
    /// 备案承诺函
    /// </summary>
    public DualFile? CommitmentLetter { get; set; }

    /// <summary>
    /// 用印版
    /// </summary>
    //public SimpleFile? SealedCommitmentLetter { get; set; }


    /// <summary>
    /// 招募说明书
    /// </summary>
    public DualFile? Prospectus { get; set; }

    //public SimpleFile? SealedProspectus { get; set; }

    /// <summary>
    /// 用印的基金合同
    /// </summary>
    public SimpleFile? SealedContract { get; set; }


    /// <summary>
    /// 募集账户监督协议
    /// </summary>
    public SimpleFile? SealedAccountOversightProtocol { get; set; }

    /// <summary>
    /// 外包服务协议
    /// </summary>
    public SimpleFile? SealedOutsourcingServicesAgreement { get; set; }

    /// <summary>
    /// 投资者明细
    /// </summary>
    public SimpleFile? SealedInvestorList { get; set; }

    /// <summary>
    /// 产品结构图
    /// </summary>
    public DualFile? StructureGraph { get; set; }


    //public SimpleFile? SealedStructureGraph { get; set; }

    /// <summary>
    /// 嵌套承诺函
    /// </summary>
    public DualFile? NestedCommitmentLetter { get; set; }

    //public SimpleFile? SealedNestedCommitmentLetter { get; set; }

    /// <summary>
    /// 备案函
    /// </summary>
    public DualFile? RegistrationLetter { get; set; }

    public override string Name { get => "基金备案"; set { } }


}



[Flags]
public enum ContractModifySection
{
    None = 0,

    Name,

    InvestManager = 0x2,

    ShareClass = 0x4,

    CollectionAccount = 0x8,

    CustodyAccount = 0x10,
}

/// <summary>
/// 合同变更工作流
/// </summary>
public class ContractModifyFlow : ContractFlow
{
    /// <summary>
    /// 协议变更
    /// </summary>
    public bool ModifyBySupplementary { get; set; }

    public ContractModifySection Section { get; set; }

    /// <summary>
    /// 补充协议
    /// </summary>
    public MultiFile? SupplementaryFile { get; set; }

    /// <summary>
    /// 备案函
    /// </summary>
    public DualFile? RegistrationLetter { get; set; }


    /// <summary>
    /// 成立公告
    /// </summary>
    public DualFile? Announcement { get; set; }

    //public SimpleFile? SealedAnnouncement { get; set; }


    public DualFile? CommitmentLetter { get; set; }


    //public SimpleFile? SealedCommitmentLetter { get; set; }

    public MultiFile? SignedSupplementary { get; set; }

    //份额、要素

    public override string Name { get => "合同变更"; set { } }
}


public class ModifyByAnnounceFlow : FundFlow
{

    /// <summary>
    /// 公告
    /// </summary>
    public DualFile? Announcement { get; set; }

    //public SimpleFile? SealedAnnouncement { get; set; }


    public override string Name { get => "合同变更"; set { } }
}

/// <summary>
/// 方案
/// ①按每单位红利分红：按照产品分红基准日的单位净值进行扣减，例如基准日单位净值为1.2513，可以选择每单位红利为 0.1，分红后单位净值为 1.1513。
///本模式下【可分单位红利上限】将按净值保留位数截位计算（例如: 公式计算出的可分单位红利上限=1.12345，单位净值保留位数=4 位，则【可分单位红利上限】=1.1234) ，
///以保证分红总金额不超过基金可供分配利润，如需将基金可供分配利润全部分配，可选择“按指定金额分红"或“按单位净值归目标净值分红”。
///②按指定金额分红：指定本次分红的总金额，系统会根据权益进行计算本次分红应该扣减的净值。
///③按单位净值归目标净值分配：系统自动计算本次分红金额，使分红结果尽可能为设定的目标净值。 
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum DividendType
{
    /// <summary>
    /// 按每单位红利分红：按照产品分红基准日的单位净值进行扣减
    /// </summary>
    [Description("按每单位红利分红")]
    PerUnitDividend = 1,

    /// <summary>
    /// 按指定金额分红：指定本次分红的总金额，系统会根据权益进行计算本次分红应该扣减的净值
    /// </summary>
    [Description("按指定金额分红")]
    SpecifiedAmount = 2,

    /// <summary>
    /// 按单位净值归目标净值分配：系统自动计算本次分红金额，使分红结果尽可能为设定的目标净值
    /// </summary>
    [Description("按单位净值归目标净值分红")]
    TargetNetValue = 3
}

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum DividendMethod
{
    /// <summary>
    /// 现金分红：将红利以现金形式发放给投资人
    /// </summary>
    [Description("现金分红")]
    Cash = 1,

    /// <summary>
    /// 红利再投资：将红利自动转为基金份额进行再投资
    /// </summary>
    [Description("红利再投资")]
    Reinvestment,


    [Description("按投资人意愿")]
    Manual,
}

public class DividendFlow : FundFlow
{
    public DividendType Type { get; set; }

    public decimal Target { get; set; }


    public DividendMethod Method { get; set; }

    /// <summary>
    /// 分红基准日：计算红利的参考净值日期
    /// </summary>
    public DateOnly DividendReferenceDate { get; set; }

    /// <summary>
    /// 权益登记日：确认投资者享有分红权益的日期
    /// </summary>
    public DateOnly RecordDate { get; set; }

    /// <summary>
    /// 除息日：基金净值扣除分红金额的日期
    /// </summary>
    public DateOnly ExDividendDate { get; set; }

    /// <summary>
    /// 现金红利发放日：投资者实际收到现金红利的日期
    /// </summary>
    public DateOnly CashPaymentDate { get; set; }



    /// <summary>
    /// 公告
    /// </summary>
    public DualFile? Announcement { get; set; }

    //public SimpleFile? SealedAnnouncement { get; set; }

    public override string Name { get => "基金分红"; set { } }
}





public class LiquidationFlow : FundFlow
{
    /// <summary>
    /// 清算报告
    /// </summary>
    public DualFile? LiquidationReport { get; set; }

    public SimpleFile? SealedLiquidationReport { get; set; }

    public DualFile? CommitmentLetter { get; set; }

    public SimpleFile? SealedCommitmentLetter { get; set; }

    /// <summary>
    /// 投资者表
    /// </summary>
    public SimpleFile? InvestorSheet { get; set; }

    /// <summary>
    /// 清算情况表
    /// </summary>
    public SimpleFile? LiquidationSheet { get; set; }



    public override string Name { get => "基金清算"; set { } }
}