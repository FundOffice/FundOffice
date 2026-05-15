using System.Security.Cryptography;
using System.Text;

namespace Utilities;

public static class AesHelper
{

    private static readonly string _aesSecret = "OHM5a0YycFI3eFEzZEc1akwxekM0dkI2bk0wYVM3d0V0TjViUjhnSzJtUDRxWDd6";

    /// <summary>
    /// AES 加密字符串
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>Base64 加密结果</returns>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var bytes = Convert.FromBase64String(_aesSecret);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            // 加密模式 & 填充模式（通用标准）
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                // 转 Base64 方便传输/存储
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    /// <summary>
    /// AES 解密字符串
    /// </summary>
    /// <param name="cipherText">Base64 加密串</param>
    /// <returns>明文</returns>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        var bytes = Convert.FromBase64String(_aesSecret);
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.FlushFinalBlock();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }


    /// <summary>
    /// AES 加密字符串
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <returns>Base64 加密结果</returns>
    public static byte[] Encrypt(byte[] plainBytes)
    {
        if (plainBytes?.Length is null or 0)
            return [];

        var bytes = Convert.FromBase64String(_aesSecret);


        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            // 加密模式 & 填充模式（通用标准）
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// AES 解密字符串
    /// </summary>
    /// <param name="cipherText">Base64 加密串</param>
    /// <returns>明文</returns>
    public static byte[] Decrypt(byte[] cipherBytes)
    {
        if (cipherBytes?.Length is null or 0)
            return [];

        var bytes = Convert.FromBase64String(_aesSecret);

        using (Aes aes = Aes.Create())
        {
            aes.Key = bytes[..32];
            aes.IV = bytes[32..];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.FlushFinalBlock();
                return ms.ToArray();
            }
        }
    }
}