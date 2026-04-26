using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;

namespace FMO.Disclosure;


public static partial class DisclosureService
{
    private static Dictionary<string, Queue<DisclosureInstance>> _instanceQueue = [];

    private static Dictionary<string, Thread> _workThreads = [];

    public static IEnumerable<DisclosureWorkflow> GetApplicableWorkflows(IDisclosureNotice report)
    {
        using var db = DbHelper.Base();
        if (report.Type > DisclosureType.ManagerLevel)
            return db.GetCollection<DisclosureWorkflow>().Find(x => x.IsEnabled && x.Type == report.Type).ToArray();
        else if (report is IFundDisclosureNotice r)
            return db.GetCollection<DisclosureWorkflow>().Query().Where(x => x.IsEnabled && x.Type == report.Type).Where(w => w.ForAllFunds || w.TargetFunds.Contains(r.FundId)).ToArray();
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
        using var db = DbHelper.Base();
        var old = db.GetCollection<DisclosureInstance>().FindById($"{workflow.Channel}-{report.Id}");
        if (old is not null && old.WorkflowId == workflow.Id)
            return old;

        var instance = new DisclosureInstance
        {
            WorkflowId = workflow.Id,
            NoticeId = report.Id,
            Channel = workflow.Channel,
            FundId = report is IFundDisclosureNotice f ? f.FundId : 0,
            Type = report.Type,
        };

        db.GetCollection<DisclosureInstance>().Upsert(instance);
        return instance;
    }

    public static DisclosureInstance[] CreateInstance(IDisclosureNotice report)
    {
        using var db = DbHelper.Base();
        var exist = db.GetCollection<DisclosureInstance>().Find(x => x.NoticeId == report.Id).ToList();

        DisclosureInstance[] gen;

        var workflows = GetApplicableWorkflows(report);
        gen = workflows.ExceptBy(exist.Select(x => x.WorkflowId), x => x.Id).Select(w => CreateInstance(w, report)).ToArray();

        db.GetCollection<DisclosureInstance>().InsertBulk(gen);
        return gen;
    }



    public static ErrorReturn AddToQueue(DisclosureInstance instance)
    {
        var channel = GetChannel(instance.Channel);
        if (channel is null)
            return new ErrorReturn(false, $"未找到通道：{instance.Channel}");

        if (!_workflows.ContainsKey(instance.WorkflowId))
            return new ErrorReturn(false, $"未找到Workflow：{instance.WorkflowId}");

        if (!_instanceQueue.ContainsKey(instance.Channel))
            _instanceQueue[instance.Channel] = new Queue<DisclosureInstance>();
        _instanceQueue[instance.Channel].Enqueue(instance);
        return new ErrorReturn(true);
    }

    /// <summary>
    /// 外层：数据库管理层
    /// 职责：加载数据 → 调用业务 → 统一保存结果
    /// </summary>
    private static async Task<ErrorReturn> ExecuteDisclosureAsync(DisclosureInstance instance, CancellationToken cancellationToken = default)
    {
        // 入参校验
        if (instance == null)
            return new ErrorReturn(false, "实例不能为空");

        if (instance.Status == DisclosureStatus.Successed)
            return new ErrorReturn(true, "实例已成功，无需重复执行");


        try
        {
            IDisclosureNotice notice;
            IWorkConfig? config;

            // 2. 加载业务数据（短连接）
            using (var db = DbHelper.Base())
            {
                notice = db.GetCollection<IDisclosureNotice>().FindById(instance.NoticeId);
                config = _workflows[instance.WorkflowId]?.Config;

                if (instance.StartedTime == default)
                {
                    instance.StartedTime = DateTime.Now;
                }
                instance.Status = DisclosureStatus.Processing;
                db.GetCollection<DisclosureInstance>().Update(instance);
                WeakReferenceMessenger.Default.Send(instance);
            }

            // 3. 调用内层纯逻辑（无DB）
            var workResult = await ExecuteDisclosureCoreAsync(
                instance, notice, config, cancellationToken);

            // 4. 【外层统一赋值 + 统一保存】
            instance.Error = workResult.Error;
            instance.Status = workResult.Successed ? DisclosureStatus.Successed : DisclosureStatus.Failed;
            if (!workResult.Successed)
                instance.FailedTimes += 1;
            instance.CompletedTime = DateTime.Now;

            using (var db = DbHelper.Base())
                db.GetCollection<DisclosureInstance>().Upsert(instance);

            WeakReferenceMessenger.Default.Send(instance);
            return workResult;
        }
        catch (Exception ex)
        {
            // 异常也由【外层统一处理、保存】 
            instance.Error = ex is OperationCanceledException ? "任务已取消" : $"执行异常：{ex.Message}";
            instance.Status = DisclosureStatus.Failed;
            instance.CompletedTime = DateTime.Now;

            using (var db = DbHelper.Base())
                db.GetCollection<DisclosureInstance>().Upsert(instance);

            WeakReferenceMessenger.Default.Send(instance);
            return new(false, ex.Message);
        }
    }

    /// <summary>
    /// 内层：纯业务核心（0数据库操作）
    /// 只返回成功/失败信息，完全不碰DB
    /// </summary>
    private static async Task<ErrorReturn> ExecuteDisclosureCoreAsync(
        DisclosureInstance instance,
        IDisclosureNotice notice,
        IWorkConfig? config,
        CancellationToken cancellationToken)
    {
        // 必须校验，失败直接 return 结果，不保存
        if (notice == null)
            return new(false, $"未找到报告：{instance.NoticeId}");

        var channel = DisclosureService.GetChannel(instance.Channel);
        if (channel == null)
            return new(false, $"未找到通道：{instance.Channel}");

        if (channel.RequireConfigWork(notice.Type) && config is null)
            return new(false, $"未找到信批配置：{instance.WorkflowId}");

        try
        {
            var verify = channel.VerifyNotice(notice);
            if (!verify.Successed)
                return new(false, $"验证失败：{verify.Error}");

            // 执行异步披露
            cancellationToken.ThrowIfCancellationRequested();

            return await channel.Disclosure(notice, config).WaitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            return new(false, e.Message);
        }
    }


    public static async Task BatchExecuteAsync(long[] noticeIds, CancellationToken cancellationToken = default)
    {
        using var db = DbHelper.Base();
        var instances = db.GetCollection<DisclosureInstance>().Query().Where(Query.In(nameof(DisclosureInstance.NoticeId), noticeIds.Select(x => new BsonValue(x)))).ToList();

        foreach (var instance in instances)
            AddToQueue(instance);
    }


    public static void StartWorker()
    {
        Task.Factory.StartNew(async () =>
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            while (await timer.WaitForNextTickAsync())
                LoopOnce();

        }, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private static void LoopOnce()
    {
        foreach (var dic in _instanceQueue)
        {
            if (dic.Value.Count == 0) continue;

            try
            {
                _ = Task.Run(() => HandleRun(dic.Value));
            }
            catch (Exception ex)
            {
                LogEx.Error($"[{dic.Key}] 处理队列异常：{ex}");
            }
        }
    }




    private static async Task HandleRun(Queue<DisclosureInstance> disclosureInstances)
    {

        while (disclosureInstances.TryDequeue(out var instance))
        {
            try
            {
                await ExecuteDisclosureAsync(instance);
            }
            catch (Exception ex)
            {
                LogEx.Error(ex);
            }
        }
    }
}

