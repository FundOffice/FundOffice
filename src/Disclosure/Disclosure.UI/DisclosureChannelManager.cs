using FMO.Utilities;

namespace FMO.Disclosure;



/// <summary>
/// 统一通道管理器：包含 通道实例注册 + 配置ViewModel创建
/// 支持插件化、动态注册、无修改扩展
/// </summary>
public static class DisclosureChannelManager
{
    // 1. 已注册的通道实例（来自 DisclosureChannelGalley）
    private static readonly Dictionary<string, IDisclosureChannel> _channels = new();

    // 2. 通道Code → 创建ViewModel的委托（来自 ChannelConfigFactory）
    private static readonly Dictionary<string, Func<ChannelConfigViewModel?>> _codeCreators = new();

    // 3. 配置实体类型 → 创建ViewModel的委托（来自 ChannelConfigFactory）
    private static readonly Dictionary<Type, Func<IDisclosureChannelConfig, ChannelConfigViewModel?>> _typeCreators = new();


    private static readonly Dictionary<string, Func<IDisclosureChannelConfig?>> _configCreators = new();

    private static readonly Dictionary<string, DisclosureWorkflow> _workflows;

    public static DisclosureType[] DisclosureTypes { get; } = Enum.GetValues<DisclosureType>().Except([DisclosureType.Temporary, DisclosureType.ManagerLevel]).ToArray();

    /// <summary>
    /// 静态构造：初始化默认通道
    /// </summary>
    static DisclosureChannelManager()
    {
        using var db = DbHelper.Base();
        _workflows = db.GetCollection<DisclosureWorkflow>().FindAll().ToDictionary(x => x.Id);
    }

    #region 初始化（原有默认通道）
    public static void Initialize()
    {


        // 注册通道实例
        Register<EmailChannelConfig>(new EmailDisclosureChannel(), () => new EmailChannelConfigViewModel(), (x) => new EmailChannelConfigViewModel(x));
        Register<PfidChannelConfig>(new PFIDDisclosureChannel(), () => new PfidChannelConfigViewModel(), (x) => new PfidChannelConfigViewModel(x));
        Register<MeiShiChannelConfig>(new MeiShiDisclosureChannel(), () => new MeiShiChannelConfigViewModel(), (x) => new MeiShiChannelConfigViewModel(x));

        // 创建季度更新通道
        RegisterQuartlyUpdateChannel();
    }

    private static void RegisterQuartlyUpdateChannel()
    {
        QuarterlyUpdateChannel quarterlyUpdateChannel = new();
        _channels[DisclosureChannelCode.QuarterlyUpdate] = quarterlyUpdateChannel;
        InitWorkflows(quarterlyUpdateChannel);
    }
    #endregion



    private static void InitWorkflows(IDisclosureChannel channel)
    {
        // 季度更新通道特殊处理：仅创建一个季度更新类型的工作流，并确保其始终启用
        if (channel is QuarterlyUpdateChannel)
        {
            var type = DisclosureType.QuarterlyUpdate;
            var id = DisclosureWorkflow.GetId(channel.Code, type);
            if (!_workflows.ContainsKey(id))
            {
                var flow = new DisclosureWorkflow { Channel = channel.Code, Type = type, ForAllFunds = true, IsEnabled = true }; 
                _workflows[id] = flow;
                using var db = DbHelper.Base();
                db.GetCollection<DisclosureWorkflow>().Insert(flow);
            }
            else
                _workflows[id].IsEnabled = true; // 确保季度更新通道的工作流始终启用

            return;
        }

        List<DisclosureWorkflow> _toUpdate = [];
        foreach (var type in DisclosureTypes)
        {
            if (!channel.IsSupported(type))
                continue;

            var id = DisclosureWorkflow.GetId(channel.Code, type);
            if (!_workflows.ContainsKey(id))
            {
                var flow = new DisclosureWorkflow { Channel = channel.Code, Type = type };
                _workflows[id] = flow;
                _toUpdate.Add(flow);
            }
        }
        if (_toUpdate.Count > 0)
        {
            using var db = DbHelper.Base();
            var col = db.GetCollection<DisclosureWorkflow>().InsertBulk(_toUpdate);
        }
    }



    public static bool Register<T>(IDisclosureChannel channel, Func<ChannelConfigViewModel?> creator, Func<T, ChannelConfigViewModel?> creator2) where T : IDisclosureChannelConfig
    {
        if (string.IsNullOrWhiteSpace(channel.Code) || creator == null) return false;

        _channels[channel.Code] = channel;
        _codeCreators[channel.Code] = creator;
        _typeCreators[typeof(T)] = config => creator2((T)config);
        InitWorkflows(channel);
        return true;
    }

    #region 通道实例管理（原 Galley 功能）


    public static bool Unregister(string channel) => _channels.Remove(channel);

    public static IEnumerable<IDisclosureChannel> GetRegisteredChannels() => _channels.Values;

    public static bool IsChannelRegistered(string channel) => _channels.ContainsKey(channel);

    public static IDisclosureChannel? GetChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? null : _channels.TryGetValue(channel, out var instance) ? instance : null;
    #endregion


    /// <summary>
    /// 根据Code创建ViewModel
    /// </summary>
    public static ChannelConfigViewModel? CreateConfig(string channelCode)
    {
        return _codeCreators.TryGetValue(channelCode, out var func) ? func() : null;
    }

    /// <summary>
    /// 根据实体Config 
    /// </summary>
    public static IDisclosureChannelConfig? CreateConfig(IDisclosureChannel channel)
    {
        if (channel?.Code is null) return null;

        return _configCreators.TryGetValue(channel.Code, out var func) ? func() : null;
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

    public static DisclosureWorkflow[] GetWorkflows() => _workflows.Values.ToArray();

    internal static void UpdateWorkflow(DisclosureWorkflow obj)
    {
        _workflows[obj.Id] = obj;

        // 持久化到数据库
        using var db = DbHelper.Base();
        db.GetCollection<DisclosureWorkflow>().Upsert(obj);
    }
}