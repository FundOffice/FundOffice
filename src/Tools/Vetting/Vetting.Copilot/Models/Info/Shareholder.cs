namespace Vetting.Copilot.Models.Info;

public class Shareholder : IResolve
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Ratio { get; set; }
    public string? Intro { get; set; }
    public string? Nature { get; set; }
    public string? PaidInAmount { get; set; }
    public string? IdentityBrief { get; set; }
    public string? CompanyRole { get; set; }
    public string? IsCoreResearch { get; set; }
    public string? CompanyPosition { get; set; }
    public bool IsActualController { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(Ratio) => Ratio,
        nameof(Intro) => Intro,
        nameof(Nature) => Nature,
        nameof(PaidInAmount) => PaidInAmount,
        nameof(IdentityBrief) => IdentityBrief,
        nameof(CompanyRole) => CompanyRole,
        nameof(IsCoreResearch) => IsCoreResearch,
        nameof(CompanyPosition) => CompanyPosition,
        _ => null,
    };
}
