using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.ESigning.MeiShi;

internal class QualficationJson
{
    [JsonPropertyName("identifyFlowId")]
    public long IdentifyFlowId { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customerType")]
    public int? CustomerType { get; set; }

    [JsonPropertyName("managerName")]
    public string? ManagerName { get; set; }

    [JsonPropertyName("investorType")]
    public int? InvestorType { get; set; }

    [JsonPropertyName("riskType")]
    public int? RiskType { get; set; }

    [JsonPropertyName("identifyWay")]
    public int? IdentifyWay { get; set; }

    [JsonPropertyName("flowStatus")]
    public int? FlowStatus { get; set; }

    [JsonPropertyName("codeValue")]
    public int? CodeValue { get; set; }

    [JsonPropertyName("codeText")]
    public string? CodeText { get; set; }

    [JsonPropertyName("currentCodeValue")]
    public int? CurrentCodeValue { get; set; }

    [JsonPropertyName("commitTime")]
    public string? CommitTime { get; set; }

    [JsonPropertyName("commitPerson")]
    public string? CommitPerson { get; set; }

    [JsonPropertyName("checkFlowStatus")]
    public int? CheckFlowStatus { get; set; }

    [JsonPropertyName("checkStatus")]
    public int? CheckStatus { get; set; }

    [JsonPropertyName("auditTime")]
    public string? AuditTime { get; set; }

    [JsonPropertyName("auditPerson")]
    public string? AuditPerson { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("identifyEndTime")]
    public string? IdentifyEndTime { get; set; }

    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }

    [JsonPropertyName("cardNumber")]
    public string? CardNumber { get; set; }

    [JsonPropertyName("isDelete")]
    public int? IsDelete { get; set; }

    [JsonPropertyName("identifyTime")]
    public string? IdentifyTime { get; set; }

    [JsonPropertyName("customerId")]
    public long? CustomerId { get; set; }

    [JsonPropertyName("customerLevel")]
    public int? CustomerLevel { get; set; }

    [JsonPropertyName("identifyStatus")]
    public int? IdentifyStatus { get; set; }

    [JsonPropertyName("identifyLimitDate")]
    public string? IdentifyLimitDate { get; set; }

    [JsonPropertyName("isVIP")]
    public int? IsVIP { get; set; }

    [JsonPropertyName("aptnessType")]
    public int? AptnessType { get; set; }

    [JsonPropertyName("channelType")]
    public int? ChannelType { get; set; }

    [JsonPropertyName("signType")]
    public int? SignType { get; set; }

    [JsonPropertyName("flowUpdateTime")]
    public string? FlowUpdateTime { get; set; }



    public InvestorQualification To()
    {
        return new InvestorQualification()
        {
            InvestorName = CustomerName,
            IdentityCode = CardNumber,
            Date = DateOnly.FromDateTime(DateTime.Parse(IdentifyTime!))
        };
    }
}



internal class QualficationSignFilesRoot
{
    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customerType")]
    public int? CustomerType { get; set; }

    [JsonPropertyName("managerName")]
    public string? ManagerName { get; set; }

    [JsonPropertyName("investorType")]
    public int? InvestorType { get; set; }

    [JsonPropertyName("identifyTime")]
    public string? IdentifyTime { get; set; }

    [JsonPropertyName("signAttachments")]
    public AttatchmentInfo[]? SignAttachments { get; set; }
}

internal class AttatchmentInfo
{
    /// <summary>
    /// 
    /// </summary>
    public int attachmentsId { get; set; }
    /// <summary>
    /// 基本信息表
    /// </summary>
    public string? documentName { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int documentType { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? documentUrl { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? sealTime { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string[]? documentUrls { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? extInfo { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? originalPdfUrl { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? signedPdfUrl { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? sealedUrl { get; set; }
    /// <summary>   
    /// 
    /// </summary>
    public string? codeType { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? fileType { get; set; }
}
