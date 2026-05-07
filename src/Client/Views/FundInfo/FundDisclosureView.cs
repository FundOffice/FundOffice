using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.AMAC.Direct;
using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using HandyControl.Controls;
using LiteDB;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// FundAnnouncementView.xaml 的交互逻辑
/// </summary>
public partial class FundDisclosureView : UserControl
{
    public FundDisclosureView()
    {
        InitializeComponent();
    }
}


public partial class FundDisclosureViewModel : ObservableObject, IRecipient<IDisclosureNotice>
{
    public FundDisclosureViewModel(int fid)
    {
        FundId = fid;

        // 获取公告列表
        using var db = DbHelper.Base();
        var data = db.GetCollection<FundAnnouncement>().Find(x => x.FundId == fid).ToArray();

        Announcements = [.. data.Select(x => new AnnouncementViewModel(x))];


        IEnumerable<PeriodicalDisclosureNotice> notices = db.GetCollection<IDisclosureNotice>().Query().
            Where(Query.EQ(nameof(PeriodicalDisclosureNotice.FundId), fid)).
            OrderByDescending($"$.{nameof(PeriodicalDisclosureNotice.ReportDate)}").Limit(30).ToArray().OfType<PeriodicalDisclosureNotice>();


        PeriodicDisclosure = [.. notices.Select(x => new PeriodicReportViewModel(x, [], []))];
        PeriodicNoticeSource.Source = PeriodicDisclosure;


        var qu = db.GetCollection<IDisclosureNotice>().Query().
            Where(Query.EQ(nameof(PeriodicalDisclosureNotice.FundId), fid)).ToArray().OfType<QuarterlyUpdate>();
        var dic = db.GetCollection<AmacProcessResult>().Query().Where(Query.In("_id", qu.Select(x => new BsonValue(x.Id)))).ToArray().ToDictionary(x => x.Id, x => x);
        QuarterlyDisclosure = [.. qu.Select(x => new QuarterlyUpdateViewModel(x, [], []))];


        //if (PeriodicDisclosure.Count == 0) PeriodicDisclosure = [new FundPeriodicReport { FundId = FundId, Type = PeriodicReportType.MonthlyReport }, new FundQuarterlyUpdate { FundId = FundId }];


 

        QuarterlyUpdate.Source = QuarterlyDisclosure;
    }

    public int FundId { get; }


    public ObservableCollection<AnnouncementViewModel> Announcements { get; init; }



    public CollectionViewSource PeriodicNoticeSource { get; } = new();

    public CollectionViewSource QuarterlyUpdate { get; } = new();


    public ObservableCollection<PeriodicReportViewModel> PeriodicDisclosure { get; }

    public ObservableCollection<QuarterlyUpdateViewModel> QuarterlyDisclosure { get; }

    [RelayCommand]
    public void AddAnnouncement()
    {
        using var db = DbHelper.Base();
        var obj = new FundAnnouncement { FundId = FundId };
        db.GetCollection<FundAnnouncement>().Insert(obj);

        Announcements?.Add(new(obj));
    }

    public void Receive(IDisclosureNotice message)
    {
    }

    [RelayCommand]
    public void LoadPeriodicNotice()
    {
        using var db = DbHelper.Base();
        IEnumerable<PeriodicalDisclosureNotice> notices = db.GetCollection<IDisclosureNotice>().Query().
            Where(Query.EQ(nameof(PeriodicalDisclosureNotice.FundId), FundId)).
            OrderByDescending($"$.{nameof(PeriodicalDisclosureNotice.ReportDate)}").Skip(PeriodicDisclosure.Count).Limit(30).ToArray().OfType<PeriodicalDisclosureNotice>();

        foreach (var item in notices)
        {
            PeriodicDisclosure.Add(new PeriodicReportViewModel(item, [], []));
        } 
    }
}


public partial class AnnouncementViewModel : EditableControlViewModelBase<FundAnnouncement>
{
    public AnnouncementViewModel(FundAnnouncement obj)
    {
        Id = obj.Id;
        FundId = obj.FundId;

        Title = new()
        {
            InitFunc = x => x.Title,
            UpdateFunc = (x, y) => x.Title = y,
            ClearFunc = x => x.Title = null
        };
        Title.Init(obj);

        Date = new()
        {
            InitFunc = x => x.Date == default ? null : new DateTime(x.Date, default),
            UpdateFunc = (x, y) => x.Date = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.Date = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        Date.Init(obj);

        File = new(obj.File);
        File.FileChanged += f =>
        {
            if (Id == 0) return; // 新建时不保存
            using var db = DbHelper.Base();
            db.GetCollection<FundAnnouncement>().UpdateMany(BsonMapper.Global.ToDocument(new { File = f }).ToString(), $"_id={Id}");
        };
    }

    public DualFileViewModel File { get; }


    public int FundId { get; }


    public ChangeableViewModel<FundAnnouncement, string?> Title { get; }

    public ChangeableViewModel<FundAnnouncement, DateTime?> Date { get; }


    protected override FundAnnouncement InitNewEntity() => new FundAnnouncement { FundId = FundId };
}

public partial class AmacDirectResultViewModel : ObservableObject
{
    public AmacDirectResultViewModel(AmacProcessResult? result)
    {
        if (result is null)
            return;

        if (result.SubmitCode == 0)
        {
            Status = State.Submit;
            IsSuccess = true;
        }
        else if (result.SubmitCode > 0)
        {
            Status = State.Submit;
            IsSuccess = false;
        }
        else if (result.ValidateCode == 10)
        {
            Status = State.Submit;
            IsSuccess = true;
        }
        else if (result.ValidateCode == 0)
        {
            Status = State.Verify;
            IsSuccess = true;
        }
        else if (result.ValidateCode > 0)
        {
            Status = State.Verify;
            IsSuccess = false;
        }
        else if (result.UploadCode == 0)
        {
            Status = State.Upload;
            IsSuccess = true;
        }
        else if (result.UploadCode > 0)
        {
            Status = State.Upload;
            IsSuccess = false;
        }
        else
        {
            Status = State.None;
            IsSuccess = false;
        }
    }

    public enum State
    {
        None = 0,
        Upload = 1,
        Verify = 2,
        Submit = 3,
    }

    [ObservableProperty]
    public partial State Status { get; set; }


    [ObservableProperty]
    public partial bool IsSuccess { get; set; }

    [ObservableProperty]
    public partial int Code { get; set; }



    [ObservableProperty]
    public partial IList<ValidationInfo>? Validations { get; set; }





}