using LiteDB;

namespace Vetting.Entity;

public class ParsedJson
{
    [BsonId]
    public int Id { get; set; }

    public string FileHash { get; set; } = "";
    public string Provider { get; set; } = "";
    public DateTime Time { get; set; }
    public string Json { get; set; } = "";
}
