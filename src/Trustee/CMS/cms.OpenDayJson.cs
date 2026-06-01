using FMO.Models;
using FMO.Utilities;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

public class OpenDayJson : JsonBase
{
    /// <summary>
    /// 基金产品信息数据传输对象 (DTO)
    /// </summary>

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; } = null!;

    /// <summary>
    /// 开放日期
    /// 格式：yyyymmdd
    /// </summary>
    [JsonPropertyName("openDate")]
    public string OpenDate { get; set; } = null!;

    /// <summary>
    /// 开放类型
    /// 可选值：
    /// - 固开 (固开申购固开赎回)
    /// - 临开 (临开申购临开赎回)
    /// - 申购固开
    /// - 申购临开
    /// - 赎回固开
    /// - 赎回临开
    /// - 申购固开赎回临开
    /// - 申购临开赎回固开
    /// </summary>
    [JsonPropertyName("openType")]
    public string OpenType { get; set; } = null!;

    /// <summary>
    /// 开放状态
    /// 可选值：申购、赎回、申购/赎回
    /// </summary>
    [JsonPropertyName("ifTempOpen")]
    public string IfTempOpen { get; set; } = null!;

    /// <summary>
    /// 是否固定时点计提报酬日
    /// 可选值：是、否
    /// </summary>
    [JsonPropertyName("ifFixedTime")]
    public string IfFixedTime { get; set; } = null!;

    /// <summary>
    /// 预留字段1
    /// </summary>
    [JsonPropertyName("remark1")]
    public string? Remark1 { get; set; }

    /// <summary>
    /// 预留字段2
    /// </summary>
    [JsonPropertyName("remark2")]
    public string? Remark2 { get; set; }

    public FundOpenDay To(int fid, ShareClass share)
    {
        var obj = new FundOpenDay
        {
            FundId = fid,
            ShareId = share.Id,
            Code = share.Code,
            Date = DateTimeHelper.TryParse(OpenDate, out var d) ? d : default,
            Source = "api"
        };

        if (OpenType.StartsWith("固开"))
        {
            obj.OpenPurchase = Models.OpenType.Fixed;
            obj.OpenRedemption = Models.OpenType.Fixed;
        }
        else if (OpenType.StartsWith("临开"))
        {
            obj.OpenPurchase = Models.OpenType.Temporary;
            obj.OpenRedemption = Models.OpenType.Temporary;
        }
        else if (OpenType.Contains("申购固开"))
        {
            obj.OpenPurchase = Models.OpenType.Fixed;
        }
        else if (OpenType.Contains("赎回固开"))
        {
            obj.OpenRedemption = Models.OpenType.Fixed;
        }
        else if (OpenType.Contains("申购临开"))
        {
            obj.OpenPurchase = Models.OpenType.Temporary;
        }
        else if (OpenType.Contains("赎回临开"))
        {
            obj.OpenRedemption = Models.OpenType.Temporary;
        }

        return obj;
    }
}
