using FMO.Models;

namespace FMO.Disclosure;

/// <summary>
/// 美市通道配置
/// </summary>
[AutoViewModel(typeof(MeiShiChannelConfig))]
public partial class MeiShiChannelConfigViewModel : ChannelConfigViewModel
{
    public override string ChannelCode => DisclosureChannelCode.MeiShi;

    protected override DisclosureChannelConfig BuildOverride() => Build();

    protected override bool VerifyOverride()
    {
        bool failed = false;
        if (UserName?.Length < 4)
        {
            Error += "用户名不合法\n";
            failed = true;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            Error += "密码不能为空\n";
            failed = true;
        }

        Error = Error?.Trim();
        return !failed;
    }
}
