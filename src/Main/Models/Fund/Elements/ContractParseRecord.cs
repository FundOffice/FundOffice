namespace FMO.Models;

/// <summary>
/// 合同 AI 解析结果缓存记录
/// </summary>
public class ContractParseRecord
{
    /// <summary>文件 MD5 hash（来自 FileMeta.Hash），作为主键</summary>
    public string Id { get; set; } = "";

    /// <summary>解析时间</summary>
    public DateTime ParsedAt { get; set; }

    /// <summary>AI 原始返回 JSON（未经过提取/序列化处理的原始响应字符串）</summary>
    public string FundInfoJson { get; set; } = "";
}
