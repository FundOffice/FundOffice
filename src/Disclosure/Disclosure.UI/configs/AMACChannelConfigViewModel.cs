using FMO.Models;

namespace FMO.Disclosure;

/// <summary>
/// 基金业协会通道配置
/// </summary>
[AutoViewModel(typeof(AMACChannelConfig))]
public partial class AMACChannelConfigViewModel : ChannelConfigViewModel
{
    public override string ChannelCode => DisclosureChannelCode.AMAC;

    protected override DisclosureChannelConfig BuildOverride()
    {
        return Build();
    }

    protected override bool VerifyOverride()
    {
        throw new NotImplementedException();
    }
}


