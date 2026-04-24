namespace FMO.Disclosure;

/// <summary>
/// 邮件通道配置
/// </summary>
public class EmailChannelConfig : DisclosureChannelConfig
{
    public override string ChannelCode => DisclosureChannelCode.Email;



    /// <summary>
    /// SMTP服务器地址
    /// </summary>
    public  string? SmtpHost { get; set; }

    /// <summary>
    /// SMTP端口
    /// </summary>
    public int SmtpPort { get; set; } = 465;

    /// <summary>
    /// 使用SSL
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// 用户名
    /// </summary>
    public  string? UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public  string? Password { get; set; }


}
