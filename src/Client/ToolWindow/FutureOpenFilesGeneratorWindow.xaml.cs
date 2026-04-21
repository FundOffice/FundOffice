using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using Microsoft.Win32;
using MiniSoftware;
using Serilog;

namespace FMO;

/// <summary>
/// FutureOpenFilesGeneratorWindow.xaml 的交互逻辑
/// </summary>
public partial class FutureOpenFilesGeneratorWindow : Window
{
    public FutureOpenFilesGeneratorWindow()
    {
        InitializeComponent();
    }
}

public partial class FutureOpenFilesGeneratorWindowViewModel : ObservableObject
{
    public FutureOpenFilesGeneratorWindowViewModel()
    {
        using var db = DbHelper.Base();
        var members = db.GetCollection<Participant>().FindAll().ToArray();

        InvestmentManagers = new(members.Where(x => x.Role.HasFlag(PersonRole.InvestmentManager)));
        ResponsePersons = new(members);
        OpenAgents = new(members.Where(x => x.Role.HasFlag(PersonRole.Agent)));
        OrderPlacers = new(members.Where(x => x.Role.HasFlag(PersonRole.OrderPlacer)));
        FundTransferors = new(members.Where(x => x.Role.HasFlag(PersonRole.FundTransferor)));
        ConfirmationPersons = new(members.Where(x => x.Role.HasFlag(PersonRole.ConfirmationPerson)));
    }


    public ObservableCollection<Participant> InvestmentManagers { get; set; }
    public ObservableCollection<Participant> ResponsePersons { get; set; }

    public ObservableCollection<Participant> OpenAgents { get; set; }

    public ObservableCollection<Participant> OrderPlacers { get; set; }

    public ObservableCollection<Participant> FundTransferors { get; set; }

    public ObservableCollection<Participant> ConfirmationPersons { get; set; }


    public required string Company { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? OpenAgent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? InvestmentManager { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? ResponsePerson { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? OrderPlacer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? FundTransferor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsParticipantValid))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial Participant? ConfirmationPerson { get; set; }


    public bool IsParticipantValid => OpenAgent is not null && OrderPlacer is not null && FundTransferor is not null && ConfirmationPerson is not null && InvestmentManager is not null && ResponsePerson is not null;

    /// <summary>
    /// 模板文件
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial string? TemplatePath { get; set; }

    /// <summary>
    /// 目标目录
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial string? TargetFolder { get; set; }


    public bool CanGenerate => IsParticipantValid && File.Exists(TemplatePath) && Directory.Exists(TargetFolder);

    public int FundId { get; init; }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    public void Generate()
    {
        try
        {
            // 模板文件
            var db = DbHelper.Base();
            var m = db.GetCollection<Models.Manager>().FindOne(x => x.IsMaster);
            var fund = db.GetCollection<Fund>().FindById(FundId);
            var daily = db.GetDailyCollection(fund.Id).Find(x => x.NetAsset > 0).MaxBy(x => x.Date)!;
            var ele = db.GetCollection<FundElements>().FindById(fund.Id);
            var legal = db.GetCollection<Participant>().FindAll().FirstOrDefault(x => x.Role.HasFlag(PersonRole.Legal));
            db.Dispose();
            // 数据
            var obj = new
            {
                Manager = new
                {
                    Id = m.Id,
                    Duration = $"{m.SetupDate}-{m.ExpireDate}",
                    Name = m.Name,
                    EnglishName = m.EnglishName,
                    LegalPerson = m.ArtificialPerson,
                    ArtificialPerson = m.ArtificialPerson,
                    SetupDate = m.SetupDate,
                    ExpireDate = m.ExpireDate,
                    RegisterAddress = m.RegisterAddress,
                    OfficeAddress = m.OfficeAddress,
                    RegisterCapital = $"{m.RegisterCapital}万",
                    RealCapital = $"{m.RealCapital}万",
                    BusinessScope = m.BusinessScope,
                    Telephone = m.Telephone,
                    //ZipCode = 
                },
                Fund = new
                {
                    Name = fund.Name,
                    NetAsset = $"{daily.NetAsset / 10000:N0}万",
                    CustodyAccount = ele.CustodyAccount.Value,
                    Duration = $"{ele.DurationInMonths}个月"
                }
                ,
                LegalPerson = FromParitcipant(legal),
                InvestmentManager = FromParitcipant(InvestmentManager),
                ResponsiblePerson = FromParitcipant(ResponsePerson),
                Trustee = new
                {
                    Name = ele.TrusteeInfo.Value?.Name
                },
                OpenAgent = FromParitcipant(OpenAgent),
                OrderPlacer = FromParitcipant(OrderPlacer),
                FundTransferor = FromParitcipant(FundTransferor),
                ConfirmationPerson = FromParitcipant(ConfirmationPerson),
            };

            MiniWord.SaveAsByTemplate(Path.Combine(TargetFolder!, "开户材料2.docx"), TemplatePath, obj);

        }
        catch (Exception e)
        {
            Log.Error($"按模板生成开户材料出错 {e}");
            HandyControl.Controls.Growl.Error("生成文件失败");
        }
    }


    private object FromParitcipant(Participant? participant)
    {
        return new
        {
            Name = participant?.Name,
            IdType = participant?.Identity?.Type switch { IDType.IdentityCard or IDType.Unknown or null => "身份证", var x => EnumDescriptionTypeConverter.GetEnumDescription(x) },
            Id = participant?.Identity?.Id,
            Phone = participant?.Phone,
            Address = participant?.Address,
        };
    }

    [RelayCommand]
    public void ChooseTemplate()
    {
        var wnd = new OpenFileDialog();
        wnd.InitialDirectory = new DirectoryInfo($"files\\tpl").FullName;
        var r = wnd.ShowDialog();
        if (r is null || !r.Value) return;

        TemplatePath = wnd.FileName;
    }

    [RelayCommand]
    public void ChooseTarget()
    {
        var wnd = new OpenFolderDialog();
        wnd.InitialDirectory = TargetFolder;
        var r = wnd.ShowDialog();
        if (r is null || !r.Value) return;

        TargetFolder = wnd.FolderName;
    }

    [RelayCommand]
    public void SetAsSame()
    {
        OpenAgent = ResponsePerson;
        OrderPlacer = ResponsePerson;
        FundTransferor = ResponsePerson;
        ConfirmationPerson = ResponsePerson;
    }
}
