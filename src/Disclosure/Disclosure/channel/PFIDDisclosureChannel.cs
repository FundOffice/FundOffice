using FMO.AMAC.Direct;
using FMO.Models;
using FMO.Utilities;

namespace FMO.Disclosure;

public class PFIDDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Pfid;


    public string Name => "信批备份系统";

    public string Description => "在中基协信批系统发布信批公告";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => null;

    public async Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config)
    {
        using var db = DbHelper.Base();
        var cc = db.GetCollection<IDisclosureChannelConfig>().FindById(Code) as PfidChannelConfig;
        if (cc is null) return new(false, "配置不正确");


        switch (Notice)
        {
            case PeriodicalDisclosureNotice n:
                return await AmacDirectReporter.DislosurePeriodical(n, new AmacReportAccount("", cc.UserName, cc.Password, cc.Secret, true));
            default:
                return new(false, $"不支持的公告类型{Notice.Type}");
        }
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

    public bool RequireConfigWork(DisclosureType type) => false;

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice)
    {
        throw new NotImplementedException();
    }
}