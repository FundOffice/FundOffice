namespace Vetting.Models.Entities;

/// <summary>
/// 文件列表项
/// </summary>
public class FileItem
{
    public string? FileName { get; set; }
    public string? FullPath { get; set; }
    public long Size { get; set; }
}
