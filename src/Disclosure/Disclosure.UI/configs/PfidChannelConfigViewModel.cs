using FMO.Models;

namespace FMO.Disclosure;

/// <summary>
/// PFID通道配置
/// </summary>
[AutoViewModel(typeof(PfidChannelConfig))]
public partial class PfidChannelConfigViewModel : ChannelConfigViewModel
{
    public override string ChannelCode => DisclosureChannelCode.Pfid;

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

        if (Secret?.Length < 10)
        {
            Error += "密钥不对\n";
            failed = true;
        }






        return !failed;
    }
}
