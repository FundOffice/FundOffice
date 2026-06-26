namespace Vetting.Copilot.Models.Info;

public enum MembershipLevel { 无, 观察会员, 普通会员 }

public class Manager : IResolve
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

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(RegisterNo) => RegisterNo,
        nameof(ArtificialPerson) => ArtificialPerson,
        nameof(RegisterCapital) => RegisterCapital,
        nameof(RealCapital) => RealCapital,
        nameof(SetupDate) => SetupDate,
        nameof(BusinessScope) => BusinessScope,
        nameof(RegisterAddress) => RegisterAddress,
        nameof(OfficeAddress) => OfficeAddress,
        nameof(Phone) => Phone,
        nameof(Telephone) => Telephone,
        nameof(Email) => Email,
        nameof(Fax) => Fax,
        nameof(EnglishName) => EnglishName,
        nameof(WebSite) => WebSite,
        nameof(AmacId) => AmacId,
        nameof(Membership) => Membership,
        nameof(InstitutionType) => InstitutionType,
        nameof(RelatedCompany) => RelatedCompany,
        nameof(Description) => Description,
        nameof(HistoricalEvolution) => HistoricalEvolution,
        nameof(OrgStructureIntro) => OrgStructureIntro,
        nameof(FutureStrategicPlan) => FutureStrategicPlan,
        nameof(GoverningSecuritiesBureau) => GoverningSecuritiesBureau,
        nameof(ActualController) => ActualController,
        nameof(ContactName) => ContactName,
        nameof(ContactPhoneAndEmail) => ContactPhoneAndEmail,
        _ => null,
    };
}
