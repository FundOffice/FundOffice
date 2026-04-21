using FMO.Models;
using FMO.Utilities;

namespace FMO.Schedule;

public class InvestorPfidAccountSyncMission : Mission
{
    /// <summary>
    /// 间隔 小时
    /// </summary>
    public int Interval { get; set; } = 12;


    public bool IsValid { get; set; }

    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddHours(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddHours(Interval);
    }



    protected override bool WorkOverride()
    {
        // 检查是否首次
        using var db = DbHelper.Base();
        var customer = db.GetCollection<Investor>().FindAll().ToList();
        var ta = db.GetCollection<TransferRecord>().Query().Select(x => new { x.FundCode, x.InvestorId, Share = x.ShareChange() }).ToList();
        var pfmap = db.GetCollection<PfidAccount>();


        if (pfmap.Count() == 0)
        {
            var acc = db.GetCollection<AmacAccount>().FindById("xinpi");
            //var ad = await PfidAssist.QueryInvestorAccounts(acc);
           // pfmap.Upsert(ad);

        }


        return true;
    }

    internal void Wed()
    {
        using var db = DbHelper.Base();
        var acc = db.GetCollection<AmacAccount>().FindById("xinpi");
        if (acc is null || !acc.IsValid)
        {
            IsEnabled = false;
            return;
        }

        
    }

}
