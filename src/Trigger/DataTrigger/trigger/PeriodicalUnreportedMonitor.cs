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

            var messages = new List<string>();

            // 💡 注意：请根据实际业务确认“未报送”的判断条件（此处假设 == 0 为未报送）
            // 💡 请将 x.Code 替换为实际的基金代码/名称属性（如 x.FundCode 或 x.Name）
            var monthly = string.Join("、", fundinfos.Where(x => x.Monthly == 0).Select(x => x.Name));
            if (!string.IsNullOrEmpty(monthly)) messages.Add($"月报：{monthly}");

            var quarterly = string.Join("、", fundinfos.Where(x => x.Quarterly == 0).Select(x => x.Name));
            if (!string.IsNullOrEmpty(quarterly)) messages.Add($"季报：{quarterly}");

            var semiAnnally = string.Join("、", fundinfos.Where(x => x.SemiAnnually == 0).Select(x => x.Name));
            if (!string.IsNullOrEmpty(semiAnnally)) messages.Add($"半年报：{semiAnnally}");

            var annally = string.Join("、", fundinfos.Where(x => x.Annually == 0).Select(x => x.Name));
            if (!string.IsNullOrEmpty(annally)) messages.Add($"年报：{annally}");

            // 仅当存在未报送记录时触发通知
            if (messages.Count > 0)
            {
                TodoService.Register(new PeriodicalUnreportedTodo
                {
                    Monthly = fundinfos.Where(x => x.Monthly > 0).Select(x => x.Name!).ToArray(),
                    Quarterly = fundinfos.Where(x => x.Quarterly > 0).Select(x => x.Name!).ToArray(),
                    SemiAnnually = fundinfos.Where(x => x.SemiAnnually > 0).Select(x => x.Name!).ToArray(),
                    Annually = fundinfos.Where(x => x.Annually > 0).Select(x => x.Name!).ToArray(),
                });
            }
        }
        catch (Exception ex)
        {
            LogEx.Error(ex);
        }
    }
}