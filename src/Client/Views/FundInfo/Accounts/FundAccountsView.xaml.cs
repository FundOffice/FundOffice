using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Logging;
using FMO.Models;
using FMO.PDF;
using FMO.Shared;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// FundAccountsView.xaml 的交互逻辑
/// </summary>
public partial class FundAccountsView : UserControl
{
    public FundAccountsView()
    {
        InitializeComponent();
    }
}


public partial class FundAccountsViewModel : ObservableObject
{
    public FundAccountsViewModel(int fundId, string code, string[] names)
    {
        FundId = fundId;
        Code = code;
        Names = names;

        /// 统一账户
        using var db = DbHelper.Base();
        var fa = db.GetCollection<FundSingletonAccounts>().FindById(FundId);
        Singleton = new(fa ?? new() { Id = fundId });
        UniversalNo = fa?.UniversalNo;
        FutureNo = fa?.FutureNo;
        CCDCBondAccount = fa?.CCDCBondAccount;
        SHCBondAccount = fa?.SHCBondAccount;


        // 股卡 
        var sas = db.GetCollection<SecurityCard>().Find(x => x.FundId == fundId).ToArray();
        SecurityCards = [.. sas.Select(x => new SecurityCardViewModel(x))];
        SHSecurityCardSource.Source = SecurityCards;
        SZSecurityCardSource.Source = SecurityCards;
        SHSecurityCardSource.SortDescriptions.Add(new SortDescription(nameof(SecurityCardViewModel.Date), ListSortDirection.Descending));
        SZSecurityCardSource.SortDescriptions.Add(new SortDescription(nameof(SecurityCardViewModel.Date), ListSortDirection.Descending));
        SHSecurityCardSource.Filter += (s, e) => e.Accepted = e.Item switch { SecurityCardViewModel v => v.Type == SecurityCardType.ShangHai, _ => false };
        SZSecurityCardSource.Filter += (s, e) => e.Accepted = e.Item switch { SecurityCardViewModel v => v.Type == SecurityCardType.ShenZhen, _ => false };

        var sac = db.GetCollection<SecurityCardChange>().Find(x => x.FundId == fundId).ToArray();
        SecurityCardChanges = [.. sac.Select(x => new SecurityCardChangeViewModel(x))];//ls.AddRange(sac.Select(x => new SecurityCardChangeViewModel(x)));

        SecurityCardChangeSource.Source = SecurityCardChanges;
        SecurityCardChangeSource.SortDescriptions.Add(new SortDescription(nameof(SecurityCardChangeViewModel.Date), ListSortDirection.Descending));

        foreach (var item in SecurityCardChanges)
        {
            if (string.IsNullOrWhiteSpace(item.UniversalNo) && item.File.Exists)
            {
                using var fs = item.File.OpenRead();

                var result = PdfHelper.GetSecurityAccounts(fs);
                if (result is null) continue;

                var m = Regex.Match(result[0].text, @"一码通\w*号码\s*[:：]\s*([a-z0-9]+)");
                item.UniversalNo = m.Success ? m.Groups[1].Value : "";
            }
        }


        // 检验
        if (!string.IsNullOrWhiteSpace(UniversalNo))
        {
            foreach (var item in SecurityCards)
            {
                if (item.UniversalNo != UniversalNo)
                    item.MaybeError = true;
            }
            foreach (var item in SecurityCardChanges)
            {
                if (item.UniversalNo != UniversalNo)
                    item.MaybeError = true;
            }
        }





        var sa = db.GetCollection<StockAccount>().Find(x => x.FundId == fundId).ToArray();
        if (sa is not null)
            StockAccounts = new(sa.Select(x => new StockAccountViewModel(x)));

        var fas = db.GetCollection<FutureAccount>().Find(x => x.FundId == fundId).ToArray();
        if (fas is not null)
            FutureAccounts = new(fas.Select(x => new FutureAccountViewModel(x)));


        if (UniversalNo is null && sas?.Select(x => x.UniversalNo).Distinct().Where(x => !string.IsNullOrWhiteSpace(x)) is IEnumerable<string> s)
        {
            UniversalNo = s.FirstOrDefault();

            if (UniversalNo is not null)
            {
                if (fa is null) fa = new();
                fa.UniversalNo = UniversalNo;
                db.GetCollection<FundSingletonAccounts>().Update(fa);
            }
        }


        SecurityCompanies = new CollectionViewSource();
        try
        {
            if (File.Exists("config\\securitycompany.txt"))
            {
                using var sr = new StreamReader("config\\securitycompany.txt");
                var arr = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var regex = new Regex("(?:股份)*有限(?:责任)*公司");
                SecurityCompanies.Source = arr.Select(x => regex.Replace(x, "")).ToArray();
            }
            else
            {
                var fs = Application.GetResourceStream(new Uri("res/securitycompany.txt", UriKind.Relative));
                using var sr = new StreamReader(fs.Stream);
                var arr = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var regex = new Regex("(?:股份)*有限(?:责任)*公司");
                SecurityCompanies.Source = arr.Select(x => regex.Replace(x, "")).ToArray();
            }

            SecurityCompanies.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SecurityCompanyKeyword) ? true : e.Item switch { string ss => ss.Contains(SecurityCompanyKeyword), _ => true };

            LogEx.Error($"加载证券公司列表失败");
        }
        catch (Exception e)
        {
            LogEx.Error($"加载证券公司列表失败{e.Message}");
        }



