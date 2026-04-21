namespace FMO.Models;

public class InvestorBalance
{
    public long Id => MakeId(InvestorId, FundId);

    public int InvestorId { get; set; }

    public int FundId { get; set; }

    public decimal Deposit { get; set; }

    public decimal Withdraw { get; set; }

    public decimal Share { get; set; }

    public DateOnly Date { get; set; }



    public static long MakeId(int investorId, int fundId) => (long)investorId << 32 | (uint)fundId;

    public static (int investorId, int fundId) ParseId(long id)
    {
        int investorId = (int)(id >> 32);
        int fundId = (int)(id & 0xFFFFFFFF);
        return (investorId, fundId);
    }
}


public class InvestorFundEntry
{
    public long Id => ((long)InvestorId << 44) | ((long)FundId << 24) | (uint)FirstBuy.DayNumber;

    public int InvestorId { get; set; }

    public int FundId { get; set; }

    public DateOnly FirstBuy { get; set; }

    public DateOnly SellOut { get; set; }

}