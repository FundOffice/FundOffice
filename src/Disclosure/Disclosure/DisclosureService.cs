using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;

namespace FMO.Disclosure;


public static partial class DisclosureService
{
    /// <summary>
    /// 待执行实例队列（内存）
    /// </summary>

    private static List<DisclosureInstance> _instanceList = [];


    private static SemaphoreSlim _semaphore = new(1);

    internal static readonly Dictionary<string, IDisclosureChannel> _channels = new();


    private static DateTime _lastFullRunTime = DateTime.MinValue;

    private static readonly Dictionary<string, DisclosureWorkflow> _workflows;

    public static DisclosureType[] DisclosureTypes { get; } = Enum.GetValues<DisclosureType>().Except([DisclosureType.Temporary, DisclosureType.ManagerLevel]).ToArray();

    /// <summary>
    /// 静态构造：初始化默认通道
    /// </summary>
    static DisclosureService()
    {
        using var db = DbHelper.Base();
        _workflows = db.GetCollection<DisclosureWorkflow>().Find(x => !string.IsNullOrWhiteSpace(x.Channel)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);

        DataTracker.Hook(RegisterNotice);
    }




    /// <summary>
    /// 初始化/更新工作流配置：根据通道支持的报告类型，自动创建对应的工作流配置（仅创建，不启用）
    /// </summary>
    /// <param name="channel"></param>
    internal static void InitWorkflows(IDisclosureChannel channel)
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
                var flow = channel.BuildWorkflow(type)!;
                _workflows[id] = flow;
                _toUpdate.Add(flow);
            }

            // 如果是PFIDDisclosureChannel，确保启用
            if (channel is PFIDDisclosureChannel && type <= DisclosureType.Annually && _workflows[id] is DisclosureWorkflow fl && (!fl.IsEnabled || !fl.ForAllFunds))
            {
                fl.IsEnabled = true;
                fl.ForAllFunds = true;
                _toUpdate.Add(fl);
            }
        }
        if (_toUpdate.Count > 0)
        {
            using var db = DbHelper.Base();
            var col = db.GetCollection<DisclosureWorkflow>().Upsert(_toUpdate);
        }
    }




    #region 通道实例管理（原 Galley 功能）
    public static bool Unregister(string channel) => _channels.Remove(channel);

    public static IEnumerable<IDisclosureChannel> GetRegisteredChannels() => _channels.Values.OrderBy(x => x switch { QuarterlyUpdateChannel => 0, EmailDisclosureChannel => 1, PFIDDisclosureChannel => 2, _ => x.Code.GetHashCode() });



    public static IDisclosureChannel? GetChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? null : _channels.TryGetValue(channel, out var instance) ? instance : null;
    #endregion





    public static DisclosureWorkflow[] GetWorkflows() => _workflows.Values.ToArray();

    internal static void UpdateWorkflow(DisclosureWorkflow obj)
    {
        _workflows[obj.Id] = obj;

        // 持久化到数据库
        using var db = DbHelper.Base();
        db.GetCollection<DisclosureWorkflow>().Upsert(obj);
    }
    public static IEnumerable<DisclosureWorkflow> GetApplicableWorkflows(IDisclosureNotice report)
    {

        if (report.Type > DisclosureType.ManagerLevel)
            return _workflows.Values.Where(x => x.IsEnabled && x.Type == report.Type).ToArray();
        else if (report is IFundDisclosureNotice r)
            return _workflows.Values.Where(x => x.IsEnabled && x.Type == report.Type).Where(w => w.ForAllFunds || w.TargetFunds.Contains(r.FundId)).ToArray();
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
            AutoRun = true
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
        return gen;
    }



    //public static ErrorReturn AddToQueue(DisclosureInstance instance)
    //{
    //    _semaphore.Wait();
    //    var channel = GetChannel(instance.Channel);
    //    if (channel is null)
    //        return new ErrorReturn(false, $"未找到通道：{instance.Channel}");

    //    if (!_workflows.ContainsKey(instance.WorkflowId))
    //        return new ErrorReturn(false, $"未找到Workflow：{instance.WorkflowId}");

    //    if (!_instanceQueue.ContainsKey(instance.Channel))
    //        _instanceQueue[instance.Channel] = [];
    //    _instanceQueue[instance.Channel].Add(instance);
    //    _semaphore.Release();
    //    return new ErrorReturn(true);
    //}

    public static ErrorReturn AddToQueue(params DisclosureInstance[] list)
    {
        _semaphore.Wait();

        _instanceList.AddRange(list);

        _semaphore.Release();

        Task.Run(() =>
        {
            foreach (var item in list)
            {
                try
                {
                    item.Status = DisclosureStatus.Waiting;
                    WeakReferenceMessenger.Default.Send(item); 
                }
                catch (Exception ex)
                {
                    LogEx.Error(ex, $"[信批 Queue {item.Channel} {item.NoticeId} ] Background task failed");
                }
            }
        });
        return new ErrorReturn(true);
    }

    public static ErrorReturn RemoveFromQueue(string instance)
    {
        _semaphore.Wait();

        for (int i = _instanceList.Count - 1; i >= 0; i--)
        {
            if (_instanceList[i].Id == instance)
                _instanceList.RemoveAt(i);
        }

        _semaphore.Release();
        return new(true);
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
            IDisclosureNotice? notice;
            IWorkConfig? config;
            instance.LastRunTime = DateTime.Now;
            WeakReferenceMessenger.Default.Send(new DisclosureRunMessage(instance.Id, "正在处理中..."));


            // 2. 加载业务数据（短连接）
            using (var db = DbHelper.Base())
            {
                notice = db.GetCollection<IDisclosureNotice>().FindById(instance.NoticeId);
                if (notice is null)
                {
                    instance.AutoRun = false;
                    instance.Error = "信批报告不存在";
                    instance.Status = DisclosureStatus.Failed;
                    WeakReferenceMessenger.Default.Send(instance);
                    return new(false, instance.Error);
                }

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
            instance.AutoRun = instance.FailedTimes < 5 && !workResult.Successed;

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
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            while (await timer.WaitForNextTickAsync())
                LoopOnce();

        });

        // 每工作日的8-18点，每小时把所有instance加入队列
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(600));

            while (await timer.WaitForNextTickAsync())
            {
                DateTime now = DateTime.Now;
                if (!Days.IsTradingDay(now))
                    continue;

                if (now.Hour is < 8 or > 18 || now.Minute > 10) continue;

                // 每小时一次，只在x:10分内执行，过时下一小时再跑
                if (_lastFullRunTime == default || now.Hour != _lastFullRunTime.Hour)
                {
                    _lastFullRunTime = now;

                    try
                    {
                        using var db = DbHelper.Base();
                        var list = db.GetCollection<DisclosureInstance>().Find(x => x.AutoRun).ToArray();
                        AddToQueue(list);
                    }
                    catch (Exception ex) { LogEx.Error(ex); }
                }
            }
        });
    }


    private static void LoopOnce()
    {
        foreach (var dic in _instanceList.GroupBy(x => x.Channel).ToArray())
        {
            try
            {
                _ = Task.Run(() => HandleRun(dic.AsEnumerable()));
            }
            catch (Exception ex)
            {
                LogEx.Error($"[{dic.Key}] 处理队列异常：{ex}");
            }
        }
    }




    private static async Task HandleRun(IEnumerable<DisclosureInstance> disclosureInstances)
    {
        foreach (var instance in disclosureInstances)
        {
            try
            {
                // 移出队列，防止任务时间过长，未执行完又再次执行
                _semaphore.Wait();
                _instanceList.Remove(instance);
                _semaphore.Release();

                await ExecuteDisclosureAsync(instance);

            }
            catch (Exception ex)
            {
                LogEx.Error(ex);
            }
        }
    }




    /// <summary>
    /// 注册新报告
    /// </summary>
    /// <param name="notice"></param>
    public static void RegisterNotice(IDisclosureNotice notice)
    {
        using var db = DbHelper.Base();
        var exist = db.GetCollection<IDisclosureNotice>().FindById(notice.Id);
        if (exist != null)
        {
            db.GetCollection<IDisclosureNotice>().Update(notice);
            LogEx.Warning($"报告已存在，ID={notice.Id}，仅更新，不再注册");
            return;
        }
        db.GetCollection<IDisclosureNotice>().Insert(notice);

        CreateInstance(notice);
    }

    public static void RemoveNotice(long id)
    {
        using var db = DbHelper.Base();
        db.GetCollection<IDisclosureNotice>().Delete(id);
        db.GetCollection<DisclosureInstance>().DeleteMany(x => x.NoticeId == id);
    }
}

