namespace Vetting.Models.Entities;

public enum EducationLevel { 高中, 大专, 本科, 硕士, 博士, MBA, 其他 }

/// <summary>
/// 人员信息 (高管/投研/风控/投资经理通用)
/// </summary>
public class Staff
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public EducationLevel Education { get; set; }
    public string? Profile { get; set; }
    public string? IdNumber { get; set; }
    public string? Years { get; set; }
    public int? Age => BirthDate.HasValue ? (int)((DateTime.Now - BirthDate.Value).TotalDays / 365.25) : null;
    public DateTime? BirthDate { get; set; }
    public string? Specialty { get; set; }
    public string? ResearchFocus { get; set; }
    public string? MobilePhone { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    /// <summary>角色: Executive(高管) / Researcher(投研) / RiskCtrl(风控) / PM(投资经理)</summary>
    public string? Role { get; set; }
}
