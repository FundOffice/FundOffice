using FMO.Models;
using System.Text.RegularExpressions;

namespace FMO.ESigning.MeiShi;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
public class CustomerJson
{


    public int? customerId { get; set; }

    public int? customerType { get; set; }

    public string customerName { get; set; }

    //public int? customerLevel { get; set; }

    public int? investorType { get; set; }

    public int? riskType { get; set; }

    //public int? riskState { get; set; }

    //public int? realNameState { get; set; }

    //public int? isDelete { get; set; }

    //public int? investedFundsNum { get; set; }

    //public string totalInvestmentAmount { get; set; }

    //public TotalInvestmentAmountDTO totalInvestmentAmountDTO { get; set; }

    //public int? fundsAlreadyOwnedNum { get; set; }

    //public string alreadyOwnedProductNames { get; set; }

    //public string alreadyOwnedProductIds { get; set; }

    //public string holdTotalAmount { get; set; }

    //public HoldTotalAmountDTO holdTotalAmountDTO { get; set; }

    //public TotalValueDTO totalValueDTO { get; set; }

    //public string holdingEarnings { get; set; }

    //public HoldingEarningsDTO holdingEarningsDTO { get; set; }

    public string mobile { get; set; }

    public string email { get; set; }

    public int? cardType { get; set; }

    public string cardNumber { get; set; }

    //public int? forbidden { get; set; }

    //public string saleUserName { get; set; }

    //public string remark { get; set; }

    //public string dealer { get; set; }

    //public string currentAutomatic { get; set; }

    //public string historyAutomatic { get; set; }

    //public string currentManual { get; set; }

    //public string historyManual { get; set; }

    //public string channelNames { get; set; }

    //public string customizeChannelName { get; set; }

    //public string origin { get; set; }

    //public string otherOrigin { get; set; }

    //public int? isVIP { get; set; }

    //public string nickName { get; set; }

    //public string openId { get; set; }

    //public string registerTime { get; set; }

    //public string firstTradeTime { get; set; }

    //public int? sourceType { get; set; }

    public string createTime { get; set; }

    //public string riskLimitDate { get; set; }

    //public int? isRiskLongTime { get; set; }

    //public string isExpire { get; set; }

    //public string legalPersonCardNumber { get; set; }

    //public string authorizedRepresentativeCardNumber { get; set; }

    //public string customerGroupNames { get; set; }

    //public int? companyCode { get; set; }

    //public int? identifyStatus { get; set; }

    //public string identifyLimitDate { get; set; }
    /// <summary>
    /// 2028年11月24日
    /// </summary>
    //public string identifyLimitDateString { get; set; }

    //public string riskLimitDateString { get; set; }

    //public string channelType { get; set; }
    ///// <summary>
    ///// 直销
    ///// </summary>
    //public string channelTypeString { get; set; }

    //public string lastLoginTime { get; set; }

    //public int? isSubscribed { get; set; }

    //public int? isCanSubscribed { get; set; }

    public string inviterName { get; set; }

    //public string oneLevelChannelName { get; set; }

    //public string twoLevelChannelName { get; set; }

    //public string customizeElementMap { get; set; }

    public string personAddress { get; set; }

    //public string companyCardValidStartTime { get; set; }

    //public string companyCardValidEndTime { get; set; }

    //public int? companyIsCardLongTime { get; set; }

    //public string businessScope { get; set; }

    //public string feeNetValue { get; set; }

    //public string archivalOrganization { get; set; }
    ///// <summary>
    ///// 6400万
    ///// </summary>
    //public string productScale { get; set; }

    //public string customizeElementInfoMap { get; set; }

    //public string customerSource { get; set; }

    //public string customerSourceTime { get; set; }

    //public int? maintenanceType { get; set; }

    //public string timeMaintenanceType { get; set; }

    //public string managerTakeOverTime { get; set; }

    //public int? aptnessType { get; set; }

    //public string netPurchaseAmount { get; set; }

    //public string netPurchaseAmountDTO { get; set; }

    //public string cumulativeIncome { get; set; }

    //public CumulativeIncomeDTO cumulativeIncomeDTO { get; set; }

    //public List<TrusteeOriginalDataItem> trusteeOriginalData { get; set; }

    //public string trusteeOriginalDataString { get; set; }

    //public string saleUserIds { get; set; }

