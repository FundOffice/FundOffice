using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.IO.AMAC;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO;

/// <summary>
/// CustomerPage.xaml 的交互逻辑
/// </summary>
public partial class CustomerPage : UserControl
{




    public CustomerPage()
    {
        //Task.Run(async () =>
        //{
        //    var obj = new CustomerPageViewModel();
        //    await Dispatcher.BeginInvoke(() => DataContext = obj);
        //});

        InitializeComponent();

        //Loaded += (s, e) => DataContext = new CustomerPageViewModel();
    }
}

public partial class CustomerPageViewModel : ObservableRecipient, IRecipient<Investor>, IRecipient<InvestorQualification>, IRecipient<RiskAssessment>, IRecipient<InvestorBankAccount>, IRecipient<EntityDeleted<InvestorBankAccount>>, IRecipient<TransferOrder>
{
    public ObservableCollection<InvestorReadOnlyViewModel> Customers { get; }

    public CollectionViewSource CustomerSource { get; } = new();


    [ObservableProperty]
    public partial InvestorReadOnlyViewModel? Selected { get; set; }


    [ObservableProperty]
    public partial CustomerViewModel? Detail { get; set; }

    [ObservableProperty]
    public partial double ImportQualificationProgress { get; set; }



    [ObservableProperty]
    public partial int CountofInvestorsHoldingPositions { get; set; }


    [ObservableProperty]
    public partial int CountOfNoRiskAssessment { get; set; }


    [ObservableProperty]
    public partial int CountOfRiskAssessmentExpired { get; set; }


    [ObservableProperty]
    public partial string? SearchKey { get; set; }


    public ObservableCollection<UndeservedAccountViewModel> UndeservedAccount { get; }


    public bool HasUndeservedAccount => UndeservedAccount.Count > 0;

