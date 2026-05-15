using LiteDB;

namespace FMO.Models;


public class TemplateGlobal
{
    public Fund[]? Funds { get; set; }

    public Investor[]? Investors { get; set; }

    /// <summary>
    /// 内部自行筛选
    /// int FundId { get; set; } 
    /// string? Class { get; set; }
    /// </summary>
    public DailyValue[]? Dailies { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public TransferRecord[]? Records { get; set; }


    public DateOnly[]? Dates { get; set; }



}


public class TemplateRefer<T> : TemplateGlobal
{

    public ILiteQueryable<T>? query { get; set; }

    //private TransferRecord[] dd(ILiteQueryable<TransferRecord> queryable)
    //{
        
    //}
}

//public class ttt:TemplateRefer<DailyValue>
//{

//    public DailyValue[] get()
//    {
//        query.Where(x => x.Date <= Dates[0]).OrderByDescending(x => x.Date).Limit(1).ToArray();
//    }
//}