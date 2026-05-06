using FMO.Models;

namespace FMO.Disclosure;

/// <summary>
/// 信批通道
/// </summary>
public interface IDisclosureChannel
{
    string Code { get; }

    string Name { get; }

    string Description { get; }

    /// <summary>
    /// 是否支持此类公告类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool IsSupported(DisclosureType type);

    bool IsWorkflowSealed(DisclosureType type) => false;


    /// <summary>
    /// 生成默认的flow
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    DisclosureWorkflow? BuildWorkflow(DisclosureType type) => IsSupported(type) ? new DisclosureWorkflow { Channel = Code, Type = type } : null;




    /// <summary>
    /// 生成默认的工作参数，若不需要配置工作参数，则返回null
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    IWorkConfig? DefaultWorkConfig(DisclosureType type);

    /// <summary>
    /// 是否需要配置工作参数，若需要，则必须提供工作参数才能进行公告披露
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool RequireConfigWork(DisclosureType type);


    public ErrorReturn VerifyNotice(IDisclosureNotice Notice);

    public Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config);

}

