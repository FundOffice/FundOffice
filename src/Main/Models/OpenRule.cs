using FMO.Models;
using System.Diagnostics;

namespace FMO;


public enum SequenceOrder { Ascend, Descend }

public class OpenRule
{
    static string[] weekhead = ["一", "二", "三", "四", "五",];
    public FundOpenType Type { get; set; }

    /// <summary>
    /// 选择季
    /// Year 1-4
    /// 其它 忽略
    /// </summary>
    public int[]? Quarters { get; set; }

    /// <summary>
    /// 选择月
    /// Year 否则1-12
    /// QuarterFlag 则1-3
    /// Month Week 忽略
    /// </summary>
    public int[]? Months { get; set; }

    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public int[]? Weeks { get; set; }

    public SequenceOrder WeekOrder { get; set; }

    /// <summary>
    /// 选择周
    /// Year 否则1-54
    /// QuarterFlag 则1-14
    /// Month 1-5
    /// Week 忽略
    /// </summary>
    public int[]? Dates { get; set; }

    /// <summary>
    /// 选择天
    /// Year 1-365
    /// QuarterFlag 1-92
    /// Month 1-31
    /// Week 7
    /// </summary>
    public SequenceOrder DayOrder { get; set; }

    public bool TradeOrNatural { get; set; }

    /// <summary>
    /// 是否顺延
    /// </summary>
    public bool Postpone‌ { get; set; }

    /// <summary>
    /// 顺延是否跨周
    /// </summary>
    public bool CrossWeek { get; set; }


    private string WeekStr()
    {
        var days = Dates?.Where(x => x < 5);
        if (days is null || !days.Any()) return "";

        if (DayOrder == SequenceOrder.Ascend)
        {
            if (TradeOrNatural)
                return $"第{string.Join('、', days!.Select(x => x))}个交易日";
            else
                return $"{string.Join('、', days!.Select(x => weekhead[x - 1]))}";
        }
        else
        {
            if (TradeOrNatural)
                return $"倒数第{string.Join('、', days!.Select(x => x))}个交易日";
            else
                return $"倒数第{string.Join('、', days!.Select(x => x))}个自然日";
        }
    }


    private string MonthStr()
    {
        if (Dates is null || Dates.Length == 0) return "";

        if (Weeks?.Length > 0)
            return $"{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x))}个的{WeekStr()}";
        else
            return $"{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}{(TradeOrNatural ? "第" : "")}{string.Join('、', Dates.Select(x => x))}{(TradeOrNatural ? "个交易" : "")}日开放";
    }

    private string QuarterStr()
    {
        if (Dates is null || Dates.Length == 0) return "";

        if (Months?.Length > 0)
            return $"第{string.Join('、', Months.Select(x => x))}月的{MonthStr()}";
        else if (Weeks?.Length > 0)
            return $"{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x))}个的{WeekStr()}";
        else
            return $"{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Dates.Select(x => x))}个{(TradeOrNatural ? "交易" : "自然")}日开放";
    }

    public override string ToString()
    {
        switch (Type)
        {
            case FundOpenType.Closed:
                return "不开放";
            case FundOpenType.Yearly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                if (Quarters?.Length > 0)
                    return $"每年第{string.Join('、', Quarters.Select(x => x))}季度的{QuarterStr()}" + (Postpone ? "，非交易日顺延" : "");
                else if (Months?.Length > 0)
                    return $"每年第{string.Join('、', Months.Select(x => x))}月的{MonthStr()}" + (Postpone ? "，非交易日顺延" : "");
                else if (Weeks?.Length > 0)
                    return $"每年{(WeekOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Weeks.Select(x => x + 1))}周的{WeekStr()}" + (Postpone ? "，非交易日顺延" : "");
                else
                    return $"每年{(DayOrder == SequenceOrder.Ascend ? "" : "倒数")}第{string.Join('、', Dates.Select(x => x + 1))}个{(TradeOrNatural ? "交易" : "自然")}日开放" + (Postpone ? "，非交易日顺延" : "");
            case FundOpenType.Quarterly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                return "每季" + QuarterStr() + (Postpone ? "，非交易日顺延" : "");
            case FundOpenType.Monthly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";
                return "每月" + MonthStr() + (Postpone ? "，非交易日顺延" : "");
            case FundOpenType.Weekly:
                if (Dates is null || Dates.Length == 0) return "无效的设置";

                return "每周" + WeekStr() + (Postpone ? "，非交易日顺延" : "");
            case FundOpenType.Daily:
                return "每日";
            default:
                return "-";
        }
    }

    public bool IsValid()
    {
        switch (Type)
        {
            case FundOpenType.Closed:
                return true;
            case FundOpenType.Yearly:
                if (Dates is null || Dates.Length == 0) return false;                
                return true;
            case FundOpenType.Quarterly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Monthly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Weekly:
                if (Dates is null || Dates.Length == 0) return false;
                return true;
            case FundOpenType.Daily:
                return true;
            default:
                return false;
        }
    }

