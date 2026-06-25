using CommunityToolkit.Mvvm.ComponentModel;
using Vetting.Data;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public abstract partial class AutoSaveViewModel<T> : ObservableObject where T : class, new()
{
    public T Entity { get; }

    protected AutoSaveViewModel(T entity)
    {
        Entity = entity;
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != null) Save();
        };
    }

    private void Save()
    {
        using var db = new VettingDbContext();
        db.UpsertEntity(Entity);
    }
}

// ═══ 唯一项 ═══

public partial class ManagerViewModel(Manager entity) : AutoSaveViewModel<Manager>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? RegisterNo { get => Entity.RegisterNo; set { Entity.RegisterNo = value; OnPropertyChanged(); } }
    public string? ArtificialPerson { get => Entity.ArtificialPerson; set { Entity.ArtificialPerson = value; OnPropertyChanged(); } }
    public string? RegisterCapital { get => Entity.RegisterCapital; set { Entity.RegisterCapital = value; OnPropertyChanged(); } }
    public string? RealCapital { get => Entity.RealCapital; set { Entity.RealCapital = value; OnPropertyChanged(); } }
    public string? SetupDate { get => Entity.SetupDate; set { Entity.SetupDate = value; OnPropertyChanged(); } }
    public string? BusinessScope { get => Entity.BusinessScope; set { Entity.BusinessScope = value; OnPropertyChanged(); } }
    public string? RegisterAddress { get => Entity.RegisterAddress; set { Entity.RegisterAddress = value; OnPropertyChanged(); } }
    public string? OfficeAddress { get => Entity.OfficeAddress; set { Entity.OfficeAddress = value; OnPropertyChanged(); } }
    public string? Phone { get => Entity.Phone; set { Entity.Phone = value; OnPropertyChanged(); } }
    public string? Telephone { get => Entity.Telephone; set { Entity.Telephone = value; OnPropertyChanged(); } }
    public string? Email { get => Entity.Email; set { Entity.Email = value; OnPropertyChanged(); } }
    public string? Fax { get => Entity.Fax; set { Entity.Fax = value; OnPropertyChanged(); } }
    public string? EnglishName { get => Entity.EnglishName; set { Entity.EnglishName = value; OnPropertyChanged(); } }
    public string? WebSite { get => Entity.WebSite; set { Entity.WebSite = value; OnPropertyChanged(); } }
    public string? AmacId { get => Entity.AmacId; set { Entity.AmacId = value; OnPropertyChanged(); } }
    public MembershipLevel Membership { get => Entity.Membership; set { Entity.Membership = value; OnPropertyChanged(); } }
    public bool InvestmentAdvisor { get => Entity.InvestmentAdvisor; set { Entity.InvestmentAdvisor = value; OnPropertyChanged(); } }
    public string? InstitutionType { get => Entity.InstitutionType; set { Entity.InstitutionType = value; OnPropertyChanged(); } }
    public string? RelatedCompany { get => Entity.RelatedCompany; set { Entity.RelatedCompany = value; OnPropertyChanged(); } }
    public string? ActualController { get => Entity.ActualController; set { Entity.ActualController = value; OnPropertyChanged(); } }
    public string? ContactName { get => Entity.ContactName; set { Entity.ContactName = value; OnPropertyChanged(); } }
    public string? ContactPhoneAndEmail { get => Entity.ContactPhoneAndEmail; set { Entity.ContactPhoneAndEmail = value; OnPropertyChanged(); } }
    public string? GoverningSecuritiesBureau { get => Entity.GoverningSecuritiesBureau; set { Entity.GoverningSecuritiesBureau = value; OnPropertyChanged(); } }
}

