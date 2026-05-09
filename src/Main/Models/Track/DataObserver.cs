using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;


public interface ITracker<T>
{
    // void DataArrival(T obj);


}

/// <summary>
/// 数据监视器
/// 下有
/// VerifyRule 检验数据是否异常
/// Trigger 检验数据并触发相应操作
/// </summary>
public abstract class DataObserver : IDisposable
{

    protected SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

    protected IDisposable[]? disposables;

    /// <summary>
    /// 释放所有订阅资源
    /// </summary>
    public void Dispose()
    {
        if (disposables?.Length is null or 0) return;

        foreach (var disposable in disposables)
            disposable?.Dispose();

        disposables = null;
    }

    public void Start() => RegisterHandler();

    public void Stop() => Dispose();

    protected abstract void RegisterHandler();


}
