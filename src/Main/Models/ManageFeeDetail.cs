using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Models;

public record class ManageFeeDetail(int Id, DateOnly Date, decimal Fee, decimal Share);


public class ManageFeeDaily(int FundId, DateOnly Date, decimal Fee, decimal Share)
{
    public string Id => $"{FundId}.{Date.DayNumber}";
    public int FundId { get; } = FundId;
    public DateOnly Date { get; } = Date;
    public decimal Fee { get; } = Fee;
    public decimal Share { get; } = Share;
}