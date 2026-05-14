using FMO.Models;

namespace TestScript;

public class TemplateUsing
{
    public Fund[]? Funds { get; set; }

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

/*
input a b c
reference a b c d e

func()
{
return object;
}

// a b c 等是一组固定可选的class，宿主程序解析后，给UI，让用户选择a类中的哪些数据，其它同理
// ref 根据选择，比如 a 选了id=1，2，3这些，生成一个object{ a=[], b=[] ,c =[]}，这些是宿主的部分
// func 是用户写的脚本
 */

