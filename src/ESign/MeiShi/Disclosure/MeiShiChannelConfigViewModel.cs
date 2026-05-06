using FMO.Disclosure;
using FMO.Models;


namespace FMO.ESigning.MeiShi;

/// <summary>
/// 美市通道配置
/// </summary>
[AutoViewModel(typeof(MeiShiChannelConfig))]
public partial class MeiShiChannelConfigViewModel : ChannelConfigViewModel
{
    public override string ChannelCode => DisclosureChannelCode.MeiShi;

    protected override DisclosureChannelConfig BuildOverride() => Build();

    protected override async Task<bool> VerifyOverride()
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

        if (!failed)
        {
            MeiShiAssit assit = new();
            var r = await assit.LoginFromDisclosure();
            if (!r.Successed)
            {
                Error = r.Error;
                failed = true;
            }
        }

        Error = Error?.Trim();
        return !failed;
    }
}
