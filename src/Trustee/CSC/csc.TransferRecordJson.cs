using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class TransferRecordJson : JsonBase
{
    /// <summary>
    /// 交易确认日期 (格式: YYYYMMDD, 8位)
    /// </summary>
    [JsonPropertyName("confDate")]
    public string ConfDate { get; set; }

    /// <summary>
    /// 申请日期 (格式: YYYYMMDD, 8位)
    /// </summary>
    [JsonPropertyName("applyDate")]
    public string ApplyDate { get; set; }

    /// <summary>
    /// 基金代码 (32位)
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 基金名称 (250字符)
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 销售商代码 (6位)
    /// </summary>
    [JsonPropertyName("agencyNo")]
    public string AgencyNo { get; set; }

    /// <summary>
    /// 销售商名称 (64字符)
    /// </summary>
    [JsonPropertyName("agencyName")]
    public string AgencyName { get; set; }

    /// <summary>
    /// 投资者名称 (64字符)
    /// </summary>
    [JsonPropertyName("custName")]
    public string CustName { get; set; }

    /// <summary>
    /// 基金账号 (20字符)
    /// </summary>
    [JsonPropertyName("fundAcco")]
    public string FundAcco { get; set; }

    /// <summary>
    /// 交易账号 (16字符)
    /// </summary>
    [JsonPropertyName("tradeAcco")]
    public string TradeAcco { get; set; }

    /// <summary>
    /// 银行账号 (32字符)
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAcco { get; set; }

    /// <summary>
    /// 银行编号 (6字符)
    /// </summary>
    [JsonPropertyName("bankNo")]
    public string BankNo { get; set; }

    /// <summary>
    /// 开户行名称 (250字符)
    /// </summary>
    [JsonPropertyName("openBankName")]
    public string OpenBankName { get; set; }

    /// <summary>
    /// 银行户名 (64字符)
    /// </summary>
    [JsonPropertyName("nameInBank")]
    public string NameInBank { get; set; }

    /// <summary>
    /// 客户类型 (6字符)
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustType { get; set; }

    /// <summary>
    /// 证件类型 (3字符)
    /// </summary>
    [JsonPropertyName("certiType")]
    public string CertiType { get; set; }

    /// <summary>
    /// 证件号 (32字符)
    /// </summary>
    [JsonPropertyName("certiNo")]
    public string CertiNo { get; set; }

    /// <summary>
    /// TA发起标志 (2字符)
    /// </summary>
    [JsonPropertyName("taFlag")]
    public string TaFlag { get; set; }

    /// <summary>
    /// 确认状态 (2字符)
    /// </summary>
    [JsonPropertyName("confStatus")]
    public string ConfStatus { get; set; }

    /// <summary>
    /// 确认结果描述 (250字符)
    /// </summary>
    [JsonPropertyName("describe")]
    public string Describe { get; set; }

    /// <summary>
    /// 分红方式 (2字符)
    /// </summary>
    [JsonPropertyName("bonusType")]
    public string BonusType { get; set; }

    /// <summary>
    /// 申请金额 (20字符)
    /// </summary>
    [JsonPropertyName("balance")]
    public string Balance { get; set; }

    /// <summary>
    /// 申请份额 (20字符)
    /// </summary>
    [JsonPropertyName("shares")]
    public string Shares { get; set; }

    /// <summary>
    /// 单位净值 (10字符)
    /// </summary>
    [JsonPropertyName("netValue")]
    public string NetValue { get; set; }

    /// <summary>
    /// 确认金额 (20字符)
    /// </summary>
    [JsonPropertyName("confBalance")]
    public string ConfBalance { get; set; }

    /// <summary>
    /// 确认份额 (20字符)
    /// </summary>
    [JsonPropertyName("confShares")]
    public string ConfShares { get; set; }

    /// <summary>
    /// 手续费 (20字符)
    /// </summary>
    [JsonPropertyName("charge")]
    public string Charge { get; set; }

    /// <summary>
    /// 归管理人手续费 (20字符)
    /// </summary>
    [JsonPropertyName("managerCharge")]
    public string ManagerCharge { get; set; }

    /// <summary>
    /// 归销售商手续费 (20字符)
    /// </summary>
    [JsonPropertyName("distributorCharge")]
    public string DistributorCharge { get; set; }

    /// <summary>
    /// 归产品手续费 (20字符)
    /// </summary>
    [JsonPropertyName("fundcharge")]
    public string Fundcharge { get; set; }

    /// <summary>
    /// 业绩报酬 (20字符)
    /// </summary>
    [JsonPropertyName("achievementPay")]
    public string AchievementPay { get; set; }

    /// <summary>
    /// TA确认编号 (32字符)
    /// </summary>
    [JsonPropertyName("cserialNo")]
    public string CserialNo { get; set; }

    /// <summary>
    /// 确认业务类型 (6字符)
    /// </summary>
    [JsonPropertyName("busiFlag")]
    public string BusiFlag { get; set; }

    /// <summary>
    /// 原外部系统的申请流水号 (32字符)
    /// </summary>
    [JsonPropertyName("originalNo")]
    public string OriginalNo { get; set; }

    /// <summary>
    /// 操作方式 (1字符)
    /// </summary>
    [JsonPropertyName("operWayNew")]
    public string OperWayNew { get; set; }

    public override string? JsonId => $"{CSC._Identifier}.{CserialNo}";

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public TransferRecord ToObject()
    {
        var r = new TransferRecord
        {
            FundCode = FundCode,
            ConfirmedDate = DateOnly.ParseExact(ConfDate, "yyyyMMdd"),
            RequestDate = DateOnly.ParseExact(ApplyDate, "yyyyMMdd"),
            FundName = FundName,
            Agency = AgencyName,
            InvestorName = CustName,
            InvestorIdentity = CertiNo,
            RequestAmount = ParseDecimal(Balance),
            RequestShare = ParseDecimal(Shares),
            ConfirmedAmount = ParseDecimal(ConfBalance),
            ConfirmedShare = ParseDecimal(ConfShares),
            CreateDate = DateOnly.FromDateTime(DateTime.Today),
            ExternalId = $"{CSC._Identifier}.{CserialNo}",
            PerformanceFee = ParseDecimal(AchievementPay),
            Fee = ParseDecimal(Charge),
            ExternalRequestId = $"{CSC._Identifier}.{OriginalNo}",
            Type = ParseType(BusiFlag),
            Source = "api",
        };
        // 净额
        r.ConfirmedNetAmount = r.ConfirmedAmount - r.Fee;

        if (r.Type == TransferRecordType.UNK)
            JsonBase.ReportSpecialType(new(0, CSC._Identifier, nameof(TransferRecord), CserialNo, BusiFlag));
        return r;
    }


    /// 120	认购
    //122	申购
    //124	赎回
    //126	转销售机构
    //127	转销售机构入
    //129	设置分红方式
    //131	份额冻结
    //132	份额解冻
    //133	转让
    //134	受让
    //136	份额转换
    //137	份额转换入
    //142	强行赎回
    //143	红利发放
    //144	强行调增
    //145	强行调减
    //149	募集失败
    //150	基金清盘
    private TransferRecordType ParseType(string str)
    {
        switch (str)
        {
            case "120": // 认购 
                return TransferRecordType.Subscription;
            case "122": // 申购 
                return TransferRecordType.Purchase;
            case "124": // 赎回 
                return TransferRecordType.Redemption;
            case "126": // 转销售机构 
                return TransferRecordType.MoveIn;
            case "127": // 转销售机构入 
                return TransferRecordType.MoveIn;
            case "129": // 设置分红方式 
                return TransferRecordType.BonusType; 
            case "131": // 份额冻结 
                return TransferRecordType.Frozen;
            case "132": // 份额解冻 
                return TransferRecordType.Thawed;
            case "133": // 转让 
                return TransferRecordType.TransferOut;
            case "134": // 受让 
                return TransferRecordType.TransferIn;
            case "136": // 份额转换 
                return TransferRecordType.SwitchOut;
            case "137": // 份额转换入 
                return TransferRecordType.SwitchIn;

            case "142": // 强行赎回 
                return TransferRecordType.ForceRedemption;
            case "143": // 红利发放 
                return TransferRecordType.Distribution;
            case "144": // 强行调增 
                return TransferRecordType.Increase;
            case "145": // 强行调减 
                return TransferRecordType.Decrease;
            case "149": // 募集失败 
                return TransferRecordType.RaisingFailed;
            case "150": // 基金清盘 
                return TransferRecordType.Clear;

            default: 
                return TransferRecordType.UNK;
        }
    }

}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。