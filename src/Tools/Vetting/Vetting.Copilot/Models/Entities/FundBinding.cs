namespace Vetting.Copilot.Models.Entities;

/// <summary>
/// 表格级基金绑定：将某个 Range 绑定到指定基金
/// </summary>
public class FundBinding
{
    public int Id { get; set; }

    /// <summary>尽调 ID</summary>
    public string? VettingId { get; set; }

    /// <summary>文件名</summary>
    public string? FileName { get; set; }

    /// <summary>范围键，格式: {Table}_{StartRow}_{StartCol}_{EndRow}_{EndCol}</summary>
    public string? Range { get; set; }

    /// <summary>绑定的基金 ID</summary>
    public int FundId { get; set; }
}