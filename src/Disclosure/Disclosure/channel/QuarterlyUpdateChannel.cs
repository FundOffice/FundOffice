using FMO.Models;

namespace FMO.Disclosure;

public class QuarterlyUpdateChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.QuarterlyUpdate;

    public string Name => "季度更新";

    public string Description => "季度更新";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => null;

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config)
    {
        throw new NotImplementedException();
    }

    public bool IsSupported(DisclosureType type) => type == DisclosureType.QuarterlyUpdate;

    public bool RequireConfigWork(DisclosureType type) => false;

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice)
    {
        return Notice is QuarterlyUpdate qu && (qu.Investor?.File?.Exists ?? false) && (qu.Operation?.File?.Exists ?? false)
            ? new ErrorReturn(true, null) : new ErrorReturn(false, "季度更新必须包含投资者关系材料和运营材料");
    }
}
