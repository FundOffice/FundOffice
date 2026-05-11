using FMO.AMAC;
using FMO.Logging;
using FMO.Models;
using FMO.Settings;
using FMO.Todo;
using FMO.Utilities;

namespace FMO.Trigger;

/// <summary>
/// 监控定期报告是否未报送
/// 每天检查AMAC 管理人页，查看是否有未报送的
/// 如果有就加TOTO
/// 
/// 当天报送，隔天才会更新！！！
/// </summary>
[AbilityUnit(SettingSections.FundOperationMonitor, "定期报告未报送", "监控定期报告是否未报送")]
public partial class PeriodicalUnreportedMonitor : ITracker<NewDay>
{
    private partial void OnDataArrival(NewDay obj)
    {
        using var db = DbHelper.Base();
        var m = db.GetCollection<Manager>().FindOne(x => x.IsMaster);


        // 建议保留 _ = 但内部必须捕获异常，或根据框架改为 await
        _ = DoAsync(m.AmacId);
    }

    private async Task DoAsync(string mid)
    {
        try
        {
            var fundinfos = await AmacHtml.CrawleManagerInfo(mid);
            if (fundinfos == null || !fundinfos.Any()) return;

            var todo = new PeriodicalUnreportedTodo
            {
                Monthly = fundinfos.Where(x => x.Monthly > 0).Select(x => x.Name!).ToArray(),
                Quarterly = fundinfos.Where(x => x.Quarterly > 0).Select(x => x.Name!).ToArray(),
                SemiAnnually = fundinfos.Where(x => x.SemiAnnually > 0).Select(x => x.Name!).ToArray(),
                Annually = fundinfos.Where(x => x.Annually > 0).Select(x => x.Name!).ToArray(),
            };
            // 仅当存在未报送记录时触发通知
            if (todo.Monthly.Length > 0 || todo.Quarterly.Length > 0 || todo.SemiAnnually.Length > 0 || todo.Annually.Length > 0)
                TodoService.Register(todo);
            else TodoService.Unregister(nameof(PeriodicalUnreportedTodo));
        }
        catch (Exception ex)
        {
            LogEx.Error(ex);
        }
    }
}