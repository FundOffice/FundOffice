using FMO.Models;
using FMO.Utilities;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

internal class OpenDayJson : JsonBase
{
    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("FUND.CODE")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 开放日期
    /// 格式：YYYYMMDD
    /// </summary>
    [JsonPropertyName("OPENINGDAY")]
    public string OpeningDay { get; set; } = null!;

    /// <summary>
    /// 开放类型
    /// 6 - 认购
    /// 7 - 申购
    /// 8 - 赎回
    /// </summary>
    [JsonPropertyName("OPENINGTYPE")]
    public string OpeningType { get; set; } = null!;

    /// <summary>
    /// 开放来源
    /// 2 - 固定开放日
    /// 7 - 临时开放日
    /// </summary>
    [JsonPropertyName("OPENINGSOURCE")]
    public string OpeningSource { get; set; } = null!;


    public FundOpenDay To(int fid, ShareClass share)
    {
        var obj = new FundOpenDay
        {
            FundId = fid,
            ShareId = share.Id,
            Code = share.Code,
            Date = DateTimeHelper.TryParse(OpeningDay, out var d) ? d : default,
            Source = "api"
        };

        if (OpeningSource == "2")
        {
            if (OpeningType is "6" or "7")
                obj.OpenPurchase = Models.OpenType.Fixed;

            if (OpeningType is "8")
                obj.OpenRedemption = Models.OpenType.Fixed;
        }
        else if (OpeningSource == "7")
        {
            if (OpeningType is "6" or "7")
                obj.OpenPurchase = Models.OpenType.Temporary;

            if (OpeningType is "8")
                obj.OpenRedemption = Models.OpenType.Temporary;
        }

        return obj;
    }
}