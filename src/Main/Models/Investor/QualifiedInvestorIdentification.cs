using System.ComponentModel;

namespace FMO.Models;


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum QualificationFileType
{
    None,

    [Description("金融资产")] Financial,

    [Description("三年年均收入")] Income,

    [Description("员工")] Employee,

    [Description("净资产")] NetAssets,

    //[Description("净资产+金融资产")] NetAndFinancial,

    [Description("金融机构")] FinancialInstitution,

    [Description("金融产品 ")] Product,
}


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum QualificationExperienceType
{
    None,

    [Description("具有2年以上证券、基金、期货、黄金、外汇等投资经历")] Invest,

    [Description("具有2年以上金融产品设计、投资、风险管理及相关工作经历")] Work,

    [Description("特殊专业机构投资者的高级管理人员")] Senior,

    [Description("获得职业资格认证的从事金融相关业务的注册会计师和律师")] Lawyer,

}



public enum QualificationType
{
    None,

    [Description("500万资产证明")] Assets500,

    [Description("300万资产证明")] Assets300,

    [Description("三年年均收入>50万")] Income,

    [Description("员工")] Employee,

    [Description("金融机构")] FinancialInstitution,

    [Description("金融产品 ")] Product,

    [Description("公益基金")] PublicWelfareFund,

    [Description("QFII")] QFII,

    [Description("RQFII")] RQFII,


}

public class InvestorFileInfo
{
    public int Id { get; set; }

    public string? Path { get; set; }

    public required string Name { get; set; }


    public DateTime Time { get; set; }

    public string? Hash { get; set; }

    /// <summary>
    /// 标注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 合格投资者认定
/// 
// （一）经有关金融监管部门批准设立的金融机构，包括证券公司、期货公司、基金管理公司及其子公司、商业银行、保险公司、信托公司、财务公司等；经行业协会备案或者登记的证券公司子公司、期货公司子公司、私募基金管理人。

//（二）上述机构面向投资者发行的理财产品，包括但不限于证券公司资产管理产品、基金管理公司及其子公司产品、期货公司资产管理产品、银行理财产品、保险产品、信托产品、经行业协会备案的私募基金。

//（三）社会保障基金、企业年金等养老基金，慈善基金等社会公益基金，合格境外机构投资者（QFII）、人民币合格境外机构投资者（RQFII）。

//（四）同时符合下列条件的法人或者其他组织：

//1.最近1年末净资产不低于2000万元；

//2.最近1年末金融资产不低于1000万元；

//3.具有2年以上证券、基金、期货、黄金、外汇等投资经历。

//（五）同时符合下列条件的自然人：

//1.金融资产不低于500万元，或者最近3年个人年均收入不低于50万元；

//2.具有2年以上证券、基金、期货、黄金、外汇等投资经历，或者具有2年以上金融产品设计、投资、风险管理及相关工作经历，或者属于本条第（一）项规定的专业投资者的高级管理人员、获得职业资格认证的从事金融相关业务的注册会计师和律师。

//前款所称金融资产，是指银行存款、股票、债券、基金份额、资产管理计划、银行理财产品、信托计划、保险产品、期货及其他衍生产品等。

///////////////////////////////////////////////////
//（一）净资产不低于1000万元的单位；
//（二）金融资产不低于300万元或者最近三年个人年均收入不低于50万元
//的个人
/// </summary>
public class InvestorQualification
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int InvestorId { get; set; }

    public string? IdentityCode { get; set; }

    public string? InvestorName { get; set; }

    /// <summary>
    /// 是否锁定不可修改
    /// </summary>
    public bool IsSealed { get; set; }

    public bool IsSettled { get; set; }

    /// <summary>
    /// 来源
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 外部id
    /// </summary>
    public string? ExternalId { get; set; }

    //public QualificationType Type { get; set; }

    /// <summary>
    /// 净资产
    /// </summary>
    public decimal? NetAssets { get; set; }

    /// <summary>
    /// 金融资产（万） （个人）
    /// </summary>
    public decimal? FinancialAssets { get; set; }

    /// <summary>
    /// 近三年年均收入
    /// </summary>
    public decimal? Income { get; set; }


    public QualificationFileType ProofType { get; set; }

    public QualificationExperienceType ExperienceType { get; set; }

    /// <summary>
    /// 普通 / 专业
    /// </summary>
    public QualifiedInvestorType Result { get; set; }

    /// <summary>
    /// 承诺函
    /// </summary>
    public SimpleFile? CommitmentLetter { get; set; }

    /// <summary>
    /// 基本信息表
    /// </summary>
    public SimpleFile? InfomationSheet { get; set; }

