using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using MimeKit;

namespace FMO.Schedule;



public class MissionMailCredentialMessage
{
    public required int Id { get; set; }

    public bool IsSuccessed { get; set; }
}


public class GatherDailyFromMailMission : Mission
{
    public string? MailName { get; set; }

    public string? MailPassword { get; set; }

    public string? MailPop3 { get; set; }


    public bool IsAccountVerified { get; set; }

    public int Interval { get; set; } = 15;


    public bool IgnoreCache { get; set; }

    protected override void SetNextRun()
    {
        NextRun = (LastRun ?? DateTime.Now).AddMinutes(Interval);
        if (NextRun < DateTime.Now) NextRun = DateTime.Now.AddMinutes(Interval);
    }

    protected override bool WorkOverride()
    {
        // 验证是否有未同步的fund


        DateTime time = DateTime.Now;

        double progress = 0;
        var log = $"Mission[{Id}][从估值邮箱采集估值表]\n";

        try
        {
            List<Fund>? funds = null;
            using (var db = DbHelper.Base())
                funds = db.GetCollection<Fund>().FindAll().ToList();

            var unsync = funds.Where(x => x.Status >= FundStatus.Normal && x.PublicDisclosureSynchronizeTime == default).ToArray();
            if (unsync.Length > 0)
            {
                log += $"{string.Join(',', unsync.Select(x => x.Name))}，{unsync.Length}个基金未初始化，待初始化后，可执行";

                HandyControl.Controls.Growl.Warning("待基金初始化完成后，再执行【净值更新】");
                return true;
            }


            if (string.IsNullOrWhiteSpace(MailName) || string.IsNullOrWhiteSpace(MailPassword) || string.IsNullOrWhiteSpace(MailPop3)) return false;

            using var pop3Client = new MailKit.Net.Pop3.Pop3Client();
            pop3Client.Connect(MailPop3, 995, true, new CancellationTokenSource(5000).Token);
            log = "连接邮箱服务器..................";


            if (!pop3Client.IsConnected)
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    log += "\n无法连接邮箱服务器，请检查配置";
                }
                else
                {
                    log += "\n网络连接失败，请检查网络";
                }

                var rec = new MissionRecord { MissionId = Id, Time = time, Record = log };
                using (var db = new MissionDatabase())
                {
                    var c = db.GetCollection<MissionRecord>();
                    c.Insert(rec);
                }

                //不再执行
                return true;
            }

            pop3Client.Authenticate(MailName, MailPassword);

            if (!pop3Client.IsAuthenticated)
            {
                IsAccountVerified = false;
                WeakReferenceMessenger.Default.Send(new MissionMailCredentialMessage { Id = Id, IsSuccessed = false });

                log += "\n账户名或密码错误，请检查配置";
                var rec = new MissionRecord { MissionId = Id, Time = time, Record = log };
                using (var db = new MissionDatabase())
                {
                    db.GetCollection<Mission>().Update(this);

                    var c = db.GetCollection<MissionRecord>();
                    c.Insert(rec);
                }

                //不再执行
                return true;
            }


            var mailcount = pop3Client.GetMessageCount();
            var mailids = pop3Client.GetMessageUids();

            log += $"\n读取邮件，共 {mailids.Count} 封";

            List<string> mails = new();
            if (!IgnoreCache)
            {
                using (var db = new MissionDatabase())
                    mails = db.GetCollection<GzMailInfo>().Query().Select(x => x.Id).ToList();
            }

            var needload = mailids.Index().ExceptBy(mails, x => x.Item);//.Select(x => mailids.IndexOf(x));

            log += $"\n检查缓存，新邮件{needload.Count()} 封";

            double unit = 100.0 / needload.Count();

            foreach (var (i, v) in needload)
            {
                try
                {
                    log += $"\n缓存邮件";
                    using MimeMessage msg = pop3Client.GetMessage(i);
                    var gz = new GzMailInfo { Id = mailids[i], Subject = msg.Subject, Time = msg.Date.DateTime };
                    log += $":     {msg.Subject}";

                    HandleMimeMessage(gz, msg, funds, ref log);

                    log += $"   完成";
                }
                catch (Exception er)
                {
                    log += $"   失败 {er.Message}";
                }
                log += "\n";
                progress += unit;
                WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = progress });
            }

        }
        catch (Exception er)
        {
            log += $"{er.Message}";
        }


        using (var db = new MissionDatabase())
        {
            var c = db.GetCollection<MissionRecord>();
            c.Insert(new MissionRecord { MissionId = Id, Time = time, Record = log });
        }
        return true;
    }


    public static void HandleMimeMessage(GzMailInfo gz, MimeMessage msg, IReadOnlyList<Fund> funds, ref string log)
    {
        // 有附件
        if (msg.Attachments.Any())
            Extract(gz, msg, funds, ref log);

        ///记录
        using (var db = new MissionDatabase())
            db.GetCollection<GzMailInfo>().Upsert(gz);
    }

    private static List<DailyValue>? Extract(GzMailInfo gz, MimeMessage msg, IReadOnlyList<Fund> funds, ref string log)
    {
        if (!msg.Attachments.Any()) { log += " 没有附件"; return null; }

        try
        {
            gz.HasError = false;
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
                    else if(filepath.Contains(".xls"))//if (Regex.IsMatch(filepath, @"估值表"))
                    {
                        var ms = new MemoryStream();
                        item.Content.DecodeTo(ms);

                        var (fn, co, dy) = ValuationSheetHelper.ParseExcel(ms);
                        log += $"\n         ↳{fn} {dy?.Date}";
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

            List<GzMailAttachInfo> attachments = new();
            List<DailyValue> days = new();
            foreach (var x in ds)
            {
                var info = new GzMailAttachInfo { Name = x.File };
                attachments.Add(info);
                if (x.Daily is null)
                    continue;

                days.Add(x.Daily);
                var fund = x.Fund;
                if (fund is null)
                {
                    log += $"\n\n         没有对应产品\n"; 
                    gz.HasError = true;

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
                if (di.Exists && di.Length == x.Stream!.Length) continue;
                using var fs = di.OpenWrite();
                x.Stream!.Seek(0, SeekOrigin.Begin);
                x.Stream!.CopyTo(fs);
                x.Daily.SheetPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), di.FullName);
                fs.Flush();
            }


            DataTracker.OnDailyValue(ds.Where(x => x.Fund is not null && x.Daily is not null).Select(x => x.Daily!)); 

            gz.Attachments = attachments.ToArray();

            return days;
        }
        catch (Exception er)
        {
            gz.HasError = true;
            log += $"\n\n         {er.Message}\n";
            return null;
        }

    }
}