    public OpenDateSheet[] Apply(int year)
    {
        if (Type == FundOpenType.Closed)
            return Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = OpenType.None }).ToArray();

        if (Type == FundOpenType.Daily)
            return Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = x.Flag.HasFlag(DayFlag.Trade) ? OpenType.Fixed : OpenType.None }).ToArray();

        var result = Days.DayInfosByYear(year).Select(x => new DateEx { Date = x.Date, Flag = x.Flag, WeekOfMonth = 1, WeekOfYear = 1, Type = OpenType.None }).ToList();

        // 计算周序号
        for (int i = 1; i < result.Count; i++)
        {
            var last = result[i - 1];
            var cur = result[i];
            if (cur.Date.DayOfWeek == DayOfWeek.Monday)
                cur.WeekOfYear = last.WeekOfYear + 1;
            else cur.WeekOfYear = last.WeekOfYear;
        }

        ////////////////////////////////////////////////////////
        // 季度
        if (Type == FundOpenType.Yearly && Quarters?.Length > 0)
        {
            // 排除不符合的季度
            // 计算
            var f = result.Where(x => Array.BinarySearch(Quarters, (x.Date.Month - 1) / 3 + 1) < 0);

            foreach (var x in f)
                x.IsExclude = true;
        }

        // 月
        if (Type switch { FundOpenType.Yearly or FundOpenType.Quarterly => true, _ => false })
        {
            if (Months?.Length > 0)
            {
                // 计算 
                var sq = Type == FundOpenType.Quarterly || Quarters?.Length > 0;
                var f = result.Where(x => Array.BinarySearch(Months, sq ? (x.Date.Month - 1) % 3 + 1 : x.Date.Month) < 0);

                foreach (var x in f)
                    x.IsExclude = true;
            }
        }
        ////////////////////////////////////////////////////////
        ///


        // 周
        if (Type switch { FundOpenType.Yearly or FundOpenType.Quarterly or FundOpenType.Monthly => true, _ => false })
        {
            var sm = Type == FundOpenType.Monthly || Months?.Length > 0;
            var sq = Type == FundOpenType.Quarterly || Quarters?.Length > 0;
            var sw = Weeks?.Length > 0;

            if (sm) // 按月
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);
                        foreach (var m in sel)
                        {
                            foreach (var w in m.GroupBy(x => x.WeekOfYear).Index().Select(x => (Index: x.Index + 1, x.Item)))
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                                if (!pairw) continue;

                                foreach (var s in w.Item.Index())
                                {
                                    if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                        s.Item.Type = OpenType.Fixed;
                                }
                            }
                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);// && Array.BinarySearch(Dates, (int)x.Date.DayOfWeek) >= 0).GroupBy(x => x.Date.Month);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var m in dw.GroupBy(x => x.Date.Month)) // 月
                            {
                                foreach (var w in m.Index()) //第N周N
                                {
                                    bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                    if (!pairw) continue;

                                    w.Item.Type = OpenType.Fixed;
                                }
                            }
                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }

            }
            else if (sq) //季
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (x.Date.Month - 1) / 3 + 1);
                        foreach (var m in sel)
                        {
                            foreach (var w in m.GroupBy(x => x.WeekOfYear).Index().Select(x => (Index: x.Index + 1, x.Item)))
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                                if (!pairw) continue;

                                foreach (var s in w.Item.Index())
                                {
                                    if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                        s.Item.Type = OpenType.Fixed;
                                }
                            }
                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => (x.Date.Month - 1) / 3 + 1);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);// && Array.BinarySearch(Dates, (int)x.Date.DayOfWeek) >= 0).GroupBy(x => x.Date.Month);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var m in dw.GroupBy(x => (x.Date.Month - 1) / 3 + 1)) // 月
                            {
                                foreach (var w in m.Index()) //第N周N
                                {
                                    bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                    if (!pairw) continue;

                                    w.Item.Type = OpenType.Fixed;
                                }
                            }
                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (x.Date.Month - 1) / 3 + 1);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else //年
            {
                // 交易日
                if (TradeOrNatural)
                {
                    if (sw && Dates?.Length > 0) //n 周的第n个交易日 此项手动不可用
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.WeekOfYear);
                        foreach (var w in sel.Index())
                        {
                            bool pairw = Array.BinarySearch(Weeks!, w.Index) >= 0;
                            if (!pairw) continue;

                            foreach (var s in w.Item.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }

                        }

                        // 非交易日及顺延
                        var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                        foreach (var item in chk)
                        {
                            item.Type = OpenType.None;

                            //if (Postpone) //顺延
                            {
                                int idx = result.IndexOf(item);
                                for (int i = idx + 1; i < result.Count; i++)
                                {
                                    if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                    {
                                        result[i].Type = OpenType.Fixed;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (Dates?.Length > 0) //的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade));

                        foreach (var s in sel.Index())
                        {
                            if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                s.Item.Type = OpenType.Fixed;
                        }

                    }
                }
                else// 自然日
                {
                    if (sw && Dates?.Length > 0) //n个周N 
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);
                        foreach (var dw in sel)
                        {
                            if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                                continue;

                            foreach (var w in dw.Index()) //第N周N
                            {
                                bool pairw = Array.BinarySearch(Weeks!, w.Index + 1) >= 0;
                                if (!pairw) continue;

                                w.Item.Type = OpenType.Fixed;
                            }

                        }


                    }
                    else if (Dates?.Length > 0) //月的第n个交易日
                    {
                        IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                        var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => x.Date.Month);

                        foreach (var m in sel)
                        {
                            foreach (var s in m.Index())
                            {
                                if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                    s.Item.Type = OpenType.Fixed;
                            }
                        }
                    }

                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (Type == FundOpenType.Weekly) //每周
        {
            // 交易日
            if (TradeOrNatural)
            {
                if (Dates?.Length > 0) //每周的第n个交易日
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.WeekOfYear);
                    foreach (var m in sel)
                    {
                        foreach (var d in m.Index())
                        {

                            if (Array.BinarySearch(Dates, d.Index + 1) >= 0)
                                d.Item.Type = OpenType.Fixed;
                        }
                    }

                    // 非交易日及顺延
                    //var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    //foreach (var item in chk)
                    //{
                    //    item.Type = OpenType.None;

                    //    //if (Postpone) //顺延
                    //    {
                    //        int idx = result.IndexOf(item);
                    //        for (int i = idx + 1; i < result.Count; i++)
                    //        {
                    //            if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                    //            {
                    //                result[i].Type = OpenType.Fixed;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //}
                }
                else if (Dates?.Length > 0) //月的第n个交易日
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude && x.Flag.HasFlag(DayFlag.Trade)).GroupBy(x => x.Date.Month);

                    foreach (var m in sel)
                    {
                        foreach (var s in m.Index())
                        {
                            if (Array.BinarySearch(Dates, s.Index + 1) >= 0)
                                s.Item.Type = OpenType.Fixed;
                        }
                    }
                }
            }
            else// 自然日
            {
                if (Dates?.Length > 0) //n个周N 
                {
                    IEnumerable<DateEx> orderd = DayOrder == SequenceOrder.Ascend ? result : result.AsEnumerable().Reverse();
                    var sel = orderd.Where(x => !x.IsExclude).GroupBy(x => (int)x.Date.DayOfWeek);
                    foreach (var dw in sel)
                    {
                        if (Array.BinarySearch(Dates, dw.Key) < 0) // 星期几不符合
                            continue;


                        foreach (var w in dw) //第N周N
                        {
                            w.Type = OpenType.Fixed;
                        }

                    }


                    // 非交易日及顺延
                    var chk = result.Where(x => !x.Flag.HasFlag(DayFlag.Trade) && x.Type == OpenType.Fixed).ToArray();
                    foreach (var item in chk)
                    {
                        item.Type = OpenType.None;

                        if (Postpone) //顺延
                        {
                            int idx = result.IndexOf(item);
                            for (int i = idx + 1; i < result.Count; i++)
                            {
                                if (result[i].Type != OpenType.Fixed && !result[i].IsExclude && result[i].Flag.HasFlag(DayFlag.Trade))
                                {
                                    result[i].Type = OpenType.Fixed;
                                    break;
                                }
                            }
                        }
                    }
                }

            }
        }
        return result.ToArray();
    }
     


    private static int QuarterOfDay(DateOnly d) => (d.Month - 1) / 3;


    public class DateEx : OpenDateSheet
    {
        public bool IsExclude { get; set; }

        public Pair Pair { get; set; }

        public int WeekOfYear { get; set; }

        public int WeekOfMonth { get; set; }

        public int WeekOfQuarter { get; set; }

        public int Calc { get; set; }

        public void PairTo(Pair pair) => Pair |= pair;
    }

    [Flags]
    public enum Pair
    {
        Quarter = 1,

        Month = 2,

        Week = 4,

        Day = 8,

        Ok = Quarter | Month | Week | Day,
    }

}

public enum OpenType
{
    None,

    /// <summary>
    /// 固定
    /// </summary>
    Fixed,

    /// <summary>
    /// 临时
    /// </summary>
    Temperaty,

    /// <summary>
    /// 顺延的
    /// </summary>
    Postpone
}

public interface IDate
{
    public DateOnly Date { get; }
}


public class OpenDateSheet : IDate
{
    public required DateOnly Date { get; init; }

    public DayFlag Flag { get; set; }

    public OpenType Type { get; set; }

}