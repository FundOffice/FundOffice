using System.Text.Json.Serialization;

public class ManagerInfo
{
    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// 南京汇新私募基金管理有限公司
    /// </summary>
    [JsonPropertyName("managerName")]
    public string? ManagerName { get; set; }

    /// <summary>
    /// 李<em>人</em>洁
    /// </summary>
    [JsonPropertyName("artificialPersonName")]
    public string? ArtificialPersonName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("registerNo")]
    public string? RegisterNo { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("establishDate")]
    public long EstablishDate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("managerHasProduct")]
    public bool ManagerHasProduct { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("registerDate")]
    public long RegisterDate { get; set; }

    /// <summary>
    /// 江苏省南京市秦淮区永智路5号五号楼F栋108-55室
    /// </summary>
    [JsonPropertyName("registerAddress")]
    public string? RegisterAddress { get; set; }

    /// <summary>
    /// 江苏省
    /// </summary>
    [JsonPropertyName("registerProvince")]
    public string? RegisterProvince { get; set; }

    /// <summary>
    /// 南京市
    /// </summary>
    [JsonPropertyName("registerCity")]
    public string? RegisterCity { get; set; }

    /// <summary>
    /// 江苏省
    /// </summary>
    [JsonPropertyName("regAdrAgg")]
    public string? RegAdrAgg { get; set; }

    /// <summary>
    /// 江苏省
    /// </summary>
    [JsonPropertyName("officeAdrAgg")]
    public string? OfficeAdrAgg { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("fundCount")]
    public int FundCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("paidInCapital")]
    public double PaidInCapital { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("subscribedCapital")]
    public int SubscribedCapital { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("hasSpecialTips")]
    public bool HasSpecialTips { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("hasCreditTips")]
    public bool HasCreditTips { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("regCoordinate")]
    public string? RegCoordinate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("officeCoordinate")]
    public string? OfficeCoordinate { get; set; }

    /// <summary>
    /// 江苏省南京市秦淮区永智路5号五号楼F栋108-55室
    /// </summary>
    [JsonPropertyName("officeAddress")]
    public string? OfficeAddress { get; set; }

    /// <summary>
    /// 江苏省
    /// </summary>
    [JsonPropertyName("officeProvince")]
    public string? OfficeProvince { get; set; }

    /// <summary>
    /// 南京市
    /// </summary>
    [JsonPropertyName("officeCity")]
    public string? OfficeCity { get; set; }

    /// <summary>
    /// 私募股权、创业投资基金管理人
    /// </summary>
    [JsonPropertyName("primaryInvestType")]
    public string? PrimaryInvestType { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("fundTypeScaleMap")]
    public string? FundTypeScaleMap { get; set; }

    /// <summary>
    /// 非会员机构
    /// </summary>
    [JsonPropertyName("memberType")]
    public string? MemberType { get; set; }

    /// <summary>
    /// 有限责任公司
    /// </summary>
    [JsonPropertyName("orgForm")]
    public string? OrgForm { get; set; }


    public override string ToString()
    {
        return ManagerName??"";
    }
}
