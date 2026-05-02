using CommunityToolkit.Mvvm.Messaging;
using FMO.Disclosure;
using FMO.Models;
using FMO.PDF;
using FMO.Utilities;
using MimeKit;
using Serilog;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace FMO.Schedule;



public record FundIdf(string Name, string Code);

public class DisclosureFromMailMission : MailMission
{
    public int Interval { get; set; } = 6;

    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddHours(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddHours(Interval);
    }

    //public void Test() => WorkOverride();

    protected override async Task<ErrorReturn> WorkOverride()
    {
        // 获取所有缓存  
        var di = new DirectoryInfo(@$"files\mailcache\{MailName}");
        if (!di.Exists)
            return new ErrorReturn(false, $"邮件缓存文件夹不存在：{di.FullName}");

        // 获取所有文件
        var files = di.GetFiles();
        using var db = DbHelper.Mission();
        // 排除估值表，加快效率
        var cat = db.GetCollection<MailCategoryInfo>().Find(x => x.Category == MailCategory.ValueSheet).Select(x => x.Id).ToList();//.FindAll().Where(x => x.Category.HasFlag(MailCategory.Disclosure)).Select(x => x.Id).ToArray();

        var coll = db.GetCollection<MailMissionRecord>($"dfm_{Id}");
        var worked = coll.FindAll().ExceptBy(cat, x => x.Id).ToArray();

        // 这里有问题， file 是uid， MailCategoryInfo是message id，不一样，无法排除
        var work = IgnoreHistory ? files.ExceptBy(cat, x => x.Name).ToArray() : files.ExceptBy(worked.Select(x => x.Id), x => x.Name).ToArray();

        var log = $"{(IgnoreHistory ? "全部重解析" : $"已解析{worked.Length}个")}\n待处理邮件 {work.Length} 个";

        double unit = 100.0 / work.Length;
        double progress = 0;

        Dictionary<string, int> fundmap;
        FundIdf[] fundCodeMap;
        using (var mdb = DbHelper.Base())
        {
            fundmap = mdb.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Code }).ToArray().DistinctBy(x => x.Code).ToDictionary(x => x.Code!, x => x.Id);
            fundCodeMap = mdb.GetCollection<Fund>().Query().Select(x => new { x.Name, x.Code }).ToArray().Select(x => new FundIdf(x.Name, x.Code!)).ToArray();
        }

        ConcurrentBag<string> logBag = new();
        Parallel.ForEach(work, f => //)
        //foreach (var f in work.AsEnumerable().Reverse())
        {
            try
            {
                var nlog = "";
                WorkOne(f, fundmap, fundCodeMap, ref nlog);
                logBag.Add(nlog);
                coll.Upsert(new MailMissionRecord { Id = f.Name, Time = DateTime.Now });
            }
            catch (Exception ex)
            {
                Log.Error($"TA From Mail {ex}");
            }

            progress += unit;
            WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = progress });
        }
        );

        log += "\n" + string.Join("\n", logBag);

        using (var mdb = DbHelper.Mission())
            mdb.GetCollection<MissionRecord>().Insert(new MissionRecord { MissionId = Id, Time = DateTime.Now, Record = log });

        return new ErrorReturn(true, log);
    }

    public void WorkOne(FileInfo f, Dictionary<string, int> fundmap, FundIdf[] fundCodeMap, ref string log)
    {
        using MimeMessage mime = LoadMail(f.FullName);

        if (DetermineCategory(mime) switch { MailCategory.Unk or MailCategory.Disclosure => false, _ => true })
            return;


        List<ParsedInfo> reports = [];


        foreach (MimePart item in mime.Attachments)
        {
            var filepath = item.FileName;
            if (filepath is null || item.Content is null) continue;
            switch (Path.GetExtension(filepath).ToLower())
            {
                case ".zip":
                    using (var ms = Copy(item.Content))
                        HandleZip(filepath, ms, fundCodeMap, reports, ref log);
                    continue;

                case ".xlsx":
                case ".doc":
                case ".docx":
                case ".pdf":
                case ".xbrl":
                    using (var ms = Copy(item.Content))
                        HandleRealFile(filepath, ms, fundCodeMap, reports, ref log);
                    continue;
            }
        }

        if (reports.Count == 0) return;

        // 合并成报告
        using var db = DbHelper.Base();
        db.BeginTrans();
        foreach (var fund in reports.GroupBy(x => x.Code))
        {
            if (!fundmap.TryGetValue(fund.Key, out int fundId)) continue;

            var fundName = fund.First().FundName;

            foreach (var type in fund.AsEnumerable().GroupBy(x => x.Type))
            {
                foreach (var r in type.AsEnumerable().GroupBy(x => x.Date))
                {
                    // 只处理周期报告
                    if (type.Key >= DisclosureType.Monthly && type.Key <= DisclosureType.Annually)
                    {
                        var fp = new PeriodicalDisclosureNotice
                        {
                            FundId = fundId,
                            FundCode = fund.Key,
                            FundName = fundName,
                            Name = $"{fundName}_{EnumDescriptionTypeConverter.GetEnumDescription(type.Key)}_{r.Key:yyyyMMdd}",
                            Type = type.Key,
                            ReportDate = r.Key
                        };

                        // 按文件类型分配
                        foreach (var ext in r.GroupBy(x => Path.GetExtension(x.FileName)))
                        {
                            switch (ext.Key)
                            {
                                case ".xlsx":
                                    fp.Excel = new SimpleFile { File = FileMeta.Create(ext.First().Stream, ext.First().FileName) };
                                    break;

                                case ".doc":
                                case ".docx":
                                    fp.Word = new SimpleFile { File = FileMeta.Create(ext.First().Stream, ext.First().FileName) };
                                    break;

                                case ".xbrl":
                                    fp.Xbrl = new SimpleFile { File = FileMeta.Create(ext.First().Stream, ext.First().FileName) };
                                    break;

                                case ".pdf":
                                    if (ext.Count(x => !Regex.IsMatch(x.FileName, "复核函")) == 1)// 避免把复核函当成报告正文
                                        fp.Pdf = new SimpleFile { File = FileMeta.Create(ext.First().Stream, ext.First().FileName) };
                                    else if (PdfHelper.Merge(ext.Select(x => x.Stream).ToArray()) is Stream nes) // 有些托管会发多个Pdf，把它们合并成一个
                                        fp.Pdf = new SimpleFile { File = FileMeta.Create(nes, $"{fundName}_{EnumDescriptionTypeConverter.GetEnumDescription(type.Key)}_{r.Key:yyyyMMdd}.pdf") };
                                    break;
                            }
                            foreach (var ff in ext)
                                ff.Stream.Dispose();
                        }

                        DataTracker.OnNewNotice(fp);

                        log += $"\n{fund.Key}: {type.Key} {r.Key}";
                    }
                    else if (type.Key == DisclosureType.QuarterlyUpdate)
                    {
                        QuarterlyUpdate fp = new() { FundId = fundId, FundCode = fund.Key, FundName = fundName, Name = $"{fundName}_{EnumDescriptionTypeConverter.GetEnumDescription(type.Key)}_{r.Key:yyyyMMdd}", ReportDate = r.Key };
                        foreach (var item in r)
                        {
                            if (Regex.IsMatch(item.FileName, "投资[者人]"))
                                fp.Investor = new SimpleFile { File = FileMeta.Create(item.Stream, item.FileName) };
                            else if (Regex.IsMatch(item.FileName, "运行|季度更新"))
                                fp.Operation = new SimpleFile { File = FileMeta.Create(item.Stream, item.FileName) };

                            item.Stream.Dispose();
                        }

                        DataTracker.OnNewNotice(fp);
                        log += $"\n{fund.Key}: {type.Key} {r.Key}";
                    }
                }
            }
        }

        db.Commit();
    }






    private MemoryStream Copy(IMimeContent c)
    {
        var ms = new MemoryStream();
        c.DecodeTo(ms);
        ms.Position = 0;
        return ms;
    }


    private DateOnly ParseDateFromFileName(string path)
    {
        var m = Regex.Match(path, @"(\d{4})[年\.-](\d{1,2})[月\.-](\d{1,2})日?|\d{8}", RegexOptions.IgnoreCase);
        if (m.Success && DateTimeHelper.TryFindDate(m.Value) is DateOnly date)
            return date;

        // 建投
        m = Regex.Match(path, @"(?<!\d)\d{6}(?!\d)");
        if (m.Success && DateOnly.TryParseExact(m.Value, "yyyyMM", out date))
            return date.AddMonths(1).AddDays(-1);

        m = Regex.Match(path, @"(\d{4})年(\d{1,2})月");
        if (m.Success && DateOnly.TryParseExact($"{m.Groups[1].Value}{m.Groups[2].Value.PadLeft(2, '0')}", "yyyyMM", out date))
            return date.AddMonths(1).AddDays(-1);

        m = Regex.Match(path, @"(\d{4})年\w*([一二三四])季度");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var year))
            return m.Groups[2].Value switch { "一" => new DateOnly(year, 3, 31), "二" => new DateOnly(year, 6, 30), "三" => new DateOnly(year, 9, 30), _ => new DateOnly(year, 12, 31) };

        return default;
    }

    private void HandleRealFile(string path, Stream stream, FundIdf[] fundCodeMap, List<ParsedInfo> reports, ref string log)
    {
        // 解析文件名
        // 备案号
        string code = "";
        string fundName = "";
        var m = Regex.Match(path, "S[0-9A-Z]{5}", RegexOptions.IgnoreCase);
        if (m.Success) code = m.Value;
        else
        {
            // 尝试从产品名获取 
            foreach (var item in fundCodeMap)
            {
                if (path.Contains(item.Name))
                {
                    code = item.Code;
                    fundName = item.Name;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            log += $"\n{path} 解析失败，未找到备案号";
            return;
        }

        ////////////////////////////////////////////////////
        //m = Regex.Match(path, @"(\d{4})[年\.-](\d{1,2})[月\.-](\d{1,2})日?|\d{8}", RegexOptions.IgnoreCase);
        //bool noDate = false;
        //if (!m.Success || DateTimeHelper.TryFindDate(m.Value) is not DateOnly date)
        //    noDate = true;

        //if (noDate)
        //{
        //    // 建投
        //    m = Regex.Match(path, @"(?<!\d)\d{6}(?!\d)");
        //    if (m.Success && DateOnly.TryParseExact(m.Value, "yyyyMM", out date))
        //        noDate = false;
        //}

        //if (noDate)
        //{
        //    log += $"\n{path} 解析失败，未找到日期";
        //    return;
        //}
        var date = ParseDateFromFileName(path);
        if (date == default)
        {
            log += $"\n{path} 解析失败，未找到日期";
            return;
        }
        ////////////////////////////////////////////////////

        var ms = new MemoryStream();
        stream.CopyTo(ms);

        switch (path)
        {
            case var p when p.Contains("投资者信息"):
                reports.Add(new ParsedInfo(DisclosureType.QuarterlyUpdate, fundName, code, date, path, ms));
                break;

            case var p when Regex.IsMatch(p, "运行信息|运行监测|季度更新"):
                reports.Add(new ParsedInfo(DisclosureType.QuarterlyUpdate, fundName, code, date, path, ms));
                break;

            case var p when p.Contains("月报"):
                reports.Add(new ParsedInfo(DisclosureType.Monthly, fundName, code, date, path, ms));
                break;

            case var p when p.Contains("季报"):
                reports.Add(new ParsedInfo(DisclosureType.Quarterly, fundName, code, date, path, ms));
                break;

            case var p when p.Contains("半年报"):
                reports.Add(new ParsedInfo(DisclosureType.SemiAnnually, fundName, code, date, path, ms));
                break;

            case var p when p.Contains("年报"):
                reports.Add(new ParsedInfo(DisclosureType.Annually, fundName, code, date, path, ms));
                break;
            default:
                break;
        }

    }

    private void HandleZip(string path, Stream stream, FundIdf[] fundCodeMap, List<ParsedInfo> reports, ref string log)
    {
        using var zip = new ZipArchive(stream);
        foreach (var ent in zip.Entries)
        {
            switch (Path.GetExtension(ent.Name).ToLower())
            {
                case ".zip":
                    HandleZip(ent.Name, ent.Open(), fundCodeMap, reports, ref log);
                    continue;

                case ".xlsx":
                case ".doc":
                case ".docx":
                case ".pdf":
                case ".xbrl":
                    HandleRealFile(ent.Name, ent.Open(), fundCodeMap, reports, ref log);
                    continue;
            }
        }
    }




    enum ParseType
    {
        InvestorInfo,
        Operation,

        /// <summary>
        /// 每月报告
        /// </summary>
        Monthly,
        /// <summary>
        /// 每季度报告
        /// </summary>
        Quarterly,
        /// <summary>
        /// 半年报告
        /// </summary>
        SemiAnnual,
        /// <summary>
        /// 年度报告
        /// </summary>
        Annual,

    }

    record ParsedInfo(DisclosureType Type, string FundName, string Code, DateOnly Date, string FileName, Stream Stream);
}


