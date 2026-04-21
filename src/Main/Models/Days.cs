using System.Reflection;

namespace FMO.Models;



public enum DayFlag
{
    None,

    Weekend = 0x1,

    Holiday = 0x2,

    Trade = 0x4
}

public record DateMeta(DateOnly Date, int Week, DayFlag Flag);


public static class Days
{
    private static List<DateOnly> Dates = new();
    private static List<DateOnly> TradingDates = new();
    private static List<DateMeta> Data = new();


    /// <summary>
    /// 获取指定年份的所有日期
    /// </summary>
    /// <param name="year"></param>
    /// <returns></returns>
    public static DateOnly[] DaysByYear(int year)
    {
        int s = Dates.BinarySearch(new DateOnly(year, 1, 1));
        int e = Dates.BinarySearch(new DateOnly(year, 12, 31));

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Dates[s..e].ToArray();
    }

    public static DateMeta[] DayInfosByYear(int year)
    {
        int s = Dates.BinarySearch(new DateOnly(year, 1, 1));
        int e = Dates.BinarySearch(new DateOnly(year, 12, 31));

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Data[s..e].ToArray();
    }


    /// <summary>
    /// 获取指定年份的所有交易日
    /// </summary>
    /// <param name="year"></param>
    /// <returns></returns>
    public static DateOnly[] TradeDaysByYear(int year)
    {
        int s = Dates.BinarySearch(new DateOnly(year, 1, 1));
        int e = Dates.BinarySearch(new DateOnly(year, 12, 31));

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        List<DateOnly> list = new(366);
        for (int j = s; j < e; j++)
        {
            if (Data[j].Flag.HasFlag(DayFlag.Trade))
                list.Add(Dates[j]);
        }
        return list.ToArray();
    }


    public static IList<DateOnly> AllTradeDays => TradingDates;


    /// <summary>
    /// 
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateOnly NextTradingDay(DateOnly date, int skip = 1)
    {
        int s = Dates.BinarySearch(date);
        s = s < 0 ? ~s : s + 1;

        for (int i = s, j = 0; i < Dates.Count; i++)
        {
            if (Data[i].Flag.HasFlag(DayFlag.Trade))
                ++j;
            if (j >= skip)
                return Dates[i];
        }
        return default;
    }

    public static DateOnly PrevTradingDay(DateOnly date)
    {
        int s = Dates.BinarySearch(date);
        s = s < 0 ? ~s : s;

        for (int i = s - 1; i >= 0; i--)
        {
            if (Data[i].Flag.HasFlag(DayFlag.Trade))
                return Dates[i];
        }
        return default;
    }

    public static DateOnly NextTradingDay(DateTime date) => NextTradingDay(DateOnly.FromDateTime(date));

    /// <summary>
    /// 包含start end
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static int CountTradingDays(DateOnly start, DateOnly end)
    {
        if (start > end) return 0;

        int s = Dates.BinarySearch(start);
        int e = Dates.BinarySearch(end);

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Data[s..e].Count(x => x.Flag.HasFlag(DayFlag.Trade));
    }

    public static IList<DateOnly> TradingDaysBetween(DateOnly start, DateOnly end)
    {
        if (start > end) return [];

        int s = Dates.BinarySearch(start);
        int e = Dates.BinarySearch(end);

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Data[s..e].Where(x => x.Flag.HasFlag(DayFlag.Trade)).Select(x => x.Date).ToList();
    }


    public static IList<DateOnly> TradingDaysFrom(DateOnly start)
    {
        var end = PrevTradingDay(DateOnly.FromDateTime(DateTime.Now));

        if (start > end) return [];

        int s = Dates.BinarySearch(start);
        int e = Dates.BinarySearch(end);

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Data[s..e].Where(x => x.Flag.HasFlag(DayFlag.Trade)).Select(x => x.Date).ToList();
    }

    public static int TradingDayCountFrom(DateOnly start)
    {
        var end = PrevTradingDay(DateOnly.FromDateTime(DateTime.Now));

        if (start > end) return 0;

        int s = Dates.BinarySearch(start);
        int e = Dates.BinarySearch(end);

        s = s < 0 ? ~s : s;
        e = e < 0 ? ~e : e + 1;

        return Data[s..e].Where(x => x.Flag.HasFlag(DayFlag.Trade)).Count();
    }





    static Days() => Init();

    private static void Init()
    {
        try
        {
            //var fi = new FileInfo(@"config\day.csv");
            //if (fi.Exists)
            //{
            //    using var sr = new StreamReader(@"config\day.csv");
            //    InitFromStream(sr);
            //}
            //else
            InitFromAssembly();
        }
        catch
        {
            InitFromAssembly();
        }
    }

    private static void InitFromAssembly()
    {
        using var ri = Assembly.GetExecutingAssembly().GetManifestResourceStream("FMO.Models.day.csv");
        using var sr = new StreamReader(ri!);
        InitFromStream(sr);
    }

    private static void InitFromStream(StreamReader sr)
    {
        string[] strings = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var d = strings.Select(x => x.Split(','));
        Dates = d.Select(x => x[0]).Select(x => DateOnly.Parse(x)).ToList();

        var tmp = d.Select(x => new { a = DateOnly.Parse(x[0]), b = (DayFlag)Enum.Parse(typeof(DayFlag), x[1]) }).ToArray();
        Data = new List<DateMeta>(tmp.Length);
        int n = 1;
        Data.Add(new DateMeta(tmp[0].a, 1, tmp[0].b));
        foreach (var item in tmp.Skip(1))
        {
            if (item.a.DayOfWeek == DayOfWeek.Monday)
                ++n;
            if (Data[^1].Date.Year < item.a.Year)
                n = 1;

            Data.Add(new DateMeta(item.a, n, item.b));
            if (item.b == DayFlag.Trade)
                TradingDates.Add(item.a);
        }
    }

    public static bool IsTradingDay(DateTime now)
    {
        int s = Dates.BinarySearch(DateOnly.FromDateTime(now));
        s = s < 0 ? ~s : s;

        return Data[s].Flag.HasFlag(DayFlag.Trade);
    }


    public static bool IsTradingDay(DateOnly date)
    {
        int s = Dates.BinarySearch(date);
        s = s < 0 ? ~s : s;

        return Data[s].Flag.HasFlag(DayFlag.Trade);
    }
}