    //public string taBankNumber { get; set; }
    /// <summary>
    /// 中国建设银行股份有限公司总行
    /// </summary>
    //public string taBankName { get; set; }

    //public string cumulativeNetInvestment { get; set; }

    //public CumulativeNetInvestmentDTO cumulativeNetInvestmentDTO { get; set; }

    //public string latestHoldingCost { get; set; }

    //public LatestHoldingCostDTO latestHoldingCostDTO { get; set; }

    //public string publishHoldingCost { get; set; }

    //public PublishHoldingCostDTO publishHoldingCostDTO { get; set; }

    //public int? isCustomerlabel { get; set; }

    //public string cnfundAssociationInvestorType { get; set; }


    public static IDType FromJsValue(int? jsValue)
    {
        return jsValue switch
        {
            1 => IDType.IdentityCard,                            // 身份证 → 居民身份证
            2 => IDType.HouseholdRegister,                       // 户口本
            3 => IDType.ForeignPermanentResidentID,              // 外国人永久居留身份证
            4 => IDType.PassportChina,                           // 护照 → 中国护照（这里 JS 的“护照”通常指中国护照）
            5 => IDType.SoldierID,                               // 士兵证
            6 => IDType.HongKongMacauPass,                       // 港澳通行证 → 港澳居民来往内地通行证
            7 => IDType.TaiwanCompatriotsID,                     // 台湾居民往来大陆通行证 → 台胞证
            8 => IDType.BusinessLicenseNumber,                   // 营业执照 → 营业执照号（或可选 UnifiedSocialCreditCode，按业务定）
            9 => IDType.OrganizationCode,                        // 组织机构代码证
            10 => IDType.Institusion,                            // 基金会 → 暂归为机构（你可能需要调整）
            11 => IDType.Institusion,                            // 行政机关 → 机构
            12 => IDType.Other,                                  // 其它
            13 => IDType.ProductFilingCode,                // 产品备案编号/登记证书 → 产品备案编码（或 ProductRegistrationCode）
            14 => IDType.OfficerID,                              // 军官证
            15 => IDType.PassportForeign,                        // 外籍护照 → 外国护照
            16 => IDType.CivilianID,                             // 文职证
            17 => IDType.PoliceID,                               // 警官证
            18 => IDType.Institusion,                            // 社会团体 → 机构（或可能需要单独扩展）
            19 => IDType.Institusion,                            // 军队 → 机构（或特殊处理）
            20 => IDType.Institusion,                            // 武警 → 机构
            21 => IDType.Institusion,                            // 下属机构
            22 => IDType.Approval,                               // 批文
            23 => IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents, // 港澳台居民居住证
            _ => IDType.Unknown
        };
    }


    private AmacInvestorType GetInvestorType()
    {
        if (string.IsNullOrWhiteSpace(inviterName)) return AmacInvestorType.None;

        if (inviterName.Contains("私募"))
            return AmacInvestorType.PrivateFundProduct;
        else if (Regex.IsMatch(inviterName, ".{2,}期货.*资产管理计划"))
            return AmacInvestorType.FuturesCompanyAssetManagementPlan;
        else if (Regex.IsMatch(inviterName, ".{2,}证券.*资产管理计划"))
            return AmacInvestorType.SecuritiesCompanyAssetManagementPlan;
        else if (Regex.IsMatch(inviterName, ".{2,}基金.*资产管理计划"))
            return AmacInvestorType.FundCompanyAssetManagementPlan;
        else if (Regex.IsMatch(inviterName, "有限公司"))
            return AmacInvestorType.LegalEntity;
        else if (Regex.IsMatch(inviterName, "有限合伙"))
            return AmacInvestorType.NonLegalEntity;

        if (customerType == 1)
            return AmacInvestorType.NonEmployee;

        return AmacInvestorType.None;
    }

    public Investor To()
    {
        return new Investor
        {
            Name = customerName,
            Email = email,
            Phone = mobile,
            Type = GetInvestorType(),
            Identity = new Identity { Id = cardNumber, Type = FromJsValue(cardType) },
            EntityType = customerType switch { 1 => EntityType.Natural, 2 => EntityType.Institution, 3 => EntityType.Product, _ => EntityType.Unk },
            Address = personAddress,
            CreateTime = DateTime.Parse(createTime),
            RiskEvaluation = riskType switch { < 6 => (RiskEvaluation)riskType, 6 => RiskEvaluation.C5, _ => RiskEvaluation.Unk },
        };
    }

}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。