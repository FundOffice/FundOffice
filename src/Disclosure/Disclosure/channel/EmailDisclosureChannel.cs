using FMO.Models;

namespace FMO.Disclosure;


public class EmailDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Email;

    public string Name => "邮件信批";

    public string Description=> "通过邮件发送信批公告";
     

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config)
    {
        throw new NotImplementedException();
    }

    public bool IsSupported(DisclosureType type)
    {
        return true;
    }

    ErrorReturn IDisclosureChannel.VerifyNotice(IDisclosureNotice Notice)
    {
        throw new NotImplementedException();
    }
}