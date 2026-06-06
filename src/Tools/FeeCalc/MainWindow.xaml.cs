using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExcelDataReader;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Growl = HandyControl.Controls.Growl;
using MessageBox = HandyControl.Controls.MessageBox;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum MonthQuarter
{
    [Description("1月")] January = 1,
    [Description("2月")] February,
    [Description("3月")] March,
    [Description("4月")] April,
    [Description("5月")] May,
    [Description("6月")] June,
    [Description("7月")] July,
    [Description("8月")] August,
    [Description("9月")] September,
    [Description("10月")] October,
    [Description("11月")] November,
    [Description("12月")] December,
    [Description("一季度")] Q1,
    [Description("二季度")] Q2,
    [Description("三季度")] Q3,
    [Description("四季度")] Q4
}

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial FundInfo[]? Funds { get; set; }

    public MonthQuarter[] MonthQuarters { get; } = (MonthQuarter[])Enum.GetValues(typeof(MonthQuarter));

    [ObservableProperty]
    public partial DateTime? Begin { get; set; }

    [ObservableProperty]
    public partial DateTime? End { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }


    [ObservableProperty]
    public partial int Year { get; set; }

    public int[] Years { get; } = Enumerable.Range(2000, DateTime.Today.Year - 1999).Reverse().ToArray();


    public bool CanCalc => Funds?.Any(x => x.IsChoosed) ?? false;


    Debouncer debouncer;

    LiteDatabase FeeDB { get; }

    public MainWindowViewModel()
    {

        try
        {
            FeeDB = new LiteDatabase(@$"FileName=data\feecalc.db;Password={MachineCodeHelper.GetMachineCode()};Connection=Shared");
            _ = FeeDB.GetCollectionNames();
        }
        catch
        {
            File.Delete("data\\feecalc.db");
            FeeDB = new LiteDatabase(@$"FileName=data\feecalc.db;Password={MachineCodeHelper.GetMachineCode()};Connection=Shared");
        }

        // 同步数据
        using var db = DbHelper.Base();
        FeeDB.GetCollection<Fund>().Upsert(db.GetCollection<Fund>().FindAll().ToArray());
        FeeDB.GetCollection<FundShares>().Upsert(db.GetCollection<FundShares>().FindAll().ToArray());
        FeeDB.GetCollection<Investor>().Upsert(db.GetCollection<Investor>().FindAll().ToArray());
        FeeDB.GetCollection<TransferRecord>().Upsert(db.GetCollection<TransferRecord>().FindAll().ToArray());
        FeeDB.GetCollection<FundDailyFee>().Upsert(db.GetCollection<FundDailyFee>().FindAll().ToArray());


        debouncer = new Debouncer(Update);


        //var files = new DirectoryInfo("plugins").GetFiles("*.dll");


        //foreach (var file in files)
        //{
        //    try
        //    {
        //        var assembly = Assembly.LoadFile(file.FullName);
        //        TryAddTrustee(assembly);
        //    }
        //    catch (Exception e)
        //    {

        //    }
        //}
    }

    partial void OnBeginChanged(DateTime? value)
    {
        debouncer.Invoke();
    }

    partial void OnEndChanged(DateTime? value)
    {
        debouncer.Invoke();
    }


    private void Update()
    {
        Funds = FeeDB.GetCollection<Fund>().FindAll().Where(x => x.Status <= FundStatus.StartLiquidation || Begin switch { DateTime d => x.ClearDate > DateOnly.FromDateTime(d), _ => true }).Select(x => new FundInfo { Fund = x, FeeDB = FeeDB }).ToArray();
        FeeDB.GetCollection<TransferRecord>().Upsert(FeeDB.GetCollection<TransferRecord>().FindAll().ToArray());

        var end = DateOnly.FromDateTime(End!.Value);
        List<DateOnly> dates = new List<DateOnly>();
        dates.Add(DateOnly.FromDateTime(Begin!.Value));
        while (dates[^1] < end)
            dates.Add(dates[^1].AddDays(1));

        foreach (var f in Funds)
        {
            f.CheckData(dates, DateOnly.FromDateTime(Begin!.Value), DateOnly.FromDateTime(End!.Value));
            f.PropertyChanged += (s, e) => Application.Current.Dispatcher.BeginInvoke(() => CalcCommand.NotifyCanExecuteChanged());// OnPropertyChanged(nameof(CanCalc));
        }
    }

    private void Check()
    {
        if (Funds is not { Length: > 0 }) return;

        var end = DateOnly.FromDateTime(End!.Value);
        List<DateOnly> dates = new List<DateOnly>();
        dates.Add(DateOnly.FromDateTime(Begin!.Value));
        while (dates[^1] < end)
            dates.Add(dates[^1].AddDays(1));

        foreach (var f in Funds)
        {
            f.CheckData(dates, DateOnly.FromDateTime(Begin!.Value), DateOnly.FromDateTime(End!.Value));
            f.PropertyChanged += (s, e) => Application.Current.Dispatcher.BeginInvoke(() => CalcCommand.NotifyCanExecuteChanged());// OnPropertyChanged(nameof(CanCalc));
        }
    }


    [RelayCommand]
    public void SetDateRange(MonthQuarter d)
    {
        switch (d)
        {
            case MonthQuarter.January:
                Begin = new DateTime(Year, 1, 1);
                End = new DateTime(Year, 1, 31);
                break;
            case MonthQuarter.February:
                Begin = new DateTime(Year, 2, 1);
                End = new DateTime(Year, 3, 1).AddDays(-1);
                break;
            case MonthQuarter.March:
            case MonthQuarter.May:
            case MonthQuarter.July:
            case MonthQuarter.August:
            case MonthQuarter.October:
            case MonthQuarter.December:
                Begin = new DateTime(Year, (int)d, 1);
                End = new DateTime(Year, (int)d, 31);
                break;
            case MonthQuarter.April:
            case MonthQuarter.June:
            case MonthQuarter.September:
            case MonthQuarter.November:
                Begin = new DateTime(Year, (int)d, 1);
                End = new DateTime(Year, (int)d, 30);
                break;
            case MonthQuarter.Q1:
                Begin = new DateTime(Year, 1, 1);
                End = new DateTime(Year, 3, 31);
                break;
            case MonthQuarter.Q2:
                Begin = new DateTime(Year, 4, 1);
                End = new DateTime(Year, 6, 30);
                break;
            case MonthQuarter.Q3:
                Begin = new DateTime(Year, 7, 1);
                End = new DateTime(Year, 9, 30);
                break;
            case MonthQuarter.Q4:
                Begin = new DateTime(Year, 10, 1);
                End = new DateTime(Year, 12, 31);
                break;
            default:
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCalc))]
    public void Calc()
    {
        if (Funds is null) return;

        IsWorking = true;

        // 生成日期列表
        var end = DateOnly.FromDateTime(End!.Value);
        List<DateOnly> dates = new List<DateOnly>();
        dates.Add(DateOnly.FromDateTime(Begin!.Value));
        while (dates[^1] < end)
            dates.Add(dates[^1].AddDays(1));

        Task.Run(() =>
        {
            var sel = Funds.Where(x => x.IsChoosed).GroupBy(x => x.Fund.Trustee);


            Parallel.ForEach(sel, async f =>
           {
               await Calc(f!, dates);
           });

            IsWorking = false;
        });

    }


    [RelayCommand]
    public void ImportFeeData()
    {
        var dlg = new OpenFileDialog();
        dlg.Title = "请选择每日费用明细";
        dlg.Filter = "Excel|*.xls;*.xlsx";
        if (dlg.ShowDialog() switch { false or null => true, _ => false }) return;

        var file = dlg.FileName;
        using var fs = new FileStream(file, FileMode.Open);
        var read = ExcelDataReader.ExcelReaderFactory.CreateReader(fs);

        // 获取表头
        read.Read();
        int iCode = -1, iDate = -1, iFee = -1, iShare = -1;
        for (int i = 0; i < read.FieldCount; i++)
        {
            var head = read.GetString(i);

            if (iCode == -1 && Regex.IsMatch(head, "产品代码"))
                iCode = i;

            if (iDate == -1 && Regex.IsMatch(head, "费用日期|业务日期"))
                iDate = i;

            if (iFee == -1 && Regex.IsMatch(head, "管理费.*?计提"))
                iFee = i;

            if (iShare == -1 && Regex.IsMatch(head, "总.*?份额"))
                iShare = i;
        }

        if (iCode == -1 || iDate == -1 || iFee == -1)
        {
            MessageBox.Show("无法识别表格，请设置表头为 费用日期 产品代码 管理费计提 总份额");
            return;
        }

        List<(string Code, ManageFeeDetail Fee)> fees = new(read.RowCount);
        while (read.Read())
        {
            var datestr = read.GetValue(iDate).ToString();
            if (!DateTimeHelper.TryParse(datestr, out var date))
            {
                if (fees.Count < 2)
                {
                    MessageBox.Show($"无法识别的日期格式：{datestr}");
                    return;
                }
                else continue;
            }

            var v = read.GetValue(iFee);
            var fee = v switch { double d => (decimal)d, decimal d => d, string s => decimal.TryParse(s, out var d) ? d : -1, _ => -1 };
            if (fee < 0)
            {
                MessageBox.Show($"无法识别的费用：{v}");
                return;
            }

            v = iShare > -1 ? read.GetValue(iShare) : -1;
            var share = v switch { double d => (decimal)d, decimal d => d, string s => decimal.TryParse(s, out var d) ? d : -1, _ => -1 };

            fees.Add((read.GetString(iCode).Trim(), new ManageFeeDetail(date.DayNumber, date, fee, share)));
        }

        // 保存
        using var fdb = DbHelper.Base();
        var funds = fdb.GetCollection<Fund>().FindAll().ToArray();

        foreach (var f in fees.GroupBy(x => x.Code))
        {
            var fund = funds.FirstOrDefault(x => x.Code == f.Key);
            if (fund is null) continue;

            var old = FeeDB.GetCollection<ManageFeeDetail>($"f{fund.Id}").FindAll().OrderBy(x => x.Date).ToList();
            foreach (var n in f.Select(x => x.Fee))
            {
                var v = old.FirstOrDefault(x => x.Date == n.Date);
                if (v is not null) v = v with { Fee = n.Fee, Share = n.Share };
                else old.Add(n);
            }

            FeeDB.GetCollection<ManageFeeDetail>($"f{fund.Id}").Upsert(old);
        }


        Growl.Success("导入成功");
        Check();
    }



    [RelayCommand]
    public void ImportTAData()
    {

        var dlg = new OpenFileDialog
        {
            Title = "请选择基金从成立至今的交易确认明细",
            Filter = "Excel|*.xls;*.xlsx",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;

        var file = dlg.FileName;

        // 表头下标（证件号不再是必填，可为-1）
        int idxFundCode = -1;        //产品代码→FundCode（必填）
        int idxInvestorName = -1;    //投资人名称→InvestorName（必填，无证件时必须有）
        int idxInvestorIdCard = -1;  //证件号码→InvestorIdentity（可选）
        int idxConfirmDate = -1;     //确认日期→ConfirmedDate（必填）
        int idxBusinessType = -1;    //业务类型→Type(枚举)（必填）
        int idxConfirmedShare = -1;  //确认份额→ConfirmedShare（必填）
        int idxPerformace = -1;

        var regexOpt = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var read = ExcelReaderFactory.CreateReader(fs);

        if (!read.Read())
        {
            Growl.Error("Excel无表头数据");
            return;
        }

        //遍历表头匹配（证件号列无匹配也不报错）
        for (int i = 0; i < read.FieldCount; i++)
        {
            var head = read.GetString(i)?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(head)) continue;

            if (idxFundCode == -1 && Regex.IsMatch(head, @"产品代码", regexOpt)) idxFundCode = i;
            if (idxInvestorName == -1 && Regex.IsMatch(head, @"投资人名称|客户名称", regexOpt)) idxInvestorName = i;
            if (idxInvestorIdCard == -1 && Regex.IsMatch(head, @"证件号码", regexOpt)) idxInvestorIdCard = i;
            if (idxConfirmDate == -1 && Regex.IsMatch(head, @"确认日期", regexOpt)) idxConfirmDate = i;
            if (idxBusinessType == -1 && Regex.IsMatch(head, @"业务类型", regexOpt)) idxBusinessType = i;
            if (idxConfirmedShare == -1 && Regex.IsMatch(head, @"确认份额", regexOpt)) idxConfirmedShare = i;
            if (idxPerformace == -1 && Regex.IsMatch(head, @"业绩报酬", regexOpt)) idxPerformace = i;
        }

        //必填字段校验（移除证件号码，仅校验核心必填项）
        List<string> miss = new();
        if (idxFundCode == -1) miss.Add("产品代码");
        if (idxInvestorName == -1) miss.Add("投资人名称/客户名称");
        if (idxConfirmDate == -1) miss.Add("确认日期");
        if (idxBusinessType == -1) miss.Add("业务类型");
        if (idxConfirmedShare == -1) miss.Add("确认份额");
        if (idxPerformace == -1) miss.Add("业绩报酬");

        if (miss.Any())
        {
            Growl.Error($"缺失必填表头：{string.Join("、", miss)}");
            return;
        }

        //【业务类型文本→枚举映射】根据你实际Excel业务名称补充
        Dictionary<string, TransferRecordType> typeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        {"基金成立",TransferRecordType.Subscription},
        {"申购",TransferRecordType.Subscription},
        {"认购",TransferRecordType.Purchase},
        {"赎回",TransferRecordType.Redemption},
        {"认购结果",TransferRecordType.Purchase},
        {"申购确认",TransferRecordType.Subscription},
        {"认购确认",TransferRecordType.Purchase},
        {"赎回确认",TransferRecordType.Redemption},
        {"强制赎回",TransferRecordType.ForceRedemption},
        {"红利再投",TransferRecordType.Distribution},
        {"基金分红",TransferRecordType.Distribution},
        {"分红",TransferRecordType.Distribution},
        {"分红确认",TransferRecordType.Distribution},
        {"分红方式变更",TransferRecordType.BonusType},
        {"设置分红方式",TransferRecordType.BonusType},
        {"份额增加",TransferRecordType.Increase},
        {"份额调增",TransferRecordType.Increase},
        {"份额减少",TransferRecordType.Decrease},
        {"份额调减",TransferRecordType.Decrease},
    };

        // 基金字典：按产品代码匹配
        var fundDic = FeeDB.GetCollection<Fund>().FindAll().ToDictionary(k => k.Code.Trim(), v => v.Id, StringComparer.OrdinalIgnoreCase);
        var shareDic = FeeDB.GetCollection<FundShares>().FindAll().Select(x => x.Shares.Select(y => (y.FundCode, x.FundId))).SelectMany(x => x).DistinctBy(x => x.FundCode).ToDictionary(k => k.FundCode, v => v.FundId);

        // 投资人字典1：优先按证件号匹配（和原逻辑一致）
        var invCardDic = FeeDB.GetCollection<Investor>().FindAll()
            .Where(x => x.Identity is not null && !string.IsNullOrWhiteSpace(x.Identity.Id))
            .ToDictionary(k => k.Identity!.Id.Trim(), v => v.Id, StringComparer.OrdinalIgnoreCase);
        // 投资人字典2：兜底按名称匹配（同名称取第一条，避免重复键异常）
        var invNameDic = FeeDB.GetCollection<Investor>().FindAll()
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var invCol = FeeDB.GetCollection<Investor>();
        int maxInvId = invCol.Max(x => (int?)x.Id) ?? 0;


        List<TransferRecord> saveList = new();
        int rowNo = 1;
        int success = 0, skip = 0, err = 0;
        List<ImportItemInfo> errLog = new();

        while (read.Read())
        {
            rowNo++;
            try
            {
                // 文本字段读取：代码、名称、证件、业务类型、日期文本
                string SafeStr(int idx)
                {
                    if (read.IsDBNull(idx)) return string.Empty;
                    var val = read.GetValue(idx);
                    return val?.ToString()?.Trim() ?? "";
                }

                // 数值专用：直接decimal，避免字符串中转
                decimal SafeDecimal(int idx)
                {
                    if (read.IsDBNull(idx)) return 0m;
                    var val = read.GetValue(idx);
                    if (decimal.TryParse(val.ToString(), out var d))
                        return d;
                    return 0m;
                }/// <summary>
                 /// 兼容 yyyyMMdd / yyyy-MM-dd / yyyy/MM/dd
                 /// </summary>
                bool SafeParseDate(string dateText, out DateOnly result)
                {
                    result = default;
                    if (string.IsNullOrWhiteSpace(dateText)) return false;

                    // 优先 8位纯数字 yyyyMMdd
                    if (dateText.Length == 8 && dateText.All(char.IsDigit))
                    {
                        if (DateOnly.TryParseExact(dateText, "yyyyMMdd", out var dt))
                        {
                            result = dt;
                            return true;
                        }
                    }

                    // 常规 - / 分隔
                    string[] fmt = { "yyyy-MM-dd", "yyyy/MM/dd" };
                    return DateOnly.TryParseExact(dateText, fmt, out result)
                        || DateOnly.TryParse(dateText, out result);
                }


                // 文本字段
                string fundCode = SafeStr(idxFundCode);
                string invName = SafeStr(idxInvestorName);
                string invIdCard = idxInvestorIdCard == -1 ? "" : SafeStr(idxInvestorIdCard);
                string confirmDateStr = SafeStr(idxConfirmDate);
                string bizTypeName = SafeStr(idxBusinessType);

                // 【关键】份额直接读出decimal，不再走string→decimal二次解析
                decimal confirmShare = SafeDecimal(idxConfirmedShare);
                decimal performace = SafeDecimal(idxPerformace);
                string confirmShareStr = confirmShare.ToString(); // 仅日志报错用

                if (string.IsNullOrWhiteSpace(fundCode) || string.IsNullOrWhiteSpace(confirmDateStr) || string.IsNullOrWhiteSpace(bizTypeName))
                {
                    skip++;
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Skip, "产品代码/确认日期/业务类型为空，已跳过"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(invIdCard) && string.IsNullOrWhiteSpace(invName))
                {
                    skip++;
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Skip, "证件号码和投资人名称均为空，无法匹配客户，已跳过"));
                    continue;
                }

                if (!SafeParseDate(confirmDateStr, out var confirmDate))
                {
                    err++;
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Error, $"确认日期[{confirmDateStr}]格式错误，支持格式：yyyyMMdd、yyyy-MM-dd、yyyy/MM/dd"));
                    continue;
                }

                if (!typeMap.TryGetValue(bizTypeName, out var recordType))
                {
                    err++;
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Error, $"未知业务类型[{bizTypeName}]"));
                    continue;
                }


                decimal reqShare = 0, reqAmt = 0, confirmAmt = 0;
                DateOnly reqDate = confirmDate;
                string? agency = null;

                if (!fundDic.TryGetValue(fundCode, out var fundId) && !shareDic.TryGetValue(fundCode, out fundId))
                {
                    err++;
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Error, $"【{fundCode}】不在基金列表中，跳过本条记录"));
                    continue;
                }

                int invId = 0;
                //优先证件匹配
                if (!string.IsNullOrWhiteSpace(invIdCard) && invCardDic.TryGetValue(invIdCard, out var idByCard))
                {
                    invId = idByCard;
                }
                //其次名称匹配（使用项目自带IsNamePair名称去符号比对）
                else if (!string.IsNullOrWhiteSpace(invName))
                {
                    //遍历字典用IsNamePair模糊匹配（适配项目名称去括号空格规则）
                    int? tempId = null;
                    foreach (var kv in invNameDic)
                    {
                        if (Investor.IsNamePair(kv.Key, invName))
                        {
                            tempId = kv.Value;
                            break;
                        }
                    }
                    if (tempId.HasValue)
                    {
                        invId = tempId.Value;
                    }
                    else
                    {
                        //证件+名称都没匹配：自动新增投资人
                        maxInvId += 1;
                        var newInv = new Investor
                        {
                            Id = maxInvId,
                            Name = invName,
                            CreateTime = DateTime.Now,
                            //证件有值就赋值Identity，无则Identity=null
                            Identity = string.IsNullOrWhiteSpace(invIdCard) ? null : new Identity { Id = invIdCard },
                            //其余字段自动取构造默认值
                        };
                        invCol.Insert(newInv);

                        //同步更新内存字典，本批次后续行复用
                        invId = maxInvId;
                        if (!string.IsNullOrWhiteSpace(invIdCard))
                            invCardDic[invIdCard] = invId;
                        invNameDic[invName.Trim()] = invId;

                        //自动新增客户用Info提示日志
                        errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Info, $"客户[{invName}]证件[{invIdCard}]系统不存在，已自动新增投资人(Id:{invId})"));
                    }
                }
                else
                {
                    //名称为空无法新建，保留0
                    errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Error, "客户名称为空无法创建投资人，InvestorId=0"));
                }

                var item = new TransferRecord
                {
                    FundId = fundId,
                    FundCode = fundCode,
                    ShareCode = fundCode,
                    InvestorId = invId,
                    InvestorName = invName,
                    InvestorIdentity = invIdCard,
                    ConfirmedDate = confirmDate,
                    RequestDate = reqDate,
                    Type = recordType,

                    RequestShare = reqShare,
                    RequestAmount = reqAmt,
                    ConfirmedShare = confirmShare, // 直接赋值
                    ConfirmedAmount = confirmAmt,
                    PerformanceFee = performace,

                    Agency = agency,
                    Source = "TAExcel导入",
                    IsLiquidating = false,
                    Background = false,
                    IsFailed = false,
                    ExternalId = null,
                    ExternalRequestId = null,
                };

                saveList.Add(item);
                success++;
            }
            catch (Exception ex)
            {
                err++;
                errLog.Add(new ImportItemInfo(rowNo, ImportItemType.Error, $"行异常：{ex.Message}"));
            }
        }

        //批量入库（保留原逻辑：先删同基金旧数据，再Upsert）
        if (saveList.Count > 0)
        {
            var col = FeeDB.GetCollection<TransferRecord>();

            // 仅删除有匹配基金的旧数据，避免误删FundId=0的无效数据
            var validFundIds = saveList.Where(x => x.FundId != 0).Select(x => new BsonValue(x.FundId)).Distinct().ToList();
            if (validFundIds.Any())
            {
                col.DeleteMany(Query.In(nameof(TransferRecord.FundId), validFundIds));
            }

            col.Upsert(saveList);
        }

        Check();

        //结果弹窗 
        if (errLog.Any())
        {
            var wnd = new TipWindow()
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false,
                Owner = App.Current.MainWindow,
                DataContext = new TipWindowViewModel { Items = errLog.ToArray() },
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            wnd.ShowDialog();
        }
        else Growl.Success("导入成功");

    }

    [RelayCommand]
    public async Task SyncByAPI()
    {
        if (Begin is null || End is null) return;

        var funds = FeeDB.GetCollection<Fund>().FindAll().ToArray();

        foreach (var t in TrusteeGallay.Trustees)
        {
            if (!t.IsValid) continue;

            var rc = await t.QueryFundDailyFee(DateOnly.FromDateTime(Begin.Value), DateOnly.FromDateTime(End.Value));

            // 保存数据库 
            if (rc.Data is not null)
            {
                // 对齐Fund
                foreach (var f in rc.Data)
                {
                    f.FundId = funds.FirstOrDefault(x => x.Code == f.FundCode)?.Id ?? 0;
                }

                FeeDB.GetCollection<FundDailyFee>().Upsert(rc.Data);
            }
        }

        debouncer.Invoke();
    }


    [RelayCommand]
    public void SetAlloc()
    {
        var wnd = new AllocWindow()
        {
            Owner = App.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            DataContext = new AllocWindowViewModel(FeeDB)
        };
        wnd.ShowDialog();
    }



    private async Task Calc(IGrouping<string, FundInfo> col, List<DateOnly> dates)
    {
        foreach (var f in col)
        {
            f.IsWorking = true;
            var begin = dates[0];
            var end = dates[^1];

            // var dc = db.GetDailyCollection(f.Fund.Id);
            // var fees = db.GetCollection<FundDailyFee>().Find(x => x.FundId == f.Fund.Id && x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).Select(x => new ManageFeeDetail(0, x.Date, x.ManagerFeeAccrued, dc.FindOne(y=>y.Date == x.Date)?.Share??0)).ToList();

            var fees = FeeDB.GetCollection<ManageFeeDetail>($"f{f.Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).ToList();
            var fdate = fees.Select(x => x.Date).ToArray();


            // 检验份额



            // 再次核验日期
            if (!dates.SequenceEqual(fdate))
            {
                f.Error = "没有完整的费用数据";
                f.IsWorking = false;
                continue;
            }

            Calc(f, fees, dates);
        }
    }

    private void Calc(FundInfo f, List<ManageFeeDetail> fees, List<DateOnly> dates)
    {
        try
        {
            var (dd, ids, names, array) = GenerateShareSheet(f.Fund.Id, dates[0], dates[^1]);

            // 份额一致
            bool sharepair = true;
            int joff = 5;
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("每日明细表");

                sheet.Cell(1, 1).Value = "日期";
                sheet.Cell(1, 2).Value = "每日管理费";
                sheet.Cell(1, 3).Value = "上日总份额";
                // 客户名
                for (int i = 0; i < ids.Count; i++)
                    sheet.Cell(1, i + joff).Value = names[i];

                for (var i = 0; i < dd.Count; i++)
                {
                    sheet.Cell(2 + i, 1).Value = dd[i].ToString("yyyy-MM-dd");

                    // 费用和份额
                    sheet.Cell(i + 2, 2).Value = fees[i].Fee;
                    if (i > 0)
                        sheet.Cell(i + 2, 3).Value = fees[i - 1].Share;

                    // 客户每日份额
                    decimal sum = 0;
                    for (var j = 0; j < ids.Count; j++)
                    {
                        sheet.Cell(i + 2, j + joff).Value = array[i, j] switch { 0 => 0, var d => d };
                        sum += array[i, j];
                    }

                    sheet.Cell(i + 2, 4).FormulaR1C1 = $"=sum(R{i + 2}C{joff}:R{i + 2}C{ids.Count + joff - 1})";

                    if (sharepair && i > 0 && sum != fees[i - 1].Share)
                        sharepair = false;

                    if (i > 0)
                        sheet.Cell(i + 2, 3).AddConditionalFormat().WhenIsTrue($"=C{2 + i}<>D{2 + i}").Fill.SetBackgroundColor(XLColor.Red);
                }

                if (!sharepair)
                {
                    sheet.Name += "份额不匹配";
                    sheet.SetTabColor(XLColor.Red);
                }
                var sn1 = sheet.Name;


                // 设置格式 
                // 设置整行单元格为居中对齐
                // 设置整行单元格自动换行
                var row = sheet.Row(1);
                row.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                row.Style.Alignment.WrapText = true;

                // 数字格式
                sheet.Range(2, 5, dates.Count + 2, ids.Count + joff).Style.Font.FontSize = 9;
                sheet.Range(2, 5, dates.Count + 2, ids.Count + joff).Style.NumberFormat.Format = "#,##0.00;-#,##0.00;";

                sheet.Column(1).Width = 14;
                sheet.Column(2).Width = 12;
                sheet.Column(3).Width = 14;
                sheet.Column(4).Width = 14;

                for (int i = joff; i < ids.Count + joff; i++)
                    sheet.Column(i).Width = 11;


                //表2 
                ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
                sheet = workbook.AddWorksheet("费用明细表", 0);
                sheet.Cell(1, 1).Value = "日期";
                sheet.Cell(1, 2).Value = "每日管理费";

                // 客户名
                for (int i = 0; i < ids.Count; i++)
                    sheet.Cell(1, i + joff).Value = names[i];

                for (var i = 0; i < dd.Count; i++)
                {
                    sheet.Cell(2 + i, 1).Value = dd[i].ToString("yyyy-MM-dd");

                    // 费用 
                    sheet.Cell(i + 2, 2).Value = fees[i].Fee;

                    // 客户每日费用
                    for (var j = 0; j < ids.Count; j++)
                        sheet.Cell(i + 2, j + joff).FormulaR1C1 = $"=R{i + 2}C2 * ({sn1}!R{i + 2}C{j + joff}/{sn1}!R{i + 2}C4)";

                }

                //sum
                sheet.Cell(dates.Count + 2, 1).Value = "汇总";
                sheet.Cell(dates.Count + 2, 2).FormulaR1C1 = $"SUM(R2C2:R{dates.Count + 1}C2)";
                sheet.Row(dates.Count + 2).Height = 12;

                for (var j = 0; j < ids.Count; j++)
                    sheet.Cell(dates.Count + 2, j + joff).FormulaR1C1 = $"SUM(R2C{j + joff}:R{dates.Count + 1}C{j + joff})";
                sheet.Row(dates.Count + 2).Style.Fill.BackgroundColor = XLColor.LightGray;

                // 数字格式
                sheet.Range(2, joff, dates.Count + 2, ids.Count + joff).Style.Font.FontSize = 9;
                sheet.Range(2, joff, dates.Count + 2, ids.Count + joff).Style.NumberFormat.Format = "#,##0.00;-#,##0.00;";
                // 设置格式 
                // 设置整行单元格为居中对齐
                // 设置整行单元格自动换行
                row = sheet.Row(1);
                row.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                row.Style.Alignment.WrapText = true;

                sheet.Column(1).Width = 14;
                sheet.Column(2).Width = 12;
                ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
                ///表3

                var sheet3 = workbook.AddWorksheet("分成表", 0);
                sheet3.Range(1, 1, 2, 1).Value = "投资人";
                sheet3.Range(1, 1, 2, 1).Merge();
                sheet3.Range(1, 3, 2, 3).Value = "管理费";
                sheet3.Range(1, 3, 2, 3).Merge();
                int rowst = 3;
                int ar = 3;
                List<int> hasFeeIds = new();
                for (int j = 0; j < ids.Count; j++)
                {
                    if (sheet.Cell(dates.Count + 2, j + joff).Value.GetNumber() == 0) continue;

                    hasFeeIds.Add(ids[j]);
                    // 客户
                    sheet3.Cell(ar, 1).Value = sheet.Cell(1, j + joff).Value;
                    // 
                    sheet3.Cell(ar, 3).FormulaR1C1 = $"费用明细表!R{dates.Count + 2}C{j + joff}";

                    ++ar;
                }

                string numfmt = "_ * #,##0.00_ ;_ * -#,##0.00_ ;_ * \"-\"_ ;_ @_ ";

                sheet3.Range(1, 1, ar, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet3.Range(1, 1, ar, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet3.Column(1).Width = 30;
                sheet3.Column(3).Width = 20;
                sheet3.Column(3).Style.NumberFormat.Format = numfmt;

                // 写入业绩报酬 
                int rowSum = ar;
                var per = FeeDB.GetCollection<TransferRecord>().Find(x => x.FundId == f.Fund.Id && x.PerformanceFee > 0).Where(x => x.RequestDate >= dd[0] && x.RequestDate <= dd[^1]).ToArray();


                // 业绩报酬 
                // 分红
                int colst = 4, colsCarry = 0, colref = 0, colref2 = 0;
                var groupDist = per.Where(x => x.Type == TransferRecordType.Distribution).GroupBy(x => x.ConfirmedDate).ToArray();
                if (groupDist.Length > 0)
                {
                    colref = colst;
                    colsCarry += groupDist.Length;
                    for (int j = 0; j < groupDist.Length; j++)
                    {
                        var date = groupDist[j].Key;

                        // 头
                        sheet3.Cell(2, colst + j).Value = $"{date:yyyy-MM-dd}";
                        sheet3.Range(1, colst + j, 2, colst + j).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        foreach (var t in groupDist[j].GroupBy(x => x.InvestorId))
                        {
                            var idx = hasFeeIds.IndexOf(t.Key);
                            sheet3.Cell(idx + rowst, colst + j).Value = t.Sum(x => x.PerformanceFee);

                        }

                        sheet3.Column(colst + j).Width = 20;
                        sheet3.Column(colst + j).Style.NumberFormat.Format = "0.00";

                        // 校验
                        var condf = sheet3.Cell(rowSum, colst + j).AddConditionalFormat();

                        // 使用公式：注意以 C2 为基准（区域左上角）
                        condf.WhenNotEquals((double)groupDist[j].Sum(x => x.PerformanceFee))
                            .Fill.SetBackgroundColor(XLColor.Red);
                    }
                    // 分红头
                    sheet3.Cell(1, colst).Value = "业绩报酬（分红）";
                    if (groupDist.Length > 1) // 加入汇总
                    {
                        colref = colst + colsCarry;
                        colsCarry += 1;

                        // 合计 
                        sheet3.Cell(1, groupDist.Length + 3).Value = "合计";
                        sheet3.Range(1, groupDist.Length + 3, 2, groupDist.Length + 3).Merge();
                        for (int i = rowst; i < ar; i++)
                            sheet3.Cell(i, groupDist.Length + 3).FormulaR1C1 = $"SUM(R{i}C{colst}:R{i}C{colst + groupDist.Length - 1})";


                        sheet3.Column(groupDist.Length + colst).Width = 20;
                        sheet3.Column(groupDist.Length + colst).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        sheet3.Column(groupDist.Length + colst).Style.NumberFormat.Format = numfmt;

                        sheet3.Range(1, colst, 2, colst + colsCarry).Merge();
                    }
                }

                ////////////////////////////////////////////////////
                // 赎回
                colst += colsCarry; colsCarry = 0;
                var groupRedem = per.Where(x => x.Type != TransferRecordType.Distribution).GroupBy(x => x.ConfirmedDate).ToArray();
                if (groupRedem.Length > 0)
                {
                    colref2 = colst;
                    colsCarry += groupRedem.Length;
                    for (int j = 0; j < groupRedem.Length; j++)
                    {
                        var date = groupRedem[j].Key;

                        // 头
                        sheet3.Cell(2, colst + j).Value = $"{date:yyyy-MM-dd}";
                        sheet3.Range(1, colst + j, 2, colst + j).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        foreach (var t in groupRedem[j].GroupBy(x => x.InvestorId))
                        {
                            var idx = hasFeeIds.IndexOf(t.Key);
                            sheet3.Cell(idx + rowst, colst + j).Value = t.Sum(x => x.PerformanceFee);

                        }

                        sheet3.Column(colst + j).Width = 16;
                        sheet3.Column(colst + j).Style.NumberFormat.Format = numfmt;

                        // 校验
                        var condf = sheet3.Cell(rowSum, j + colst).AddConditionalFormat();

                        // 使用公式：注意以 C2 为基准（区域左上角）
                        condf.WhenNotEquals((double)groupRedem[j].Sum(x => x.PerformanceFee))
                            .Fill.SetBackgroundColor(XLColor.Red);
                    }
                    // 头
                    sheet3.Cell(1, colst).Value = "业绩报酬（赎回）";
                    if (groupRedem.Length > 1) // 加入汇总
                    {
                        colref2 = colst + colsCarry;
                        colsCarry += 1;

                        // 合计 
                        sheet3.Cell(2, groupRedem.Length + colst).Value = "合计";
                        for (int i = rowst; i < ar; i++)
                            sheet3.Cell(i, groupRedem.Length + colst).FormulaR1C1 = $"SUM(R{i}C{colst}:R{i}C{colst + groupRedem.Length - 1})";

                        sheet3.Column(groupRedem.Length + colst).Width = 20;
                        sheet3.Column(groupRedem.Length + colst).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        sheet3.Column(groupRedem.Length + colst).Style.NumberFormat.Format = numfmt;

                        sheet3.Range(1, colst, 1, colst + groupRedem.Length).Merge();
                    }
                }

                ////////////////////////////////////////////////////////////


                // 合计 
                int colSumLast = colsCarry + colst - 1;

                sheet3.Cell(1, 2).Value = "费用合计";
                sheet3.Range(1, 2, 2, 2).Merge();
                for (int i = rowst; i < ar; i++)
                    sheet3.Cell(i, 2).FormulaR1C1 = $"R{i}C3+{(colref == 0 ? 0 : $"R{i}C{colref}")}+{(colref2 == 0 ? 0 : $"R{i}C{colref2}")}";//$"SUM(R{i}C2:R{i}C{2 - 1})";

                sheet3.Column(2).Width = 20;
                sheet3.Column(2).Style.NumberFormat.Format = numfmt;


                // 下方合计 
                sheet3.Cell(rowSum, 1).Value = "合计";
                for (int j = 2; j <= colSumLast; j++)
                    sheet3.Cell(rowSum, j).FormulaR1C1 = $"SUM(R{rowst}C{j}:R{rowSum - 1}C{j})";
                sheet3.Row(rowSum).Height = 40;


                ////// 首行
                sheet3.Range(1, 1, 1, colSumLast).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet3.Range(1, 1, 1, colSumLast).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet3.Range(1, 1, 1, colSumLast).Style.Font.FontSize = 14;
                sheet3.Row(1).Height = 20;

                sheet3.Range(2, 2, ar, colSumLast).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // 间隔行
                for (int i = rowst; i < ar; i += 2)
                    sheet3.Range(i, 1, i, colSumLast).Style.Fill.BackgroundColor = XLColor.LightGray;

                for (int i = rowst; i < ar; i++)
                    sheet3.Row(i).Height = 32;

                // 冻结首行
                sheet3.SheetView.FreezeRows(2);

                string path = $"files/fee/{f.Fund.ShortName}_{dates[0]:yyyy.MM.dd}-{dates[^1]:yyyy.MM.dd}.xlsx";
                workbook.SaveAs(path);

                System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath($"files/fee/"), UseShellExecute = true });
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
    }



    /// <summary>
    /// 生成每日份额表
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    public (IList<DateOnly> Dates, IList<int> InvestorIds, IList<string> Names, decimal[,] Data) GenerateShareSheet(int fundId, DateOnly begin, DateOnly end)
    {
        IEnumerable<TransferRecord> uncheck = FeeDB.GetCollection<TransferRecord>().Find(x => x.FundId == 0);
        if (uncheck.Count() > 0)
        {
            var funds = FeeDB.GetCollection<Fund>().FindAll().Select(x => new { x.Id, x.Code, x.Name }).ToArray();
            foreach (var item in uncheck)
            {
                item.FundId = (funds.FirstOrDefault(x => x.Code == item.FundCode) ?? funds.FirstOrDefault(x => x.Name == item.FundName))!.Id;
            }
            FeeDB.GetCollection<TransferRecord>().Update(uncheck);
        }

        // 计算份额表，排除已全部赎回的
        var data = FeeDB.GetCollection<TransferRecord>().Find(x => x.FundId == fundId).OrderBy(x => x.ConfirmedDate).ToList();
        data = data.GroupBy(x => x.InvestorId).Where(x => x.Max(y => y.ConfirmedDate) >= begin || x.Sum(y => y.ShareChange()) > 0).SelectMany(x => x).ToList();

        /// 生成行、列头
        List<DateOnly> dates = new List<DateOnly>();
        var idname = data.Select(x => (x.InvestorId, x.InvestorName)).DistinctBy(x => x.InvestorId);
        var ids = idname.Select(x => x.InvestorId).ToList();
        var names = idname.Select(x => x.InvestorName).ToList();

        var date = begin;
        while (date <= end)
        {
            dates.Add(date);
            date = date.AddDays(1);
        }

        var array = new decimal[dates.Count, ids.Count];

        Dictionary<DateOnly, Dictionary<int, decimal>> result = new();


        for (int i = 0; i < dates.Count; i++)
        {
            foreach (var d in data)
            {
                if (d.ConfirmedDate >= dates[i]) continue;

                var cid = ids.IndexOf(d.InvestorId);
                array[i, cid] += d.ShareChange();
            }
        }


        return (dates, ids, names, array);
    }
}


public partial class FundInfo : ObservableObject
{
    public required Fund Fund { get; set; }


    [ObservableProperty]
    public partial bool IsDataValid { get; set; }

    [ObservableProperty]
    public partial bool IsChoosed { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial string? Error { get; internal set; }

    public required LiteDatabase FeeDB { get; set; }

    public void CheckData(List<DateOnly> dates, DateOnly begin, DateOnly end)
    {
        // 从platform中同步
        using var pdb = DbHelper.Base();

        var fees = FeeDB.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).DistinctBy(x => x.Date).ToList();
        var fdate = fees.Select(x => x.Date).ToArray();

        IsDataValid = dates.SequenceEqual(fdate);
        if (!IsDataValid)
        {
            // 尝试从 中同步
            var data = pdb.GetCollection<FundDailyFee>().Find(x => x.FundId == Fund.Id).ToArray();
            var nvs = pdb.GetDailyCollection(Fund.Id).FindAll().OrderBy(x => x.Date).ToList();
            var fe = data.Select(x => new ManageFeeDetail(x.Date.DayNumber, x.Date, x.ManagerFeeAccrued, nvs.LastOrDefault(y => y.Date <= x.Date)?.Share ?? 0));

            // 保存
            FeeDB.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Upsert(fe);

        }

        // 再次加载
        fees = FeeDB.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).DistinctBy(x => x.Date).ToList();
        fdate = fees.Select(x => x.Date).ToArray();

        IsDataValid = dates.SequenceEqual(fdate);
        if (!IsDataValid)
            Error = $"费用数据{fdate.Length}个";

        // 检验份额
        List<DateOnly> unpair = new();
        var ta = pdb.GetCollection<TransferRecord>().Find(x => x.FundId == Fund.Id).ToArray();
        foreach (var item in fees)
        {
            var share = ta.Where(x => x.ConfirmedDate <= item.Date).Sum(x => x.ShareChange());
            if (item.Share != share)
                unpair.Add(item.Date);
        }
        if (unpair.Count > 0)
            Error += $"份额不一致：{string.Join('、', unpair)}";

    }
}


