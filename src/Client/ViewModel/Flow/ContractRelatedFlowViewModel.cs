using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.PDF;
using FMO.Shared;
using FMO.Utilities;
using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace FMO;


public partial class ShareClassViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public required partial string Name { get; set; }

    [ObservableProperty]
    public partial string? Requirement { get; set; }

    public ShareClassViewModel() { }

    [SetsRequiredMembers]
    public ShareClassViewModel(ShareClass s)
    {
        Id = s.Id;
        Name = s.Name;
        Requirement = s.Requirement;
    }
}



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
    /// 份额类型有变动
    /// </summary>
    private bool _shareChanged;


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
                Log.Information($"设置 {accountType} 账户成功 {FundId}.{FlowId}");
            }
            catch (Exception e) { Log.Error($"设置 {accountType} 账户出错 {FundId}.{FlowId} {e}"); }

        });
    }

}
