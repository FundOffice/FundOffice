using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

/// <summary>
/// 产品信息
/// </summary>
public class ProductInfoJson : JsonBase
{
    /// <summary>
    /// 协会备案代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; } = null!;

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("pdCode")]
    public string PdCode { get; set; } = null!;

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("pdName")]
    public string PdName { get; set; } = null!;

    /// <summary>
    /// 份额级别
    /// 0-总, 1-A级, 2-B级, 3-C级, 4-D级, 5-E级, 6-F级, 7-G级, 8-H级, 
    /// 9-I级, 10-J级, 11-K级, 12-L级, 13-M级, 14-N级, 15-O级, 16-P级,
    /// 17-Q级, 18-R级, 19-S级, 20-T级, 21-U级, 22-V级, 23-W级, 24-X级, 
    /// 25-Y级, 26-Z级
    /// </summary>
    [JsonPropertyName("fejb")]
    public string Fejb { get; set; } = null!;

    /// <summary>
    /// 分级基金对应母基金代码
    /// </summary>
    [JsonPropertyName("fjjjdynjjdm")]
    public string Fjjjdynjjdm { get; set; } = null!;

    /// <summary>
    /// 分级基金对应母基金名称
    /// </summary>
    [JsonPropertyName("fjjjdynjjmc")]
    public string Fjjjdynjjmc { get; set; } = null!;

    /// <summary>
    /// 服务内容
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = null!;

    /// <summary>
    /// 产品类型代码
    /// </summary>
    [JsonPropertyName("pdType")]
    public string PdType { get; set; } = null!;

    /// <summary>
    /// 产品类型中文释义
    /// </summary>
    [JsonPropertyName("pdTypeCN")]
    public string PdTypeCN { get; set; } = null!;

    /// <summary>
    /// 状态代码
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    /// <summary>
    /// 状态中文释义
    /// </summary>
    [JsonPropertyName("statusCN")]
    public string StatusCN { get; set; } = null!;

    /// <summary>
    /// 成立日期 (格式：YYYYMMDD)
    /// </summary>
    [JsonPropertyName("setUpDate")]
    public string SetUpDate { get; set; } = null!;


    public SubjectFundMapping To()
    {
        return new SubjectFundMapping
        {
            FundCode = PdCode,
            FundName = PdName,
            ShareClass = Fejb switch { "0" => null, _ => Fejb },
            AmacCode = FundCode,
            MasterCode = Fjjjdynjjmc,
            MasterName = Fjjjdynjjmc,
            Status = ConvertToFundStatus(Status)
        };
    }

    public static FundStatus ConvertToFundStatus(string status)
    {
        if (string.IsNullOrEmpty(status))
            return FundStatus.Unk;

        return status switch
        {
            "3" => FundStatus.ContractFinalized,      // 合同已定稿
            "5" => FundStatus.ContractFinalized,      // 募集中 -> 项目发起
            "6" => FundStatus.ContractFinalized,      // 募集截止 -> 项目发起
            "7" => FundStatus.Setup,                  // 产品成立
            "8" => FundStatus.Normal,                 // 运行中
            "9" => FundStatus.StartLiquidation,       // 清盘中
            "11" => FundStatus.Unk,                   // 撤销产品
            "13" => FundStatus.Initiate,              // 募集准备 -> 项目发起
            "18" => FundStatus.Registration,          // 备案中
            "20" => FundStatus.Unk,                   // 产品逾期
            "21" => FundStatus.AdvisoryTerminated,    // 合同终止
            "22" => FundStatus.StartLiquidation,      // 待清盘
            "24" => FundStatus.Liquidation,           // 产品清盘
            "25" => FundStatus.ContractFinalized,     // 重新定稿中
            "28" => FundStatus.Initiate,              // 报会中
            "29" => FundStatus.Initiate,              // 确认合作意向及报价
            "30" => FundStatus.Initiate,              // FA协议沟通
            "31" => FundStatus.AdvisoryTerminated,    // 服务转出
            "55" => FundStatus.Liquidation,           // 基金清盘
            "66" => FundStatus.Normal,                // 正常运营
            "77" => FundStatus.Initiate,              // 募集期
            "88" => FundStatus.Initiate,              // 产品立项
            "99" => FundStatus.Initiate,              // 基金发行状态
            _ => FundStatus.Unk
        };
    }

    // 或者使用整数版本的转换函数
    public static FundStatus ConvertToFundStatus(int status)
    {
        return status switch
        {
            3 => FundStatus.ContractFinalized,
            5 => FundStatus.ContractFinalized,
            6 => FundStatus.ContractFinalized,
            7 => FundStatus.Setup,
            8 => FundStatus.Normal,
            9 => FundStatus.StartLiquidation,
            11 => FundStatus.Unk,
            13 => FundStatus.Initiate,
            18 => FundStatus.Registration,
            20 => FundStatus.Unk,
            21 => FundStatus.AdvisoryTerminated,
            22 => FundStatus.StartLiquidation,
            24 => FundStatus.Liquidation,
            25 => FundStatus.ContractFinalized,
            28 => FundStatus.Initiate,
            29 => FundStatus.Initiate,
            30 => FundStatus.Initiate,
            31 => FundStatus.AdvisoryTerminated,
            55 => FundStatus.Liquidation,
            66 => FundStatus.Normal,
            77 => FundStatus.Initiate,
            88 => FundStatus.Initiate,
            99 => FundStatus.Initiate,
            _ => FundStatus.Unk
        };
    }
}
