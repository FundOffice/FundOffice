using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FMO.PFID;

public class DirectTest
{
    private const string FileUrl = "http://amac-api.i-moss.net:10080/pof/v1/report/direct-file.do";
    private const string QueryUrl = "http://amac-api.i-moss.net:10080/pof/v1/report/direct-query.do";
    private const string SubmitUrl = "http://amac-api.i-moss.net:10080/pof/v1/report/direct-submit.do";

    private const string UserName = "P1003";
    private const string DirectPwd = "111111";

    private const string EntityCode = "1003C";
    private const string ReportType = "PB0001";
    private const string ReportEndDate = "2022-07-31";
    private const string XbrlFileName = "CN_1003C_PB0001_2022-07-31.zip";
    private const string SubCompany = "测试机构名称";

    private const string PublicKey = "02556ebadc8680275ff894307164e9421598700d230a55267d9f070e567c4b4bc7";
    private const string LocalFile = @"C:\Users\iyumo\Downloads\季度更新数据_20250805_222300.zip";//@"E:\Users\csy\Desktop\直连测试样例\1\CN_1003C_PB0001_2022-07-31.zip";

    public static async Task Test()
    {
        string handle = null;
        try
        {
            handle = await DoDirectFileAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        if (handle != null)
        {
            Thread.Sleep(3000);
            await DoDirectQueryAsync(handle);
            // 如果需要提交
            // await DoDirectSubmitAsync(handle);
        }
    }

    private static async Task<string> DoDirectFileAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

        // 生成认证头部
        var pwd = Sm3Utils.Encrypt32(DirectPwd);
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Sm3Utils.Encrypt(UserName + salt + pwd);

        httpClient.DefaultRequestHeaders.Add("userName", UserName);
        httpClient.DefaultRequestHeaders.Add("salt", salt);
        httpClient.DefaultRequestHeaders.Add("sign", sign);

        // 读取文件内容
        var fileBytes = File.ReadAllBytes(LocalFile);
        var base64File = Convert.ToBase64String(fileBytes);

        // 计算校验和
        var checksum = Sm3Utils.Encrypt(fileBytes);
        checksum = Sm2Utils.Encrypt(PublicKey, checksum);

        // 构建请求体
        var request = new
        {
            entityCode = EntityCode,
            reportType = ReportType,
            reportEndDate = ReportEndDate,
            xbrlFileName = XbrlFileName,
            subCompany = SubCompany,
            body = base64File,
            checksum = checksum
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 发送请求
        var response = await httpClient.PostAsync(FileUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);

        // 解析响应
        var result = JsonSerializer.Deserialize<DirectFileResponse>(responseContent);
        return result?.ProcessCode == "100" ? result.Handle : null;
    }

    private static async Task DoDirectQueryAsync(string handle)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

        // 生成认证头部
        var pwd = Sm3Utils.Encrypt32(DirectPwd);
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Sm3Utils.Encrypt(UserName + salt + pwd);

        httpClient.DefaultRequestHeaders.Add("userName", UserName);
        httpClient.DefaultRequestHeaders.Add("salt", salt);
        httpClient.DefaultRequestHeaders.Add("sign", sign);

        // 构建请求体
        var request = new { handle };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 发送请求
        var response = await httpClient.PostAsync(QueryUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);
    }

    private static async Task DoDirectSubmitAsync(string handle)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

        // 生成认证头部
        var pwd = Sm3Utils.Encrypt32(DirectPwd);
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Sm3Utils.Encrypt(UserName + salt + pwd);

        httpClient.DefaultRequestHeaders.Add("userName", UserName);
        httpClient.DefaultRequestHeaders.Add("salt", salt);
        httpClient.DefaultRequestHeaders.Add("sign", sign);

        // 构建请求体
        var request = new { handle, subCompany = SubCompany };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 发送请求
        var response = await httpClient.PostAsync(SubmitUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);
    }
}