public partial class CreditStandingViewModel(CreditStanding entity) : AutoSaveViewModel<CreditStanding>(entity)
{
    public string? AdminPenalty { get => Entity.AdminPenalty; set { Entity.AdminPenalty = value; OnPropertyChanged(); } }
    public string? BusinessException { get => Entity.BusinessException; set { Entity.BusinessException = value; OnPropertyChanged(); } }
    public string? SeriousIllegal { get => Entity.SeriousIllegal; set { Entity.SeriousIllegal = value; OnPropertyChanged(); } }
    public string? ExecutionInfo { get => Entity.ExecutionInfo; set { Entity.ExecutionInfo = value; OnPropertyChanged(); } }
    public string? SecuritiesDishonesty { get => Entity.SecuritiesDishonesty; set { Entity.SecuritiesDishonesty = value; OnPropertyChanged(); } }
    public string? CorePersonDishonesty { get => Entity.CorePersonDishonesty; set { Entity.CorePersonDishonesty = value; OnPropertyChanged(); } }
    public string? FundAssocCreditReport { get => Entity.FundAssocCreditReport; set { Entity.FundAssocCreditReport = value; OnPropertyChanged(); } }
    public string? AICQuery { get => Entity.AICQuery; set { Entity.AICQuery = value; OnPropertyChanged(); } }
    public string? CSRCQuery { get => Entity.CSRCQuery; set { Entity.CSRCQuery = value; OnPropertyChanged(); } }
    public string? AssociationQuery { get => Entity.AssociationQuery; set { Entity.AssociationQuery = value; OnPropertyChanged(); } }
    public string? JudicialQuery { get => Entity.JudicialQuery; set { Entity.JudicialQuery = value; OnPropertyChanged(); } }
    public string? AntiMoneyLaundering { get => Entity.AntiMoneyLaundering; set { Entity.AntiMoneyLaundering = value; OnPropertyChanged(); } }
}

public partial class InvestmentInfoViewModel(InvestmentInfo entity) : AutoSaveViewModel<InvestmentInfo>(entity)
{
    // 理念
    public string? Target { get => Entity.Target; set { Entity.Target = value; OnPropertyChanged(); } }
    public string? Philosophy { get => Entity.Philosophy; set { Entity.Philosophy = value; OnPropertyChanged(); } }
    // 流程
    public string? Research { get => Entity.Research; set { Entity.Research = value; OnPropertyChanged(); } }
    public string? Decision { get => Entity.Decision; set { Entity.Decision = value; OnPropertyChanged(); } }
    public string? Trading { get => Entity.Trading; set { Entity.Trading = value; OnPropertyChanged(); } }
    public string? Evaluation { get => Entity.Evaluation; set { Entity.Evaluation = value; OnPropertyChanged(); } }
    public string? RiskControl { get => Entity.RiskControl; set { Entity.RiskControl = value; OnPropertyChanged(); } }
    public string? PortfolioAdjust { get => Entity.PortfolioAdjust; set { Entity.PortfolioAdjust = value; OnPropertyChanged(); } }
    public string? PositionBuilding { get => Entity.PositionBuilding; set { Entity.PositionBuilding = value; OnPropertyChanged(); } }
    public string? CommitteeRole { get => Entity.CommitteeRole; set { Entity.CommitteeRole = value; OnPropertyChanged(); } }
    public string? ResearchAuthority { get => Entity.ResearchAuthority; set { Entity.ResearchAuthority = value; OnPropertyChanged(); } }
    public string? SystemAndData { get => Entity.SystemAndData; set { Entity.SystemAndData = value; OnPropertyChanged(); } }
    public string? DataStorage { get => Entity.DataStorage; set { Entity.DataStorage = value; OnPropertyChanged(); } }
    public string? TradingControl { get => Entity.TradingControl; set { Entity.TradingControl = value; OnPropertyChanged(); } }
    public string? TradingErrorFix { get => Entity.TradingErrorFix; set { Entity.TradingErrorFix = value; OnPropertyChanged(); } }
    public string? AbnormalTrading { get => Entity.AbnormalTrading; set { Entity.AbnormalTrading = value; OnPropertyChanged(); } }
    public string? AccountFairness { get => Entity.AccountFairness; set { Entity.AccountFairness = value; OnPropertyChanged(); } }
}

