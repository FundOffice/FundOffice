using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Trustee;


/// <summary>
/// 已经获取的数据区间
/// </summary>
/// <param name="Id">Identifier+Method</param>
/// <param name="Begin"></param>
/// <param name="End"></param>
public class TrusteeMethodShotRange(string Id, DateOnly Begin, DateOnly End)
{
    public string Id { get; } = Id;
    public DateOnly Begin { get; set; } = Begin;
    public DateOnly End { get; set; } = End;

    public void Merge(DateOnly begin, DateOnly end)
    {
        if (begin < Begin) Begin = begin;
        if (end > End) End = end;
    }
}
