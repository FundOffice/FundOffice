using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class SubjectFundMappingJson : JsonBase
{
    /// <summary>
    /// 产品编码（必填，最大长度32）
    /// </summary>
    [JsonPropertyName("productNo")]
    public string ProductNo { get; set; }

    /// <summary>
    /// 产品名称（必填，最大长度250）
    /// </summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; set; }

    /// <summary>
    /// 公司名称（必填，最大长度128）
    /// </summary>
    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; }

    /// <summary>
    /// 产品状态（必填，字典见附录3.4，最大长度2）
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// 成立时间（必填，格式yyyyMMdd）
    /// </summary>
    [JsonPropertyName("publishDate")]
    public string PublishDate { get; set; }

    /// <summary>
    /// 托管机构（非必填，最大长度128）
    /// </summary>
    [JsonPropertyName("custodianOrg")]
    public string CustodianOrg { get; set; }

    /// <summary>
    /// 外包机构（非必填，最大长度128）
    /// </summary>
    [JsonPropertyName("outsourcingOrg")]
    public string OutsourcingOrg { get; set; }

    /// <summary>
    /// 母产品代码（非必填，仅子产品返回，最大长度32）
    /// </summary>
    [JsonPropertyName("parentProductNo")]
    public string ParentProductNo { get; set; }

    /// <summary>
    /// 母产品名称（非必填，仅子产品返回，最大长度250）
    /// </summary>
    [JsonPropertyName("parentProductName")]
    public string ParentProductName { get; set; }


    public SubjectFundMapping ToObject()
    {
        var sc = !string.IsNullOrWhiteSpace(ParentProductName) ? ProductName.Replace(ProductName, "") : "";
        return new SubjectFundMapping
        {
            FundCode = ProductNo,
            FundName = ProductName,
            MasterCode = ParentProductNo,
            MasterName = ParentProductName,
            ShareClass = sc,
            Status = GetStatus(Status),
        };
    }


    public FundStatus GetStatus(string s)
    {
        return s switch
        {
            "0" => FundStatus.Initiate,
            "1" => FundStatus.Normal,
            "2" => FundStatus.StartLiquidation,
            "3" => FundStatus.Liquidation,
            _ => FundStatus.Unk,
        };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。