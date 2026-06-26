namespace Vetting.Copilot.Models.Info;

public enum EducationLevel { 高中, 大专, 本科, 硕士, 博士, MBA, 其他 }

[Flags]
public enum StaffRole
{
    高管 = 1,
    投研 = 2,
    风控 = 4,
    投资经理 = 8,
    合规 = 16,
    运营 = 32,
    联系人 = 64,
}

public class Staff : IResolve
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
    public StaffRole Role { get; set; }
    public int? DepartmentId { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(Title) => Title,
        nameof(Education) => Education,
        nameof(Profile) => Profile,
        nameof(IdNumber) => IdNumber,
        nameof(Years) => Years,
        nameof(Age) => Age,
        nameof(BirthDate) => BirthDate,
        nameof(Specialty) => Specialty,
        nameof(ResearchFocus) => ResearchFocus,
        nameof(MobilePhone) => MobilePhone,
        nameof(Telephone) => Telephone,
        nameof(Email) => Email,
        _ => null,
    };
}
