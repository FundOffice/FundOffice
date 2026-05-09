using FMO.Disclosure;
using FMO.Logging;
using FMO.Models;
using System.Collections.Concurrent;

namespace FMO.Utilities;



[Hookable(typeof(IEnumerable<TransferOrder>))]
[Hookable(typeof(IEnumerable<TransferRequest>))]
[Hookable(typeof(IEnumerable<TransferRecord>))]
[Hookable(typeof(IEnumerable<DailyValue>))]
[Hookable(typeof(IDisclosureNotice))]
[Hookable(typeof(NewDay))]
[Hookable(typeof(EntityChanged<Fund, DateOnly>))]
[Hookable(typeof(FundFlow))]
[Hookable(typeof(EntityRemoved<FundFlow, int>))]
[Hookable(typeof(EntityChanged<FundElements, DateOnly, int>))]
[Hookable(typeof(IEnumerable<FundShareRecordByDaily>))]
[Hookable(typeof(IEnumerable<FundShareRecordByTransfer>))]
public sealed partial class DataHub
{
    // 内部类型路由容器，对外完全透明
    private static readonly ConcurrentDictionary<Type, object> _managers = new();
    private static readonly ConcurrentDictionary<Type, Delegate> _processors = new();



    /// <summary>
    /// 订阅者异常处理钩子，可重写或注入 ILogger
    /// </summary>
    private static void OnSubscriberError(Type dataType, Exception ex)
    {
        LogEx.Error($"[DataHub] 派发 {dataType.Name} 时订阅者异常: {ex.StackTrace}");
    }

    // ================= 内部泛型管理器 =================
    private sealed class SubscriptionManager<T>
    {
        private readonly List<Action<T>> _handlers = new();
        private readonly Lock _lock = new();

        public IDisposable Add(Action<T> handler)
        {
            lock (_lock) _handlers.Add(handler);
            return new Unsubscriber(this, handler);
        }

        public Action<T>[] GetSnapshot()
        {
            lock (_lock) return _handlers.ToArray();
        }

        public void Remove(Action<T> handler)
        {
            lock (_lock) _handlers.Remove(handler);
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly SubscriptionManager<T> _manager;
            private readonly Action<T> _handler;

            public Unsubscriber(SubscriptionManager<T> manager, Action<T> handler)
            {
                _manager = manager;
                _handler = handler;
            }

            public void Dispose() => _manager.Remove(_handler);
        }
    }
}