namespace FMO.Disclosure;

public class QuarterlyUpdateChannelConfig : DisclosureChannelConfig
{
    public override string ChannelCode => DisclosureChannelCode.QuarterlyUpdate;



    public required string UserName { get; set; }

    public required string Password { get; set; }

    public required string Secret { get; set; }
}