public partial class RiskControlViewModel(RiskControl entity) : AutoSaveViewModel<RiskControl>(entity)
{
    public string? SystemIntro { get => Entity.SystemIntro; set { Entity.SystemIntro = value; OnPropertyChanged(); } }
    public string? DecisionMechanism { get => Entity.DecisionMechanism; set { Entity.DecisionMechanism = value; OnPropertyChanged(); } }
    public string? RiskMgmtCommittee { get => Entity.RiskMgmtCommittee; set { Entity.RiskMgmtCommittee = value; OnPropertyChanged(); } }
    public string? DrawdownControl { get => Entity.DrawdownControl; set { Entity.DrawdownControl = value; OnPropertyChanged(); } }
    public string? SystemicRiskResponse { get => Entity.SystemicRiskResponse; set { Entity.SystemicRiskResponse = value; OnPropertyChanged(); } }
    public string? TradingMonitoring { get => Entity.TradingMonitoring; set { Entity.TradingMonitoring = value; OnPropertyChanged(); } }
    public string? RiskMeasures { get => Entity.RiskMeasures; set { Entity.RiskMeasures = value; OnPropertyChanged(); } }
    public string? ManualVsSystem { get => Entity.ManualVsSystem; set { Entity.ManualVsSystem = value; OnPropertyChanged(); } }
    public string? RiskMeasurement { get => Entity.RiskMeasurement; set { Entity.RiskMeasurement = value; OnPropertyChanged(); } }
    public string? MaxDrawdownTolerance { get => Entity.MaxDrawdownTolerance; set { Entity.MaxDrawdownTolerance = value; OnPropertyChanged(); } }
    public string? TailRisk { get => Entity.TailRisk; set { Entity.TailRisk = value; OnPropertyChanged(); } }
    public string? RiskReserve { get => Entity.RiskReserve; set { Entity.RiskReserve = value; OnPropertyChanged(); } }
    public string? LiquidityMgmt { get => Entity.LiquidityMgmt; set { Entity.LiquidityMgmt = value; OnPropertyChanged(); } }
    public string? InsiderTradingPrevention { get => Entity.InsiderTradingPrevention; set { Entity.InsiderTradingPrevention = value; OnPropertyChanged(); } }
    public string? EmployeeTradingMonitor { get => Entity.EmployeeTradingMonitor; set { Entity.EmployeeTradingMonitor = value; OnPropertyChanged(); } }
    public string? ProductFairness { get => Entity.ProductFairness; set { Entity.ProductFairness = value; OnPropertyChanged(); } }
}

// ═══ 列表项 ═══

public partial class StaffVM(Staff entity) : AutoSaveViewModel<Staff>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Title { get => Entity.Title; set { Entity.Title = value; OnPropertyChanged(); } }
    public EducationLevel Education { get => Entity.Education; set { Entity.Education = value; OnPropertyChanged(); } }
    public string? Profile { get => Entity.Profile; set { Entity.Profile = value; OnPropertyChanged(); } }
    public string? IdNumber { get => Entity.IdNumber; set { Entity.IdNumber = value; OnPropertyChanged(); } }
    public string? Years { get => Entity.Years; set { Entity.Years = value; OnPropertyChanged(); } }
    public int? Age => Entity.Age;
    public DateTime? BirthDate { get => Entity.BirthDate; set { Entity.BirthDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(Age)); } }
    public string? Specialty { get => Entity.Specialty; set { Entity.Specialty = value; OnPropertyChanged(); } }
    public string? ResearchFocus { get => Entity.ResearchFocus; set { Entity.ResearchFocus = value; OnPropertyChanged(); } }
    public string? MobilePhone { get => Entity.MobilePhone; set { Entity.MobilePhone = value; OnPropertyChanged(); } }
    public string? Telephone { get => Entity.Telephone; set { Entity.Telephone = value; OnPropertyChanged(); } }
    public string? Email { get => Entity.Email; set { Entity.Email = value; OnPropertyChanged(); } }
    public string? Role { get => Entity.Role; set { Entity.Role = value; OnPropertyChanged(); } }
}

