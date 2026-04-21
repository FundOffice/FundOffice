namespace FMO.Models;

/// <summary>
/// 基金份额记录，在有ta日，记录剩余份额
/// </summary>
/// <param name="Id"></param>
/// <param name="FundId"></param>
/// <param name="Date"></param>
/// <param name="Share"></param>
public record class FundShareRecord(int FundId, DateOnly Date, decimal Share)
{
    public long Id => ((long)FundId << 32) | (long)Date.DayNumber;
    public int FundId { get; } = FundId;
    public DateOnly Date { get; } = Date;
    public decimal Share { get; } = Share;
}

public record class FundShareRecordByDaily(int FundId, DateOnly Date, decimal Share)
{
    public long Id => ((long)FundId << 32) | (long)Date.DayNumber;
    public int FundId { get; } = FundId;
    public DateOnly Date { get; } = Date;
    public decimal Share { get; } = Share;
}


public record class FundShareRecordByTransfer(int FundId, DateOnly RequestDate, DateOnly Date, decimal Share) : FundShareRecordByDaily(FundId, Date, Share)
{
    public DateOnly RequestDate { get; } = RequestDate;
}
