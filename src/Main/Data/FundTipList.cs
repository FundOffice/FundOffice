namespace FMO.Utilities;

public class FundTipList : ThreadSafeList<FundTip>
{
    public override void Add(FundTip item)
    {
        _lock.EnterWriteLock();
        bool add = false;
        try
        {
            // 不重复添加
            if (!_innerList.Any(x => x.FundId == item.FundId && x.Type == item.Type))
            {
                _innerList.Add(item);
                add = true;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
            if (add)
                CollectionChanged?.Invoke();
        }
    }
}

