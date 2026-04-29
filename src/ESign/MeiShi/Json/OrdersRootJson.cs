using FMO.Models;
using FMO.Utilities;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FMO.ESigning.MeiShi;



#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null.
public class OrdersRootJson
{
    [JsonPropertyName("list")]
    public OrderInfoJson[] List { get; set; }

    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }

}







public class OrderInfoJson
{
    [JsonPropertyName("orderType")]
    public int OrderType { get; set; }

    //[JsonPropertyName("productRiskType")]
    //public int ProductRiskType { get; set; }

    [JsonPropertyName("redemptionMoney")]
    public decimal? RedemptionMoney { get; set; }

    [JsonPropertyName("identifyStatus")]
    public int? IdentifyStatus { get; set; }

    [JsonPropertyName("tradeMoneyStr")]
    public string TradeMoneyStr { get; set; }

    [JsonPropertyName("tradeMoney")]
    public decimal? TradeMoney { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; }

    //[JsonPropertyName("customerType")]
    //public int CustomerType { get; set; }

    //[JsonPropertyName("contractWay")]
    //public int ContractWay { get; set; }

    [JsonPropertyName("signType")]
    public int SignType { get; set; }

    //[JsonPropertyName("capitalistName")]
    //public object CapitalistName { get; set; }

    [JsonPropertyName("subbranch")]
    public string Subbranch { get; set; }

    //[JsonPropertyName("invalidSource")]
    //public object InvalidSource { get; set; }

    //[JsonPropertyName("secondReviewCheckType")]
    //public object SecondReviewCheckType { get; set; }

    //[JsonPropertyName("checkType")]
    //public int CheckType { get; set; }

    ////[JsonPropertyName("parentProductId")]
    ////public object ParentProductId { get; set; }

    //[JsonPropertyName("productId")]
    //public long ProductId { get; set; }

    //[JsonPropertyName("aptnessTypeStr")]
    //public string AtnessTypeStr { get; set; }

    [JsonPropertyName("signFlowId")]
    public long SignFlowId { get; set; }

    //[JsonPropertyName("downloadSignConfirm")]
    //public bool DownloadSignConfirm { get; set; }

    [JsonPropertyName("managerNames")]
    public string ManagerNames { get; set; }

    //[JsonPropertyName("customerSignedRiskType")]
    //public int CustomerSignedRiskType { get; set; }

    //[JsonPropertyName("displayRecordStatus")]
    //public bool DisplayRecordStatus { get; set; }

