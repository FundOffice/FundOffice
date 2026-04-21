using FMO.Models;
using FMO.Utilities;

namespace FMO.Schedule;

[Obsolete]
public class FillFundDailyMission : Mission
{

    public FillFundDailyMission()
    {

    }

    public override void Init()
    {
        IsEnabled = true;
        SetNextRun();
    }

    protected override void SetNextRun()
    {
        NextRun = LastRun switch { null => DateTime.Today, DateTime d => d < DateTime.Today ? DateTime.Today : d.AddDays(1) };
    }

    protected override bool WorkOverride()
    {
        try
        {
            using var db = DbHelper.Base();
            var funds = db.GetCollection<Fund>().Query().Where(x => x.Status == FundStatus.Normal).ToArray();

            var idt = TradingDay.Days.BinarySearch(DateOnly.FromDateTime(DateTime.Today));
            idt = idt < 0 ? ~idt : idt + 1;
            foreach (var f in funds)
            {
                var last = db.GetDailyCollection(f.Id).Query().OrderByDescending(x => x.Date).FirstOrDefault()?.Date ?? f.SetupDate;

                int idx = TradingDay.Days.BinarySearch(last);
                idx = idx < 0 ? ~idx : idx + 1;
                if (idx >= idt) continue;

                var add = TradingDay.Days[idx..idt].Select(x => new DailyValue { Date = x, FundId = f.Id });
                if (add.Any()) DataTracker.OnDailyValue(add);//db.GetDailyCollection(f.Id).Insert(add);
            }

            Serilog.Log.Information("FillFundDailyMission Done");
            return true;
        }
        catch { return false; }
    }
}


public struct MissionRecord
{
    public int MissionId { get; set; }

    public DateTime Time { get; set; }

    public string Record { get; set; }
}
