using FMO.Models;

namespace FMO.Disclosure;

internal class PFIDDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Pfid;


    public string Name => "中基协PFID系统";

    public string Description => "在中基协信批系统发布信批公告";

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config)
    {
        throw new NotImplementedException();
    }

    public bool IsSupported(DisclosureType type)
    {
      return type switch
      {
          DisclosureType.Monthly => true,
          DisclosureType.Quarterly => true,
          DisclosureType.SemiAnnually => true,
          DisclosureType.Annually => true,
          _ => false
      };
    }

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice)
    {
        throw new NotImplementedException();
    }
}