using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using System.Text;



namespace FMO.AMAC.Direct;

public static class Sm3Utils
{
    private static readonly Encoding TextEncoding = Encoding.UTF8;

    public static string? Encrypt32(string text)
    {
        var v = Encrypt(text);
        return v?.Length == 64 ? v[..32] : v;
    }

    public static string? Encrypt(string text)
    {
        if (string.IsNullOrEmpty(text))
            text = "";
        byte[] srcData = TextEncoding.GetBytes(text);
        return Encrypt(srcData);
    }

    public static string? Encrypt(byte[] srcData)
    {
        if (srcData == null)
            return null;

        byte[] resultHash = Hash(srcData);
        return ByteArrayToHexString(resultHash);
    }

    public static byte[] Hash(byte[] srcData)
    {
        if (srcData == null)
            return null;

        SM3Digest digest = new SM3Digest();
        digest.BlockUpdate(srcData, 0, srcData.Length);
        byte[] hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        return hash;
    }

    private static string? ByteArrayToHexString(byte[] bytes)
    {
        if (bytes == null)
            return null;

        StringBuilder hex = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            hex.AppendFormat("{0:x2}", b);
        }
        return hex.ToString();
    }
}


public static class Sm2Utils
{
    /// <summary>
    /// SM2加密算法
    /// </summary>
    /// <param name="publicKey">公钥(十六进制字符串)</param>
    /// <param name="data">要加密的数据</param>
    /// <returns>加密结果的十六进制字符串</returns>
    public static string Encrypt(string publicKey, string data)
    {
        try
        {
            // 1. 获取SM2曲线参数
            var curve = GMNamedCurves.GetByName("sm2p256v1");
            var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N);

            // 2. 解析公钥
            byte[] publicKeyBytes = Hex.Decode(publicKey);
            ECPoint publicKeyPoint = curve.Curve.DecodePoint(publicKeyBytes);
            var publicKeyParam = new ECPublicKeyParameters(publicKeyPoint, domainParams);

            // 3. 创建并初始化SM2引擎
            var sm2Engine = new SM2Engine();
            var rand = new SecureRandom();
            //rand.SetSeed(11);
            sm2Engine.Init(true, new ParametersWithRandom(publicKeyParam, rand));

            // 4. 加密数据
            byte[] input = Encoding.UTF8.GetBytes(data);
            byte[] encrypted = sm2Engine.ProcessBlock(input, 0, input.Length);

            // 5. 返回十六进制结果
            return Hex.ToHexString(encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SM2加密失败: {ex.Message}");
            return null;
        }
    }
}