using FMO.Models;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


namespace FMO.Trustee;

internal class TransferRecordJson : JsonBase
{
    public override string? JsonId => $"{CMS._Identifier}.{Remark1}";

    [JsonPropertyName("custName")]
    public required string CustName { get; set; }

    [JsonPropertyName("custType")]
    public required string CustType { get; set; }

    [JsonPropertyName("certificateType")]
    public required string CertificateType { get; set; }

    [JsonPropertyName("certificateNo")]
    public required string CertificateNo { get; set; }

    [JsonPropertyName("taAccountId")]
    public required string TaAccountId { get; set; }

    [JsonPropertyName("transactionAccountId")]
    public required string TransactionAccountId { get; set; }

    [JsonPropertyName("fundName")]
    public required string FundName { get; set; }

    [JsonPropertyName("fundCode")]
    public required string FundCode { get; set; }

    [JsonPropertyName("businessCode")]
    public required string BusinessCode { get; set; }

    [JsonPropertyName("applicationAmount")]
    public required string ApplicationAmount { get; set; } // 保留两位小数

    [JsonPropertyName("applicationVol")]
    public required string ApplicationVol { get; set; } // 保留两位小数

    [JsonPropertyName("transactionDate")]
    public required string TransactionDate { get; set; } // 格式：yyyymmdd

    [JsonPropertyName("nav")]
    public required string Nav { get; set; } // 清盘日可保留8位小数

    [JsonPropertyName("transactionCfmDate")]
    public required string TransactionCfmDate { get; set; } // 格式：yyyymmdd

    [JsonPropertyName("confirmedVol")]
    public required string ConfirmedVol { get; set; } // 保留两位小数

    [JsonPropertyName("confirmedAmount")]
    public required string ConfirmedAmount { get; set; } // 保留两位小数

    [JsonPropertyName("confirmedNavVol")]
    public required string ConfirmedNavVol { get; set; } // 保留两位小数

    [JsonPropertyName("charge")]
    public required string Charge { get; set; } // 保留两位小数

    [JsonPropertyName("performance")]
    public required string Performance { get; set; } // 保留两位小数

    [JsonPropertyName("distributorCode")]
    public required string DistributorCode { get; set; }

    [JsonPropertyName("distributorName")]
    public required string DistributorName { get; set; }

    [JsonPropertyName("remark1")]
    public required string Remark1 { get; set; }

    [JsonPropertyName("remark2")]
    public required string Remark2 { get; set; }

    [JsonPropertyName("note")]
    public required string Note { get; set; }

    [JsonPropertyName("origDefNo")]
    public required string OrigDefNo { get; set; }

    [JsonPropertyName("shareBonusType")]
    public required string ShareBonusType { get; set; }

    [JsonPropertyName("attributionManagerFee")]
    public required string AttributionManagerFee { get; set; } // 保留两位小数

    [JsonPropertyName("attributionFundAssetFee")]
    public required string AttributionFundAssetFee { get; set; } // 保留两位小数

    [JsonPropertyName("interest")]
    public required string Interest { get; set; } // 保留两位小数

    [JsonPropertyName("attributionSellAgencyFee")]
    public required string AttributionSellAgencyFee { get; set; } // 保留两位小数

    [JsonPropertyName("applyNo")]
    public required string ApplyNo { get; set; }

    public TransferRecord ToObject()
    {
        TransferRecordType transferRecordType = Translate(BusinessCode);
        if (transferRecordType == TransferRecordType.UNK && BusinessCode switch { "120" => false, _ => true })
            ReportJsonUnexpected(CMS._Identifier, nameof(CMS.QueryTransferRecords), $"TA[{ApplyNo}] {TransactionCfmDate} 份额：{ConfirmedNavVol} 金额：{ConfirmedAmount} 的业务类型[{BusinessCode}]无法识别");

        var r = new TransferRecord
        {
            InvestorIdentity = CertificateNo,
            InvestorName = CustName,
            Agency = DistributorName,
            RequestDate = DateOnly.ParseExact(TransactionDate, "yyyyMMdd"),
            RequestAmount = ParseDecimal(ApplicationAmount),
            RequestShare = ParseDecimal(ApplicationVol),
            ConfirmedDate = DateOnly.ParseExact(TransactionCfmDate, "yyyyMMdd"),
            ConfirmedAmount = ParseDecimal(ConfirmedAmount),
            ConfirmedShare = ParseDecimal(ConfirmedVol),
            ConfirmedNetAmount = ParseDecimal(ConfirmedNavVol),
            CreateDate = DateOnly.FromDateTime(DateTime.Today),
            ExternalId = $"{CMS._Identifier}.{Remark1}",
            IsFailed = Remark2 == "确认失败",
            Type = transferRecordType,
            Fee = ParseDecimal(Charge),
            PerformanceFee = ParseDecimal(Performance),
            ExternalRequestId = $"{CMS._Identifier}.{ApplyNo}",
            FundCode = FundCode,
            FundName = FundName, 
            Background = Regex.IsMatch(Note, "自动申购|付费强赎|子基金"),
            Source = "api",
        }; 
         

        if (r.Type == TransferRecordType.UNK)
            JsonBase.ReportSpecialType(new(0, CMS._Identifier, nameof(TransferRecord), Remark1, BusinessCode));

        return r;
    }



    public static TransferRecordType Translate(string c)
    {
        return c switch
        {
            "120" => TransferRecordType.InitialOffer, //"认购确认" 此项没有份额数据, 用认购结果
            "122" => TransferRecordType.Purchase,     //"申购确认",
            "124" => TransferRecordType.Redemption,// "赎回确认",
            "126" => TransferRecordType.MoveIn,   //"转托确认",
            "127" => TransferRecordType.MoveIn,   //"转销售人/机构转入",
            "128" => TransferRecordType.MoveOut,  //"转销售人/机构转出",
            "129" => TransferRecordType.BonusType,//"分红方式",
            "130" => TransferRecordType.Subscription, // "认购结果",
            "131" => TransferRecordType.Frozen,       //"基金份数冻结",
            "132" => TransferRecordType.Thawed,       //"基金份数解冻",
                                                      //"133" => TARecordType.TransferIn,   //"非交易过户",
            "134" => TransferRecordType.TransferIn,   //"非交易过户转入",
            "135" => TransferRecordType.TransferOut,  //"非交易过户转出",
                                                      //"136" => TARecordType.SwitchIn,     //"基金转换",
            "137" => TransferRecordType.SwitchIn,     //"基金转换转入",
            "138" => TransferRecordType.SwitchOut,    //"基金转换转出",


            "139" => TransferRecordType.Purchase,     //"定时定额申购",
            "142" => TransferRecordType.ForceRedemption,//"强制赎回",
            "143" => TransferRecordType.Distribution,     //"分红确认",
            "144" => TransferRecordType.Increase,     //"强行调增",
            "145" => TransferRecordType.Decrease,     //"强行调减",
            //"152" => TransferRecordType. // 撤单
            _ => TransferRecordType.UNK,              //"未知业务类型"
        };
    }
}
