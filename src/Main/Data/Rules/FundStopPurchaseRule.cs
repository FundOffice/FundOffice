using FMO.Logging;
using FMO.Models;
using System.Collections.Concurrent;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FMO.Utilities;



public record FundStopPurchaseContext(string FundName, bool PurchaseLimited, decimal DailyAverage, DateOnly LimitedDate, DateOnly? ClearDate);


/// <summary>
/// 停止申购预警
/// </summary>
public class FundStopPurchaseRule : VerifyRule<DailyValue>
{
    public List<DailyValue> DailyValues { get; set; } = [];


    public ConcurrentDictionary<int, IDataTip> Tips { get; } = [];

    private const decimal Threshold = 5000000m;

    public override void Init()
    {
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name, x.SetupDate, x.Status }).ToList();


        // 运行中的产品，重新算一遍
        foreach (var f in funds.Where(x => x.Status == FundStatus.Normal))
        {
            // 组织数据 
            var begin = f.SetupDate.Year < 2024 ? new DateOnly(2024, 1, 1) : f.SetupDate;
            var tradingdays = Days.TradingDaysBetween(begin, DateOnly.FromDateTime(DateTime.Now));

            // 合并，缺失的值=0
            var dys = db.GetDailyCollection(f.Id).Query().Where(x => x.Date >= begin).Select(x => new { x.Date, x.NetAsset }).ToList();
            var fdys = from t in tradingdays join d in dys on t equals d.Date into r from x in r.DefaultIfEmpty() orderby t select (Date: t, NetAsset: x?.NetAsset ?? 0);

            var dates = fdys.Select(x => x.Date).ToArray();
            if (dates.Length == 0) continue;

            var assets = fdys.Select(x => x.NetAsset).ToArray();
            FundLimit fundLimit = new() { Id = f.Id };
            Process(fundLimit, dates, assets);

            db.GetCollection<FundLimit>().Upsert(fundLimit);

            if (fundLimit.PurchaseLimited || fundLimit.PotentialLimitDate != default || fundLimit.PotentialClearDate != default)
            {
                var tip = new DataTip<FundStopPurchaseContext>()
                {
                    Tags = ["Fund", $"Fund{f.Id}", nameof(FundStopPurchaseRule)],
                    _Context = new FundStopPurchaseContext(f.Name, fundLimit.PurchaseLimited, fundLimit.EstimatedDailyAssets, fundLimit.PotentialLimitDate, fundLimit.PotentialClearDate == default ? null : fundLimit.PotentialClearDate)
                };
                Tips.TryAdd(f.Id, tip);
                Send(tip);
            }
        }

    }


    /// <summary>
    /// 1. 初始化，从2024年或成立日起算
    /// 2. 增量，dates > checked
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="dates"></param>
    /// <param name="assets"></param>
    private void Process(FundLimit limit, DateOnly[] dates, decimal[] assets)
    {
        int currentYear = DateTime.Now.Year;
        DateOnly begin = new DateOnly(2024, 8, 1);

        for (int i = 0; i < dates.Length; i++)
        {
            var date = dates[i];
            var asset = assets[i];

            if (asset != 0)
            {
                limit.TotalAsset += asset;
                ++limit.DaysThisYear;
            }

            if (!limit.PurchaseLimited)
            {
                if (asset == 0)
                    limit.DataMissing = true;

                // 连续60天
                if (limit.PotentialLimitDate.Year > 2023 && date >= limit.PotentialLimitDate)
                {
                    limit.PurchaseLimited = true;
                    limit.LimitDate = date;
                }
                else if (date >= begin && asset != 0 && asset < Threshold && limit.LimitBaseDate == default)
                {
                    limit.LimitBaseDate = date;
                    limit.PotentialLimitDate = Days.NextTradingDay(date, 60);
                }
                else if (date >= begin && asset >= Threshold && limit.LimitBaseDate != default)
                {
                    limit.LimitBaseDate = default;
                    limit.PotentialLimitDate = default;
                }


                // 跨年
                if (i < dates.Length - 1 && date.Year < dates[i + 1].Year)
                {
                    // 历史年度
                    if (date.Year != currentYear)
                    {
                        var avg = limit.TotalAsset / limit.DaysThisYear;
                        if (avg < Threshold)
                        {
                            limit.PurchaseLimited = true;
                            limit.LimitDate = date;
                        }
                    }

                    limit.TotalAsset = 0;
                    limit.DaysThisYear = 0;
                }
            }

            if (limit.PurchaseLimited)
            {
                if (asset < Threshold && limit.ClearBaseDate == default)
                {
                    limit.ClearBaseDate = date;
                    limit.PotentialClearDate = Days.NextTradingDay(date, 120);
                }
                else if (asset >= Threshold)
                {
                    limit.ClearBaseDate = default;
                    limit.PotentialClearDate = default;
                }
            }
        }

        // 更新年度平均

        var remaindays = Days.CountTradingDays(dates.Last(), new DateOnly(currentYear, 12, 31));
        limit.EstimatedDailyAssets = (limit.TotalAsset + assets.Last(x => x != 0) * remaindays) / (limit.DaysThisYear + remaindays) / 10000;


        limit.CheckDate = dates.Last();
    }

    //private void OnDate(FundLimit limit, DateOnly date, decimal asset)
    //{
    //    if (!limit.PurchaseLimited)
    //    {
    //        if (limit.PotentialLimitDate.DayNumber > 2000 && limit.PotentialLimitDate <= date)
    //        {
    //            limit.PurchaseLimited = true;
    //            limit.LimitDate = limit.PotentialLimitDate;

    //            limit.PotentialClearDate = asset < 5000000 ? Days.NextTradingDay(limit.LimitDate, 120) : default;
    //        }
    //        else if (asset < 5000000)
    //        {
    //            if (limit.NearestFirstBelow == default)
    //            {
    //                limit.NearestFirstBelow = date;
    //                limit.PotentialLimitDate = Days.NextTradingDay(date, 60);
    //            }

    //        }
    //        else
    //        {
    //            limit.NearestFirstBelow = default;
    //            limit.PotentialLimitDate = default;
    //        }
    //    }

    //    // 预估年度，如果全>500，没必要算
    //    if (!limit.PurchaseLimited && limit.NearestFirstBelow != default)
    //    {

    //    }



    //    // 受限产品估算清盘日
    //    if (limit.PurchaseLimited)
    //    {
    //        if (limit.PotentialClearDate.DayNumber > 2000 && limit.PotentialClearDate <= date)
    //        {
    //            limit.ShouldClear = true;
    //        }
    //        else if (asset < 5000000)
    //        {
    //            if (limit.NearestFirstBelow == default)
    //            {
    //                limit.NearestFirstBelow = date;
    //                limit.PotentialClearDate = Days.NextTradingDay(date, 120);
    //            }

    //        }
    //        else
    //        {
    //            limit.NearestFirstBelow = default;
    //            limit.PotentialClearDate = default;
    //        }
    //    }

    //    limit.CheckDate = date;
    //}


    protected override void ClearParamsOverride()
    {
        DailyValues.Clear();
    }

    protected override void OnEntityOverride(IEnumerable<DailyValue> obj)
    {
        DailyValues.AddRange(obj);
    }


    protected override void VerifyOverride()
    {
        using var db = DbHelper.Base();
        foreach (var fv in DailyValues.GroupBy(x => x.FundId))
        {

            var fundLimit = db.GetCollection<FundLimit>().FindById(fv.Key);
            var f = db.GetCollection<Fund>().FindById(fv.Key);
            try
            {
                if (fundLimit.DataMissing)
                {
                    // 组织数据 
                    var begin = f.SetupDate.Year < 2024 ? new DateOnly(2024, 1, 1) : f.SetupDate;
                    var tradingdays = Days.TradingDaysBetween(begin, DateOnly.FromDateTime(DateTime.Now));

                    // 合并，缺失的值=0
                    var dys = db.GetDailyCollection(f.Id).Query().Where(x => x.Date >= begin).Select(x => new { x.Date, x.NetAsset }).ToList();
                    var fdys = from d in dys join t in tradingdays on d.Date equals t into r from x in r.DefaultIfEmpty() orderby d.Date select (d.Date, d.NetAsset);

                    var dates = fdys.Select(x => x.Date).ToArray();
                    var assets = fdys.Select(x => x.NetAsset).ToArray();

                    if (dates.Length == 0)
                    {
                        //LogEx.Error($"{f.Name} From {begin} Has No Nv");
                        continue;
                    }

                    fundLimit = new() { Id = f.Id };
                    Process(fundLimit, dates, assets);

                    db.GetCollection<FundLimit>().Upsert(fundLimit);

                    if (fundLimit.PurchaseLimited || fundLimit.PotentialLimitDate != default || fundLimit.PotentialClearDate != default)
                    {
                        if (Tips.TryRemove(f.Id, out var old))
                            Revoke(old.Id);

                        var tip = new DataTip<FundStopPurchaseContext>()
                        {
                            Tags = ["Fund", $"Fund{f.Id}", nameof(FundStopPurchaseRule)],
                            _Context = new FundStopPurchaseContext(f.Name, fundLimit.PurchaseLimited, fundLimit.EstimatedDailyAssets, fundLimit.PotentialLimitDate, fundLimit.PotentialClearDate == default ? null : fundLimit.PotentialClearDate)
                        };
                        Tips.TryAdd(f.Id, tip);
                        Send(tip);
                    }
                }
                else
                {
                    var fdys = fv.Where(x => x.Date > fundLimit.CheckDate).OrderBy(x => x.Date).ToArray();

                    var dates = fdys.Select(x => x.Date).ToArray();
                    var assets = fdys.Select(x => x.NetAsset).ToArray();

                    if (dates.Length == 0)
                    {
                        //LogEx.Error($"{f.Name} From {fundLimit.CheckDate} Has No Nv");
                        continue;
                    }

                    Process(fundLimit, dates, assets);

                    db.GetCollection<FundLimit>().Upsert(fundLimit);

                    if (fundLimit.PurchaseLimited || fundLimit.PotentialLimitDate != default || fundLimit.PotentialClearDate != default)
                    {
                        if (Tips.TryRemove(f.Id, out var old))
                            Revoke(old.Id);

                        var tip = new DataTip<FundStopPurchaseContext>()
                        {
                            Tags = ["Fund", $"Fund{f.Id}", nameof(FundStopPurchaseRule)],
                            _Context = new FundStopPurchaseContext(f.Name, fundLimit.PurchaseLimited, fundLimit.EstimatedDailyAssets, fundLimit.PotentialLimitDate, fundLimit.PotentialClearDate == default ? null : fundLimit.PotentialClearDate)
                        };

                        Tips.TryAdd(f.Id, tip);
                        Send(tip);
                    }
                }

            }
            catch (Exception ex)
            {
                LogEx.Error($"{f.Name} FundStopPurchaseRule", ex);
            }

        }
    }
}