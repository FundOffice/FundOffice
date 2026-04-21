using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class TransferRequestJson : JsonBase
{
    public override string? JsonId => $"{CITICS._Identifier}.{RequestNo}";
    /// <summary>
    /// 申请编号
    /// </summary>
    [JsonPropertyName("requestNo")]
    public string RequestNo { get; set; }

    /// <summary>
    /// 申请日期（格式：YYYYMMDD）
    /// </summary>
    [JsonPropertyName("requestDate")]
    public string RequestDate { get; set; }

    /// <summary>
    /// 申请时间（格式：HHMMSS）
    /// 可能为null
    /// </summary>
    [JsonPropertyName("requestTime")]
    public string RequestTime { get; set; }

    /// <summary>
    /// 基金代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 业务类型
    /// 参照 BusinFlagEnum 枚举定义
    /// </summary>
    [JsonPropertyName("businFlag")]
    public string BusinFlag { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("tradeAcco")]
    public string TradeAccount { get; set; }

    /// <summary>
    /// 基金帐号
    /// </summary>
    [JsonPropertyName("fundAcco")]
    public string FundAccount { get; set; }

    /// <summary>
    /// 销售商代码
    /// </summary>
    [JsonPropertyName("agencyNo")]
    public string AgencyCode { get; set; }

    /// <summary>
    /// 销售商名称
    /// </summary>
    [JsonPropertyName("agencyName")]
    public string AgencyName { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary>
    [JsonPropertyName("balance")]
    public decimal? ApplyAmount { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary>
    [JsonPropertyName("shares")]
    public decimal? ApplyShares { get; set; }

    /// <summary>
    /// 银行账号
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAccountNo { get; set; }

    /// <summary>
    /// 银行开户行
    /// </summary>
    [JsonPropertyName("bankName")]
    public string BankName { get; set; }

    /// <summary>
    /// 银行户名
    /// </summary>
    [JsonPropertyName("nameInBank")]
    public string BankAccountName { get; set; }

    /// <summary>
    /// 网点代码
    /// </summary>
    [JsonPropertyName("netNo")]
    public string BranchCode { get; set; }

    /// <summary>
    /// 指定费用
    /// </summary>
    [JsonPropertyName("definedFee")]
    public decimal? DefinedFee { get; set; }

    /// <summary>
    /// 折扣
    /// </summary>
    [JsonPropertyName("agio")]
    public string DiscountRate { get; set; }

    /// <summary>
    /// 巨额赎回方式：
    /// 0 - 取消
    /// 1 - 顺延
    /// null - 默认顺延
    /// </summary>
    [JsonPropertyName("exceedFlag")]
    public string? ExceedFlag { get; set; }

    /// <summary>
    /// 币种：人民币（默认值）
    /// </summary>
    [JsonPropertyName("moneyTypeCn")]
    public string Currency { get; set; } = "人民币";

    /// <summary>
    /// 投资者名称
    /// </summary>
    [JsonPropertyName("custName")]
    public string InvestorName { get; set; }

    /// <summary>
    /// 客户类型（参考附录4）
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustomerType { get; set; }

    /// <summary>
    /// 证件类型（参考附录4）
    /// </summary>
    [JsonPropertyName("certiType")]
    public string CertificateType { get; set; }

    /// <summary>
    /// 证件号
    /// </summary>
    [JsonPropertyName("certiNo")]
    public string CertificateNumber { get; set; }


    public TransferRequest ToObject()
    {
        var r = new TransferRequest
        {
            InvestorIdentity = CertificateNumber,
            InvestorName = InvestorName,
            FundCode = FundCode,
            FundName = "unset",
            RequestDate = DateOnly.ParseExact(RequestDate, "yyyyMMdd"),
            RequestType = ParseRequestType(BusinFlag),
            ExternalId = $"{CITICS._Identifier}.{RequestNo}",
            Agency = AgencyName,
            FeeDiscount = ParseDecimal(DiscountRate),
            LargeRedemptionFlag = ExceedFlag switch { "0" or null => LargeRedemptionFlag.CancelRemaining, _ => LargeRedemptionFlag.RollOver },
            Source = "api",
            RequestAmount = ApplyAmount ?? 0,
            RequestShare = ApplyShares ?? 0,
            Fee = DefinedFee ?? 0,
            CreateDate = DateOnly.FromDateTime(DateTime.Now),
        };


        if (r.RequestType == TransferRequestType.UNK)
            JsonBase.ReportSpecialType(new(0, CITICS._Identifier, nameof(TransferRequest), RequestNo, BusinFlag));

        return r;
    }



    /// <summary>
    /// 01-认购	50-基金成立
    ///02-申购	51-基金终止
    ///03-赎回	52-基金清盘
    ///04-转托管	54-发行失败
    ///05-托管转入	70-强制调增
    ///06-托管转出	71-强制调减
    ///07-修改分红方式	74-红利发放
    ///10-份额冻结	77-定期申购修改
    ///11-份额解冻	78-定期赎回修改
    ///12-非交易过户	81-基金开户
    ///13-基金转换(出)  82-基金销户
    ///14-非交易过户出	83-账户修改
    ///15-非交易过户入	84-账户冻结
    ///16-基金转换入	85-账户解冻
    ///17-撤单	88-账户登记
    ///20-内部转托管	89-取消登记
    ///21-内部转托管入	90-定期申购协议
    ///22-内部转托管出	91-定期赎回协议
    ///31-增开交易账号	92-承诺优惠协议
    ///32-变更交易账号	93-定期申购取消
    ///41-买入	94-定期赎回取消
    ///42-卖出	96-销交易账号
    ///46-联名卡查询	97-内部转托管
    ///47-联名卡开通	98-内部托管转入
    ///48-联名卡取消	99-内部托管转出
    ///49-联名卡还款
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private static TransferRequestType ParseRequestType(string str)
    {
        switch (str)
        {
            case "01": return TransferRequestType.Subscription;
            //case "50": return TransferRequestType.Subscription; 没有返回
            case "02": return TransferRequestType.Purchase;
            case "03": return TransferRequestType.Redemption;
            //case "04": return TransferRequestType.Subscription;
            case "07": return TransferRequestType.BonusType;
            case "05":
            case "70":
            case "15":
            case "16":
            case "21":
            case "41":
                return TransferRequestType.Increase;
            case "06":
            case "71":
            case "14":
            case "13":
            case "22":
            case "42":
                return TransferRequestType.Decrease;
            default: // 其它类型忽略
                return TransferRequestType.UNK;
        }
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。