        FutureCompanies = new CollectionViewSource();
        try
        {
            if (File.Exists("config\\Futurecompany.txt"))
            {
                using var sr = new StreamReader("config\\Futurecompany.txt");
                var arr = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var regex = new Regex("(?:股份)*有限(?:责任)*公司");
                FutureCompanies.Source = arr.Select(x => regex.Replace(x, "")).ToArray();
            }
            else
            {
                var fs = Application.GetResourceStream(new Uri("res/Futurecompany.txt", UriKind.Relative));
                using var sr = new StreamReader(fs.Stream);
                var arr = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var regex = new Regex("(?:股份)*有限(?:责任)*公司");
                FutureCompanies.Source = arr.Select(x => regex.Replace(x, "")).ToArray();
            }

            FutureCompanies.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(FutureCompanyKeyword) ? true : e.Item switch { string ss => ss.Contains(FutureCompanyKeyword), _ => true };


        }
        catch (Exception e)
        {
            Log.Error($"加载期货公司列表失败{e.Message}");
        }


    }

    public int FundId { get; }
    public string Code { get; }
    public string[] Names { get; }

    public ObservableCollection<SecurityCardViewModel> SecurityCards { get; }


    public ObservableCollection<SecurityCardChangeViewModel> SecurityCardChanges { get; }

    public CollectionViewSource SecurityCardChangeSource { get; } = new();
    public CollectionViewSource SHSecurityCardSource { get; } = new();
    public CollectionViewSource SZSecurityCardSource { get; } = new();


    #region Stock

    [ObservableProperty]
    public partial bool ShowChooseStockCompany { get; set; }

    public CollectionViewSource SecurityCompanies { get; set; }



    [ObservableProperty]
    public partial string? SecurityCompanyKeyword { get; set; }

    private AutoResetEvent ChooseStockCompanyEvent { get; } = new(false);

    [ObservableProperty]
    public partial string? SelectedSecurityCompany { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<StockAccountViewModel>? StockAccounts { get; set; }

    #endregion

    #region Future

    public CollectionViewSource FutureCompanies { get; set; }

    [ObservableProperty]
    public partial bool ShowChooseFutureCompany { get; set; }


    [ObservableProperty]
    public partial string? FutureCompanyKeyword { get; set; }

    private AutoResetEvent ChooseFutureCompanyEvent { get; } = new(false);

    [ObservableProperty]
    public partial string? SelectedFutureCompany { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<FutureAccountViewModel>? FutureAccounts { get; set; }

    #endregion




    [ObservableProperty]
    public partial string? UniversalNo { get; set; }

    /// <summary>
    /// 统一开户编码
    /// </summary>
    [ObservableProperty]
    public partial string? FutureNo { get; set; }

    /// <summary>
    /// 中债登债券账户
    /// </summary>
    [ObservableProperty]
    public partial string? CCDCBondAccount { get; set; }

    /// <summary>
    /// 上清所债券账户
    /// </summary>
    [ObservableProperty]
    public partial string? SHCBondAccount { get; set; }

    [ObservableProperty]
    public partial FundSingletonAccountsViewModel Singleton { get; set; }



    /// <summary>
    /// 添加股卡
    /// </summary>
    [RelayCommand]
    public void AddSecurityCard()
    {
        var fd = new OpenFileDialog();
        fd.Filter = "PDF|*.pdf";
        fd.Multiselect = true;
        var dr = fd.ShowDialog();
        if (dr is null || !dr.Value) return;

        List<SecurityCard> list = new();
        List<SecurityCardChange> cl = [];
        int cnt = 0;
        int failed = 0;
        foreach (var f in fd.FileNames)
        {
            try
            {
                using var fs = new FileStream(f, FileMode.Open);

                var result = PdfHelper.GetSecurityAccounts(fs);
                if (result is null) continue;

                // 解析
                foreach (var c in result)
                {
                    var m = Regex.Match(c.text, @"流水号\s*[:：]\s*(\w+)");
                    var a = m.Success ? m.Groups[1].Value : null;

                    m = Regex.Match(c.text, @"一码通\w*号码\s*[:：]\s*([a-z0-9]+)");
                    var b = m.Success ? m.Groups[1].Value : null;

                    m = Regex.Match(c.text, @"客户名称\s*[:：]\s*([\w（）\(\)]+\s*[-－]\w+)");
                    var d = m.Success ? m.Groups[1].Value : null;

                    m = Regex.Match(c.text, @"\b(?:子账户号码|证券账户号码)\s*[:：]\s*([A-Z0-9]+)");
                    var e = m.Success ? m.Groups[1].Value : null;

                    m = Regex.Match(c.text, @"(?:开立|受理)日期\s*[:：]\s*([\d年月日\s]+)");
                    var g = m.Success ? m.Groups[1].Value.Trim() : null;



                    if (c.text.Contains("变更") && a is not null && d is not null && b is not null) // 股卡变更
                    {
                        m = Regex.Match(c.text, @"(\d{4})\s*年\s*(\d{2})\s*月\s*(\d{2})\s*日", RegexOptions.Singleline);
                        if (m.Success)
                        {
                            SecurityCardChange change = new()
                            {
                                FundId = FundId,
                                Name = d,
                                SerialNo = a,
                                UniversalNo = b,
                                Date = DateOnly.ParseExact($"{m.Groups[1].Value}{m.Groups[2].Value}{m.Groups[3].Value}", "yyyyMMdd")
                            };
                            // 保存文件
                            using var file = new FileStream(@$"files\accounts\security\G-{a}.pdf", FileMode.Create);
                            file.Write(c.page);
                            file.Flush();

                            using var db = DbHelper.Base();
                            var name = change.Name.Split('-', '－');
                            var (fund, _) = db.FindByName(name.Last());
                            change.FundId = fund?.Id ?? 0;
                            db.GetCollection<SecurityCardChange>().Upsert(change);
                            if (fund is null) LogEx.Error($"股卡变更 {change.Id} 的基金 {change.Name} 不在库中");

                            if (fund?.Id == FundId)
                                cl.Add(change);
                            continue;
                        }
                        else ++failed;
                    }

                    if (a is null || b is null || d is null || e is null || g is null || DateTimeHelper.TryFindDate(g) is not DateOnly date)
                    {
                        if (c.text.Contains("申请日期")) continue;

                        ++failed;
                        Log.Error($"解析股卡失败:\n {c.text}");
                        continue;
                    }

                    var sa = new SecurityCard
                    {
                        SerialNo = a,
                        CardNo = e,
                        Name = d,
                        UniversalNo = b,
                        Date = date
                    };
                    m = Regex.Match(c.text, @"产品编码\s*[:：]\s*(\w+)");
                    if (m.Success)
                        sa.FundCode = m.Groups[1].Value;


                    if (c.text.Contains("沪市A"))
                        sa.Type = SecurityCardType.ShangHai;
                    else if (c.text.Contains("深市A"))
                        sa.Type = SecurityCardType.ShenZhen;

                    // 保存文件
                    using var outf = new FileStream(@$"files\accounts\security\{a}-{e}.pdf", FileMode.Create);
                    outf.Write(c.page);
                    outf.Flush();


                    using (var db = DbHelper.Base())
                    {
                        var name = sa.Name.Split('-', '－');
                        var (fund, _) = db.FindByName(name.Last());
                        sa.FundId = fund?.Id ?? 0;
                        sa.FundCode = fund?.Code;
                        db.GetCollection<SecurityCard>().Upsert(sa);

                        if (fund is null) LogEx.Error($"股卡 {sa.CardNo} 的基金 {sa.Name} 不在库中");
                    }
                    ++cnt;
                     
                    if (sa.FundId == FundId)
                        list.Add(sa); 
                }

            }
            catch (Exception e)
            {

            }
        }

        foreach (var x in list.ExceptBy(SecurityCards.Select(x => x.Id), x => x.Id))
            SecurityCards.Add(new SecurityCardViewModel(x));
        foreach (var x in cl.ExceptBy(SecurityCardChanges.Select(x => x.Id), x => x.Id))
            SecurityCardChanges.Add(new SecurityCardChangeViewModel(x));

        SecurityCardChangeSource.View.Refresh();
        SHSecurityCardSource.View.Refresh();
        SZSecurityCardSource.View.Refresh();

        var cuc = list.Count + cl.Count;
        HandyControl.Controls.Growl.Info($"已解析{cuc}个股卡{(cuc < cnt ? $"，失败{failed}个，{cnt - cuc}个不属于本产品" : "")}");
    }


    //private void FindFundAndAdd(SecurityCard sa)
    //{
    //    using (var db = DbHelper.Base())
    //    {
    //        var name = sa.Name.Split('-', '－').Last();
    //        var (fund, _) = db.FindByName(name);
    //        sa.FundId = fund?.Id ?? 0;
    //        sa.FundCode = fund?.Code;
    //        db.GetCollection<SecurityCard>().Upsert(sa);

    //        if (fund is null) LogEx.Error($"股卡 {sa.CardNo} 的基金 {sa.Name} 不在库中");
    //    }
    //}




    [RelayCommand]
    public void AddStock()
    {
        ShowChooseStockCompany = true;

        ChooseStockCompanyEvent.Reset();
        SelectedSecurityCompany = null;


        Task.Run(() =>
        {
            ChooseStockCompanyEvent.WaitOne();

            ShowChooseStockCompany = false;

            if (!string.IsNullOrWhiteSpace(SelectedSecurityCompany))
                CreateStockAccount(SelectedSecurityCompany.Trim());

        });
    }

    [RelayCommand]
    public void DeleteStock(StockAccountViewModel v)
    {
        if (HandyControl.Controls.MessageBox.Ask($"确认删除{v.Company}吗") == MessageBoxResult.Cancel)
            return;

        using var db = DbHelper.Base();
        db.GetCollection<StockAccount>().Delete(v.Id);
        StockAccounts?.Remove(v);
    }


    #region Stock

    private void CreateStockAccount(string selectedSecurityCompany)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var obj = new StockAccount
            {
                FundId = FundId,
                Company = selectedSecurityCompany,
                Common = new OpenAccountEvent { Name = "基本账户" },
               // Credit = new OpenAccountEvent { Name = "信用账户" }
            };

            using var db = DbHelper.Base();
            db.GetCollection<StockAccount>().Insert(obj);


            if (StockAccounts is null)
                StockAccounts = [new StockAccountViewModel(obj)];
            else
                StockAccounts.Add(new StockAccountViewModel(obj));
        });
    }

    partial void OnShowChooseStockCompanyChanged(bool value)
    {
        if (!value)
        {
            ChooseStockCompanyEvent.Set();
        }
    }

    partial void OnSelectedSecurityCompanyChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ChooseStockCompanyEvent.Set();
    }


    partial void OnSecurityCompanyKeywordChanged(string? value)
    {
        SecurityCompanies.View.Refresh();
    }
    #endregion


    [RelayCommand]
    public void AddFuture()
    {
        ShowChooseFutureCompany = true;

        ChooseFutureCompanyEvent.Reset();
        SelectedFutureCompany = null;


        Task.Run(() =>
        {
            ChooseFutureCompanyEvent.WaitOne();

            ShowChooseFutureCompany = false;

            if (!string.IsNullOrWhiteSpace(SelectedFutureCompany))
                CreateFutureAccount(SelectedFutureCompany.Trim());

        });
    }

    [RelayCommand]
    public void DeleteFuture(FutureAccountViewModel v)
    {
        using var db = DbHelper.Base();
        db.GetCollection<FutureAccount>().Delete(v.Id);
        FutureAccounts?.Remove(v);
    }


    #region Future

    private void CreateFutureAccount(string selectedFutureCompany)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var obj = new FutureAccount
            {
                FundId = FundId,
                Company = selectedFutureCompany,
                Common = new OpenAccountEvent { Name = "基本账户" }
            };

            using var db = DbHelper.Base();
            db.GetCollection<FutureAccount>().Insert(obj);


            if (FutureAccounts is null)
                FutureAccounts = [new FutureAccountViewModel(obj)];
            else
                FutureAccounts.Add(new FutureAccountViewModel(obj));
        });
    }

    partial void OnShowChooseFutureCompanyChanged(bool value)
    {
        if (!value)
        {
            ChooseFutureCompanyEvent.Set();
        }
    }

    partial void OnSelectedFutureCompanyChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ChooseFutureCompanyEvent.Set();
    }


    partial void OnFutureCompanyKeywordChanged(string? value)
    {
        FutureCompanies.View.Refresh();
    }
    #endregion




}


