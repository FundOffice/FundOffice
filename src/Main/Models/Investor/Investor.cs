using System.Text.RegularExpressions;

namespace FMO.Models;

public class Investor
{
    public Investor()
    {
    }

    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// 证件号
    /// </summary>
    public Identity? Identity { get; set; }

    /// <summary>
    /// 证件有效限
    /// </summary>
    public DateEfficient Efficient { get; set; }

    /// <summary>
    /// 证件文件
    /// </summary>
    //public VersionedFileInfo? Certifications { get; set; }

    //public List<FileStorageInfo>? IDCards { get; set; }

    /// <summary>
    /// 法人类型
    /// </summary>
    public EntityType EntityType { get; set; }

    public AmacInvestorType Type { get; set; }


    //public RiskLevel RiskLevel { get; set; }

    public RiskEvaluation RiskEvaluation { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public static bool IsNamePair(string? a, string? b)
    {
        if (a == b) return true;
        if (a is null || b is null) return false;

        // 清除符号
        Regex regex = new Regex(@"[\(\)（）\s－-]");
        var aa = regex.Replace(a, "");
        var bb = regex.Replace(b, "");
        return aa == bb;
    }

}


public class InvestorCertifications : MultiFile
{
    public int Id { get; set; }
}


/// <summary>
/// 
/// </summary>
/// <param name="Id">investor id</param>
/// <param name="Account"></param>
public record PfidAccount(int Id, string Account, DateOnly Update = default);
