using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace FMO.ESigning.MeiShi;





#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

/// <summary>
/// 基金报告上传请求实体（完全对应你提供的JSON）
/// </summary>
internal class FundReportUploadRequest
{
    [JsonPropertyName("file")]
    public FileBag File { get; set; }

    [JsonPropertyName("documentName")]
    public string DocumentName { get; set; }

    [JsonPropertyName("documentType")]
    public int DocumentType { get; set; }

    [JsonPropertyName("fileAuthority")]
    public List<int> FileAuthority { get; set; }

    [JsonPropertyName("disclosureConditions")]
    public int DisclosureConditions { get; set; }

    [JsonPropertyName("publishStatus")]
    public int PublishStatus { get; set; }

    [JsonPropertyName("sendEmail")]
    public int SendEmail { get; set; }

    [JsonPropertyName("noticeStatus")]
    public int NoticeStatus { get; set; }

    [JsonPropertyName("productIdList")]
    public string ProductIdList { get; set; }

    [JsonPropertyName("startStatus")]
    public int StartStatus { get; set; }

    [JsonPropertyName("attachmentsId")]
    public long AttachmentsId { get; set; }

    [JsonPropertyName("publishTime")]
    public long PublishTime { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; set; }

    [JsonPropertyName("mrpReportLibraryLogId")]
    public string MrpReportLibraryLogId { get; set; }

    [JsonPropertyName("sourceFile")]
    public int SourceFile { get; set; }

    [JsonPropertyName("productContactMap")]
    public Dictionary<string, object> ProductContactMap { get; set; }
}


internal class FileBag
{
    [JsonPropertyName("file")]
    public UploadFileInfo File { get; set; }

    [JsonPropertyName("fileList")]
    public List<UploadFileInfo> FileList { get; set; }
}

/// <summary>
/// 上传文件信息实体
/// </summary>
internal class UploadFileInfo
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("lastModified")]
    public long LastModified { get; set; }

    [JsonPropertyName("lastModifiedDate")]
    public DateTime LastModifiedDate { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("percent")]
    public int Percent { get; set; }

    [JsonPropertyName("originFileObj")]
    public OriginFileObj OriginFileObj { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("response")]
    public RootJson Response { get; set; }

    [JsonPropertyName("xhr")]
    public object Xhr { get; set; }
}

/// <summary>
/// 原始文件对象
/// </summary>
public class OriginFileObj
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; }
}

/// <summary>
/// 文件上传响应结果
/// </summary>
public class UploadResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public FileUploadDataDto Data { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// 上传响应数据详情
/// </summary>
public class UploadResponseData
{
    [JsonPropertyName("attachmentsId")]
    public long AttachmentsId { get; set; }

    [JsonPropertyName("source")]
    public int Source { get; set; }

    [JsonPropertyName("companyCode")]
    public long CompanyCode { get; set; }

    [JsonPropertyName("sourceId")]
    public object SourceId { get; set; }

    [JsonPropertyName("codeType")]
    public int CodeType { get; set; }

    [JsonPropertyName("fileType")]
    public string FileType { get; set; }

    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; set; }

    [JsonPropertyName("thumFileUrl")]
    public object ThumFileUrl { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("systemStatus")]
    public int SystemStatus { get; set; }

    [JsonPropertyName("needSealStatus")]
    public int NeedSealStatus { get; set; }

    [JsonPropertyName("userType")]
    public object UserType { get; set; }

    [JsonPropertyName("documentJsonObject")]
    public object DocumentJsonObject { get; set; }

    [JsonPropertyName("videoUrl")]
    public object VideoUrl { get; set; }

    [JsonPropertyName("contractNo")]
    public object ContractNo { get; set; }

    [JsonPropertyName("operateUserId")]
    public long OperateUserId { get; set; }

    [JsonPropertyName("operateUserName")]
    public string OperateUserName { get; set; }

    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }

    [JsonPropertyName("updateTime")]
    public DateTime UpdateTime { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("baseThumUrl")]
    public object BaseThumUrl { get; set; }

    [JsonPropertyName("signUrl")]
    public object SignUrl { get; set; }

    [JsonPropertyName("serverIp")]
    public object ServerIp { get; set; }
}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。