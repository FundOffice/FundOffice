using FMO.Models;
using FMO.Utilities;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

internal class OpenDayJson : JsonBase
{
    /// <summary>
    /// 产品代码 (注：图片中对应描述为“产品名称”，疑似文档笔误，此处按字段名 fundCode 处理)
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 开放状态
    /// 1: 开放申购赎回
    /// 3: 只开放申购
    /// 4: 只开放赎回
    /// </summary>
    [JsonPropertyName("openStatus")]
    public string OpenStatus { get; set; } = null!;

    /// <summary>
    /// 开放日期
    /// 格式：yyyyMMdd
    /// </summary>
    [JsonPropertyName("openDay")]
    public string OpenDay { get; set; } = null!;

    /// <summary>
    /// 开放类型
    /// 0: 固开
    /// 1: 临开
    /// </summary>
    [JsonPropertyName("openType")]
    public string OpenType { get; set; } = null!;

    /// <summary>
    /// 备注
    /// </summary>
    [JsonPropertyName("remark")]
    public string? Remark { get; set; }


    public FundOpenDay To(int fid, ShareClass share)
    {
        var obj = new FundOpenDay
        {
            FundId = fid,
            ShareId = share.Id,
            Code = share.Code,
            Date = DateTimeHelper.TryParse(OpenDay, out var d) ? d : default,
            Source = "api"
        };

        if (OpenType == "0")
        {
            if (OpenStatus is "1" or "3")
                obj.OpenPurchase = Models.OpenType.Fixed;

            if (OpenStatus is "1" or "4")
                obj.OpenRedemption = Models.OpenType.Fixed;
        }
        else if (OpenType == "1")
        {
            if (OpenStatus is "1" or "3")
                obj.OpenPurchase = Models.OpenType.Temporary;

            if (OpenStatus is "1" or "4")
                obj.OpenRedemption = Models.OpenType.Temporary;
        }

        return obj;
    }
}