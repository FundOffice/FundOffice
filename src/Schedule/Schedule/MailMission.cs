using MimeKit;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FMO.Schedule;

public class MailMission : Mission
{

    static MailMission() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

    public string? MailName { get => field; set { field = value; _collection = value is null ? null : BitConverter.ToString(SHA256.HashData(Encoding.Default.GetBytes(value))).Replace("-", "").ToLowerInvariant(); } }

    public bool IgnoreHistory { get; set; }


    protected string? _collection { get; set; }

    public override void Init()
    {
        base.Init();
        IgnoreHistory = false;
    }

    public virtual MailCategory DetermineCategory(MimeMessage? message)
    {
        if (message is null) return MailCategory.Unk;
        MailCategory category = MailCategory.Unk;


        if (message.Subject.Contains("估值表"))
            category |= MailCategory.ValueSheet;

        if (message.Subject.Contains("对账单") || (message.TextBody?.Contains("对账单") ?? false))
            category |= MailCategory.Statement;

        if (message.Subject.Contains("交易确认") || (message.TextBody?.Contains("交易确认") ?? false))
            category |= MailCategory.TA;

        if (Regex.IsMatch($"{message.Subject} {message.TextBody}", "类季报|类月报|类年报|类半年报|季度更新"))
            category |= MailCategory.Disclosure;

        using var db = new MissionDatabase();
        db.GetCollection<MailCategoryInfo>().Upsert(new MailCategoryInfo(message.MessageId, message.Subject, MailCategory.ValueSheet));
        return category;
    }


    protected MimeMessage LoadMail(string path)
    {
        MimeMessage msg = MimeMessage.Load(path);

        if (GarbledTextChecker.IsGarbled(msg.Subject) || GarbledTextChecker.IsGarbled(msg.TextBody ?? ""))
        {
            msg.Dispose();
            msg = MimeMessage.Load(new ParserOptions { CharsetEncoding = Encoding.GetEncoding("GBK") }, path);

            if (GarbledTextChecker.IsGarbled(msg.Subject) || GarbledTextChecker.IsGarbled(msg.TextBody ?? ""))
            {
                msg.Dispose();
                msg = MimeMessage.Load(new ParserOptions { CharsetEncoding = Encoding.GetEncoding("GB18030") }, path);
            }
        }

        return msg;
    }

}

public class GarbledTextChecker
{
    // 检查字符是否在允许的范围内
    private static bool IsValidChar(char c)
    {
        // 英文数字: A-Z, a-z, 0-9
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= 'a' && c <= 'z') return true;
        if (c >= '0' && c <= '9') return true;

        // 中文范围: 基本汉字 (4E00-9FFF)
        if (c >= '\u4E00' && c <= '\u9FFF') return true;

        // 常见符号白名单
        string commonSymbols = " \t\n\r.,;:?!'\"()[]{}<>-_+=@#$%^&*|\\/~`";
        if (commonSymbols.Contains(c)) return true;

        return false;
    }

    // 检查字符串是否乱码（非法字符超过15%）
    public static bool IsGarbled(string input, double threshold = 0.15)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        int totalCount = input.Length;
        int invalidCount = 0;

        foreach (char c in input)
        {
            if (!IsValidChar(c))
                invalidCount++;
        }

        // 计算非法字符比例
        double invalidRatio = (double)invalidCount / totalCount;

        // 返回是否超过阈值
        return invalidRatio > threshold;
    }
}
