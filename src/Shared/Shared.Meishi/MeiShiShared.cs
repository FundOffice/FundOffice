using FMO.Shared.MeiShi;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;




namespace FMO.Shared;




public static class MeiShiShared
{

    public static async Task<(bool Success, string? Token, string? Error)> Login(HttpClient client, string user, string password)
    {
        // 校验 

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            return (false, null, "登录MeiShi错误：用户名或密码为空");

        // ===== 第一个请求：SSO 登录 =====
        var request1 = new HttpRequestMessage(HttpMethod.Post, "https://sso.simu800.com/ssocenter/login/doLogin");

        // 设置 Headers（不含 Content-Type，它属于 Content）
        request1.Headers.Add("accept", "application/json, text/plain, */*");
        request1.Headers.Add("accept-language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        request1.Headers.Add("origin", "https://sso.simu800.com");
        request1.Headers.Add("priority", "u=1, i");
        //request1.Headers.Add("referer", "https://sso.simu800.com/ssoweb/user/login?v=1764310000298");
        request1.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"142\", \"Microsoft Edge\";v=\"142\", \"Not_A Brand\";v=\"99\"");
        request1.Headers.Add("sec-ch-ua-mobile", "?0");
        request1.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        request1.Headers.Add("sec-fetch-dest", "empty");
        request1.Headers.Add("sec-fetch-mode", "cors");
        request1.Headers.Add("sec-fetch-site", "same-origin");
        // 忽略无效头: "token;" （如需 token，请用 request1.Headers.Add("token", "xxx")）
        request1.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");


        // 构造 POST 请求体
        var jsonContent = @$"{{""encryptData"":""{LoginEncryptor.EncryptLogin(user, password)}""}}";

        request1.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        request1.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        // 发送 POST 请求
        var response = await client.SendAsync(request1);

        var loginjson = await response.Content.ReadAsStringAsync();

        if (loginjson.Contains("密码错误"))
            return (false, null, "登录MeiShi错误：用户名或密码错误");

        var result = JsonSerializer.Deserialize<LoginResultJson>(loginjson);
        if (!result!.success)
            return (false, null, $"登录MeiShi错误：{result.message}");

        // ===== 从 response1 提取 Set-Cookie =====
        var updatedCookies = new Dictionary<string, string>(); // 以初始 cookie 为基础

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (string setCookie in setCookieHeaders)
            {
                // 使用正则提取 "name=value" 部分（忽略 ; 后的属性）
                var match = Regex.Match(setCookie, @"^([^=]+)=((?:[^;]|(?<=\\);)*)");
                if (match.Success)
                {
                    string name = match.Groups[1].Value.Trim();
                    string value = match.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(value)) // 防止删除 cookie（value 为空时应删除，但此处简化）
                        updatedCookies[name] = value;
                    else
                        updatedCookies.Remove(name); // 可选：处理 cookie 删除
                }
            }
        }


        // ===== 第二个请求：VIP OAuth 登录 =====
        var request2 = new HttpRequestMessage(HttpMethod.Post, "https://vipfunds.simu800.com/vip-manager/managerUser/managerLoginOauth");

        // 设置 Headers
        request2.Headers.Add("accept", "application/json, text/plain, */*");
        request2.Headers.Add("accept-language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        request2.Headers.Add("origin", "https://vipfunds.simu800.com");
        request2.Headers.Add("priority", "u=1, i");
        //request2.Headers.Add("referer", "https://vipfunds.simu800.com/vipmanager/singleSignOn?loginSucUri=https://vipfunds.simu800.com/vipmanager/panel&auth_channel=null&v=1764310139517");
        request2.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"142\", \"Microsoft Edge\";v=\"142\", \"Not_A Brand\";v=\"99\"");
        request2.Headers.Add("sec-ch-ua-mobile", "?0");
        request2.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        request2.Headers.Add("sec-fetch-dest", "empty");
        request2.Headers.Add("sec-fetch-mode", "cors");
        request2.Headers.Add("sec-fetch-site", "same-origin");
        request2.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");
        // 构建更新后的 Cookie 字符串（用于 request2）
        try
        {
            string cookieString2 = string.Join("; ", updatedCookies.Select(kv => $"{kv.Key}={kv.Value}"));
            request2.Headers.Add("Cookie", cookieString2);
        }
        catch (Exception e)
        {
            return (false, null, $"登录MeiShi错误：构建 Cookie 失败 - {e.Message}");
        }

        var jsonBody = "{}";
        request2.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        // 发送 POST 请求 
        response = await client.SendAsync(request2);

        var json = await response.Content.ReadAsStringAsync();

        var m = Regex.Match(json, "\"token\"\\s*:\\s*\"([^\"]+)\"");

        return (m.Success, m.Success ? m.Groups[1].Value : null, m.Success ? null : "登录MeiShi错误：获取 token 失败");
    }



    public static async Task<MeishiFundInfo[]> QueryFundInfo(HttpClient client, string Token)
    {
        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/product/queryByProductName");
        request.Headers.Add("tokenid", Token);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = client.Send(request);

        var cont = await response.Content.ReadAsStringAsync();

        if (Regex.IsMatch(cont, "token已失效|重新登录"))
            throw new InvalidCredentialException(cont);

        var root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root is null) return [];

        return root.data.Deserialize<MeishiFundInfo[]>() ?? [];
    }


    private static string? GetFileExtentionFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            return Path.GetExtension(uri.LocalPath);
        }
        catch
        {
            return null;
        }
    }


}


file static class LoginEncryptor
{
    private const string Key = "4B19127F45A2DAF7"; // 16 字节，AES-128

    public static string EncryptLoginData(object loginData)
    {
        // 1. 序列化为 JSON（无空格，与 JS 的 JSON.stringify 一致）
        var json = JsonSerializer.Serialize(loginData);

        // 2. UTF-8 编码
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);

        // 3. AES-128-ECB + PKCS7 填充
        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(Key);
            aes.Mode = CipherMode.ECB;          // ECB 模式
            aes.Padding = PaddingMode.PKCS7;    // PKCS7 填充

            using (var encryptor = aes.CreateEncryptor())
            {
                byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                // 4. Base64 编码（与 CryptoJS.toString() 一致）
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    // 便捷方法：直接传入字段
    public static string EncryptLogin(string userName, string password, string loginType = "1", int passwordType = 1)
    {
        var data = new
        {
            loginType,
            userName,
            password,
            passwordType,
            authorize = new { } // 空对象
        };
        return EncryptLoginData(data);
    }


}


internal class LoginResultJson
{
    /// <summary>
    /// 
    /// </summary>
    public bool success { get; set; }
    /// <summary>
    /// 账号不存在或密码错误
    /// </summary>
    public string? message { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int code { get; set; }

    /// <summary>
    /// 
    /// </summary>
    //public int timestamp { get; set; } 

}

