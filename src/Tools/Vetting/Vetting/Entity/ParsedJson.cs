using LiteDB;

namespace Vetting.Entity;

public class ParsedJson
{
    [BsonId]
    public int Id { get; set; }

    /// <summary>文件名</summary>
    public string FileName { get; set; } = "";
    public string Provider { get; set; } = "";
    public DateTime Time { get; set; }
    public string Json { get; set; } = "";
}