using CommunityToolkit.Mvvm.Messaging;
using MimeKit;
using Serilog;
using System.IO;
using System.Net.NetworkInformation;

namespace FMO.Schedule;


[Flags]
public enum MailCategory
{
    Unk,

    /// <summary>
    /// 估值表
    /// </summary>
    ValueSheet,

    /// <summary>
    /// TA
    /// </summary>
    TA = 0x2,

    /// <summary>
    /// 结算单
    /// </summary>
    Statement = 0x4,

    Disclosure = 0x8,
}

public record MailCategoryInfo(string Id, string Subject, MailCategory Category, bool IsSealed = false);



public class MailCacheMission : MailMission
{

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
        var log = $"Mission[{Id}][缓存邮件]\n";

        try
        {
            if (string.IsNullOrWhiteSpace(MailName) || string.IsNullOrWhiteSpace(MailPassword) || string.IsNullOrWhiteSpace(MailPop3))
            {
                Log.Error($"{log} 邮箱配置错误");
                return false;
            }
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

            // 登录
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


            // 缓存文件夹
            var di = new DirectoryInfo(@$"files\mailcache\{MailName}");
            if (!di.Exists) di.Create();

            // 获取已缓存
            var files = di.GetFiles();

            var mailcount = pop3Client.GetMessageCount();
            var mailids = pop3Client.GetMessageUids();

            log += $"\n读取邮件，共 {mailids.Count} 封";

            // 未缓存
            List<string> mails = new();
            if (!IgnoreCache)
                mails = files.Where(x => x.Length > 0).Select(x => x.Name).ToList();

            var needload = mailids.Index().ExceptBy(mails, x => x.Item);

            log += $"\n检查缓存，新邮件{needload.Count()} 封";

            double unit = 100.0 / needload.Count();


            foreach (var (i, v) in needload)
            {
                log += $"\n缓存邮件";
                using MimeMessage msg = pop3Client.GetMessage(i);

                try
                {
                    DetermineCategory(msg);

                    using var fs = new FileStream(Path.Combine(di.FullName, v), FileMode.Create);
                    msg.WriteTo(fs);
                    fs.Flush();

                    log += $":     {msg.Subject}";
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

            WeakReferenceMessenger.Default.Send(new MissionProgressMessage { Id = Id, Progress = 100 });
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
}
