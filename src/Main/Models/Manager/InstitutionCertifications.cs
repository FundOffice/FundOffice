namespace FMO.Models;

/// <summary>
/// 机构证件
/// </summary>
public class InstitutionCertifications
{
    /// <summary>
    /// 证件号
    /// </summary>
    public required string Id { get; set; }


    public MultiDualFile BusinessLicense { get; init; } = new MultiDualFile { Label = "营业执照正本" };


    public MultiDualFile BusinessLicense2 { get; init; } = new MultiDualFile { Label = "营业执照副本" };

    public MultiDualFile AccountOpeningLicense { get; init; } = new MultiDualFile { Label = "开户许可证" };
    
    public MultiDualFile CharterDocument { get; init; } = new MultiDualFile { Label = "章程" };

    public MultiDualFile LegalPersonIdCard { get; init; } = new MultiDualFile { Label = "法人身份证" };

 

}