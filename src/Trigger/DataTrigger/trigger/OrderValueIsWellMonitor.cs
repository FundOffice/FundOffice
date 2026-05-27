
using FMO.Models;
using FMO.Settings;
using FMO.Todo;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.Data;

namespace FMO.Trigger;

/// <summary>
/// 监控订单的数据
/// </summary>
[AbilityUnit(SettingSections.TransferMonitor, "检测交易订单数据是否正常", "检测金额异常（<10000）\n检测是否符合合同约定")]
public partial class OrderValueIsWellMonitor : ITracker<IEnumerable<TransferOrder>>
{
    internal record History(int Id, DateTime Time);
    private ILiteCollection<History> GetCollection(LiteDatabase db) => db.GetCollection<History>($"r_{nameof(OrderValueIsWellMonitor)}");

    private partial void OnDataArrival(IEnumerable<TransferOrder> obj)
    {
        // 排除已处理的
        using var tdb = DbHelper.Tracker();
        var handled = GetCollection(tdb).Query().Select(x => x.Id).ToArray();


        var orders = obj.ExceptBy(handled, x => x.Id).ToArray();
        if (orders.Length == 0) return;

        foreach (var item in orders)
        {
            var uid = $"{nameof(OrderValueIsWellMonitor)}-{item.Id}";

            switch (item.Type)
            {
                case TransferOrderType.FirstTrade:
                    if (item.Number < 1000000)
                        TodoService.Register(new JustNotifyTodo
                        {
                            UniqueId = uid,
                            Message = $"认购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】的金额{item.Number}可能不合适"
                        });
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
                                TodoService.Register(new JustNotifyTodo
                                {
                                    UniqueId = uid,
                                    Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金有{string.Join(',', sc.Select(x => x.Name))}分级，但订单未填写份额类别"
                                });
                            else cid = -1;
                        }
                        else
                        {
                            // 订单设置份额，但要素中未分级
                            if (sc?.Length is null or <= 1)
                            {
                                TodoService.Register(new JustNotifyTodo
                                {
                                    UniqueId = uid,
                                    Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金没有分级，但订单份额类别{item.ShareClass}"
                                });
                            }
                            else
                            {
                                var ps = sc.FirstOrDefault(x => x.Name == item.ShareClass);

                                // 订单份额与要素不对齐
                                if (ps is null)
                                    TodoService.Register(new JustNotifyTodo
                                    {
                                        UniqueId = uid,
                                        Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】可能不正确，基金有{string.Join(',', sc.Select(x => x.Name))}分级，但订单份额类别{item.ShareClass}"
                                    });
                                else cid = ps.Id;
                            }
                        }

                        if (cid == 0) break;

                        var rule = ele.PurchasRule.Value[cid];
                        if (rule is null)
                        {
                            Logg.Error($"{nameof(OrderValueIsWellMonitor)} 异常，ShareClass检验过，但是申购规则是null");
                            break;
                        }

                        // 追加金额<限制
                        if (item.Number < rule.AdditionalDeposit)
                        {
                            TodoService.Register(new JustNotifyTodo
                            {
                                UniqueId = uid,
                                Message = $"申购订单{item.OpenDate}【{item.InvestorName}】购买【{item.FundName}】 {item.Number}可能不正确，【要素】中追加金额{rule.AdditionalDeposit}"
                            });
                        }
                    }
                    break;
                case TransferOrderType.Share:
                case TransferOrderType.Amount:
                case TransferOrderType.RemainAmout:
                    if (item.Number < 10000)
                        TodoService.Register(new JustNotifyTodo
                        {
                            UniqueId = uid,
                            Message = $"赎回订单{item.OpenDate}【{item.InvestorName}】赎回【{item.FundName}】 {EnumDescriptionTypeConverter.GetEnumDescription(item.Type)} {item.Number}可能不正确，金额过小"
                        });

                    break;
                default:
                    break;
            }
        }
         
        GetCollection(tdb).Upsert(orders.Select(x => new History(x.Id, DateTime.Now)));
    }

}