    public CustomerPageViewModel()
    {
        IsActive = true;

        using var db = DbHelper.Base();

        var cusomers = db.GetCollection<Investor>().FindAll().ToArray();


        // 合投日期 
        var qs = db.GetCollection<InvestorQualification>().FindAll().ToList();
        var accounts = db.GetCollection<InvestorBankAccount>().FindAll().ToList();

        Customers = new(cusomers.Select(x => new InvestorReadOnlyViewModel(x,
            qs.Where(y => y.InvestorId == x.Id).OrderByDescending(x => x.Date).FirstOrDefault(), accounts.Where(y => y.OwnerId == x.Id))));

        // 持仓客户
        var ib = db.GetCollection<InvestorBalance>().FindAll().ToArray();
        CountofInvestorsHoldingPositions = ib.Where(x => x.Share > 0).DistinctBy(x => x.InvestorId).Count();

        // 更新到customers
        foreach (var item in Customers.IntersectBy(ib.Where(x => x.Share == 0).Select(x => x.InvestorId), x => x.Id))
            item.Holding = InvestorReadOnlyViewModel.HoldingMode.Previous;
        foreach (var item in Customers.IntersectBy(ib.Where(x => x.Share > 0).Select(x => x.InvestorId), x => x.Id))
            item.Holding = InvestorReadOnlyViewModel.HoldingMode.Current;



        // 是否缺失订单
        var ta = db.GetCollection<TransferRecord>().FindAll().ToArray();
        var cleard = db.GetCollection<Fund>().FindAll().ToDictionary(x => x.Id, x => x.ClearDate == default ? DateOnly.MaxValue : x.ClearDate);

        foreach (var item in Customers.IntersectBy(ta.Where(x => x.FundId != 0 && x.OrderId == 0 && x.ConfirmedDate < cleard[x.FundId] && TAHelper.RequiredOrder(x.Type)).Select(x => x.InvestorId), x => x.Id))
            item.LackOrder = true;

        CustomerSource.Source = Customers;
        CustomerSource.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(InvestorReadOnlyViewModel.Holding), System.ComponentModel.ListSortDirection.Descending));
        CustomerSource.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(InvestorReadOnlyViewModel.Type), System.ComponentModel.ListSortDirection.Ascending));
        CustomerSource.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(InvestorReadOnlyViewModel.Id), System.ComponentModel.ListSortDirection.Ascending));
        CustomerSource.Filter += CustomerSource_Filter;
        CustomerSource.IsLiveSortingRequested = true;
        CustomerSource.LiveSortingProperties.Add(nameof(InvestorReadOnlyViewModel.Holding));
        CustomerSource.View.MoveCurrentTo(null);

        RefreshRiskAssessmentData(db, ib);

        // 未匹配的银行账号 排除产品、特殊账户
        // 托管户
        var tracc = db.GetCollection<FundElements>().FindAll().SelectMany(x => x.CollectionAccount.Changes.Select(y => y.Value.Number).Union(x.CustodyAccount.Changes.Select(y => y.Value.Number))).ToList();
        var fnames = db.GetCollection<FundElements>().FindAll().SelectMany(x => x.FullName.Changes.Select(x => x.Value)).Distinct().Union(db.GetCollection<Fund>().Query().Select(x => x.Name).ToArray()).ToList();
        var acc = db.GetCollection<InvestorBankAccount>().Find(x => x.OwnerId == 0).Where(x => !Regex.IsMatch(x.Name!, "登记专户|子账户|募集专户|公共计息收费")).
                    Where(x => !tracc.Contains(x.Number)).Where(x => !fnames.Any(y => x.Name!.Contains(y))).ToArray();
        UndeservedAccount = [.. acc.Select(x => new UndeservedAccountViewModel(x, Customers))];
    }

    private void CustomerSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = string.IsNullOrWhiteSpace(SearchKey) ? true : e.Item switch
        {
            InvestorReadOnlyViewModel v =>
            (v.Name?.Contains(SearchKey) ?? false) || (v.Identity?.Id?.Contains(SearchKey, StringComparison.OrdinalIgnoreCase) ?? false),
            _ => false
        };
    }

    private void RefreshRiskAssessmentData(BaseDatabase db, InvestorBalance[] ib)
    {
        // 风测失效或缺失
        var ra = db.GetCollection<RiskAssessment>().FindAll().ToArray();
        // 持有过产品，但是没有风测
        CountOfNoRiskAssessment = ib.ExceptBy(ra.Select(x => x.InvestorId), x => x.InvestorId).DistinctBy(x => x.InvestorId).Count();
        // 过期
        var limitday = DateOnly.FromDateTime(DateTime.Now).AddYears(-3);
        var expired = ra.GroupBy(x => x.InvestorId).Select(x => new { id = x.Key, date = x.Max(y => y.Date) }).Where(x => limitday > x.date).ToArray();
        // 更新到customers
        foreach (var item in Customers.IntersectBy(expired.Select(x => x.id), x => x.Id))
            item.EvaluationExpired = true;

        CountOfRiskAssessmentExpired = expired.Length;
    }

    [RelayCommand]
    public void AddInvestor(DataGrid grid)
    {
        AddInvestorWindowViewModel vm = new();
        var wnd = new AddInvestorWindow { DataContext = vm, Owner = Application.Current.MainWindow };

        if (wnd.ShowDialog() ?? false)
        {
            var c = vm.Investor!;
            using var db = DbHelper.Base();
            db.GetCollection<Investor>().Insert(c);
            Customers.Add(new InvestorReadOnlyViewModel(c, null, null));
        }
    }

    [RelayCommand]
    public void RemoveInvestor()
    {
        if (Selected is null) return;

        if (Selected.Id != 0)
        {
            if (Selected.Name?.Length > 1 && Selected.Identity != default && HandyControl.Controls.MessageBox.Show($"是否删除投资人 【{Selected.Name}】资料", "提示", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.No)
                return;

            using var db = DbHelper.Base();
            db.GetCollection<Investor>().Delete(Selected.Id);
        }

        Application.Current.Dispatcher.BeginInvoke((() => { Customers.Remove(Selected); Selected = null; }));

    }

    [RelayCommand]
    public async Task GeneratePfidSheet()
    {
        try
        {
            // 加载模板
            var file = new FileInfo(@"files\tpl\pfid_investor.xlsx");
            if (!file.Exists)
            {
                HandyControl.Controls.Growl.Error("未找到模板文件");
                return;
            }

            using FileStream stream = file.OpenRead();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet("投资者信息");
            if (sheet is null)
                return;

            using var db = DbHelper.Base();
            var customer = db.GetCollection<Investor>().FindAll().ToList();
            var ta = db.GetCollection<TransferRecord>().FindAll().ToList();
            var pfmap = db.GetCollection<PfidAccount>();


            if (pfmap.Count() == 0)
            {
                if (MessageBoxResult.Yes == HandyControl.Controls.MessageBox.Show("是否从pfid系统下载已知账户，如选择否，生成的账号会与系统中已有的账号冲突（同一投资人有不同的账号）", button: MessageBoxButton.YesNo))
                {
                    var acc = db.GetCollection<AmacAccount>().FindById("xinpi");
                    var ad = await PfidAssist.QueryInvestorAccounts(acc);
                    pfmap.Upsert(ad);
                }
            }


            int row = 2;

            foreach (var c in customer)
            {
                // 检查有无仓位 
                var cta = ta.Where(x => x.InvestorId == c.Id).GroupBy(x => x.FundCode);
                cta = cta.Where(x => x.Sum(y => y.ShareChange()) > 0);
                var hasposition = cta.Any();

                if (c.Identity is not null)
                {
                    if (pfmap.FindById(c.Id) is PfidAccount pac)
                        sheet.Cell(row, 1).Value = pac.Account;
                    else if (!hasposition) continue; // 如果无仓位，也没有账号，跳过

                    else sheet.Cell(row, 1).Value = c.EntityType switch
                    {
                        EntityType.Product => c.Identity.Id[^6..], //S 码
                        EntityType.Institution => c.Identity.Id[^9..], // 组织机构码
                        _ => string.IsNullOrWhiteSpace(c.Phone) ? c.Email : c.Phone // 手机或邮箱
                    };// $"xxsc{c.Identity.Id[^6..]}";

                    sheet.Cell(row, 2).Value = c.Name;
                    sheet.Cell(row, 3).Value = EnumDescriptionTypeConverter.GetEnumDescription(c.Type);
                    sheet.Cell(row, 4).Value = c.Identity.Type == IDType.IdentityCard ? "身份证" : EnumDescriptionTypeConverter.GetEnumDescription(c.Identity.Type);
                    if (c.Identity.Type == IDType.Other)
                        sheet.Cell(row, 5).Value = c.Identity.Other;

                    sheet.Cell(row, 6).Value = c.Identity.Id;
                    sheet.Cell(row, 8).Value = c.Email;
                    if (string.IsNullOrWhiteSpace(c.Email))
                        sheet.Cell(row, 7).Value = c.Phone;
                    sheet.Cell(row, 9).Value = hasposition ? "启用" : "关闭";
                    sheet.Cell(row, 10).Value = string.Join(",", cta.Select(x => x.Key));
                    ++row;
                }
                //foreach (var t in cta)
                //{
                //    var fundc = t.Key;

                //    if (t.Sum(x => x.ShareChange()) > 0)
                //    {
                //        has = true;

                //        sheet.Cell(row, 1).Value = $"xxsc{c.Identity.Id[^6..]}";
                //        sheet.Cell(row, 2).Value = c.Name;
                //        sheet.Cell(row, 3).Value = EnumDescriptionTypeConverter.GetEnumDescription(c.Type);
                //        sheet.Cell(row, 4).Value = c.Identity.Type == IDType.IdentityCard ? "身份证": EnumDescriptionTypeConverter.GetEnumDescription(c.Identity.Type);
                //        if (c.Identity.Type == IDType.Other)
                //            sheet.Cell(row, 5).Value = c.Identity.Other;

                //        sheet.Cell(row, 6).Value = c.Identity.Id; 
                //        sheet.Cell(row, 8).Value = c.Email;
                //        if(string.IsNullOrWhiteSpace(c.Email))
                //            sheet.Cell(row, 7).Value = c.Phone;
                //        sheet.Cell(row, 9).Value = "启用";
                //        sheet.Cell(row, 10).Value = fundc;
                //        ++row;
                //    }
                //}  
            }

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "投资者账号.xlsx");
            workbook.SaveAs(path);

            // 上传
            

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(new FileInfo(path).Directory!.FullName) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            HandyControl.Controls.Growl.Error($"生成失败：{e.Message}");
        }
    }


    [RelayCommand]
    public async Task SyncPfidSheet()
    {
        using var db = DbHelper.Base();
        var acc = db.GetCollection<AmacAccount>().FindById("xinpi");
        var (s,e) = await PfidAssist.UpdateInvestorAccounts(acc);
        if (s)
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, $"同步完成：{e}"));
        else WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, $"同步失败：{e}"));
    }

    [RelayCommand]
    public async Task ImportQualificationData()
    {
        var fd = new OpenFileDialog();
        if (!fd.ShowDialog() ?? true) return;

        var file = fd.FileName;

        await Task.Run(() =>
        {
            ImportQualificationProgress = 0;

            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read);

            var info = zip.Entries.Where(x => string.IsNullOrWhiteSpace(x.Name)).Select(x => (x, Regex.Match(x.FullName, @"投资者数据库列表文件/([\w\(\)（）]+)/([^/]+)/$"))).
                            Where(x => x.Item2.Success).Select(x => (Fund: x.Item2.Groups[1].Value, Investor: x.Item2.Groups[2].Value, Folder: x.x.FullName));

            using var db = DbHelper.Base();
            var customers = db.GetCollection<Investor>().FindAll().ToArray();

            var unit = 100.0 / info.Count();

            foreach (var fund in info.GroupBy(x => x.Fund))
            {
                foreach (var customer in fund)
                {
                    ImportQualificationProgress += unit;
                    //
                    var m = Regex.Match(customer.Investor, @"(.*?)\(([a-zA-Z0-9]+)\)-\((\w+)\)");
                    if (!m.Success)
                    {
                        Debug.WriteLine($"{customer.Investor} 解析失败");
                        continue;
                    }

                    var name = m.Groups[1].Value;
                    var idend = m.Groups[2].Value;
                    var type = m.Groups[3].Value;

                    // 匹配id 
                    var cus = customers.Where(x => x.Identity?.Id.EndsWith(idend) ?? false).FirstOrDefault(x => Investor.IsNamePair(x.Name, name));
                    if (cus is null)
                    {
                        Debug.WriteLine($"{customer.Investor} 未找到");
                        continue;
                    }


                    // 风险测评材料/
                    var riskf = zip.Entries.Where(x => x.Name?.Length > 0 && x.FullName.StartsWith(customer.Folder + "风险测评材料/"));
                    foreach (var item in riskf)
                    {
                        try
                        {
                            // 解析20250510-C5
                            m = Regex.Match(item.Name, @"(\d{8})-(C\d)");
                            if (!m.Success)
                                continue;

                            var date = DateOnly.ParseExact(m.Groups[1].Value, "yyyyMMdd");
                            var ra = Enum.Parse<RiskEvaluation>(m.Groups[2].Value);
                            // 检查是否已存在
                            var old = db.GetCollection<RiskAssessment>().FindOne(x => x.InvestorId == cus.Id && x.Date == date);
                            if (old is null)
                                old = new RiskAssessment { Date = date, InvestorId = cus.Id, Level = ra };
                            db.GetCollection<RiskAssessment>().Upsert(old);

                            old.File = FileMeta.Create(item.Open(), $"风险调查问卷-{cus.Name}{Path.GetExtension(item.Name)}", new DateTime(date, default));
                            //string savePath = @$"files\evaluation\{old.Id}{Path.GetExtension(item.Name)}";
                            //using var sw = new FileStream(savePath, FileMode.Create);
                            //using var ffs = item.Open();
                            //ffs.CopyTo(sw);
                            //sw.Flush();
                            //old.Path = savePath;
                            db.GetCollection<RiskAssessment>().Upsert(old);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"ImportQualificationData {e}");
                        }
                    }


                    // 合投
                    var qs = zip.Entries.Where(x => string.IsNullOrWhiteSpace(x.Name) && x.FullName.StartsWith(customer.Folder + "合格投资者认定材料/"));
                    foreach (var item in qs)
                    {
                        m = Regex.Match(item.FullName, @"\d{8}");
                        if (!m.Success) continue;

                        var date = DateOnly.ParseExact(m.Value, "yyyyMMdd");

                        var old = db.GetCollection<InvestorQualification>().FindOne(x => x.InvestorId == cus.Id && x.Date == date);
                        if (old is null)
                        {
                            old = new InvestorQualification { InvestorId = cus.Id, InvestorName = cus.Name, Date = date };
                            db.GetCollection<InvestorQualification>().Insert(old);
                        }

                        // 所有文件
                        var files = zip.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.FullName.StartsWith(item.FullName));
                        foreach (var qf in files)
                        {
                            string path = @$"files\qualification\{old.Id}\{qf.Name}";
                            Directory.CreateDirectory(@$"files\qualification\{old.Id}");

                            using var ffs = new FileStream(path, FileMode.Create);
                            using var tmp = qf.Open();
                            tmp.CopyTo(ffs);
                            ffs.Flush();
                            var fsi = FileMeta.Create(path);// new FileStorageInfo(path, FileHelper.ComputeHash(ffs)!, File.GetLastWriteTime(path));

                            if (qf.Name.Contains("基本信息表"))
                                old.InfomationSheet = new SimpleFile { File = fsi };
                            else if (qf.Name.Contains("税收居民身份声明"))
                                old.TaxDeclaration = new SimpleFile { File = fsi };
                            else if (qf.Name.Contains("投资者告知书"))
                                old.Notice = new SimpleFile { File = fsi };
                            else if (qf.Name.Contains("投资经历"))
                                old.ProofOfExperience = new SimpleFile { File = fsi };
                            else if (qf.Name.Contains("合格投资者承诺函"))
                                old.CommitmentLetter = new SimpleFile { File = fsi };
                            else if (qf.Name.Contains("证明"))
                            {
                                if (old.CertificationFiles is null)
                                    old.CertificationFiles = new();

                                //if (!old.CertificationFiles.Files.Any(x => x.Path == fsi.Path))
                                old.CertificationFiles.Files.Add(fsi);
                            }
                            else Log.Warning($"未识别的合投文件 {cus.Name}:{qf.Name}");
                        }

                        db.GetCollection<InvestorQualification>().Upsert(old);


                        // 身份证
                        var idcards = files.Where(x => x.Name.Contains("身份证件正反面") && x.Name.Contains(cus.Name)).ToArray();
                        if (idcards.Length == 2)
                        {
                            using var ms = MergeCards(db, idcards);
                            if (ms is not null)
                            {

                                var cert = db.GetCollection<InvestorCertifications>().FindById(cus.Id) ?? new();
                                cert.Files.Add(FileMeta.Create(ms, $"身份证-{cus.Name}.jpg", idcards[0].LastWriteTime.LocalDateTime));

                                db.GetCollection<InvestorCertifications>().Upsert(cert);
                            }
                        }
                    }


                }
            }

        });
    }

    private BitmapSource? LoadImage(ZipArchiveEntry entry)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var fs = entry.Open())
                fs.CopyTo(ms);

            ms.Seek(0, SeekOrigin.Begin);
            return BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private MemoryStream? MergeCards(BaseDatabase db, ZipArchiveEntry[] idcards)
    {
        // 加载图像
        BitmapSource? bitmap1 = LoadImage(idcards[0]);
        BitmapSource? bitmap2 = LoadImage(idcards[1]);

        if (bitmap1 == null || bitmap2 == null)
            return null;


        // 计算总宽度和最大高度
        int totalWidth = bitmap1.PixelWidth + bitmap2.PixelWidth;
        int maxHeight = Math.Max(bitmap1.PixelHeight, bitmap2.PixelHeight);

        // 创建绘图目标
        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(bitmap1, new Rect(0, 0, bitmap1.PixelWidth, bitmap1.PixelHeight));
            context.DrawImage(bitmap2, new Rect(bitmap1.PixelWidth, 0, bitmap2.PixelWidth, bitmap2.PixelHeight));
        }

        // 渲染为位图
        RenderTargetBitmap rtb = new RenderTargetBitmap(
            totalWidth, maxHeight,
            96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        // 保存为 JPG 文件
        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        encoder.QualityLevel = 90; // 设置画质
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        var ms = new MemoryStream();
        encoder.Save(ms);
        return ms;
    }

    [RelayCommand]
    public void ClearSearch() => SearchKey = null;


    partial void OnSearchKeyChanged(string? value)
    {
        CustomerSource.View.Refresh();
    }

    partial void OnSelectedChanged(InvestorReadOnlyViewModel? oldValue, InvestorReadOnlyViewModel? newValue)
    {
        //if (oldValue is null) return;
        Detail = newValue is null ? null : new CustomerViewModel(newValue.Id);
    }

    public void Receive(Investor message)
    {
        Application.Current.Dispatcher.BeginInvoke((() =>
        {

            try
            {
                for (int i = 0; i < Customers.Count; i++)
                {
                    if (Customers[i].Id == message.Id)
                    {
                        Customers[i].Update(message);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"更新investor出错 {e.Message}");
            }

        }));
    }

    public void Receive(InvestorQualification message)
    {
        var c = Customers.FirstOrDefault(x => x.Id == message.InvestorId);
        c?.RecheckQualification();
    }

    public void Receive(RiskAssessment message)
    {
        using var db = DbHelper.Base();
        var ra = db.GetCollection<RiskAssessment>().Find(x => x.InvestorId == message.InvestorId).OrderByDescending(x => x.Date).FirstOrDefault();

        var c = Customers.FirstOrDefault(x => x.Id == message.InvestorId);
        if (c is null) return;

        c.RiskEvaluation = ra?.Level ?? message.Level;
        var cus = db.GetCollection<Investor>().FindById(message.InvestorId);
        if (cus is not null)
        {
            cus.RiskEvaluation = c.RiskEvaluation;
            db.GetCollection<Investor>().Upsert(cus);
        }

        RefreshRiskAssessmentData(db, db.GetCollection<InvestorBalance>().FindAll().ToArray());
    }

    public void Receive(InvestorBankAccount message)
    {
        if (Customers.FirstOrDefault(x => x.Id == message.OwnerId) is InvestorReadOnlyViewModel v && !v.HasBankAccount)
            v.HasBankAccount = true;
    }

    public void Receive(EntityDeleted<InvestorBankAccount> message)
    {
        if (Customers.FirstOrDefault(x => x.Id == message.Value.OwnerId) is InvestorReadOnlyViewModel v)
        {
            using var db = DbHelper.Base();
            v.HasBankAccount = db.GetCollection<InvestorBankAccount>().Query().Where(x => x.OwnerId == v.Id).Count() > 0;
        }
    }

    public void Receive(TransferOrder message)
    {
        var c = Customers.FirstOrDefault(x => x.Id == message.InvestorId);
        if (c is not null)
        {
            using var db = DbHelper.Base();
            c.LackOrder = db.GetCollection<TransferRecord>().Find(x => x.InvestorId == c.Id).Any(x => x.OrderId == 0 && TAHelper.RequiredOrder(x.Type));
        }
    }
}


