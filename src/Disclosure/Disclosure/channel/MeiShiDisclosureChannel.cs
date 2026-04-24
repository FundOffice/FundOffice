using FMO.Models;

namespace FMO.Disclosure;

internal class MeiShiDisclosureChannel : IDisclosureChannel
{
    public string Code =>  DisclosureChannelCode.MeiShi;


    public string Name => "易私募";

    public string Description => "在易私募平台发布信批公告";

     

    public IWorkConfig? Build(DisclosureType disclosureType)
    {
        switch (disclosureType)
        {
            case DisclosureType.Monthly: 
            case DisclosureType.Quarterly: 
            case DisclosureType.SemiAnnually: 
            case DisclosureType.Annually:
                return new MeiShiWorkConfig(); 
            default:
                return null;
        }
    }

    ErrorReturn IDisclosureChannel.VerifyNotice(IDisclosureNotice Notice)
    {
        throw new NotImplementedException();
    }

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config)
    {
        throw new NotImplementedException();
    }

    public bool IsSupported(DisclosureType type)
    {
        return true;
    }
}

internal class MeiShiWorkConfig : IWorkConfig
{
    /// <summary>
    /// 通知
    /// </summary>
    public bool Notify { get; set; }

    /// <summary>
    /// 用印
    /// </summary>
    public bool Seal { get; set; }

}