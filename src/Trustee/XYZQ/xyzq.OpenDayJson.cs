using FMO.Models;
using FMO.Utilities;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

internal class OpenDayJson : JsonBase
{
    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("fundcode")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 开放状态名称
    /// </summary>
    [JsonPropertyName("fdstatusname")]
    public string FdStatusName { get; set; } = null!;

    /// <summary>
    /// 开放状态
    /// 0-正常开放
    /// 1-认购期
    /// 2-发行成功
    /// 3-发行失败
    /// 4-暂停交易
    /// 5-开放赎回
    /// 6-开放申购
    /// 9-基金终止
    /// B-分红登记
    /// C-红利发放
    /// D-基金清盘
    /// F-净值归一
    /// G-业绩报酬
    /// I-违约赎回
    /// </summary>
    [JsonPropertyName("fdstatus")]
    public string FdStatus { get; set; } = null!;

    /// <summary>
    /// 开放日
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    /// <summary>
    /// 临时开放状态"
    /// 0--正常开放
    /// 1--临时开放
    /// 2--违约赎回
    /// 3--违约申购
    /// T--特殊业务"

    /// </summary>
    [JsonPropertyName("fdlskf")]
    public string FdLskf { get; set; } = null!;

    public FundOpenDay To(int fid, ShareClass share)
    {
        var obj = new FundOpenDay
        {
            FundId = fid,
            ShareId = share.Id,
            Code = share.Code,
            Date = DateTimeHelper.TryParse(Date, out var d) ? d : default,
            Source = "api"
        };

        if (FdStatus is "0" or "6" && FdLskf is "0")
            obj.OpenPurchase = Models.OpenType.Fixed;
        else if (FdStatus is "0" or "5" && FdLskf is "6")
            obj.OpenRedemption = Models.OpenType.Fixed;
        else if (FdStatus is "0" or "6" && FdLskf is "1" or "3")
            obj.OpenPurchase = Models.OpenType.Temporary;
        else if (FdStatus is "0" or "5" && FdLskf is "1" or "2")
            obj.OpenRedemption = Models.OpenType.Temporary;


        return obj;
    }
}