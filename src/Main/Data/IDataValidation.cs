using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;


namespace FMO.Utilities;

public interface IDataValidation
{
    public Type[] Related { get; }

    void Init();
    //void OnEntityArrival(IEnumerable<object> obj);
    void Verify();
}


public abstract class VerifyRule : IDataValidation
{
    protected VerifyRule()
    {
        debouncer = new(Verify, 1000);
    }

    private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

    public abstract Type[] Related { get; }


    protected Debouncer debouncer { get; set; }

    public abstract void Init();


    public void OnEntityArrival(IEnumerable<object> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }

    protected abstract void OnEntityOverride(IEnumerable<object> obj);


    public void Verify()
    {
        try
        {
            semaphoreSlim.Wait();

            VerifyOverride();

            ClearParamsOverride();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }

    protected abstract void VerifyOverride();

    protected abstract void ClearParamsOverride();

    protected void Send(IDataTip tip) => WeakReferenceMessenger.Default.Send(tip);
}



public abstract class VerifyRuleBase : IDataValidation
{
    protected VerifyRuleBase()
    {
        debouncer = new(Verify, 1000);
    }


    protected SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

    protected Debouncer debouncer { get; set; }

    public Type[] Related => throw new NotImplementedException();

    public void Verify()
    {
        try
        {
            semaphoreSlim.Wait();

            VerifyOverride();

            ClearParamsOverride();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }

    protected abstract void VerifyOverride();

    protected abstract void ClearParamsOverride();

    protected void Send(IDataTip tip) => WeakReferenceMessenger.Default.Send(tip);

    protected void Revoke(long tipId) => WeakReferenceMessenger.Default.Send(new DataTipRemove(tipId));

    public abstract void Init();
}

public abstract class VerifyRule<T> : VerifyRuleBase
{
    public void OnEntityArrival(IEnumerable<T> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }

    protected abstract void OnEntityOverride(IEnumerable<T> obj);

}

public abstract class VerifyRule<T1, T2> : VerifyRuleBase
{
    public void OnEntityArrival(IEnumerable<T1> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }
    public void OnEntityArrival(IEnumerable<T2> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }
    protected abstract void OnEntityOverride(IEnumerable<T1> obj);
    protected abstract void OnEntityOverride(IEnumerable<T2> obj);
}

public abstract class VerifyRule<T1, T2, T3> : VerifyRuleBase
{
    public void OnEntityArrival(IEnumerable<T1> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }
    public void OnEntityArrival(IEnumerable<T2> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }
    public void OnEntityArrival(IEnumerable<T3> obj)
    {
        if (obj is null || !obj.Any()) return;

        try
        {
            semaphoreSlim.Wait();

            OnEntityOverride(obj);

            debouncer.Invoke();
        }
        catch (Exception e) { LogEx.Error($"{e}"); }
        finally { semaphoreSlim.Release(); }
    }
    protected abstract void OnEntityOverride(IEnumerable<T1> obj);
    protected abstract void OnEntityOverride(IEnumerable<T2> obj);
    protected abstract void OnEntityOverride(IEnumerable<T3> obj);
}