/// <summary>
/// 用于列表展示
/// </summary>
public partial class InvestorReadOnlyViewModel : ObservableObject
{
    public int Id { get; }


    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial AmacInvestorType Type { get; set; }

    [ObservableProperty]
    public partial Identity Identity { get; set; }


    [ObservableProperty]
    public partial DateEfficient Efficient { get; set; }

    [ObservableProperty]
    public partial RiskEvaluation RiskEvaluation { get; set; }

    /// <summary>
    /// 缺失email
    /// </summary>
    [ObservableProperty]
    public partial bool LackEmail { get; set; }

    /// <summary>
    /// 缺少联系方式
    /// </summary>
    [ObservableProperty]
    public partial bool LackPhone { get; set; }

    /// <summary>
    /// 缺失订单
    /// </summary>
    [ObservableProperty]
    public partial bool LackOrder { get; set; }


    [ObservableProperty]
    public partial HoldingMode Holding { get; set; }

    /// <summary>
    /// 持有产品
    /// </summary>
    //[ObservableProperty]
    //public partial bool HasPosition { get; set; }

    ///// <summary>
    ///// 曾经持有过
    ///// </summary>
    //[ObservableProperty]
    //public partial bool PreviouslyHasPosition { get; set; }

    /// <summary>
    /// 风测过期
    /// </summary>
    [ObservableProperty]
    public partial bool EvaluationExpired { get; set; }



