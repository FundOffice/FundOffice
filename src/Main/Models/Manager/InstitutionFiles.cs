namespace FMO.Models;

public class InstitutionFiles
{
    public int Id { get; set; }

    public required string InstitutionId { get; set; }

    /// <summary>
    /// 营业执照正本
    /// </summary>
    public VersionedFileInfo? BusinessLicense { get; set; }

    /// <summary>
    /// 营业执照副本
    /// </summary>
    public VersionedFileInfo? BusinessLicense2 { get; set; }

    /// <summary>
    /// 开户许可证
    /// </summary>
    public VersionedFileInfo? AccountOpeningLicense { get; set; }


    /// <summary>
    /// 章程
    /// </summary>
    public VersionedFileInfo? CharterDocument { get; set; }

    /// <summary>
    /// 法人身份证
    /// </summary>
    public VersionedFileInfo? LegalPersonIdCard { get; set; }

}
