using LiteDB;

namespace FMO.Schedule;

public class GzMailInfo
{
    public required string Id { get; set; }

    public required string Subject { get; set; }

    public DateTime Time { get; set; }

    public GzMailAttachInfo[]? Attachments { get; set; }

    public bool HasError { get; set; }
}
