using FMO.Logging;
using FMO.Models;
using FMO.Schedule;
using FMO.Todo;
using FMO.Utilities;
using Schedule;
using System.Data;

namespace FMO.Trigger;

internal static class TransferOrderTriggers
{
    /// <summary>
    /// 订单的数量不合适
    /// </summary>
    [HookData]
    public static void OrderValueNotWell(IEnumerable<TransferOrder> orders)
    {
        foreach (var item in orders)
        {
            switch (item.Type)
            {
                case TransferOrderType.FirstTrade:
                    if (item.Number < 1000000)
                        TodoService.Register(new JustNotifyTodo { Message = $"认购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】的金额{item.Number}可能不合适" });
                    break;
                case TransferOrderType.Buy:
                    // 检查要素，追加申购的要求
                    using (var db = DbHelper.Base())
                    {
                        var ele = db.GetCollection<FundElements>().FindById(item.FundId);
                        if (ele?.PurchasRule.Changes.Count is null or 0)
                        {
                            TodoService.Register(new FundElementFillTodo { FundCode = "", FundName = item.FundName!, FundId = item.FundId, Missing = ["申购规则"] });
                            break;
                        }

                        // 比对份额类型
                        var sc = ele.ShareClasses.Value;
                        int cid = 0; // 单一份额是-1;

                        if (string.IsNullOrWhiteSpace(item.ShareClass))
                        {
                            // 订单未设置份额，但要素中已分级
                            if (sc?.Length > 1)
                                TodoService.Register(new JustNotifyTodo { Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金有{string.Join(',', sc.Select(x => x.Name))}分级，但订单未填写份额类别" });
                            else cid = -1;
                        }
                        else
                        {
                            // 订单设置份额，但要素中未分级
                            if (sc?.Length is null or <= 1)
                            {
                                TodoService.Register(new JustNotifyTodo { Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金没有分级，但订单份额类别{item.ShareClass}" });
                            }
                            else
                            {
                                var ps = sc.FirstOrDefault(x => x.Name == item.ShareClass);

                                // 订单份额与要素不对齐
                                if (ps is null)
                                    TodoService.Register(new JustNotifyTodo { Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金有{string.Join(',', sc.Select(x => x.Name))}分级，但订单份额类别{item.ShareClass}" });
                                else cid = ps.Id;
                            }
                        }

                        if (cid == 0) break;

                        var rule = ele.PurchasRule.Value[cid];
                        if (rule is null)
                        {
                            LogEx.Error($"{nameof(TransferOrderTriggers)} {nameof(OrderValueNotWell)} 异常，ShareClass检验过，但是申购规则是null");
                            break;
                        }

                        // 追加金额<限制
                        if (item.Number < rule.AdditionalDeposit)
                        {
                            TodoService.Register(new JustNotifyTodo { Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】 {item.Number}可能不正确，【要素】中追加金额{rule.AdditionalDeposit}" });
                        }
                    }
                    break;
                case TransferOrderType.Share:
                case TransferOrderType.Amount:
                case TransferOrderType.RemainAmout:
                    if (item.Number < 10000)
                        TodoService.Register(new JustNotifyTodo { Message = $"赎回订单{item.OpenDate}【{item.InvestorName}】赎回【{item.FundName}】 {EnumDescriptionTypeConverter.GetEnumDescription(item.Type)} {item.Number}可能不正确，金额过小" });

                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// 监控订单是否忘了录
    /// </summary>
    /// <param name="orders"></param>
    [HookData]
    public static void RequestIsForget(IEnumerable<TransferOrder> orders)
    {
        if (!orders.Any()) return;

        // 获取成立日期
        using var db = DbHelper.Base();
        var dicSetup = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.SetupDate }).ToList().ToDictionary(x => x.Id, x => x.SetupDate);



        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var order in orders)
        {
            // 认购订单
            if (dicSetup == default)
            {
                Check(order, db);
                continue;
            }

            // 如果是历史订单，今天已过开放日，跳过
            if (order.OpenDate.Year > 2000 && today > order.OpenDate)
                continue;

            Check(order, db);
        }
    }

    private static void Check(TransferOrder order, BaseDatabase db)
    {
        // 检查有没有对应的request

        var req = db.GetCollection<TransferRequest>().FindOne(x => x.OrderId == order.Id);
        if (req is not null)
            return;

        // 建立监控mission
        var ms = new OrderEntryMonitorMission
        {
            FundId = order.FundId,
            FundName = order.FundName,
            OpenDay = order.OpenDate,
            Name = $"追踪订单是否录单",
            Description = $"{order.FundName} {order.InvestorName} {EnumDescriptionTypeConverter.GetEnumDescription(order.Type)} {order.Number}",
        };

        MissionSchedule.Register(ms);
    }
}