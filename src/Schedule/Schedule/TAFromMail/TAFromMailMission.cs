using CommunityToolkit.Mvvm.Messaging;
using ExcelDataReader;
using FMO.Models;
using FMO.Utilities;
using MimeKit;
using Serilog;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace FMO.Schedule;

/// <summary>
/// 
/// </summary>
public class MailMissionRecord
{
    public required string Id { get; set; }

    public DateTime Time { get; set; }
    public bool HasError { get; internal set; }
}

public class TAFromMailMission : MailMission
{

    public int Interval { get; set; } = 15; 


    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddMinutes(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddMinutes(Interval);
    }

    protected override bool WorkOverride()
    {
        // 获取所有缓存  
        var di = new DirectoryInfo(@$"files\mailcache\{MailName}");
        if (!di.Exists)
        {
            WeakReferenceMessenger.Default.Send(new MissionWorkMessage(Id, "邮件缓存文件夹不存在"));
            Log.Error($"Mission[{Id}] 邮件缓存文件夹不存在");
            return false;
        }

        // 获取所有文件
        var files = di.GetFiles();
        using var db = new MissionDatabase();
        var cat = db.GetCollection<MailCategoryInfo>().FindAll().Where(x => x.Category != MailCategory.Unk && x.Category.HasFlag(MailCategory.TA)).Select(x => x.Id).ToArray();
        //var cat = db.GetCollection<MailCategoryInfo>().Find(x => x.Category != MailCategory.Unk && (x.Category & MailCategory.TA) > 0).Select(x => x.Id).ToArray();

        var coll = db.GetCollection<MailMissionRecord>($"mm_{Id}");
        var worked = coll.FindAll().ExceptBy(cat, x => x.Id).ToArray();

        var work = IgnoreHistory ? files : files.ExceptBy(worked.Select(x => x.Id), x => x.Name).ToArray();

        var log = $"待处理邮件 {work.Length} 个";

        double unit = 100.0 / work.Length;
        double progress = 0;
        foreach (var f in work)
        {
            try
            {
                WorkOne(f,ref log);
                coll.Upsert(new MailMissionRecord { Id = f.Name, Time = DateTime.Now });
            }
            catch (Exception ex)
            {
                Log.Error($"TA From Mail {ex}");
            }

            progress += unit;
            WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = progress });
        }

        log += $"完成";


        using (var mdb = new MissionDatabase())
            mdb.GetCollection<MissionRecord>().Insert(new MissionRecord { MissionId = Id, Time = DateTime.Now, Record = log });

        return true;
    }

    private void WorkOne(FileInfo f, ref string log)
    {
        using MimeMessage mime = LoadMail(f.FullName);

        if (DetermineCategory(mime) switch { MailCategory.Unk or MailCategory.TA => false, _ => true })
            return;


        // 没有附件
        if (!mime.Attachments.Any()) return;

        var sss = mime.Subject;
        var sender = (mime.From[0] as MailboxAddress)?.Domain ?? "";

        foreach (MimePart item in mime.Attachments)
        {
            var filepath = item.FileName;
            if (filepath.ToLower().EndsWith(".zip"))
            {
                using var ms = new MemoryStream();
                item.Content.DecodeTo(ms);
                ZipArchive zip = new ZipArchive(ms);

                foreach (var ent in zip.Entries)
                {
                    if (Regex.IsMatch(ent.Name, ".xls|.xlsx", RegexOptions.IgnoreCase))
                    {
                        if (ent.Name.Contains("交易"))
                        {
                            log += $"\n{ent.Name}";
                        }

                        using var ss = ent.Open();
                        var mss = new MemoryStream();
                        ss.CopyTo(mss);
                        WorkOnSheet(mss, sender, log);
                    }
                    else if (Regex.IsMatch(ent.Name, ".pdf", RegexOptions.IgnoreCase))
                    {
                        using var ss = ent.Open();
                        var mss = new MemoryStream();
                        ss.CopyTo(mss);
                        WorkOnPdf(mss, sender);
                    }
                }
            }
            else if (Regex.IsMatch(filepath, ".xls|.xlsx", RegexOptions.IgnoreCase))
            {
                if (filepath.Contains("交易确认"))
                {
                    log += $"\n{filepath}";
                }

                var ms = new MemoryStream();
                item.Content.DecodeTo(ms);

                WorkOnSheet(ms, sender,log);
            }
            else if (Regex.IsMatch(filepath, ".pdf", RegexOptions.IgnoreCase))
            {
                var ms = new MemoryStream();
                item.Content.DecodeTo(ms);

                WorkOnPdf(ms, sender);
            }
        }





    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mss"></param>
    /// <param name="domain">识别托管</param>
    private void WorkOnPdf(MemoryStream mss, string domain)
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="domain">识别托管</param>
    private bool WorkOnSheet(Stream stream, string domain, string log)
    {
        if (stream is null || stream.Length == 0) return false;

        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        using var reader = ExcelReaderFactory.CreateReader(stream);

        if (!IsTASheet(reader)) return false;

        reader.Reset();
        reader.Read();
        var head = new object[reader.FieldCount];
        reader.GetValues(head);


        List<TransferRecord> records = new List<TransferRecord>();

        // 列表、确认函
        if (head.Length > 6)
        {
            //列表 
            reader.Reset();
            SheetParser parser = SheetParser.Create(domain);
            var ta = parser.ParseTASheet(reader);
            records.AddRange(ta);
        }
        else
        {
            //确认函
            reader.Reset();
            SheetParser parser = SheetParser.Create(domain);
            var ta = parser.ParseTAConfirm(reader);
            records.AddRange(ta);
        }

        // 更新
        if (records.Count > 0)
        {
            log += $"\n {string.Join('\n', records.Select(x => $"{x.InvestorName}-{x.ConfirmedDate}-{x.FundName}"))}\n";
            using var db = DbHelper.Base();
            foreach (var rec in records)
            {
                // 校验
                if (rec.Type == TransferRecordType.UNK || (rec.ConfirmedShare == 0 && rec.ConfirmedAmount == 0))
                {
                    Log.Error($"TA Bad Data {rec.PrintProperties()}");
                    continue;
                }

                // 通过平台获取的不会被更新
                var old = db.GetCollection<TransferRecord>().FindOne(x => x.ExternalId == rec.ExternalId);

                if (old is null)
                    old = db.GetCollection<TransferRecord>().FindOne(x => x.InvestorIdentity == rec.InvestorIdentity && x.ConfirmedDate == rec.ConfirmedDate &&
                                x.FundCode == rec.FundCode && x.ConfirmedShare == rec.ConfirmedShare && x.ConfirmedAmount == rec.ConfirmedAmount);

                if (old is not null && (old.Source != "manual" && !string.IsNullOrWhiteSpace(old.Source)))
                    continue;

                rec.Id = old?.Id ?? 0;
                db.GetCollection<TransferRecord>().Upsert(rec);

                WeakReferenceMessenger.Default.Send(rec);
            }
        }

        return records.Count > 0;
    }



    private static void PostHandle(TransferRecord r)
    {
        // 后处理
        // 对应fund id
        using var db = DbHelper.Base();
        var fund = db.FindFund(r.FundCode);

        // 没找到对应的fund
        if (fund is null)
        {
            Log.Error($"{r.FundName}({r.FundCode}) {r.InvestorName} {r.ConfirmedDate} 未找到对应基金");
            return;
        }

        if (string.IsNullOrWhiteSpace(r.FundName))
        {
            Log.Error($"{r.FundName}({r.FundCode}) {r.InvestorName} {r.ConfirmedDate} 数据异常");
            return;
        }
        r.FundId = fund.Id;

        // 有子份额的
        var m = Regex.Match(r.FundName, @$"{fund.Name}(\w+)");
        if (m.Success)
            r.ShareClass = m.Groups[1].Value;

        r.Source = "Mail";
    }



    public static TAFieldGather[] GetFieldGather(string domain)
    {
        switch (domain)
        {
            case "cstisc.com":
                return [new() { Fee = new("总费用", x => FieldGather.DecimalValue(x), (x, y) => x.Fee = y), ExternalId = new("TA确认号", o => FieldGather.StringValue(o), (x, y) => x.ExternalId = y) }];


        }

        return [new()];
    }



    public bool IsTASheet(IExcelDataReader reader)
    {
        reader.Reset();

        for (int j = 0; j < Math.Min(4, reader.RowCount); j++)
        {
            if (!reader.Read()) return false;

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var s = reader.GetString(i);
                if (s is null) continue;
                if (s.Contains("交易确认") || s.Contains("确认日期"))
                    return true;
            }
        }

        return false;
    }

}