    [ObservableProperty]
    public partial DateOnly? QualificationDate { get; set; }

    [ObservableProperty]
    public partial bool QualificationAbnormal { get; set; }

    [ObservableProperty]
    public partial bool QualificationSettled { get; set; }


    [ObservableProperty]
    public partial bool HasBankAccount { get; set; }

    public Investor Investor { get; set; }

    public InvestorReadOnlyViewModel(Investor investor, InvestorQualification? qs, IEnumerable<InvestorBankAccount>? accounts)
    {
        Id = investor.Id;
        Name = investor.Name;
        Type = investor.Type;

        Identity = investor.Identity ?? new Identity { Id = "" };
        Efficient = investor.Efficient;

        RiskEvaluation = investor.RiskEvaluation;

        LackPhone = string.IsNullOrWhiteSpace(investor.Phone);
        LackEmail = string.IsNullOrWhiteSpace(investor.Email);
        Investor = investor;


        QualificationDate = qs?.Date switch { null => null, DateOnly d when d == default => null, var n => n };

        QualificationAbnormal = qs?.HasError ?? true;//?.Check().HasError ?? true;
        QualificationSettled = qs?.IsSettled ?? false;

        HasBankAccount = accounts?.Any() ?? false;
    }

    public void Update(Investor investor)
    {
        Name = investor.Name;
        Type = investor.Type;

        Identity = investor.Identity;
        Efficient = investor.Efficient;

        RiskEvaluation = investor.RiskEvaluation;
        LackPhone = string.IsNullOrWhiteSpace(investor.Phone);
        LackEmail = string.IsNullOrWhiteSpace(investor.Email);
        Investor = investor;
    }

