using FMO.Models;
using FMO.Schedule;
using FMO.Utilities;

namespace Schedule;



/// <summary>
/// 监控order是否有对应的录单
/// </summary>
public class OrderEntryMonitorMission : OnceMission
{
    public int FundId { get; set; }

    public int OrderId { get; set; }

    public string? FundName { get; set; }

    public string? FundCode { get; set; }

    public DateOnly OpenDay { get; set; }




    protected override async Task<ErrorReturn> WorkOverride()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (OpenDay.Year > 2000 && today > OpenDay)
        {
            IsAborted = true;
            return new(false, "已过开放日，不再监控");
        }

        using var db = DbHelper.Base();
        var req =  db.GetCollection<TransferRequest>().FindOne(x => x.OrderId == OrderId);

        if(req is not null)
        {

            IsFinished = true;
            return new(true, $"订单{OrderId} 已录单");
        }

        return new(false, "未录单");
    }



}