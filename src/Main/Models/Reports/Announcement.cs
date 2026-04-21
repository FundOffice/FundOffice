namespace FMO.Models;

/// <summary>
/// 基金公告
/// </summary>
public class FundAnnouncement
{

    public int Id { get; set; }

    public int FundId { get; set; }

    public DateOnly Date { get; set; }

    public string? Title { get; set; }

    public DualFile? File { get; set; }

}
