using FMO.Logging;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Utilities;

namespace FMO.Utilities;


public static class SecurityHelper
{
    private static byte[] _by;

    static SecurityHelper()
    {
        using var stream = Assembly.GetAssembly(typeof(SecurityHelper))!.GetManifestResourceStream("Utilities.18ffds930");
        _by = new byte[stream!.Length];
        stream.ReadExactly(_by, 0, _by.Length);
    }

    public static bool IsAuthorSigned(string dllPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return false;

            var fileinfo = new FileInfo(dllPath);


            var sigPath = Path.Combine(fileinfo.Directory!.Parent!.FullName, ".ck", fileinfo.Directory.Name, fileinfo.Name + ".ck");
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
            rsa.ImportRSAPublicKey(Convert.FromBase64String(Encoding.UTF8.GetString(AesHelper.Decrypt(_by))), out _);

            return rsa.VerifyData(dllBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
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