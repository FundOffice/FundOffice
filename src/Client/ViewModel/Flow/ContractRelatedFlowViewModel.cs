using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FMO.AI;
using FMO.Models;
using FMO.PDF;
using FMO.Shared;
using FMO.Utilities;
using MoT;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using Utilities;

namespace FMO;



public abstract partial class ContractRelatedFlowViewModel : FlowViewModel, IElementChangable//, IFileSetter
{
    /// <summary>
    /// 定稿合同
    /// </summary>
    //[ObservableProperty]
    //    public partial FlowSimpleFile? Contract { get; set; }


    public SimpleFileViewModel Contract { get; }

    /// <summary>
    /// 募集账户函
    /// </summary>
    public SimpleFileViewModel CollectionAccount { get; set; }


    /// <summary>
    /// 托管账户函
    /// </summary>
    public SimpleFileViewModel CustodyAccount { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<ShareClassViewModel> Shares { get; set; }


    [ObservableProperty]
    public partial bool IsDividingShare { get; set; }


    public SimpleFileViewModel RiskDisclosureDocument { get; set; }

    /// <summary>
    /// 份额分类
    /// </summary>
    [ObservableProperty]
    public partial bool ModifyShareClass { get; set; }


    /// <summary>
    /// 正在 AI 解析合同要素
    /// </summary>
    [ObservableProperty]
    public partial bool IsParsingContract { get; set; }

    /// <summary>
    /// AI 解析进度状态文字
    /// </summary>
    [ObservableProperty]
    public partial string ParseStatus { get; set; } = "";

    /// <summary>
    /// AI 解析已接收的 token 数
    /// </summary>
    [ObservableProperty]
    public partial int ParsedTokenCount { get; set; }


    /// <summary>
    /// 份额类型有变动
    /// </summary>
    //private bool _shareChanged;


    [SetsRequiredMembers]
#pragma warning disable CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
    public ContractRelatedFlowViewModel(ContractFlow flow) : base(flow)
#pragma warning restore CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
    {
        //Contract = new(FundId, FlowId, "合同定稿", flow.ContractFile?.Path, "Contract", nameof(ContractFlow.ContractFile));

        Contract = new(flow.ContractFile) { Label = "合同定稿", Filter = "文本|*.docx;*.doc;*.pdf" };
        Contract.FileChanged += f => SaveFileChanged(new { Contract = f });

        RiskDisclosureDocument = new(flow.RiskDisclosureDocument) { Label = "风险揭示书", Filter = "文本|*.docx;*.doc;*.pdf" };
        RiskDisclosureDocument.FileChanged += f => SaveFileChanged(new { RiskDisclosureDocument = f });

        CollectionAccount = new(flow.CollectionAccountFile) { Label = "募集账户函", Filter = "文本|*.docx;*.doc;*.pdf" };
        CollectionAccount.FileChanged += f => SaveFileChanged(new { CollectionAccount = f });

        CustodyAccount = new(flow.CustodyAccountFile) { Label = "托管账户函", Filter = "文本|*.docx;*.doc;*.pdf" };
        CustodyAccount.FileChanged += f => SaveFileChanged(new { CustodyAccount = f });



        //Contract = new()
        //{
        //    Label = "合同定稿",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Contract"),
        //    GetProperty = x => x switch { ContractFlow f => f.ContractFile, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractFlow f) f.ContractFile = y; },
        //    Filter = "文本|*.docx;*.doc;*.pdf"
        //};
        //Contract.Init(flow);

        //RiskDisclosureDocument = new()
        //{
        //    Label = "风险揭示书",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Contract"),
        //    GetProperty = x => x switch { ContractFlow f => f.RiskDisclosureDocument, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractFlow f) f.RiskDisclosureDocument = y; },
        //    Filter = "文本|*.docx;*.doc;*.pdf"
        //};
        //RiskDisclosureDocument.Init(flow);

        //CollectionAccount = new()
        //{
        //    Label = "募集账户函",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Account"),
        //    GetProperty = x => x switch { ContractFlow f => f.CollectionAccountFile, _ => null },
        //    SetProperty = async (x, y) => { if (x is not ContractFlow f) return; f.CollectionAccountFile = y; await UpdateElement(y?.Path is null ? null : new FileInfo(y.Path), x => x.CollectionAccount, FundAccountType.Collection); },
        //    Filter = "文本|*.docx;*.doc;*.pdf"
        //};
        //CollectionAccount.Init(flow);

        //CustodyAccount = new()
        //{
        //    Label = "托管账户函",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Account"),
        //    GetProperty = x => x switch { ContractFlow f => f.CustodyAccountFile, _ => null },
        //    SetProperty = async (x, y) => { if (x is not ContractFlow f) return; f.CustodyAccountFile = y; await UpdateElement(y?.Path is null ? null : new FileInfo(y.Path), x => x.CustodyAccount, FundAccountType.Custody); },
        //    Filter = "文本|*.docx;*.doc;*.pdf"
        //};
        //CustodyAccount.Init(flow);


    }




    [RelayCommand]
    public async Task ParseContractElements()
    {
        var meta = Contract.Meta;
        if (meta is null)
        {
            Toast.Warning("合同文件不存在");
            return;
        }

        // 1. 加载可用的 AI 提供商（从数据库中筛选配置完整的）
        TokenProvider? provider;
        using (var db = DbHelper.Base())
        {
            provider = db.GetCollection<TokenProvider>().Query().ToEnumerable()
                .Where(x => !string.IsNullOrWhiteSpace(x.Company)
                    && !string.IsNullOrWhiteSpace(x.Url)
                    && !string.IsNullOrWhiteSpace(x.Key)
                    && !string.IsNullOrWhiteSpace(x.Model))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();

            if (provider is null)
            {
                Toast.Warning("没有可用的 AI 提供商，请在平台设置中配置完整的提供商（地址、密钥、模型）");
                return;
            }
        }

        Toast.Info($"正在 AI [{provider.Company}] 解析合同要素...");
        IsParsingContract = true;
        ParsedTokenCount = 0;
        ParseStatus = "发送中...";

        // 2. 并行启动：查找上一个 contract flow 的解析记录（与 AI 解析同时进行）
        var prevInfoTask = Task.Run(() => LoadPreviousContractInfo());
        var progress = new Progress<int>(count =>
        {
            ParsedTokenCount = count;
            ParseStatus = $"接收中... {count} tokens";
        });

        try
        {
            // 3. 强制重新解析（流式接收，实时报告 token 进度）
            var parser = new FundDocxAiParser(provider, provider.Model);
            ParseStatus = "等待响应...";
            var result = await parser.ParseAsync(meta.GetFullPath(), progress);
            ParseStatus = "解析完成";

            // 4. 填充 ReadonlyFundInfo
            var fundInfo = new ReadonlyFundInfo();
            fundInfo.FillBy(result!.Factors);

            // 5. 保存到 DB（有有效数据时才保存）
            if (result.Factors.Length > 0)
            {
                var record = new ContractParseRecord
                {
                    Id = meta.Hash,
                    ParsedAt = DateTime.Now,
                    FundInfoJson = result.Json
                };
                using (var db = DbHelper.Base())
                {
                    db.GetCollection<ContractParseRecord>().Upsert(record);
                }
            }

            // 6. 等待上一个合同解析记录（AI 解析期间已并行查询）
            var oldInfo = await prevInfoTask;

            // 7. 打开对比窗口（警告信息在窗口内展示）
            var vm = new ContractElementsCompareViewModel(fundInfo, oldInfo, result.Warnings);
            var window = new ContractElementsCompareWindow
            {
                DataContext = vm,
                Owner = App.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (HttpRequestException ex)
        {
            Logg.Error($"AI 解析合同要素网络错误: {ex}");
            Toast.Error($"网络请求失败：{ex.Message}，请检查网络连接或 AI 服务地址");
        }
        catch (TaskCanceledException)
        {
            Logg.Error("AI 解析合同要素超时");
            Toast.Error("AI 请求超时（5分钟），请检查网络连接或稍后重试");
        }
        catch (System.IO.InvalidDataException ex)
        {
            Logg.Error(ex, $"AI 配置错误");
            Toast.Error($"AI 配置错误：{ex.Message}");
        }
        catch (Exception ex)
        {
            Logg.Error(ex, $"AI 解析合同要素失败");
            Toast.Error($"AI 解析失败: {ex.Message}");
        }
        finally
        {
            IsParsingContract = false;
            ParseStatus = "";
        }
    }

#if DEBUG
    /// <summary>
    /// 从 DB 加载最新的解析记录并显示对比窗口（仅调试用）
    /// </summary>
    [RelayCommand]
    private void LoadParsedJsonFromTemp()
    {
        try
        {
            using var db = DbHelper.Base();
            var record = db.GetCollection<ContractParseRecord>().Query().OrderByDescending(r => r.ParsedAt).FirstOrDefault();

            if (record is null)
            {
                Toast.Warning("DB 中没有解析记录");
                return;
            }

            var fundInfo = TokenProvider.ToFundInfo(record.FundInfoJson);
            if (fundInfo is null)
            {
                Toast.Warning("JSON 反序列化失败");
                return;
            }

            var warnings = new List<string> { $"从 DB 加载: {record.ParsedAt:yyyy-MM-dd HH:mm:ss}" };
            var vm = new ContractElementsCompareViewModel(fundInfo, null, warnings);
            var window = new ContractElementsCompareWindow
            {
                DataContext = vm,
                Owner = App.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            Logg.Error(ex, "LoadParsedJsonFromTemp 失败");
            Toast.Error($"加载失败: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// 查找上一个 contract flow 的合同解析记录
    /// </summary>
    private ReadonlyFundInfo? LoadPreviousContractInfo()
    {
        try
        {
            using var db = DbHelper.Base();
            var prevContractFlow = db.GetCollection<FundFlow>()
                .Find(x => x.FundId == FundId && x.Id < FlowId)
                .OfType<ContractFlow>()
                .Where(cf => cf.ContractFile?.File?.Hash is not null)
                .OrderByDescending(cf => cf.Id)
                .FirstOrDefault();

            if (prevContractFlow?.ContractFile?.File?.Hash is string prevHash)
            {
                var prevRecord = db.GetCollection<ContractParseRecord>().FindById(prevHash);
                if (prevRecord is not null)
                    return TokenProvider.ToFundInfo(prevRecord.FundInfoJson);
            }
        }
        catch (Exception ex)
        {
            Logg.Error($"查找上一个合同解析记录失败: {ex}");
        }
        return null;
    }


    [RelayCommand]
    public async Task ParseAccountInfo(SimpleFileViewModel f)
    {
        if (f == CollectionAccount)
            await UpdateElement(f, x => x.CollectionAccount, FundAccountType.Collection);
        else if (f == CustodyAccount)
            await UpdateElement(f, x => x.CustodyAccount, FundAccountType.Custody);
    }




    private Task UpdateElement(SimpleFileViewModel? file, Func<FundElements, Mutable<BankAccount>> property, FundAccountType accountType)
    {
        return Task.Run(() =>
        {
            try
            {
                using var fs = file?.Meta?.OpenRead();

                if (fs is not null)
                {
                    var ac = PdfHelper.GetAccountInfo(fs);

                    if (ac is not null)
                    {
                        using var db = DbHelper.Base();
                        var ele = db.GetCollection<FundElements>().FindById(FundId);
                        property(ele).SetValue(ac.First(), FlowId);
                        db.GetCollection<FundElements>().Update(ele);
                        WeakReferenceMessenger.Default.Send(new ElementChangedBackgroundMessage(FundId, FlowId));
                        WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, accountType));
                    }
                }
                else
                {
                    using var db = DbHelper.Base();
                    var ele = db.GetCollection<FundElements>().FindById(FundId);
                    property(ele).RemoveValue(FlowId);
                    db.GetCollection<FundElements>().Update(ele);
                    WeakReferenceMessenger.Default.Send(new ElementChangedBackgroundMessage(FundId, FlowId));
                    WeakReferenceMessenger.Default.Send(new FundAccountChangedMessage(FundId, accountType));
                }
                Logg.Information($"设置 {accountType} 账户成功 {FundId}.{FlowId}");
            }
            catch (Exception e) { Logg.Error($"设置 {accountType} 账户出错 {FundId}.{FlowId} {e}"); }

        });
    }

}
