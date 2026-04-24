using FMO.Utilities;
using LiteDB;
using System.Collections.Concurrent;

namespace FMO.Disclosure;

public static class DisclosureChannelGalley
{
    private static readonly Dictionary<string, IDisclosureChannel> _channels = new();

    public static bool Register(IDisclosureChannel channelInstance)
    {
        // 注册通道实例，可以存储在一个字典中
        // 这里可以使用反射或者工厂模式来实现
        if (_channels.ContainsKey(channelInstance.Code))
            return false; // 已经注册过了 

        _channels[channelInstance.Code] = channelInstance;
        return true;
    }

    public static bool Unregister(string channel) => _channels.Remove(channel);

    public static IEnumerable<IDisclosureChannel> GetRegisteredChannels() => _channels.Values;

    public static bool IsChannelRegistered(string channel) => _channels.ContainsKey(channel);

    public static void Initialize()
    {
        // 初始化默认的通道实例
        Register(new EmailDisclosureChannel());
        Register(new PFIDDisclosureChannel());
        Register(new MeiShiDisclosureChannel());
    }


    public static IDisclosureChannel? GetChannel(string channel) => _channels.TryGetValue(channel, out var channelInstance) ? channelInstance : null;



}


public static class DisclosureWorkflowService
{











    public static IEnumerable<DisclosureWorkflow> GetApplicableWorkflows(IDisclosureNotice report)
    {
        using var db = DbHelper.Base();
        if (report.Type > DisclosureType.ManagerLevel)
            return db.GetCollection<DisclosureWorkflow>().Find(x => x.IsEnabled && x.Type == report.Type).ToList();
        else if (report is IFundDisclosureNotice r)
            return db.GetCollection<DisclosureWorkflow>().Query().Where(x => x.IsEnabled && x.Type == report.Type).Where(w => w.ForAllFunds || w.TargetFunds.Contains(r.FundId)).ToList();
        else return [];
    }


    /// <summary>
    /// 创建信批实例
    /// </summary>
    /// <param name="workflow"></param>
    /// <param name="report"></param>
    /// <returns></returns>
    public static DisclosureInstance CreateInstance(DisclosureWorkflow workflow, IDisclosureNotice report)
    {
        return new DisclosureInstance
        {
            WorkflowId = workflow.Id,
            NoticeId = report.Id,
            Channel = workflow.Channel,
            FundId = report is IFundDisclosureNotice f ? f.FundId : 0,
            Type = report.Type,
        };
    }





    /// <summary>
    /// 外层：数据库管理层
    /// 职责：加载数据 → 调用业务 → 统一保存结果
    /// </summary>
    public static async Task<DisclosureResult> ExecuteDisclosureAsync(DisclosureInstance instance, CancellationToken cancellationToken = default)
    {
        // 入参校验
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        DisclosureResult result;

        // 1. 加载/初始化结果，并标记执行中
        using (var db = DbHelper.Base())
        {
            var col = db.GetCollection<DisclosureResult>();
            result = col.FindById(instance.Id) ?? new DisclosureResult { Id = instance.Id };

            if (result.Status == DisclosureStatus.Published)
                return result;

            // 更新执行状态
            result.StartedTime = DateTime.UtcNow;
            result.Status = DisclosureStatus.Processing;
            col.Upsert(result);
        }

        try
        {
            IDisclosureNotice notice;
            IWorkConfig config;

            // 2. 加载业务数据（短连接）
            using (var db = DbHelper.Base())
            {
                notice = db.GetCollection<IDisclosureNotice>().FindById(instance.NoticeId);
                config = db.GetCollection<DisclosureWorkflow>().FindById(instance.WorkflowId)?.Config!;
            }

            // 3. 调用内层纯逻辑（无DB）
            var workResult = await ExecuteDisclosureCoreAsync(
                instance, notice, config, cancellationToken);

            // 4. 【外层统一赋值 + 统一保存】
            result.Error = workResult.Error;
            result.Status = workResult.Successed ? DisclosureStatus.Published : DisclosureStatus.Failed;
            result.CompletedTime = DateTime.Now;

            using (var db = DbHelper.Base())
            {
                db.GetCollection<DisclosureResult>().Upsert(result);
            }

            return result;
        }
        catch (Exception ex)
        {
            // 异常也由【外层统一处理、保存】 
            result.Error = ex is OperationCanceledException ? "任务已取消" : $"执行异常：{ex.Message}";
            result.Status = DisclosureStatus.Failed;
            result.CompletedTime = DateTime.Now;

            using (var db = DbHelper.Base())
            {
                db.GetCollection<DisclosureResult>().Upsert(result);
            }

            return result;
        }
    }

    /// <summary>
    /// 内层：纯业务核心（0数据库操作）
    /// 只返回成功/失败信息，完全不碰DB
    /// </summary>
    private static async Task<(bool Successed, string? Error)> ExecuteDisclosureCoreAsync(
        DisclosureInstance instance,
        IDisclosureNotice notice,
        IWorkConfig config,
        CancellationToken cancellationToken)
    {
        // 必须校验，失败直接 return 结果，不保存
        if (notice == null)
            return (false, $"未找到报告：{instance.NoticeId}");

        if (config == null)
            return (false, $"未找到信批配置：{instance.WorkflowId}");

        var channel = DisclosureChannelGalley.GetChannel(instance.Channel);
        if (channel == null)
            return (false, $"未找到通道：{instance.Channel}");

        var verify = channel.VerifyNotice(notice);
        if (!verify.Successed)
            return (false, $"验证失败：{verify.Error}");

        // 执行异步披露
        cancellationToken.ThrowIfCancellationRequested();
        var result = await channel.Disclosure(notice, config).WaitAsync(cancellationToken);

        return (result.Successed, result.Error);
    }


    public static async Task BatchExecuteAsync(long[] noticeIds, CancellationToken cancellationToken = default)
    {
        using var db = DbHelper.Base();
        var notice = db.GetCollection<DisclosureInstance>().Query().Where(Query.In(nameof(DisclosureInstance.NoticeId), noticeIds.Select(x => new BsonValue(x)))).ToList();

        ConcurrentDictionary<string, DisclosureResult> results = [];

        // 按照通道分组，理论上不同通道之间可以并行执行
        Parallel.ForEach(notice.GroupBy(x => x.Channel), async item =>
        {
            foreach (var instance in item)
                results[instance.Id] = await ExecuteDisclosureAsync(instance, cancellationToken);
        });



    }

}

internal record DisclosureInstanceLog(string Level, string Message);
