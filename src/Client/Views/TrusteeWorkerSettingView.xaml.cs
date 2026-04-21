using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Office2010.Excel;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using System.Windows.Controls;
using static FMO.Trustee.TrusteeWorker;

namespace FMO;

/// <summary>
/// TrusteeWorkerSettingView.xaml 的交互逻辑
/// </summary>
public partial class TrusteeWorkerSettingView : UserControl
{
    public TrusteeWorkerSettingView()
    {
        InitializeComponent();

        DataContext = new TrusteeWorkerSettingViewModel();
    }

    private void Init()
    {
        using var pdb = DbHelper.Platform();
        var ranges = pdb.GetCollection<TrusteeMethodShotRange>().FindAll().ToArray();



    }
}


public partial class TrusteeWorkerSettingViewModel : ObservableObject
{

    public TrusteeWorkingConfigViewModel[] Configs { get; set; }


    //public TrusteeWorker.WorkConfig[] WorkConfigs { get; set; }

    //public TrusteeWorker.WorkConfig? QueryTransferRequestsConfig { get; set; }

    //public TrusteeWorkerUniViewModel RaisingBalanceConfig { get; private set; }

    public TrusteeWorkerSettingViewModel()
    {
        using var pdb = DbHelper.Platform();
        var ranges = pdb.GetCollection<TrusteeMethodShotRange>().FindAll().ToArray();

        var cms = Create(CMS._Identifier, ranges);
        var citics = Create(CITICS._Identifier, ranges);
        var csc = Create(CSC._Identifier, ranges);
        var xyzq = Create(XYZQ._Identifier, ranges);

        Configs = [cms, citics, csc, xyzq];


        var cfg = pdb.GetCollection<WorkConfig>().FindAll().ToArray();

        //RaisingBalanceConfig = new(cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryRaisingBalance)));
        //TransferRecordConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryTransferRecords)) ?? new(nameof(ITrustee.QueryTransferRecords)) { Interval = 60 }; // 每6个小时
        //TransferRequestConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryTransferRequests)) ?? new(nameof(ITrustee.QueryTransferRequests));
        //DailyFeeConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryFundDailyFee)) ?? new(nameof(ITrustee.QueryFundDailyFee)) { Interval = 60 * 12 }; // 每天一次
        //RaisingAccountTransctionConfig = cfg.FirstOrDefault(x => x.Id == nameof(ITrustee.QueryRaisingAccountTransction)) ?? new(nameof(ITrustee.QueryRaisingAccountTransction));

        //QueryTransferRequestsConfig = WorkConfigs.FirstOrDefault(x => x.Id == "TransferRequestConfig");
    }

    private TrusteeWorkingConfigViewModel Create(string idf, TrusteeMethodShotRange[] ranges)
    {
        var vm = new TrusteeWorkingConfigViewModel { Identifier = idf, };
        //vm.QueryTransferRequestConfig = GetConfig(idf, nameof(ITrustee.QueryTransferRecords));
        vm.QueryNetValue = Create(ranges, idf, nameof(ITrustee.QueryNetValue));
        vm.QueryTransferRecord = Create(ranges, idf, nameof(ITrustee.QueryTransferRecords));
        vm.QueryTransferRequest = Create(ranges, idf, nameof(ITrustee.QueryTransferRequests));
        vm.QueryRaisingAccountTransction = Create(ranges, idf, nameof(ITrustee.QueryRaisingAccountTransction));
        vm.QueryRaisingBalance = Create(ranges, idf, nameof(ITrustee.QueryRaisingBalance));
        vm.QueryFundDailyFee = Create(ranges, idf, nameof(ITrustee.QueryFundDailyFee));
        return vm;
    }


    public TrusteeMethodConfigViewModel Create(TrusteeMethodShotRange[] ranges, string identifier, string method)
    {
        var range = ranges.FirstOrDefault(x => x.Id == $"{identifier}.{method}");
        return new TrusteeMethodConfigViewModel
        {
            Identifier = identifier,
            Method = method,
            DateBegin = range?.Begin,
            DateEnd = range?.End
        };
    }


}

public partial class TrusteeMethodConfigViewModel : ObservableObject
{
    public required string Identifier { get; set; }

    public required string Method { get; set; }

    [ObservableProperty]
    public partial DateOnly? DateBegin { get; set; }

    [ObservableProperty]
    public partial DateOnly? DateEnd { get; set; }

    [RelayCommand]
    public void SaveDataRange(DataGrid v)
    {
        using var pdb = DbHelper.Platform();

        var start = StartDate();
        DateBegin ??= start;
        DateEnd ??= start;
        if(DateBegin > DateEnd)
            DateBegin = DateEnd;

        var ran = new TrusteeMethodShotRange($"{Identifier}.{Method}", DateBegin.Value, DateEnd.Value);

        var ranges = pdb.GetCollection<TrusteeMethodShotRange>().Upsert(ran);

        v.CommitEdit();
    }


    private DateOnly StartDate()
    {
        using var db = DbHelper.Base();
        var StartOfAny = db.GetCollection<Manager>().Query().First().SetupDate;

        var fda = db.GetCollection<Fund>().Query().Select(x => x.SetupDate).ToArray().Where(x => x.Year > 1970).ToArray();
        if (fda.Length > 0 && fda.Min() is DateOnly d && d < StartOfAny)
            StartOfAny = d;

        return StartOfAny;
    }
}

public partial class TrusteeWorkingConfigViewModel : ObservableObject
{
    public required string Identifier { get; set; }


    public TrusteeMethodConfigViewModel? QueryNetValue { get; set; }

    public TrusteeMethodConfigViewModel? QueryTransferRequest { get; set; }

    public TrusteeMethodConfigViewModel? QueryTransferRecord { get; set; }
    public TrusteeMethodConfigViewModel? QueryRaisingAccountTransction { get; internal set; }
    public TrusteeMethodConfigViewModel? QueryRaisingBalance { get; internal set; }
    public TrusteeMethodConfigViewModel? QueryFundDailyFee { get; internal set; }


}


public partial class TrusteeWorkerUniViewModel : ObservableObject
{
    public TrusteeWorkerUniViewModel(WorkConfig? workConfig)
    {


    }

    public string? Name { get; set; }

    public string? Id { get; set; }

    [ObservableProperty]
    public partial int Interval { get; set; }

}