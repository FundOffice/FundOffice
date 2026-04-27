using FMO.Utilities;

namespace FMO.Disclosure;



public static class DisclosureChannelManager
{
    private static readonly Dictionary<string, Func<ChannelConfigViewModel?>> _codeCreators = new();

    private static readonly Dictionary<Type, Func<IDisclosureChannelConfig, ChannelConfigViewModel?>> _typeCreators = new();


    public static void Initialize()
    {
        // 注册通道实例
        Register<QuarterlyUpdateChannelConfig>(new QuarterlyUpdateChannel(), () => new QuarterlyUpdateChannelConfigViewModel(), (x) => new QuarterlyUpdateChannelConfigViewModel(x));

        Register<EmailChannelConfig>(new EmailDisclosureChannel(), () => new EmailChannelConfigViewModel(), (x) => new EmailChannelConfigViewModel(x));
        Register<PfidChannelConfig>(new PFIDDisclosureChannel(), () => new PfidChannelConfigViewModel(), (x) => new PfidChannelConfigViewModel(x));
        Register<MeiShiChannelConfig>(new MeiShiDisclosureChannel(), () => new MeiShiChannelConfigViewModel(), (x) => new MeiShiChannelConfigViewModel(x));
        
        // 创建季度更新通道
        //RegisterQuartlyUpdateChannel();

        using var db = DbHelper.Base();
        var ins = db.GetCollection<DisclosureInstance>().Find(x => x.Status == DisclosureStatus.Waiting || x.Status == DisclosureStatus.Processing).ToArray();
        foreach (var item in ins)
            DisclosureService.AddToQueue(item);
        DisclosureService.StartWorker();
    }


    public static bool Register<T>(IDisclosureChannel channel, Func<ChannelConfigViewModel?> creator, Func<T, ChannelConfigViewModel?> creator2) where T : IDisclosureChannelConfig
    {
        if (string.IsNullOrWhiteSpace(channel.Code) || creator == null) return false;

        DisclosureService._channels[channel.Code] = channel;
        _codeCreators[channel.Code] = creator;
        _typeCreators[typeof(T)] = config => creator2((T)config);
        DisclosureService.InitWorkflows(channel);
        return true;
    }
    private static void RegisterQuartlyUpdateChannel()
    {
        QuarterlyUpdateChannel quarterlyUpdateChannel = new();
        DisclosureService._channels[DisclosureChannelCode.QuarterlyUpdate] = quarterlyUpdateChannel;
        DisclosureService.InitWorkflows(quarterlyUpdateChannel);
    }




    // 原方法 1：按 Code 创建（不变）
    public static ChannelConfigViewModel? CreateViewModel(string channelCode)
    {
        return _codeCreators.TryGetValue(channelCode, out var func) ? func() : null;
    }

    // 原方法 2：按实体创建（不变）
    public static ChannelConfigViewModel? CreateViewModel(IDisclosureChannelConfig config)
    {
        if (config is null) return null;
        var type = config.GetType();
        return _typeCreators.TryGetValue(type, out var func) ? func(config) : null;
    }
}

/// <summary>
/// 统一通道管理器：包含 通道实例注册 + 配置ViewModel创建
/// 支持插件化、动态注册、无修改扩展
/// </summary>
 