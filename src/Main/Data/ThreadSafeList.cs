using LiteDB;
using System.Collections;

namespace FMO.Utilities;

public class ThreadSafeList<T> : IEnumerable<T>
{
    protected readonly List<T> _innerList = new List<T>();
    protected readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    public delegate void CollectionChangedHandler();

    public CollectionChangedHandler? CollectionChanged;

    public virtual void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _innerList.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }
    public void Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _innerList.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }
    public void Remove(Func<T, bool> cond)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var item in _innerList.Where(cond).ToArray())
                _innerList.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
            CollectionChanged?.Invoke();
        }
    }

    public T Get(int index)
    {
        _lock.EnterReadLock();
        try
        {
            return _innerList[index];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerator<T> GetEnumerator() => _innerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _innerList.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}

