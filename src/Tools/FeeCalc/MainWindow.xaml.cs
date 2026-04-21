using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Trustee;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

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


    public MainWindowViewModel()
    {
        debouncer = new Debouncer(() => Update());


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
        using var db = DbHelper.Base();
        Funds = db.GetCollection<Fund>().FindAll().Where(x => x.Status <= FundStatus.StartLiquidation || Begin switch { DateTime d => x.ClearDate > DateOnly.FromDateTime(d), _ => true }).Select(x => new FundInfo { Fund = x }).ToArray();


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
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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

        using var db = new LiteDatabase(@"FileName=data\feecalc.db;Connection=Shared");
        foreach (var f in fees.GroupBy(x => x.Code))
        {
            var fund = funds.FirstOrDefault(x => x.Code == f.Key);
            if (fund is null) continue;

            var old = db.GetCollection<ManageFeeDetail>($"f{fund.Id}").FindAll().OrderBy(x => x.Date).ToList();
            foreach (var n in f.Select(x => x.Fee))
            {
                var v = old.FirstOrDefault(x => x.Date == n.Date);
                if (v is not null) v = v with { Fee = n.Fee, Share = n.Share };
                else old.Add(n);
            }

            //db.GetCollection<ManageFeeDetail>($"f{fund.Id}").EnsureIndex(x => x.Date, true);
            db.GetCollection<ManageFeeDetail>($"f{fund.Id}").Upsert(old);
        }


        HandyControl.Controls.Growl.Success("导入成功");
    }


    [RelayCommand]
    public async Task SyncByAPI()
    {
        if (Begin is null || End is null) return;

        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().FindAll().ToArray();

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

                db.GetCollection<FundDailyFee>().Upsert(rc.Data);
            }
        }

        debouncer.Invoke();
    }





    private async Task Calc(IGrouping<string, FundInfo> col, List<DateOnly> dates)
    {
        foreach (var f in col)
        {
            f.IsWorking = true;
            using var db = new LiteDatabase(@"FileName=data\feecalc.db;Connection=Shared");
            var begin = dates[0];
            var end = dates[^1];

            // var dc = db.GetDailyCollection(f.Fund.Id);
            // var fees = db.GetCollection<FundDailyFee>().Find(x => x.FundId == f.Fund.Id && x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).Select(x => new ManageFeeDetail(0, x.Date, x.ManagerFeeAccrued, dc.FindOne(y=>y.Date == x.Date)?.Share??0)).ToList();

            var fees = db.GetCollection<ManageFeeDetail>($"f{f.Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).ToList();
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
            var (dd, ids, names, array) = FundHelper.GenerateShareSheet(f.Fund.Id, dates[0], dates[^1]);

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
                using var db = DbHelper.Base();
                var per = db.GetCollection<TransferRecord>().Find(x => x.FundId == f.Fund.Id && x.PerformanceFee > 0).Where(x => x.RequestDate >= dd[0] && x.RequestDate <= dd[^1]).ToArray();


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

    public void CheckData(List<DateOnly> dates, DateOnly begin, DateOnly end)
    {
        // 从platform中同步
        using var pdb = DbHelper.Base();
        var data = pdb.GetCollection<FundDailyFee>().Find(x => x.FundId == Fund.Id).ToArray();


        using var db = new LiteDatabase(@"FileName=data\feecalc.db;Connection=Shared");
        var fees = db.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).DistinctBy(x => x.Date).ToList();
        var fdate = fees.Select(x => x.Date).ToArray();

        IsDataValid = dates.SequenceEqual(fdate);
        if (!IsDataValid)
        {
            // 尝试从 中同步
            var nvs = pdb.GetDailyCollection(Fund.Id).FindAll().OrderBy(x => x.Date).ToList();
            var fe = data.Select(x => new ManageFeeDetail(x.Date.DayNumber, x.Date, x.ManagerFeeAccrued, nvs.LastOrDefault(y => y.Date <= x.Date)?.Share ?? 0));

            // 保存
            db.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Upsert(fe);
            // db.GetCollection<ManageFeeDetail>($"f{Fund.Id}").DeleteMany(x => x.Id < 10000);

        }

        // 再次加载
        fees = db.GetCollection<ManageFeeDetail>($"f{Fund.Id}").Find(x => x.Date >= begin && x.Date <= end).OrderBy(x => x.Date).DistinctBy(x => x.Date).ToList();
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