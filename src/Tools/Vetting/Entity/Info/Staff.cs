namespace Vetting.Models.Entities;

/// <summary>
/// 人员信息 (高管/投研/风控/投资经理通用)
/// </summary>
public class Staff
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Education { get; set; }
    public string? Profile { get; set; }
    public string? IdNumber { get; set; }
    public string? Years { get; set; }
    public string? Age { get; set; }
    public string? BirthDate { get; set; }
    public string? Undergraduate { get; set; }
    public string? Masters { get; set; }
    public string? Doctoral { get; set; }
    public string? Specialty { get; set; }
    public string? ResearchFocus { get; set; }
    public string? MobilePhone { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// 角色: Executive(高管) / Researcher(投研) / RiskCtrl(风控) / PM(投资经理)
    /// </summary>
    public string? Role { get; set; }
}