public partial class FundSingletonAccountsViewModel : EditableControlViewModelBase<FundSingletonAccounts>
{
    public ChangeableViewModel<FundSingletonAccounts, string?> UniversalNo { get; }
    public ChangeableViewModel<FundSingletonAccounts, string?> FutureNo { get; }

    public ChangeableViewModel<FundSingletonAccounts, string?> CCDCBondAccount { get; }

    public ChangeableViewModel<FundSingletonAccounts, string?> SHCBondAccount { get; }


    public FundSingletonAccountsViewModel(FundSingletonAccounts obj)
    {
        Id = obj.Id;

        UniversalNo = new()
        {
            Label = "证券一码通",
            InitFunc = x => x.UniversalNo,
            UpdateFunc = (x, y) => x.UniversalNo = y,
            ClearFunc = x => x.UniversalNo = null
        };
        UniversalNo.Init(obj);

        FutureNo = new()
        {
            Label = "统一开户编码",
            InitFunc = x => x.FutureNo,
            UpdateFunc = (x, y) => x.FutureNo = y,
            ClearFunc = x => x.FutureNo = null
        };
        FutureNo.Init(obj);

        CCDCBondAccount = new()
        {
            Label = "中债登债券账户",
            InitFunc = x => x.CCDCBondAccount,
            UpdateFunc = (x, y) => x.CCDCBondAccount = y,
            ClearFunc = x => x.CCDCBondAccount = null
        };
        CCDCBondAccount.Init(obj);

        SHCBondAccount = new()
        {
            Label = "上清所债券账户",
            InitFunc = x => x.SHCBondAccount,
            UpdateFunc = (x, y) => x.SHCBondAccount = y,
            ClearFunc = x => x.SHCBondAccount = null
        };
        SHCBondAccount.Init(obj);
    }


    protected override FundSingletonAccounts InitNewEntity()
    {
        return new FundSingletonAccounts { Id = Id };
    }
}