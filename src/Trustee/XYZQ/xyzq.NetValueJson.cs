using System.Text.Json.Serialization;

namespace FMO.Trustee;

internal class NetValueJson : JsonBase
{
    /// <summary>
    /// 产品编码
    /// </summary>
    [JsonPropertyName("fundcode")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundname")]
    public string FundName { get; set; } = null!;

    /// <summary>
    /// 单位净值
    /// </summary>
    [JsonPropertyName("dwjz")]
    public string Dwjz { get; set; } = null!;

    /// <summary>
    /// 累计单位净值
    /// </summary>
    [JsonPropertyName("ljdwjz")]
    public string Ljdwjz { get; set; } = null!;

    /// <summary>
    /// 资产净值
    /// </summary>
    [JsonPropertyName("zcjz")]
    public string Zcjz { get; set; } = null!;

    /// <summary>
    /// 资产份额
    /// </summary>
    [JsonPropertyName("zcfe")]
    public string Zcfe { get; set; } = null!;

    /// <summary>
    /// 资产总值
    /// </summary>
    [JsonPropertyName("zchj")]
    public string Zchj { get; set; } = null!;

    /// <summary>
    /// 确认状态
    /// </summary>
    [JsonPropertyName("sfqr")]
    public string Sfqr { get; set; } = null!;

    /// <summary>
    /// 净值日期
    /// </summary>
    [JsonPropertyName("rq")]
    public string NetDate { get; set; } = null!;

    /// <summary>
    /// 是否分级
    /// </summary>
    [JsonPropertyName("ifgrading")]
    public string IfGrading { get; set; } = null!;

    /// <summary>
    /// 分级类型
    /// </summary>
    [JsonPropertyName("investfunds")]
    public string InvestFunds { get; set; } = null!;


}
