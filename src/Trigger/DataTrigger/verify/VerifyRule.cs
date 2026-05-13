using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;

namespace FMO.Trigger;





/// <summary>
/// 这里定义了一系列用来监控数据是否异常的类
/// Tips 显示在首页中
/// 如果需要交互，应该用Trigger
/// 
/// 注册 Start
/// 取消 Stop
/// </summary>
public abstract class VerifyRule : DataObserver, IVerifyRule
{
    protected Debouncer debouncer;

    protected VerifyRule()
    {
        debouncer = new(Verify, 1000);
    }


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
}




