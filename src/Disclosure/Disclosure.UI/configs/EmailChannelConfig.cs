using FMO.Models;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace FMO.Disclosure;

/// <summary>
/// 邮件通道配置
/// </summary>
[AutoViewModel(typeof(EmailChannelConfig))]
public partial class EmailChannelConfigViewModel : ChannelConfigViewModel
{
    public override string ChannelCode => DisclosureChannelCode.Email;

    protected override DisclosureChannelConfig BuildOverride() => Build();

    protected override bool VerifyOverride()
    {
        bool failed = false;
        Error = string.Empty; // 初始化错误信息

        // 参数验证
        if (string.IsNullOrWhiteSpace(SmtpHost) || !Regex.IsMatch(SmtpHost, @"^[a-zA-Z0-9.-]+$"))
        {
            Error += "SMTP服务器地址不合法\n";
            failed = true;
        }
        if (SmtpPort <= 0 || SmtpPort > 65535)
        {
            Error += "SMTP端口不合法\n";
            failed = true;
        }
        if (string.IsNullOrWhiteSpace(UserName) || !Regex.IsMatch(UserName, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
        {
            Error += "用户名不合法\n";
            failed = true;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            Error += "密码不能为空\n";
            failed = true;
        }

        if (failed) return false;

        // 连接 + 验证
        try
        {
            using var tcpClient = new TcpClient();
            tcpClient.ReceiveTimeout = 10000;
            tcpClient.SendTimeout = 10000;

            // 同步连接（避免 async/await 混用问题）
            if (!tcpClient.ConnectAsync(SmtpHost!, SmtpPort).Wait(10000))
            {
                throw new Exception("连接超时");
            }

            Stream baseStream = tcpClient.GetStream();

            // SSL/TLS 加密
            if (UseSsl)
            {
                var sslStream = new SslStream(baseStream, false, (_, _, _, _) => true);
                sslStream.AuthenticateAsClient(SmtpHost!);
                baseStream = sslStream;
            }

            // 使用 UTF8 编码避免字符问题，AutoFlush 确保及时发送
            var reader = new StreamReader(baseStream, Encoding.UTF8);
            var writer = new StreamWriter(baseStream, Encoding.UTF8) { AutoFlush = true, NewLine = "\r\n" };

            // ✅ 修复：正确的 SMTP 多行响应读取
            string ReadAllResponse()
            {
                StringBuilder sb = new StringBuilder();
                string line;

                while ((line = reader.ReadLine()!) != null)
                {
                    sb.AppendLine(line);

                    // SMTP 协议规则：只有第4个字符是空格，才是最后一行
                    // 这行是关键！你之前缺这个！
                    if (line.Length >= 4 && line[3] == ' ')
                        break;
                }

                return sb.ToString().Trim();
            }

            // 1. 读取欢迎信息
            string welcome = ReadAllResponse();
            if (!welcome.StartsWith("220"))
                throw new Exception($"SMTP 欢迎响应异常: {welcome}");

            writer.Flush();
            // 2. 发送 EHLO
            writer.WriteLine($"EHLO {Dns.GetHostName()}");
            writer.Flush();  // 确保命令立即发送
            string ehloResponse = ReadAllResponse();  // 只读取一次

            // 如果服务器不支持 EHLO，降级使用 HELO
            if (ehloResponse.StartsWith("502") || ehloResponse.StartsWith("500"))
            {
                writer.WriteLine($"HELO {Dns.GetHostName()}");
                writer.Flush();
                ehloResponse = ReadAllResponse();
            }


            // 3. 尝试认证（优先 PLAIN，失败则尝试 LOGIN）
            bool authSuccess = TryAuthPlain(writer, reader, ReadAllResponse) ||
                              TryAuthLogin(writer, reader, ReadAllResponse);

            if (!authSuccess)
                throw new Exception("用户名/密码错误，所有认证方式均失败");

            // 4. 正常退出
            writer.WriteLine("QUIT");
            ReadAllResponse();
            return true;
        }
        catch (Exception ex)
        {
            failed = true;
            Error += ParseSmtpError(ex);
            return false;
        }
    }

    // ✅ 分离 AUTH PLAIN 逻辑
    private bool TryAuthPlain(StreamWriter writer, StreamReader reader, Func<string> readResponse)
    {
        try
        {
            string authString = $"\0{UserName}\0{Password}";
            string authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

            writer.WriteLine("AUTH PLAIN " + authBase64);
            string response = readResponse();

            return response.StartsWith("235") || response.StartsWith("250");
        }
        catch
        {
            return false; // PLAIN 失败，尝试其他方式
        }
    }

    // ✅ 修复 AUTH LOGIN 流程（关键！）
    private bool TryAuthLogin(StreamWriter writer, StreamReader reader, Func<string> readResponse)
    {
        try
        {
            // 步骤1: 发送 AUTH LOGIN
            writer.WriteLine("AUTH LOGIN");
            string response = readResponse();
            // 服务器应返回 334 (Base64: "Username:")，但也可能直接返回 334 无提示

            // 步骤2: 发送用户名（Base64）
            string userBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(UserName!));
            writer.WriteLine(userBase64);
            response = readResponse();
            // 服务器应返回 334 (Base64: "Password:")

            // 步骤3: 发送密码（Base64）
            string passBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Password!));
            writer.WriteLine(passBase64);
            response = readResponse();

            // 步骤4: 检查最终结果
            return response.StartsWith("235") || response.StartsWith("250");
        }
        catch (Exception ex)
        {
            // 记录详细错误便于调试
            System.Diagnostics.Debug.WriteLine($"AUTH LOGIN 失败: {ex.Message}");
            return false;
        }
    }

    // ✅ 统一错误解析（大小写不敏感 + 常见错误映射）
    private string ParseSmtpError(Exception ex)
    {
        string msg = ex.Message.ToLowerInvariant();

        if (msg.Contains("535") || msg.Contains("authentication failed") ||
            msg.Contains("invalid credentials") || msg.Contains("密码") || msg.Contains("用户名"))
            return "SMTP 认证失败：用户名或密码不正确";

        if (msg.Contains("504") || msg.Contains("not implemented") ||
            msg.Contains("bad command") || msg.Contains("command unrecognized"))
            return "SMTP 服务器不支持当前认证方式，请检查服务器配置";

        if (ex is SocketException || msg.Contains("connection") || msg.Contains("连接") || msg.Contains("timeout"))
            return "无法连接 SMTP 服务器：地址/端口错误或防火墙拦截";

        return $"验证失败：{ex.Message}";
    }
}
