using LiteDB;

namespace FMO.Models;


public class ScriptGlobal
{
    public object[] inputs { get; set; } = [];


    public Fund[]? Funds { get; set; }


    public FundElements[]? Elements { get; set; }

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
 
public class TemplateRefer<T> : ScriptGlobal
{

    public ILiteQueryable<T>? query { get; set; }
     
}

