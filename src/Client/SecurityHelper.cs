using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace FMO;


public static class SecurityHelper
{
    // 🔒 硬编码信任公钥（发布后不可更改，篡改此常量会导致验证失败）
    private const string TrustedPublicKeyBase64 = "MIIDGzCCAgOgAwIBAgIQOp921VoerKdJSwOdnPfdzTANBgkqhkiG9w0BAQsFADAVMRMwEQYDVQQDDApGdW5kT2ZmaWNlMB4XDTI2MDUxMDA1MTczNFoXDTI3MDUxMDA1MjczNFowFTETMBEGA1UEAwwKRnVuZE9mZmljZTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAOGriaUW93YpbRr7FYoN2YPoKRYABlgDK9JaMg6FAg+D4Delb4PESyFO4iNiGocQzwcHx7xMeePzAXyMyhCer+JGPN7z3mhPrN4g5Q53Ks01HWa2PLBel4PzGz26BMqC4wduj4En4ScSli6qZZm4Ep6qj2Vz3ryDmB57iya2r0bHlpSILlHTlDZxgvvwxsZAnzTzSNnno2K51El2jx36n2mnBAz1DztrHWX6LyDKc9gC/t5JDU98ANoSYOMSJQzOv06Dfy8eFUaWDyLnGsYSTs5ONuPq4fYd6R/xidtz2ebKkFN+TERCu4XHG3mtUCy49IBDyRWgxAd8wMvbc588ib0CAwEAAaNnMGUwDgYDVR0PAQH/BAQDAgWgMB0GA1UdJQQWMBQGCCsGAQUFBwMCBggrBgEFBQcDATAVBgNVHREEDjAMggpGdW5kT2ZmaWNlMB0GA1UdDgQWBBTvldrWyTJm6IivPTMe9hQXH9f41jANBgkqhkiG9w0BAQsFAAOCAQEACJvK5kwM5HX6ugQ7FY5vrrSWvG02bobx4fffKUhsiUKlgT+QVlXWULec9RascV5d18mghkkRDRy3J6cXR5vx0GaS6iiqcZLP0xDv/PT6JHwabHpohxPp1+kcIQl717MqxS34FtxOftZab88GqF5ilSjI2dNjrS22BQOU3qOd/X1nN40jduXD29VcRFIuCSfP1qbWE8okFlAmv8FW7sABQpD5N3wDzyA/nNUqGkBlFTgIiM/wwV9jfgpHT7tIFoD5oFTpA6tnv093dO/JRVheTggC1dTyzMokTMwJd6WAiJkWgZbcS8Y/DLUEuSiVV2lMwU19RUhmEbrRCb25JhqrQg==";
     

    /// <summary>
    /// 验证单个 DLL 是否为你本人签名
    /// </summary>
    public static bool IsAuthorSigned(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            return false;

        string sigPath = dllPath + ".sig";
        if (!File.Exists(sigPath)) return false; // 缺少签名文件直接拒绝

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(TrustedPublicKeyBase64), out _);

            byte[] dllBytes = File.ReadAllBytes(dllPath);
            byte[] sigBytes = File.ReadAllBytes(sigPath);

            // 验证 SHA256 哈希与签名是否匹配
            return rsa.VerifyData(dllBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false; // 任何异常均视为验证失败
        }
    }

    /// <summary>
    /// WPF 启动时调用：扫描文件夹、验证、加载插件
    /// </summary>
    public static void LoadTrustedPlugins(string pluginsFolder, Action<Assembly> onLoad)
    {
        if (!Directory.Exists(pluginsFolder)) return;

        foreach (string dll in Directory.GetFiles(pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (!IsAuthorSigned(dll))
            {
                System.Diagnostics.Trace.WriteLine($"[安全拦截] 插件未通过作者验证，已跳过: {Path.GetFileName(dll)}");
                continue;
            }

            try
            {
                // ✅ 验证通过，安全加载
                Assembly asm = Assembly.LoadFrom(dll);
                onLoad?.Invoke(asm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[加载失败] {dll} -> {ex.Message}");
            }
        }
    }
}