    internal void RecheckQualification()
    {
        // 合投日期
        using var db = DbHelper.Base();
        var qs = db.GetCollection<InvestorQualification>().Find(x => x.InvestorId == Id).OrderByDescending(x => x.Date);
        var qf = qs.FirstOrDefault(x => x.IsSettled || !x.HasError);
        QualificationDate = qf?.Date switch { null => qs.FirstOrDefault()?.Date, DateOnly d when d == default => null, var n => n };

        QualificationAbnormal = qf?.Check().HasError ?? true;
        QualificationSettled = qf?.IsSettled ?? false;
    }



    public enum HoldingMode
    {
        None,
        Previous,
        Current
    }
}


public partial class UndeservedAccountViewModel : ObservableObject
{
    public string? Name { get; }

    public string? Number { get; }
    public int Id { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial InvestorReadOnlyViewModel? Customer { get; set; }

    [ObservableProperty]
    public partial int InvestorId { get; set; }

    public IEnumerable<InvestorReadOnlyViewModel> Customers { get; set; }

    public bool CanConfirm => Customer is not null;

    public UndeservedAccountViewModel(InvestorBankAccount bank, IEnumerable<InvestorReadOnlyViewModel> customers)
    {
        Name = bank.Name;
        Number = bank.Number;
        Id = bank.Id;
        Customers = customers.Where(x => CheckNameSimiliar(x.Name, bank.Name));
    }

    /// <summary>
    /// 检查2个名字有相似的字符
    /// 排除 企业名的后缀、产品名后缀等相同字符
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private bool CheckNameSimiliar(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;

        var regex = new Regex(@"股份|有限|责任|公司|证券|期货|私募\w{2}基金|单一|集合|资产管理计划|合伙企业|特殊");
        a = regex.Replace(a, "");
        b = regex.Replace(b, "");

        return a.ToArray().Intersect(b.ToArray()).Count() > 1;
    }


    [RelayCommand]
    public void Confirm()
    {
        if (Customer is null) return;

        Customer.HasBankAccount = true;
        using var db = DbHelper.Base();
        var bank = db.GetCollection<InvestorBankAccount>().FindById(Id);
        bank.OwnerId = Customer.Id;
        db.GetCollection<InvestorBankAccount>().Update(bank);
    }
}