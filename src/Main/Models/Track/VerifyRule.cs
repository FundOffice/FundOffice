namespace FMO.Models;





/// <summary>
/// 这里定义了一系列用来监控数据是否异常的类
/// Tips 显示在首页中
/// 如果需要交互，应该用Trigger
/// 
/// 注册 Start
/// 取消 Stop
/// </summary>
public interface IVerifyRule : ISettingFunction
{

    public void Init();

    public void Verify();


    //protected void Send(IDataTip tip);

    //protected void Revoke(long tipId);
}




