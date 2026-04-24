using FMO.Models;

namespace FMO.Disclosure;

public class MeiShiDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.MeiShi;


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

    public IWorkConfig? DefaultWorkConfig(DisclosureType type)
    {
        throw new NotImplementedException();
    }

    public bool RequireConfigWork(DisclosureType type)
    {
        return type switch
        {
            DisclosureType.TemporaryOpen => true,
            DisclosureType.HugeRedemption => true,
            DisclosureType.FundSetup => true,
            DisclosureType.OtherFundNotice => true,
            DisclosureType.ManagerLevel => true,
            DisclosureType.MangerChange => true,
            DisclosureType.OfficeAddressChange => true,
            DisclosureType.OtherManagerNotice => true,
            _ => false
        };
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