using FMO.Models;

namespace FMO.Disclosure;


public class EmailDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Email;

    public string Name => "邮件";

    public string Description=> "通过邮件发送信批公告";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => null;

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config)
    {
        throw new NotImplementedException();
    }

    public bool IsSupported(DisclosureType type)
    {
        return true;
    }

    public bool RequireConfigWork(DisclosureType type) => false;

    ErrorReturn IDisclosureChannel.VerifyNotice(IDisclosureNotice Notice)
    {
        throw new NotImplementedException();
    }
}