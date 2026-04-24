using LiteDB;

namespace FMO.Disclosure;

/// <summary>
/// 通道配置基类
/// </summary>
public interface IDisclosureChannelConfig
{

    string ChannelCode { get; }

    /// <summary>
    /// 是否可用
    /// </summary>
    bool IsAvailable { get; set; }
}


public abstract class DisclosureChannelConfig : IDisclosureChannelConfig
{
    [BsonId]
    public abstract string ChannelCode { get; }

    public bool IsAvailable { get; set; }
}