public partial class ShareholderVM(Shareholder entity) : AutoSaveViewModel<Shareholder>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Ratio { get => Entity.Ratio; set { Entity.Ratio = value; OnPropertyChanged(); } }
    public string? Intro { get => Entity.Intro; set { Entity.Intro = value; OnPropertyChanged(); } }
    public string? Nature { get => Entity.Nature; set { Entity.Nature = value; OnPropertyChanged(); } }
    public string? PaidInAmount { get => Entity.PaidInAmount; set { Entity.PaidInAmount = value; OnPropertyChanged(); } }
    public string? IdentityBrief { get => Entity.IdentityBrief; set { Entity.IdentityBrief = value; OnPropertyChanged(); } }
    public string? CompanyRole { get => Entity.CompanyRole; set { Entity.CompanyRole = value; OnPropertyChanged(); } }
    public string? IsCoreResearch { get => Entity.IsCoreResearch; set { Entity.IsCoreResearch = value; OnPropertyChanged(); } }
    public string? CompanyPosition { get => Entity.CompanyPosition; set { Entity.CompanyPosition = value; OnPropertyChanged(); } }
    public bool IsActualController { get => Entity.IsActualController; set { Entity.IsActualController = value; OnPropertyChanged(); } }
}

public partial class DepartmentVM(Department entity) : AutoSaveViewModel<Department>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Headcount { get => Entity.Headcount; set { Entity.Headcount = value; OnPropertyChanged(); } }
    public string? MainFunction { get => Entity.MainFunction; set { Entity.MainFunction = value; OnPropertyChanged(); } }
    public string? Head { get => Entity.Head; set { Entity.Head = value; OnPropertyChanged(); } }
    public string? HasPartTime { get => Entity.HasPartTime; set { Entity.HasPartTime = value; OnPropertyChanged(); } }
}

public partial class StrategyVM(Strategy entity) : AutoSaveViewModel<Strategy>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Manager { get => Entity.Manager; set { Entity.Manager = value; OnPropertyChanged(); } }
    public string? Scale { get => Entity.Scale; set { Entity.Scale = value; OnPropertyChanged(); } }
    public string? Type { get => Entity.Type; set { Entity.Type = value; OnPropertyChanged(); } }
    public string? Capacity { get => Entity.Capacity; set { Entity.Capacity = value; OnPropertyChanged(); } }
    public string? SameStrategyCount { get => Entity.SameStrategyCount; set { Entity.SameStrategyCount = value; OnPropertyChanged(); } }
    public string? FactorPool { get => Entity.FactorPool; set { Entity.FactorPool = value; OnPropertyChanged(); } }
    public string? CapacityAndRisk { get => Entity.CapacityAndRisk; set { Entity.CapacityAndRisk = value; OnPropertyChanged(); } }
    public string? Replicated { get => Entity.Replicated; set { Entity.Replicated = value; OnPropertyChanged(); } }
    public string? StyleExposure { get => Entity.StyleExposure; set { Entity.StyleExposure = value; OnPropertyChanged(); } }
    public string? Turnover { get => Entity.Turnover; set { Entity.Turnover = value; OnPropertyChanged(); } }
    public string? HoldingPeriod { get => Entity.HoldingPeriod; set { Entity.HoldingPeriod = value; OnPropertyChanged(); } }
    public string? WeightAllocation { get => Entity.WeightAllocation; set { Entity.WeightAllocation = value; OnPropertyChanged(); } }
    public string? WarningStoploss { get => Entity.WarningStoploss; set { Entity.WarningStoploss = value; OnPropertyChanged(); } }
}

public partial class FundInfoVM(FundInfo entity) : AutoSaveViewModel<FundInfo>(entity)
{
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Code { get => Entity.Code; set { Entity.Code = value; OnPropertyChanged(); } }
    public string? Duration { get => Entity.Duration; set { Entity.Duration = value; OnPropertyChanged(); } }
    public string? Type { get => Entity.Type; set { Entity.Type = value; OnPropertyChanged(); } }
    public string? MinSubscription { get => Entity.MinSubscription; set { Entity.MinSubscription = value; OnPropertyChanged(); } }
    public string? Frequency { get => Entity.Frequency; set { Entity.Frequency = value; OnPropertyChanged(); } }
    public string? Custodian { get => Entity.Custodian; set { Entity.Custodian = value; OnPropertyChanged(); } }
    public string? RiskLevel { get => Entity.RiskLevel; set { Entity.RiskLevel = value; OnPropertyChanged(); } }
    public string? MgmtFee { get => Entity.MgmtFee; set { Entity.MgmtFee = value; OnPropertyChanged(); } }
    public string? StrategyType { get => Entity.StrategyType; set { Entity.StrategyType = value; OnPropertyChanged(); } }
    public string? Scale { get => Entity.Scale; set { Entity.Scale = value; OnPropertyChanged(); } }
    public string? UnitNav { get => Entity.UnitNav; set { Entity.UnitNav = value; OnPropertyChanged(); } }
    public string? AnnualReturn { get => Entity.AnnualReturn; set { Entity.AnnualReturn = value; OnPropertyChanged(); } }
    public string? MaxDrawdown { get => Entity.MaxDrawdown; set { Entity.MaxDrawdown = value; OnPropertyChanged(); } }
    public string? Sharpe { get => Entity.Sharpe; set { Entity.Sharpe = value; OnPropertyChanged(); } }
    public string? EstablishmentDate { get => Entity.EstablishmentDate; set { Entity.EstablishmentDate = value; OnPropertyChanged(); } }
    public string? Scope { get => Entity.Scope; set { Entity.Scope = value; OnPropertyChanged(); } }
}

