namespace Vetting.Copilot.Models.Info;

/// <summary>
/// 图片元数据映射，关联 LiteDB FileStorage
/// </summary>
public class PhotoMap : IResolve
{
    public int Id { get; set; }

    /// <summary>原始文件名（含扩展名）</summary>
    public string? FileName { get; set; }

    /// <summary>LiteDB FileStorage ID（GUID 字符串）</summary>
    public string? FileId { get; set; }

    /// <summary>MIME 类型：image/png, image/jpeg 等</summary>
    public string? ContentType { get; set; }

    /// <summary>上传时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>可选描述/替代文本</summary>
    public string? Description { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long Size { get; set; }

    /// <summary>图片原始宽度（像素）</summary>
    public int Width { get; set; }

    /// <summary>图片原始高度（像素）</summary>
    public int Height { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Id) => Id,
        nameof(FileName) => FileName,
        nameof(FileId) => FileId,
        nameof(ContentType) => ContentType,
        nameof(CreatedAt) => CreatedAt,
        nameof(Description) => Description,
        nameof(Size) => Size,
        nameof(Width) => Width,
        nameof(Height) => Height,
        _ => null,
    };
}