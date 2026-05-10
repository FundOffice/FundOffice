using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace FMO;


public static class SecurityHelper
{
    // 🔒 硬编码信任公钥（发布后不可更改，篡改此常量会导致验证失败）
    private const string TrustedPublicKeyBase64 =
        "MIIBCgKCAQEAqhCL4/Duo/72lJHPPBH0ozki+q+HYT2ibpMTSU1T5TCo0/+JIpCTztLBd9ELbjeBd9ddZHsGE+wmgR0Wj4Tt7Mp8+FfwbXCeW5zyl1UY3fw2rhkmQbKxU0y6nHzy1tLd5/FbXP+ZhuVc0LfzLH8oQujwBn2F/DDQBhX/x5ch++le1L6XivlspLaH/Aki4J2aX0z52NMH0O1B0Qgfy2etdkXZylpQJe4ShfP9k9nYET+rXt6GnKZYT7EkKlh0B/iAu1WCjeK2INebEA1/JxCvxSjjfr9194d4IJBM6hq+nOgufpAHqXfQK5KJKvHXyMPXIi/INKkPZPkEHQBtJA195QIDAQAB";

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