using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using FMO.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

namespace FMO;

public partial class ContractModifyFlowViewModel : ContractRelatedFlowViewModel, IElementChangable
{



    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifyCollectionAccount))]
    [NotifyPropertyChangedFor(nameof(ModifyCustodyAccount))]
    public partial bool ModifyName { get; set; }


    [ObservableProperty]
    public partial bool ModifyBySupplementary { get; set; }


    public bool ModifyCollectionAccount { get => ModifyName || field; set => SetProperty(ref field, value); }

    public bool ModifyCustodyAccount { get => ModifyName || field; set => SetProperty(ref field, value); }

    /// <summary>
    /// 变更投资经理
    /// </summary>
    [ObservableProperty]
    public partial bool ModifyInvestmentManager { get; set; }

    /// <summary>
    /// 备案函
    /// </summary>
    public DualFileViewModel RegistrationLetter { get; }

    /// <summary>
    /// 补充协议
    /// </summary>
    public MultiFileViewModel SupplementaryFile { get; }


    //[ObservableProperty]
    //public partial ObservableCollection<FileInfo>? SupplementaryFile { get; set; }

    /// <summary>
    /// 变更公告
    /// </summary> 
    public DualFileViewModel Announcement { get; }


    //public  SimpleFileViewModel SealedAnnouncement { get; set; }

    public DualFileViewModel CommitmentLetter { get; }

    //public SimpleFileViewModel SealedCommitmentLetter { get; }

    /// <summary>
    /// 签署的补充协议
    /// </summary>
    public MultiFileViewModel SignedSupplementary { get; set; }


    [SetsRequiredMembers]
    public ContractModifyFlowViewModel(ContractModifyFlow flow) : base(flow)
    {
        if (flow.Section.HasFlag(ContractModifySection.Name))
            ModifyName = true;
        if (flow.Section.HasFlag(ContractModifySection.InvestManager))
            ModifyInvestmentManager = true;
        if (flow.Section.HasFlag(ContractModifySection.ShareClass))
            ModifyShareClass = true;
        if (flow.Section.HasFlag(ContractModifySection.CollectionAccount))
            ModifyCollectionAccount = true;
        if (flow.Section.HasFlag(ContractModifySection.CustodyAccount))
            ModifyCustodyAccount = true;

        ModifyBySupplementary = flow.ModifyBySupplementary;
        if (flow.SupplementaryFile?.Files.Any() ?? false)
            ModifyBySupplementary = true;

        using var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(FundId).Name;

        Contract.SpecificFileName = x => $"{fund}_基金合同_{Date:yyyy年MM月dd日}{x}";
        Contract.FileChanged += f => SaveFileChanged(new { ContractFile = f });

        ///补充协议
        SupplementaryFile = new(flow.SupplementaryFile) { Label = "补充协议", Filter = "文本|*.docx;*.doc;*.pdf" };
        SupplementaryFile.FileChanged += f => SaveFileChanged(new { SupplementaryFile = f });


        SignedSupplementary = new(flow.SignedSupplementary) { Label = "签署的协议", Filter = "PDF|*.pdf" };
        SignedSupplementary.FileChanged += f => SaveFileChanged(new { SignedSupplementary = f });

        RegistrationLetter = new(flow.RegistrationLetter) { Label = "备案函", Filter = "PDF|*.pdf" };
        RegistrationLetter.FileChanged += f => SaveFileChanged(new { RegistrationLetter = f });


        Announcement = new(flow.Announcement) { Label = "变更公告", Filter = "文本|*.docx;*.doc;*.pdf", SpecificFileName = x => $"{fund}_变更公告_{Date:yyyy年MM月dd日}{x}" };
        Announcement.FileChanged += f => SaveFileChanged(new { Announcement = f });
        Announcement.Normal.SpecificFileName = Announcement.SpecificFileName;
        Announcement.Another.SpecificFileName = Announcement.SpecificFileName;


        CommitmentLetter = new(flow.CommitmentLetter) { Label = "信息变更承诺函", Filter = "文本|*.docx;*.doc;*.pdf", SpecificFileName = x => $"{fund}_信息变更承诺函_{Date:yyyy年MM月dd日}{x}" };
        CommitmentLetter.FileChanged += f => SaveFileChanged(new { CommitmentLetter = f });

        CommitmentLetter.Another.SpecificFileName = CommitmentLetter.SpecificFileName;


        //SignedSupplementary = new()
        //{
        //    Label = "签署的协议",
        //    Filter = "压缩文件|*.zip;*.rar;*.gzip;*.7z",
        //    SaveFolder = FundHelper.GetFolder(FundId, "SignedSupplementary"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.SignedSupplementary, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.SignedSupplementary = y; },
        //}; SignedSupplementary.Init(flow);

        //RegistrationLetter = new()
        //{
        //    Label = "备案函",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Registration"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.RegistrationLetter, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.RegistrationLetter = y; },
        //}; RegistrationLetter.Init(flow);



        //Announcement = new()
        //{
        //    Label = "变更公告",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Announcement"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.Announcement, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.Announcement = y; },
        //}; Announcement.Init(flow);
        //SealedAnnouncement = new()
        //{
        //    Label = "变更公告",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Announcement"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.SealedAnnouncement, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.SealedAnnouncement = y; },
        //    Filter = "PDF (*.pdf)|*.pdf;",
        //    SpecificFileName = x =>
        //    {
        //        using var db = DbHelper.Base();
        //        var fund = db.GetCollection<Fund>().FindById(FundId);

        //        return $"{fund.Name}_变更公告_{Date:yyyy年MM月dd日}{x}";
        //    }
        //}; SealedAnnouncement.Init(flow);


        //CommitmentLetter = new()
        //{
        //    Label = "信息变更承诺函",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Registration"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.CommitmentLetter, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.CommitmentLetter = y; },
        //}; CommitmentLetter.Init(flow);


        //SealedCommitmentLetter = new()
        //{
        //    Label = "信息变更承诺函",
        //    SaveFolder = FundHelper.GetFolder(FundId, "Registration"),
        //    GetProperty = x => x switch { ContractModifyFlow f => f.SealedCommitmentLetter, _ => null },
        //    SetProperty = (x, y) => { if (x is ContractModifyFlow f) f.SealedCommitmentLetter = y; },
        //    SpecificFileName = x =>
        //    {
        //        using var db = DbHelper.Base();
        //        var fund = db.GetCollection<Fund>().FindById(FundId);

        //        return $"{fund.Name}_信息变更承诺函_{Date:yyyy年MM月dd日}{x}";
        //    }
        //}; SealedCommitmentLetter.Init(flow);


        Initialized = true;
    }

    //private void SupplementaryFile_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var fund = db.GetCollection<Fund>().FindById(FundId);

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = fund.Folder();
    //            dir = dir.CreateSubdirectory("Supplementary");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var flow = db.GetCollection<FundFlow>().FindById(FlowId) as ContractModifyFlow;

    //            if (flow!.SupplementaryFile is null)
    //                flow.SupplementaryFile = new VersionedFileInfo { Name = nameof(flow.SupplementaryFile) };

    //            flow!.SupplementaryFile.Files.Add(fileVersion);
    //            db.GetCollection<FundFlow>().Update(flow);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var flow = db.GetCollection<FundFlow>().FindById(FlowId) as ContractModifyFlow;


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = flow!.SupplementaryFile?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                flow.SupplementaryFile!.Files.Remove(file);
    //        }

    //        db.GetCollection<FundFlow>().Update(flow!);
    //    }

    //}



    [RelayCommand]
    public void GenerateFile(DualFileViewModel v)
    {
        if (v == CommitmentLetter)
        {
            string path = Path.GetTempFileName();
            try
            {
                // 需要先设置日期
                if (Date is null)
                {
                    HandyControl.Controls.Growl.Warning("请先设置变更日期");
                    return;
                }

                using var db = DbHelper.Base();
                var fund = db.GetCollection<Fund>().FindById(FundId);

                if (Tpl.GenerateByPredefined(path, "信息变更承诺函.docx", new { Name = fund.Name, Code = fund.Code, Date = Date }))
                    v.Normal.Meta = FileMeta.Create(path, @$"信息变更承诺函_{Date:yyyy年MM月dd日}.docx");
                else HandyControl.Controls.Growl.Error("生成文件失败，请查看Log，检查模板是否存在");
            }
            catch { }
            File.Delete(path);
        }
    }


    [RelayCommand]
    public void SaveModifySection()
    {
        ContractModifySection section = default;
        if (ModifyName) section |= ContractModifySection.Name;
        if (ModifyInvestmentManager) section |= ContractModifySection.InvestManager;
        if (ModifyShareClass) section |= ContractModifySection.ShareClass;
        if (ModifyCollectionAccount) section |= ContractModifySection.CollectionAccount;
        if (ModifyCustodyAccount) section |= ContractModifySection.CustodyAccount;

        using var db = DbHelper.Base();
        var flow = db.GetCollection<FundFlow>().FindById(FlowId) as ContractModifyFlow;
        if (flow is not null)
        {
            flow.ModifyBySupplementary = ModifyBySupplementary;
            flow.Section = section;
            db.GetCollection<FundFlow>().Update(flow);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);


    }

    protected override void UpdateFileDateOverride()
    {
        if (Date is null) return;

        var d = DateOnly.FromDateTime(Date.Value);
        using var db = DbHelper.Base();
        if (db.GetCollection<FundFlow>().FindById(FlowId) is ContractModifyFlow flow)
        {
            SwitchDate(Contract, d);
            SwitchDate(RegistrationLetter, d);
            SwitchDate(SupplementaryFile, d);
            SwitchDate(Announcement, d);
            SwitchDate(CommitmentLetter, d);

            //if (Contract is not null && SwitchDate(Contract, d) && flow.ContractFile is not null)
            //    flow.ContractFile.Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), Contract.File.FullName);

            //if (Announcement?.File is not null && SwitchDate(Announcement, d) && flow.Announcement is not null)
            //    flow.Announcement.Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), Announcement.File.FullName);
            //if (SealedAnnouncement?.File is not null && SwitchDate(SealedAnnouncement, d) && flow.SealedAnnouncement is not null)
            //    flow.SealedAnnouncement.Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), SealedAnnouncement.File.FullName);

            //if (CommitmentLetter?.File is not null && SwitchDate(CommitmentLetter, d) && flow.CommitmentLetter is not null)
            //    flow.CommitmentLetter.Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), CommitmentLetter.File.FullName);

            //if (SealedCommitmentLetter?.File is not null && SwitchDate(SealedCommitmentLetter, d) && flow.SealedCommitmentLetter is not null)
            //    flow.SealedCommitmentLetter.Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), SealedCommitmentLetter.File.FullName);


            db.GetCollection<FundFlow>().Update(flow);
        }

    }

    private void SwitchDate(DualFileViewModel df, DateOnly d)
    {
        if (df.Normal.Meta is not null)
            df.Normal.Meta = df.Normal.Meta with { Name = Regex.Replace(df.Normal.Meta.Name, @$"_(\d{{4}}年\d+月\d+日)", $"_{d:yyyy年MM月dd日}") };


        if (df.Another.Meta is not null)
            df.Another.Meta = df.Another.Meta with { Name = Regex.Replace(df.Another.Meta.Name, @$"_(\d{{4}}年\d+月\d+日)", $"_{d:yyyy年MM月dd日}") };

    }

    private void SwitchDate(MultiFileViewModel supplementaryFile, DateOnly d)
    {
        foreach (var file in supplementaryFile.Files.Where(x => x.Meta is not null))
            file.Meta = file.Meta! with { Name = Regex.Replace(file.Meta.Name, @$"_(\d{{4}}年\d+月\d+日)", $"_{d:yyyy年MM月dd日}") };
    }

    private bool SwitchDate(SimpleFileViewModel file, DateOnly d)
    {
        if (file.Meta is null) return false;

        file.Meta = file.Meta with { Name = Regex.Replace(file.Meta.Name, @$"_(\d{{4}}年\d+月\d+日)", $"_{d:yyyy年MM月dd日}") };
        return true;
    }

}