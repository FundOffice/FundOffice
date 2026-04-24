using FMO.Models;

namespace FMO.Disclosure;

/// <summary>
/// 信批通道
/// </summary>
public interface IDisclosureChannel
{
    public string Code { get; }

    string Name { get; }

    string Description { get; }


    bool IsSupported(DisclosureType type);

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice);

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig config);
 
}

