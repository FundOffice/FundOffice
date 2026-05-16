using FMO.TPL;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Utilities;

namespace FMO.Utilities;

/// <summary>
/// 模板文件保存/加载器
/// </summary>
public static class TemplateFileHandler
{
    // 二进制分隔符：24个0x00字节（替换原有字符串分隔符）
    private static readonly byte[] Separator = new byte[24];
    private static readonly Encoding TextEncoding = Encoding.UTF8;

    /// <summary>
    /// RSA签名（使用你的私钥）
    /// </summary>
    public static string RsaSign(string data, RSA rsaPrivateKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signBytes = rsaPrivateKey.SignData(
            dataBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signBytes);
    }

    #region 保存模板（原有Path版本 + 新增Stream版本，参数完全不变）
    /// <summary>
    /// 保存模板到文件（明文头部 + 加密正文 + RSA签名）【原有Path版本，参数不动】
    /// </summary>
    public static void SaveTemplate(TemplateMeta meta, TemplateScript script, string filePath, RSA rsaPrivateKey, string key)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        SaveTemplate(meta, script, stream, rsaPrivateKey, key);
    }

    /// <summary>
    /// 保存模板到流（明文头部 + 加密正文 + RSA签名）【新增Stream版本】
    /// </summary>
    public static void SaveTemplate(TemplateMeta meta, TemplateScript script, Stream stream, RSA rsaPrivateKey, string key)
    {
        // 1. 构建敏感数据（需要加密+签名的内容）【原有逻辑不动】
   
        string sensitiveJson = JsonSerializer.Serialize(script);

        // 2. RSA签名（存入Sign字段）【原有逻辑不动】
        meta.Sign = RsaSign(sensitiveJson, rsaPrivateKey);

        // 3. AES加密敏感数据【原有逻辑不动】
        string encryptedData = AesHelper.Encrypt(sensitiveJson, key);

        // 4. 构建明文头部（不包含任何分隔符）【原有逻辑不动，但改为JSON格式】
        string header = JsonSerializer.Serialize(meta);

        // 5. 转换为byte[]，二进制写入：头部 + 24个0分隔符 + 加密数据
        byte[] headerBytes = TextEncoding.GetBytes(header);
        byte[] encryptedBytes = TextEncoding.GetBytes(encryptedData);

        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(Separator, 0, Separator.Length);
        stream.Write(encryptedBytes, 0, encryptedBytes.Length);
    }
    #endregion

    #region 加载模板（原有Path版本 + 新增Stream版本，参数完全不变）
    /// <summary>
    /// 从文件加载模板（验签 + 解密）【原有Path版本，参数不动】
    /// </summary>
    public static TemplateMeta LoadTemplate(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return LoadTemplate(stream);
    }

    /// <summary>
    /// 从流加载模板（验签 + 解密）【新增Stream版本】
    /// </summary>
    public static TemplateMeta LoadTemplate(Stream stream)
    {
        // 读取全部二进制数据
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] allBytes = ms.ToArray();

        // 查找24个0分隔符
        int separatorIndex = FindSeparatorIndex(allBytes);
        if (separatorIndex == -1)
            throw new InvalidDataException("模板文件格式错误");

        // 分割二进制数据
        byte[] headerBytes = allBytes.AsSpan(0, separatorIndex).ToArray();
        byte[] encryptedBytes = allBytes.AsSpan(separatorIndex + Separator.Length).ToArray();

        // 直接从二进制转换字符串，无任何多余拼接
        string header = TextEncoding.GetString(headerBytes).Trim();
        string encryptedBase64 = TextEncoding.GetString(encryptedBytes).Trim();
        var headerDict = ParseHeader(header);

        // 2. 字段校验【原有逻辑不动】
        var tempId = GetHeaderValue(headerDict, "Id");
        var tempName = GetHeaderValue(headerDict, "Name");
        var tempDescription = GetHeaderValue(headerDict, "Description");
        var tempVersion = GetHeaderValue(headerDict, "Version");
        var tempLimit = GetHeaderValue(headerDict, "Limit");
        var tempClass = GetHeaderValue(headerDict, "Class");
        var tempSign = GetHeaderValue(headerDict, "Sign");

        if (string.IsNullOrWhiteSpace(tempId)) throw new ArgumentNullException(nameof(tempId), "模板ID不能为空");
        if (string.IsNullOrWhiteSpace(tempName)) throw new ArgumentNullException(nameof(tempName), "模板名称不能为空");
        if (string.IsNullOrWhiteSpace(tempDescription)) throw new ArgumentNullException(nameof(tempDescription), "模板描述不能为空");
        if (string.IsNullOrWhiteSpace(tempVersion)) throw new ArgumentNullException(nameof(tempVersion), "模板版本不能为空");
        if (string.IsNullOrWhiteSpace(tempLimit)) throw new ArgumentNullException(nameof(tempLimit), "模板限制不能为空");
        if (string.IsNullOrWhiteSpace(tempSign)) throw new ArgumentNullException(nameof(tempSign), "模板签名不能为空");

        var meta = new TemplateMeta
        {
            Id = tempId,
            Name = tempName,
            Description = tempDescription,
            Version = tempVersion,
            Limit = tempLimit,
            Class = tempClass ?? "",
            Sign = tempSign
        };

        // 3. AES解密【原有逻辑不动】
        string sensitiveJson;
        try
        {
            sensitiveJson = AesHelper.Decrypt(encryptedBase64, GetCode(meta));
        }
        catch
        {
            throw new InvalidDataException("AES解密失败，密钥错误或文件已损坏");
        }

        // 4. RSA验签【原有逻辑不动】
        bool verifySuccess = SecurityHelper.Verify(sensitiveJson, meta.Sign!);
        if (!verifySuccess)
            throw new InvalidDataException("RSA验签失败，文件已被篡改");

        // 5. 反序列化【原有逻辑不动】
        var sensitiveData = JsonSerializer.Deserialize<TemplateMetaDTO>(sensitiveJson);
        if (sensitiveData?.Script is null)
            throw new InvalidDataException("Script内容异常");

        meta.Input = sensitiveData.Input ?? [];
        meta.Refer = sensitiveData.Refer ?? [];
        meta.Script = sensitiveData.Script;

        return meta;
    }
    #endregion

    #region 辅助方法（全部保留，新增分隔符查找方法）
    /// <summary>
    /// 解析明文头部为键值对【原有方法不动】
    /// </summary>
    private static Dictionary<string, string> ParseHeader(string header)
    {
        var dict = new Dictionary<string, string>();
        foreach (var line in header.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = line.IndexOf('=');
            if (index <= 0) continue;
            var key = line[..index].Trim();
            var value = line[(index + 1)..].Trim();
            dict[key] = value;
        }
        return dict;
    }

    /// <summary>
    /// 从头部字典获取值【原有方法不动】
    /// </summary>
    private static string? GetHeaderValue(Dictionary<string, string> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 获取加密密钥【原有方法+注册表逻辑完全不动】
    /// </summary>
    private static string GetCode(TemplateMeta meta)
    {
        if (meta.Limit != "vip") return "everyone";

#pragma warning disable CA1416
#if RELEASE
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif
            return AesHelper.Decrypt((key!.GetValue("Code") as string)!);
#pragma warning restore CA1416
    }






    /// <summary>
    /// 查找24个0分隔符的索引
    /// </summary>
    private static int FindSeparatorIndex(byte[] data)
    {
        for (int i = 0; i <= data.Length - Separator.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < Separator.Length; j++)
            {
                if (data[i + j] != Separator[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
    #endregion
}