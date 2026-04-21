using System.ComponentModel;

namespace FMO.Models;



/// <summary>
/// 存储在base
/// </summary>
public interface IInvestor
{
    int Id { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 证件
    /// </summary>
    Identity Identity { get; set; }

    /// <summary>
    /// 证件有效期
    /// </summary>
    DateEfficient Efficient { get; set; }


    //List<InvestorFileInfo> Certifications { get; set; }

    VersionedFileInfo? Certifications { get; set; }

    // DetailCustomerType DetailType { get; set; }

    //InvestorType CustomerType => this switch { NaturalInvestor => InvestorType.Natural, InstitutionInvestor => InvestorType.Institution, ProductInvestor => InvestorType.Product, _ => throw new NotImplementedException() };
}

/// <summary>
/// 自然人
/// </summary>
public class NaturalInvestor : IInvestor
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required Identity Identity { get; set; }

    /// <summary>
    /// 性别
    /// </summary>
    public Gender? Gender { get; set; }


    /// <summary>
    /// 民族
    /// </summary>
    public string? Nation { get; set; }

    /// <summary>
    /// 生日
    /// </summary>
    public DateOnly Birthday { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    public DateEfficient Efficient { get; set; }

    public VersionedFileInfo? Certifications { get; set; }

    // public DetailCustomerType DetailType { get; set; }


    public NaturalType DetailType { get; set; }
}


/// <summary>
/// 机构类型
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum InstitutionType
{
    [Description("法人机构")] LegalEntity,

    [Description("有限合伙")] LimitedPartnership,

    [Description("个人独资")] IndividualProprietorship,

    [Description("QFII、RQFII等")] QFII,

    [Description("其它境外机构")] Foreign,

    [Description("其它机构")] Other,
}

/// <summary>
/// 机构
/// </summary>
public class InstitutionInvestor : IInvestor
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required Identity Identity { get; set; }

    public required InstitutionCustomerType DetailType { get; set; }

    public DateEfficient Efficient { get; set; }


    public VersionedFileInfo? Certifications { get; set; }
}


/// <summary>
/// 产品
/// </summary>
public class ProductInvestor : IInvestor
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required Identity Identity { get; set; }


    public required ProductCustomerType ProductType { get; set; }

    public DateEfficient Efficient { get; set; }

    public DetailCustomerType DetailType { get; set; }

    public VersionedFileInfo? Certifications { get; set; }
}


