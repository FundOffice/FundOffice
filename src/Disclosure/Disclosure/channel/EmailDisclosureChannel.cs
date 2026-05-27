
using FMO.Models;
using FMO.Utilities;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MoT;
using System.Text.RegularExpressions;

namespace FMO.Disclosure;


public class EmailDisclosureChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.Email;

    public string Name => "邮件";

    public string Description => "通过邮件发送信批公告";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => new EmailWorkConfig();

    public async Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config)
    {
        // 获取对应投资人的邮箱地址列表，发送邮件
        if (Notice is IFundDisclosureNotice fundNotice)
        {
            // 获取在公告发布日期之前持有该基金的投资人列表
            using var db = DbHelper.Base();
            var owner = db.GetCollection<TransferRecord>().Find(x => x.FundId == fundNotice.FundId && x.ConfirmedDate <= Notice.PublishDate).ToArray().GroupBy(x => x.InvestorId).Where(x => x.Sum(y => y.ShareChange()) > 0).Select(x => x.Key);

            var investors = db.GetCollection<Investor>().Find(x => owner.Contains(x.Id)).ToArray();

            // 找出缺少邮箱地址的投资人
            var missing = investors.Where(x => string.IsNullOrWhiteSpace(x.Email)).ToArray();


            if (config is EmailWorkConfig emailWorkConfig && !emailWorkConfig.ContinueSendOnMissingEmail && missing.Length > 0)
                return new(false, $"有{missing.Length}位投资人缺少邮箱地址，无法继续发送邮件\n[{string.Join(", ", missing.Select(x => x.Name))}]");

            return SendMail(Notice, investors.Where(x => !string.IsNullOrWhiteSpace(x.Email)).Select(x => x).ToArray());
        }


        return new ErrorReturn(false, "邮件发送功能尚未实现");
    }

    public ErrorReturn SendMail(IDisclosureNotice notice, Investor[] investors)
    {
        var attch = GetAttachment(notice);
        if (attch is null || attch.Value.Stream is null) return new(false, "未能找到公告附件，无法发送邮件");

        using var db = DbHelper.Base();
        var channelConfig = db.GetCollection<DisclosureChannelConfig>().FindById(Code) as EmailChannelConfig;
        if (channelConfig is null) return new(false, "邮件通道配置不正确");
        DisclosureEmailSendRecord record = db.GetCollection<DisclosureEmailSendRecord>().FindById(notice.Id) ?? new();
        var sended = record.Records.ToDictionary(x=>x.InvestorId);
        var domain = channelConfig.UserName!.Split('@').LastOrDefault() ?? "yourdomain.com";

        bool failed = false;
        using var client = new SmtpClient();

        // 连接 SMTP 服务器
        try
        {
            client.Connect(channelConfig.SmtpHost!, channelConfig.SmtpPort, channelConfig.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto);
            client.Authenticate(channelConfig.UserName!, channelConfig.Password!);
        }
        catch (Exception e)
        {
            return new(false, "连接邮件服务器失败: " + e.Message);
        }

        string bodyText = "尊敬的投资人，您好：\n 请查收附件中的公告内容。";
        List<string> mids = [];

        foreach (var inv in investors)
        {
            // 如果已经发送过了，就跳过（幂等性保障）
            var r = sended.ContainsKey(inv.Id) ? sended[inv.Id] : new(inv.Id, "", false);
            if (r.Success) continue;

            if (inv.Email is  null || !Regex.IsMatch(inv.Email.Trim(), @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                sended[inv.Id] = r;
                continue;
            }

            try
            {
                // 1. 构建邮件
                var message = new MimeMessage();
                string messageId = $"<{notice.Id}-{inv.Id}-{Guid.NewGuid().ToString("N")[..8]}@{domain}>";
                mids.Add(messageId);
                message.Headers.Add("Message-Id", messageId);

                message.From.Add(new MailboxAddress("IR", channelConfig.UserName!));

                // 添加 TO/CC/BCC
                message.To.Add(new MailboxAddress(inv.Name, inv.Email!));

                message.Headers.Add("X-Notice-Id", notice.Id.ToString());
                message.Subject = notice.Name;
                message.Body = new TextPart("plain") { Text = "尊敬的投资人，您好：\n 请查收附件中的公告内容。" };
                attch.Value.Stream.Position = 0;
                var attachment = new MimePart("application", "pdf")
                {
                    Content = new MimeContent(attch.Value.Stream, ContentEncoding.Default),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
                    {
                        FileName = attch.Value.File,
                    },
                    ContentTransferEncoding = ContentEncoding.Base64
                };

                // 🔗 使用 Multipart 组合正文 + 附件（MailKit 标准写法）
                var multipart = new Multipart("mixed");
                multipart.Add(new TextPart("plain") { Text = bodyText });
                multipart.Add(attachment);
                message.Body = multipart;

                var result = client.Send(message);
            }
            //catch (SmtpCommandException ex)
            //{
            //    record.Failed.Add(inv.Name); 
            //}
            catch (Exception ex)
            {
                failed = true;
                record.Records[inv.Id] = new(inv.Id, "", false);
                Logg.Error($"邮件信批{notice.Name}，发送邮件给投资人 {inv.Name} 失败: {ex.Message}");
            }
        }

        attch.Value.Stream.Close();
        
        return failed ? new(false, $"部分邮件发送失败：{string.Join(", ", record.Failed)}") : new(true);
    }


    private (string File, FileStream Stream)? GetAttachment(IDisclosureNotice notice)
    {
        if (notice is ITemporaryDisclosureNotice n && n.Pdf?.Exists == true)
            return (n.Pdf.File!.Name, n.Pdf.File!.OpenRead()!);
        if (notice is PeriodicalDisclosureNotice d)
        {
            if (d.Sealed?.Exists == true) return (d.Sealed.File!.Name, d.Sealed.File?.OpenRead()!);
            if (d.Pdf?.Exists == true) return (d.Pdf.File!.Name, d.Pdf.File?.OpenRead()!);
        }
        return null;
    }

    public bool IsSupported(DisclosureType type)
    {
        return true;
    }

    public bool RequireConfigWork(DisclosureType type) => true;

    ErrorReturn IDisclosureChannel.VerifyNotice(IDisclosureNotice Notice)
    {
        switch (Notice.Type)
        {
            case DisclosureType.Monthly:
            case DisclosureType.Quarterly:
            case DisclosureType.SemiAnnually:
            case DisclosureType.Annually:
                if (Notice is not PeriodicalDisclosureNotice d) return new(false, "公告类型与通道不匹配");
                if (d.Pdf?.Exists != true && d.Sealed?.Exists != true) return new(false, "PDF文件不存在");
                return new(true);

            case DisclosureType.TemporaryOpen:
            case DisclosureType.HugeRedemption:
            case DisclosureType.FundSetup:
            case DisclosureType.OtherFundNotice:
            case DisclosureType.MangerChange:
            case DisclosureType.OfficeAddressChange:
            case DisclosureType.OtherManagerNotice:
                return Notice is ITemporaryDisclosureNotice n && n.Pdf?.Exists == true ? new(true) : new(false, "文件不存在");

            default:
                return new(false, "不支持的公告类型");
        }
    }
}



public class EmailWorkConfig : IWorkConfig
{
    /// <summary>
    /// 当部分投资人缺少邮箱地址时，是否仍然执行发送操作，保证其它投资人能够收到邮件
    /// </summary>
    public bool ContinueSendOnMissingEmail { get; set; } = true;
}

internal class DisclosureEmailSendRecord
{
    internal record SendedRecord(int InvestorId, string MessageId, bool Success);

    public long Id { get; set; }

    public List<SendedRecord> Records { get; set; } = [];

    public List<int> Sended { get; set; } = [];

    public List<string> Failed { get; set; } = [];

}