public partial class AwardVM(Award entity) : AutoSaveViewModel<Award>(entity)
{
    public string? Time { get => Entity.Time; set { Entity.Time = value; OnPropertyChanged(); } }
    public string? Entity2 { get => Entity.Entity; set { Entity.Entity = value; OnPropertyChanged(); } }
    public string? Name { get => Entity.Name; set { Entity.Name = value; OnPropertyChanged(); } }
    public string? Evaluator { get => Entity.Evaluator; set { Entity.Evaluator = value; OnPropertyChanged(); } }
}

public partial class AUMVM(AUM entity) : AutoSaveViewModel<AUM>(entity)
{
    public string? Year { get => Entity.Year; set { Entity.Year = value; OnPropertyChanged(); } }
    public string? Scale { get => Entity.Scale; set { Entity.Scale = value; OnPropertyChanged(); } }
}

public partial class DrawdownRecordVM(DrawdownRecord entity) : AutoSaveViewModel<DrawdownRecord>(entity)
{
    public string? ProductName { get => Entity.ProductName; set { Entity.ProductName = value; OnPropertyChanged(); } }
    public string? Date { get => Entity.Date; set { Entity.Date = value; OnPropertyChanged(); } }
    public string? Amplitude { get => Entity.Amplitude; set { Entity.Amplitude = value; OnPropertyChanged(); } }
    public string? Reason { get => Entity.Reason; set { Entity.Reason = value; OnPropertyChanged(); } }
    public string? Countermeasures { get => Entity.Countermeasures; set { Entity.Countermeasures = value; OnPropertyChanged(); } }
    public string? RecoveryDays { get => Entity.RecoveryDays; set { Entity.RecoveryDays = value; OnPropertyChanged(); } }
}

public partial class FinancialStatementVM(FinancialStatement entity) : AutoSaveViewModel<FinancialStatement>(entity)
{
    public string? Year { get => Entity.Year; set { Entity.Year = value; OnPropertyChanged(); } }
    public string? TotalAssets { get => Entity.TotalAssets; set { Entity.TotalAssets = value; OnPropertyChanged(); } }
    public string? TotalLiabilities { get => Entity.TotalLiabilities; set { Entity.TotalLiabilities = value; OnPropertyChanged(); } }
    public string? OwnersEquity { get => Entity.OwnersEquity; set { Entity.OwnersEquity = value; OnPropertyChanged(); } }
    public string? Revenue { get => Entity.Revenue; set { Entity.Revenue = value; OnPropertyChanged(); } }
    public string? Cost { get => Entity.Cost; set { Entity.Cost = value; OnPropertyChanged(); } }
    public string? NetProfit { get => Entity.NetProfit; set { Entity.NetProfit = value; OnPropertyChanged(); } }
}

public partial class QAVM(QA entity) : AutoSaveViewModel<QA>(entity)
{
    public int Source { get => Entity.Source; set { Entity.Source = value; OnPropertyChanged(); } }
    public string? Question { get => Entity.Question; set { Entity.Question = value; OnPropertyChanged(); } }
    public string? Answer { get => Entity.Answer; set { Entity.Answer = value; OnPropertyChanged(); } }
}
