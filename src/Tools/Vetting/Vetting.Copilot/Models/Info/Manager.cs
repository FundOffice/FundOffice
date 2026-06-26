namespace Vetting.Copilot.Models.Info;

public enum MembershipLevel { 无, 观察会员, 普通会员 }

public class Manager
{
    public int Id { get; set; } = 1;
    public string? Name { get; set; }
    public string? RegisterNo { get; set; }
    public string? ArtificialPerson { get; set; }
    public string? RegisterCapital { get; set; }
    public string? RealCapital { get; set; }
    public string? SetupDate { get; set; }
    public string? BusinessScope { get; set; }
    public string? RegisterAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string? Phone { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Fax { get; set; }
    public string? EnglishName { get; set; }
    public string? WebSite { get; set; }
    public string? AmacId { get; set; }
    public MembershipLevel Membership { get; set; }
    public bool InvestmentAdvisor { get; set; }
    public string? InstitutionType { get; set; }
    public string? RelatedCompany { get; set; }
    public string? Description { get; set; }
    public string? HistoricalEvolution { get; set; }
    public string? OrgStructureIntro { get; set; }
    public string? FutureStrategicPlan { get; set; }
    public string? GoverningSecuritiesBureau { get; set; }
    public string? ActualController { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhoneAndEmail { get; set; }
}
