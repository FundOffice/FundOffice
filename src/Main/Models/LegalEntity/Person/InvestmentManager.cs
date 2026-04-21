namespace FMO.Models;




public class FundInvestmentManager 
{
    public int Id { get; set; }


    /// <summary>
    /// ParticipantId
    /// </summary>
    public int PersonId { get; set; }

    public int FundId { get; set; }

    /// <summary>
    /// 姓名
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// 简介
    /// </summary>
    public string? Profile { get; set; }


    public DateOnly Start { get; set; }

    public DateOnly End { get; set; }
}