    [JsonPropertyName("flowUpdateTime")]
    public long FlowUpdateTimeTicks { get; set; }



    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; }

    [JsonPropertyName("signFlowStartDate")]
    public string SignFlowStartDate { get; set; }

    //[JsonPropertyName("redemptionMoneySpan")]
    //public object RedemptionMoneySpan { get; set; }

    //[JsonPropertyName("modifyOpenDay")]
    //public bool ModifyOpenDay { get; set; }

    [JsonPropertyName("cardNumber")]
    public string CardNumber { get; set; }

    //[JsonPropertyName("investorType")]
    //public int InvestorType { get; set; }

    ////[JsonPropertyName("managerUserNames")]
    ////public string ManagerUserNames { get; set; }

    //[JsonPropertyName("customerRiskType")]
    //public int CustomerRiskType { get; set; }

    [JsonPropertyName("openDay")]
    public string OpenDay { get; set; }

    [JsonPropertyName("tradeFee")]
    public decimal? TradeFee { get; set; }

    //[JsonPropertyName("signStatus")]
    //public int SignStatus { get; set; }

    //[JsonPropertyName("signFlowEndDate")]
    //public string SignFlowEndDate { get; set; }

    //[JsonPropertyName("aptnessType")]
    //public int AtnessType { get; set; }

    [JsonPropertyName("bankName")]
    public string BankName { get; set; }

    //[JsonPropertyName("nextCodeValue")]
    //public int NextCodeValue { get; set; }

    //[JsonPropertyName("signConfirm")]
    //public object SignConfirm { get; set; }

    [JsonPropertyName("codeValue")]
    public int? CodeValue { get; set; }

    [JsonPropertyName("openType")]
    public int? OpenType { get; set; }

    //[JsonPropertyName("orgOrderStatus")]
    //public int OrgOrderStatus { get; set; }

    //[JsonPropertyName("customerId")]
    //public long CustomerId { get; set; }

    [JsonPropertyName("productFullName")]
    public string ProductFullName { get; set; }

    //[JsonPropertyName("productDayId")]
    //public object ProductDayId { get; set; }

    [JsonPropertyName("tradeType")]
    public int? TradeType { get; set; }

    ////[JsonPropertyName("capitalist")]
    ////public object Capitalist { get; set; }

    [JsonPropertyName("signFlowSourceType")]
    public int? SignFlowSourceType { get; set; }

    [JsonPropertyName("signMoney")]
    public decimal? SignMoney { get; set; }

    [JsonPropertyName("redemptionType")]
    public int? RedemptionType { get; set; }

    [JsonPropertyName("isDelete")]
    public int? IsDelete { get; set; }

    //[JsonPropertyName("signOpenDay")]
    //public object SignOpenDay { get; set; }

    //[JsonPropertyName("incomeDistributionType")]
    //public object IncomeDistributionType { get; set; }

    //[JsonPropertyName("accountNumber")]
    //public string AccountNumber { get; set; }

    ////[JsonPropertyName("invalidDate")]
    ////public object InvalidDate { get; set; }

    //[JsonPropertyName("tradeMoney")]
    //public decimal? TradeMoney { get; set; }

    ////[JsonPropertyName("recordStatus")]
    ////public object RecordStatus { get; set; }

    //[JsonPropertyName("codeText")]
    //public string CodeText { get; set; }

    //[JsonPropertyName("completionDate")]
    //public string CompletionDate { get; set; }

    //[JsonPropertyName("isRollBacked")]
    //public int IsRollBacked { get; set; }

    //[JsonPropertyName("flowType")]
    //public int FlowType { get; set; }

    internal TransferOrder To()
    {
        return new TransferOrder
        {
            FundName = this.ProductName,
            OpenDate = DateTimeHelper.TryParse(this.OpenDay, out var d) ? d : default,
            Type = GetOrderType(),
            Number = GetOrderNumber(),
            Fee = TradeFee ?? 0,
            InvestorName = this.CustomerName,
            InvestorIdentity = this.CardNumber,
            ExternalId = SignFlowId.ToString(),
            Source = "meishi",
            Date = GetDate()
        };
    }


    private DateOnly GetDate()
    {
        var m = Regex.Match(SignFlowStartDate, @"\d{4}-\d{2}-\d{2}");
        return m.Success ? DateOnly.ParseExact(m.Value, "yyyy-MM-dd") : default;
    }

    private TransferOrderType GetOrderType()
    {
        if (SignType == 1)
            return TransferOrderType.FirstTrade;

        if (SignType == 2)
            return TransferOrderType.Buy;

        // SignType 为其他值时，根据 RedemptionType 判断
        return RedemptionType switch
        {
            0 => TransferOrderType.Share,
            1 => TransferOrderType.Amount,
            _ => TransferOrderType.RemainAmout
        };
    }

    private decimal GetOrderNumber()
    {
        if (SignType == 1 || SignType == 2)
            return SignMoney ?? 0;

        // SignType 为其他值时，根据 RedemptionType 判断
        return RedemptionType switch
        {
            0 => SignMoney ?? TradeMoney ?? 0,// TransferOrderType.Share,
            _ => RedemptionMoney ?? TradeMoney ?? 0,// TransferOrderType.Amount,
            // TransferOrderType.RemainAmout
        };
    }

}


public class OrderFilesJson
{
    [JsonPropertyName("codeType")]
    public int CodeType { get; set; }

    [JsonPropertyName("documentName")]
    public string DocumentName { get; set; }



    //[JsonPropertyName("signedPdfUrl")]
    //public string SignedPdfUrl { get; set; }


    [JsonPropertyName("sealedUrl")]
    public string SealedUrl { get; set; }
}

public class OrderFilesRootJson
{
    [JsonPropertyName("sealedDocuments")]
    public OrderFilesJson[] Files { get; set; }

    [JsonPropertyName("doubleRecordingUrl")]
    public string DoubleRecordingUrl { get; set; }
}


public class OrderFileBJson
{

    [JsonPropertyName("fileName")]
    public string DocumentName { get; set; }

    [JsonPropertyName("codeType")]
    public int CodeType { get; set; }

    [JsonPropertyName("fileUrl")]
    public string SealedUrl { get; set; }
}

public class OrderFilesRootBJson
{
    [JsonPropertyName("attachments")]
    public OrderFileBJson[] Files { get; set; }


}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。