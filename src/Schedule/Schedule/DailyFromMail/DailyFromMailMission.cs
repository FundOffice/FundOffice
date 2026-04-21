using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using MimeKit;
using Serilog;
using System.IO;
using System.IO.Compression;

namespace FMO.Schedule;



public class DailyFromMailMission : MailMission
{
    public int Interval { get; set; } = 15;



    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddMinutes(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddMinutes(Interval);
    }

    protected override bool WorkOverride()
    {
        if (MailName is null)
        {
            IsEnabled = false;
            return false;
        }
        // 获取所有缓存 
        var di = new DirectoryInfo(@$"files\mailcache\{MailName}");
        if (!di.Exists)
        {
            Log.Error($"Mission[{Id}] 邮件缓存文件夹不存在");
            return false;
        }

        string log = "";
        // 获取所有文件
        var files = di.GetFiles();
        using var db = new MissionDatabase();
        LiteDB.ILiteCollection<MailMissionRecord> coll = db.GetCollection<MailMissionRecord>($"mm_{Id}");
        var cat = db.GetCollection<MailCategoryInfo>().FindAll().Where(x => x.Category != MailCategory.Unk && x.Category.HasFlag(MailCategory.ValueSheet)).Select(x => x.Id).ToArray();

        var worked = coll.FindAll().ExceptBy(cat, x => x.Id).ToArray();

        var work = IgnoreHistory ? files : files.ExceptBy(worked.Select(x => x.Id), x => x.Name).ToArray();

        double unit = 100.0 / work.Length;
        double progress = 0;
        foreach (var f in work)
        {
            try
            {
                bool err = WorkOne(f, log);
                coll.Upsert(new MailMissionRecord { Id = f.Name, Time = DateTime.Now, HasError = err });
            }
            catch (Exception ex)
            {
                Log.Error($"Daily From Mail {ex}");
            }

            progress += unit;
            WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = progress });
        }

        using (var mdb = new MissionDatabase())
            mdb.GetCollection<MissionRecord>().Insert(new MissionRecord { MissionId = Id, Time = DateTime.Now, Record = log });

        WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = 100 });
        return true;
    }

    private bool WorkOne(FileInfo file, string log)
    {
        using MimeMessage msg = LoadMail(file.FullName);

        DetermineCategory(msg);

        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().FindAll().ToArray();
        bool haserror = false;

        // 有附件
        if (msg.Attachments.Any())
        {
            var dy = Extract(msg, funds, ref haserror, ref log);
            if (msg.Attachments.Count() == 1 && dy?.Count > 0)
                db.GetCollection<MailCategoryInfo>().Upsert(new MailCategoryInfo(msg.MessageId, msg.Subject, MailCategory.ValueSheet, true));
        }
        return haserror;
    }



    private static List<DailyValue>? Extract(MimeMessage msg, IReadOnlyList<Fund> funds, ref bool haserror, ref string log)
    {
        haserror = false;

        try
        {
            List<(string? FundName, Fund? Fund, string File, DailyValue? Daily, Stream? Stream)> ds = new();
            foreach (MimePart item in msg.Attachments)
            {
                var filepath = item.FileName;
                log += $"\n         {filepath}";

                try
                {
                    //zip
                    if (filepath.ToLower().EndsWith(".zip"))
                    {
                        using var ms = new MemoryStream();
                        item.Content.DecodeTo(ms);
                        ZipArchive zip = new ZipArchive(ms);

                        foreach (var ent in zip.Entries)
                        {
                            using var ss = ent.Open();
                            var mss = new MemoryStream();
                            ss.CopyTo(mss);
                            var (fn, co, dy) = ValuationSheetHelper.ParseExcel(mss);
                            log += $"\n         ↳{fn} {dy?.Date}";

                            ds.Add((fn, funds.FirstOrDefault(x => x.Name == fn || x.Code == co), ent.Name, dy, mss));
                        }
                    }
                    else if (filepath.Contains(".xls"))
                    {
                        var ms = new MemoryStream();
                        item.Content.DecodeTo(ms);

                        var (fn, co, dy) = ValuationSheetHelper.ParseExcel(ms);
                        log += $"\n         ↳{fn} {dy?.Date}";
                        if (dy is not null)
                            ds.Add((fn, funds.FirstOrDefault(x => x.Name == fn || x.Code == co), filepath, dy, ms));
                    }
                    //else
                    //    ds.Add((null, null, filepath, null, null));
                }
                catch (Exception er)
                {
                    log += $"\n         {er.GetType().Name}:{er.Message}";
                    return null;
                }
            }

            List<DailyValue> days = new();
            foreach (var x in ds)
            {
                var info = new GzMailAttachInfo { Name = x.File };
                if (x.Daily is null)
                    continue;

                days.Add(x.Daily);
                var fund = x.Fund;
                if (fund is null)
                {
                    log += $"\n\n         没有对应产品\n";
                    haserror = true;

                    // 文件保存到
                    Directory.CreateDirectory("files\\unk_sheets");

                    using var stream = new FileStream($"files\\unk_sheets\\{x.File}", FileMode.Create);
                    x.Stream!.Seek(0, SeekOrigin.Begin);
                    x.Stream!.CopyTo(stream);
                    stream.Close();

                    continue;
                }


                x.Daily.FundId = fund.Id;
                info.DailyId = x.Daily.Id;
                info.FundId = fund.Id;

                ///保存
                var folder = FundHelper.GetFolder(fund.Id, "Sheet");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var di = new FileInfo(Path.Combine(folder, x.File));
                if (!di.Exists || di.Length != x.Stream!.Length)
                {
                    using var fs = di.OpenWrite();
                    x.Stream!.Seek(0, SeekOrigin.Begin);
                    x.Stream!.CopyTo(fs);
                    fs.Flush();
                }
                x.Daily.SheetPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), di.FullName);
            }

            DataTracker.OnDailyValue(ds.Where(x => x.Fund is not null && x.Daily is not null).Select(x => x.Daily!));
            return days;
        }
        catch (Exception er)
        {
            haserror = true;
            log += $"\n\n         {er.Message}\n";
            return null;
        }

    }
}
