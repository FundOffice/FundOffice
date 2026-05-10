using FMO.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace FMO;


public static class SecurityHelper
{
    // 🔒 硬编码信任公钥（发布后不可更改，篡改此常量会导致验证失败）
    private const string TrustedPublicKeyBase64 = "MIIBCgKCAQEA4auJpRb3diltGvsVig3Zg+gpFgAGWAMr0loyDoUCD4PgN6Vvg8RLIU7iI2IahxDPBwfHvEx54/MBfIzKEJ6v4kY83vPeaE+s3iDlDncqzTUdZrY8sF6Xg/MbPboEyoLjB26PgSfhJxKWLqplmbgSnqqPZXPevIOYHnuLJravRseWlIguUdOUNnGC+/DGxkCfNPNI2eejYrnUSXaPHfqfaacEDPUPO2sdZfovIMpz2AL+3kkNT3wA2hJg4xIlDM6/ToN/Lx4VRpYPIucaxhJOzk424+rh9h3pH/GJ23PZ5sqQU35MREK7hccbea1QLLj0gEPJFaDEB3zAy9tznzyJvQIDAQAB";

    /// <summary>
    /// 验证单个 DLL 是否为你本人签名
    /// </summary>
    public static bool IsAuthorSigned(string dllPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return false;

            var sigPath = dllPath + ".ck";
            if (!File.Exists(sigPath))
                return false; // 🔹 缺少签名文件 = 未授权，静默拒绝

            // 🔹 用 FileShare.Read 允许其他进程读取（避免 SmartScreen/杀毒软件锁定冲突）
            using var dllStream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sigStream = new FileStream(sigPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            byte[] dllBytes = new byte[dllStream.Length];
            dllStream.ReadExactly(dllBytes, 0, dllBytes.Length);

            byte[] sigBytes = new byte[sigStream.Length];
            sigStream.ReadExactly(sigBytes, 0, sigBytes.Length);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(TrustedPublicKeyBase64), out _);

            return rsa.VerifyData(dllBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch(Exception ex)
        {
            // 🔹 任何异常（格式错误/权限不足/解码失败）均视为验证失败，绝不崩溃
            LogEx.Error(ex); 
            return false;
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