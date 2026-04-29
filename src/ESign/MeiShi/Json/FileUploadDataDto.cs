using System.Text.Json.Serialization;

namespace FMO.ESigning.MeiShi;

/// <summary>
/// 文件上传返回的Data实体
/// </summary>
public class FileUploadDataDto
{
    [JsonPropertyName("attachmentsId")]
    public long AttachmentsId { get; set; }

    [JsonPropertyName("source")]
    public int Source { get; set; }

    [JsonPropertyName("companyCode")]
    public long CompanyCode { get; set; }

    [JsonPropertyName("sourceId")]
    public long? SourceId { get; set; }

    [JsonPropertyName("codeType")]
    public int CodeType { get; set; }

    [JsonPropertyName("fileType")]
    public string? FileType { get; set; }

    [JsonPropertyName("fileUrl")]
    public string? FileUrl { get; set; }

    [JsonPropertyName("thumFileUrl")]
    public string? ThumFileUrl { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("systemStatus")]
    public int SystemStatus { get; set; }

    [JsonPropertyName("needSealStatus")]
    public int NeedSealStatus { get; set; }

    [JsonPropertyName("userType")]
    public int? UserType { get; set; }

    [JsonPropertyName("documentJsonObject")]
    public object? DocumentJsonObject { get; set; }

    [JsonPropertyName("videoUrl")]
    public string? VideoUrl { get; set; }

    [JsonPropertyName("contractNo")]
    public string? ContractNo { get; set; }

    [JsonPropertyName("operateUserId")]
    public long OperateUserId { get; set; }

    [JsonPropertyName("operateUserName")]
    public string? OperateUserName { get; set; }

    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }

    [JsonPropertyName("updateTime")]
    public DateTime UpdateTime { get; set; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("baseThumUrl")]
    public string? BaseThumUrl { get; set; }

    [JsonPropertyName("signUrl")]
    public string? SignUrl { get; set; }

    [JsonPropertyName("serverIp")]
    public string? ServerIp { get; set; }
}