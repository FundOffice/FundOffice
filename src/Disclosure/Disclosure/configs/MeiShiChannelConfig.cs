namespace FMO.Disclosure;

/// <summary>
/// 美市通道配置
/// </summary>
public class MeiShiChannelConfig : DisclosureChannelConfig
{
    public override string ChannelCode => DisclosureChannelCode.MeiShi;




    /// <summary>
    /// 用户名
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// 是否发送通知
    /// </summary>
    //public bool Notify { get; set; } = true;

    /// <summary>
    /// 是否用印
    /// </summary>
    //public bool Seal { get; set; } = false;
}