    /// <summary>
    /// 普通/专业投资者告知书
    /// </summary>
    public SimpleFile? Notice { get; set; }

    /// <summary>
    /// 税收声明
    /// </summary>
    public SimpleFile? TaxDeclaration { get; set; }

    /// <summary>
    /// 证明材料：资产证明、收入证明、资产负债表
    /// </summary>
    //public SimpleFile? CertificationMaterials { get; set; }

    //public List<SimpleFile>? CertificationFiles { get; set; }
    public MultiFile? CertificationFiles { get; set; }

    /// <summary>
    /// 投资经历
    /// </summary>
    public SimpleFile? ProofOfExperience { get; set; }

    /// <summary>
    /// 特殊投资者
    /// </summary>
    public SimpleFile? ProofOfSpecial { get; set; }


    /// <summary>
    /// 代理人证件
    /// </summary>
    public SimpleFile? Agent { get; set; }

    /// <summary>
    /// 授权书
    /// </summary>
    public SimpleFile? Authorization { get; set; }

    /// <summary>
    /// 代理人是法代
    /// </summary>
    public bool AgentIsLeagal { get; set; }

    /// <summary>
    /// 是否有错误
    /// </summary>
    public string? Error { get; set; }

    public bool HasError => Check().HasError;
     
    private bool IsFileExists(SimpleFile? info) => info?.File?.Exists ?? false;

    public (bool HasError, string? Info) Check()
    {
        bool er = false;
        List<string> info = new();
        if (Date.Year < 1970)
        {
            er = true;
            info.Add("无日期");
        }

        if (ProofType == QualificationFileType.None)
        {
            er = true;
            info.Add("无认证类型");
        }

        if (!IsFileExists(InfomationSheet))
        {
            er = true;
            info.Add("基本信息表");
        }

        if (!IsFileExists(CommitmentLetter))
        {
            er = true;
            info.Add("承诺函");
        }

        //if (!IsFileExists(Notice))
        //{
        //    er = true;
        //    info.Add("普通/专业投资者告知书");
        //}
        if (CertificationFiles is null || CertificationFiles?.Count == 0)
        {
            er = true;
            info.Add("证明文件");
        }
        if ((Result == QualifiedInvestorType.Professional && ProofType != QualificationFileType.Product && ProofType != QualificationFileType.FinancialInstitution && !IsFileExists(ProofOfExperience)))
        {
            er = true;
            info.Add("投资经历");
        }

        //string s = "";
        //switch (ProofType)
        //{
        //    case QualificationFileType.Financial:
        //        s = FinancialAssets switch { >= 500 => "500万金融资产证明", >= 300 => "300万金融资产证明", > 0 => "金融资产证明(无效金额)", _ => "金融资产证明(请填写金额)" };
        //        break;
        //    case QualificationFileType.Income:
        //        s = Income switch { >= 50 => "50万年均收入证明", > 0 => "年均收入证明(无效金额)", _ => "年均收入证明(请填写金额)" };
        //        break;
        //    case QualificationFileType.Employee:
        //        s = "管理人员工";
        //        break;
        //    case QualificationFileType.NetAssets:
        //        s = Result == QualifiedInvestorType.Professional ? (NetAssets switch { >= 2000 => "年末净资产>2000万", > 0 => "年末净资产(无效金额)", _ => "年末净资产(请填写金额)" } + FinancialAssets switch { >= 1000 => "1000万金融资产证明", > 0 => "金融资产证明(无效金额)", _ => "金融资产证明(请填写金额)" }) : NetAssets switch { >= 1000 => "年末净资产>1000万", _ => "年末净资产(请填写金额)" };
        //        break;
        //    case QualificationFileType.FinancialInstitution:
        //        s = "金融机构";
        //        break;
        //    case QualificationFileType.Product:
        //        s = "基金产品";
        //        break;
        //    default:
        //        break;
        //}

        //switch (ExperienceType)
        //{
        //    case QualificationExperienceType.Invest:
        //        s += " + 2年投资经历";
        //        break;
        //    case QualificationExperienceType.Work:
        //        s += " + 2年以上金融产品设计、投资、风险管理及相关工作经历";
        //        break;
        //    case QualificationExperienceType.Senior:
        //        s += " + 特殊专业机构投资者的高级管理人员";
        //        break;
        //    case QualificationExperienceType.Lawyer:
        //        s += " + 获得职业资格认证的从事金融相关业务的注册会计师和律师";
        //        break;
        //}


        return (er, string.Join('，', info));
    }
}
