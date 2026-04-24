namespace FMO.Disclosure;

/// <summary>
/// PFID通道配置
/// </summary>
public class PfidChannelConfig : DisclosureChannelConfig
{
    public override string ChannelCode => DisclosureChannelCode.Pfid;
     


    public required string UserName { get; set; }

    public required string Password { get; set; }

    public required string Secret { get; set; }
}
