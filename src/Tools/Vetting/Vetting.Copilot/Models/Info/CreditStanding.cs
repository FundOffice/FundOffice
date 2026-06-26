namespace Vetting.Copilot.Models.Info;

public class CreditStanding : IResolve
{
    public int Id { get; set; } = 1;
    public string? AdminPenalty { get; set; }
    public string? BusinessException { get; set; }
    public string? SeriousIllegal { get; set; }
    public string? ExecutionInfo { get; set; }
    public string? SecuritiesDishonesty { get; set; }
    public string? CorePersonDishonesty { get; set; }
    public string? FundAssocCreditReport { get; set; }
    public string? AICQuery { get; set; }
    public string? CSRCQuery { get; set; }
    public string? AssociationQuery { get; set; }
    public string? JudicialQuery { get; set; }
    public string? RegPenalty3Y { get; set; }
    public string? AdminPenalty3Y { get; set; }
    public string? MoneyLaundering5Y { get; set; }
    public string? FalseMaterials3Y { get; set; }
    public string? MajorChange { get; set; }
    public string? MajorOperationalRisk { get; set; }
    public string? PendingInvestigation { get; set; }
    public string? NegativeReports { get; set; }
    public string? ExecViolation { get; set; }
    public string? OtherNegative { get; set; }
    public string? AntiMoneyLaundering { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(AdminPenalty) => AdminPenalty,
        nameof(BusinessException) => BusinessException,
        nameof(SeriousIllegal) => SeriousIllegal,
        nameof(ExecutionInfo) => ExecutionInfo,
        nameof(SecuritiesDishonesty) => SecuritiesDishonesty,
        nameof(CorePersonDishonesty) => CorePersonDishonesty,
        nameof(FundAssocCreditReport) => FundAssocCreditReport,
        nameof(AICQuery) => AICQuery,
        nameof(CSRCQuery) => CSRCQuery,
        nameof(AssociationQuery) => AssociationQuery,
        nameof(JudicialQuery) => JudicialQuery,
        nameof(AntiMoneyLaundering) => AntiMoneyLaundering,
        _ => null,
    };
}
