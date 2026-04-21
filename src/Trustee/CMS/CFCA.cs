using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace FMO.Trustee;

public class SignatureUtils
{
    /// <summary>
    /// 获取管理签名
    /// </summary>
    //private static string GetManagerSignature(string companyId, string timestamp, string certPath, string password)
    //{
    //    // 构造待签名字符串（模拟 MD5）
    //    string waitSignStr = "20250101010101"; // 可替换为真正的 MD5(companyId + timestamp)
    //    byte[] sourceData = Encoding.UTF8.GetBytes(waitSignStr);

    //    // 加载 PFX 证书和私钥
    //    var pfxCert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password, X509KeyStorageFlags.Exportable);
    //    var bcCert = DotNetUtilities.FromX509Certificate(pfxCert);
    //    RSA? rSA = pfxCert.GetRSAPrivateKey();
    //    byte[] privateKeyInfoData = rSA.ExportPkcs8PrivateKey();
    //    var privateKey = (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(privateKeyInfoData);

    //    // 调用签名方法
    //    byte[] signedBytes = P7SignMessageDetach("SHA256WITHRSA", sourceData, privateKey, bcCert);
    //    return Convert.ToBase64String(signedBytes);
    //}

    public static string GetManagerSignature(string companyId, string timestamp, X509Certificate2 pfxCert)
    {
        // 构造待签名字符串（模拟 MD5）
        string waitSignStr = GetMD5String(companyId + timestamp);
        byte[] sourceData = Encoding.UTF8.GetBytes(waitSignStr);

        // 加载 PFX 证书和私钥
        // var pfxCert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password, X509KeyStorageFlags.Exportable);
        var bcCert = DotNetUtilities.FromX509Certificate(pfxCert);
        RSA? rSA = pfxCert.GetRSAPrivateKey();
        byte[] privateKeyInfoData = rSA!.ExportPkcs8PrivateKey();
        var privateKey = (RsaPrivateCrtKeyParameters)PrivateKeyFactory.CreateKey(privateKeyInfoData);

        // 调用签名方法
        byte[] signedBytes = P7SignMessageDetach("SHA256WITHRSA", sourceData, privateKey, bcCert);
        return Convert.ToBase64String(signedBytes);
    }

    private static string GetMD5String(string data)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(data);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // 转换为 16 进制字符串（大写）
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2")); // "X2" 表示 2 位大写十六进制
            }
            return sb.ToString();
        }
    }


    /// <summary>
    /// 签名并生成 PKCS#7 detached signature
    /// </summary>
    public static byte[] P7SignMessageDetach(string signAlg, byte[] sourceData, RsaPrivateCrtKeyParameters privateKey, X509Certificate cert)
    {
        bool isDetached = false; // 不附加原文数据
        string? contentType = null;
        X509Certificate[] certs = new[] { cert };

        return PackageRSASignedData(isDetached, contentType, sourceData, signAlg, privateKey, certs);
    }

    /// <summary>
    /// 构建 PKCS#7 SignedData 结构
    /// </summary>
    public static byte[] PackageRSASignedData(bool ifAttach, string? contentType, byte[] sourceData, string signAlgName, RsaPrivateCrtKeyParameters privateKey, X509Certificate[] certs)
    {
        if (certs == null || certs.Length == 0)
            throw new ArgumentException("证书不能为空");

        // 1. 解析摘要算法
        var digestOid = ResolveDigestAlgorithm(signAlgName);
        var digestAlgIdentifier = new AlgorithmIdentifier(digestOid, DerNull.Instance);

        // 2. 获取签发者信息
        var signerCert = certs[0];
        var issuer = (X509Name)signerCert.IssuerDN;
        var serialNumber = signerCert.SerialNumber;
        var issuerAndSn = new IssuerAndSerialNumber(issuer, serialNumber);

        // 3. 签名算法标识符
        var sigAlgIdentifier = new AlgorithmIdentifier(PkcsObjectIdentifiers.IdRsaEncryption, DerNull.Instance);

        // 4. 计算签名值
        ISigner signer = SignerUtilities.GetSigner(signAlgName);
        signer.Init(true, new ParametersWithRandom(privateKey));
        signer.BlockUpdate(sourceData, 0, sourceData.Length);
        byte[] signature = signer.GenerateSignature();

        var encryptedData = new DerOctetString(signature);

        // 5. 构建 SignerInfo
        var signerInfo = new SignerInfo(
            new DerInteger(1),
            issuerAndSn,
            digestAlgIdentifier,
            null,
            sigAlgIdentifier,
            encryptedData,
            null);

        // 6. 构建 ContentInfo
        ContentInfo contentInfo;
        if (ifAttach)
        {
            var content = new DerOctetString(sourceData);
            var oid = string.IsNullOrEmpty(contentType) ? PkcsObjectIdentifiers.Data : new DerObjectIdentifier(contentType);
            contentInfo = new ContentInfo(oid, content);
        }
        else
        {
            var oid = string.IsNullOrEmpty(contentType) ? PkcsObjectIdentifiers.Data : new DerObjectIdentifier(contentType);
            contentInfo = new ContentInfo(oid, null);
        }

        // 7. 构建 DigestAlgorithms
        var derV = new Asn1EncodableVector();
        derV.Add(digestAlgIdentifier);
        var digestAlgorithmSets = new BerSet(derV);

        // 8. 构建 SignerInfos
        var signerInfosVec = new Asn1EncodableVector();
        signerInfosVec.Add(signerInfo);
        var signerInfos = new DerSet(signerInfosVec);

        // 9. 构建 Certificates  
        var certList = new Asn1EncodableVector();
        foreach (var cert in certs)
        {
            // 直接解析证书的 DER 编码为 ASN.1 对象

            byte[] encoded = cert.GetEncoded();
            Asn1Object asn1Cert = Asn1Object.FromByteArray(encoded);
            certList.Add(asn1Cert); // 强转为 Asn1Sequence
        }


        var setCert = new BerSet(certList);

        // 10. 构建 SignedData
        var signedData = new SignedData(
            new DerInteger(1),
            digestAlgorithmSets,
            contentInfo,
            setCert,
            null,
            signerInfos);

        var contentInfoTemp = new ContentInfo(PkcsObjectIdentifiers.SignedData, signedData);

        // 11. 返回 DER 编码字节
        return contentInfoTemp.GetDerEncoded();
    }

    /// <summary>
    /// 将签名算法名称映射到 OID
    /// </summary>
    private static DerObjectIdentifier ResolveDigestAlgorithm(string algName)
    {
        switch (algName.ToUpper())
        {
            case "SHA256WITHRSA":
                return PkcsObjectIdentifiers.IdSha256;
            case "SHA1WITHRSA":
                return PkcsObjectIdentifiers.IdSha1;
            default:
                throw new NotSupportedException($"不支持的签名算法: {algName}");
        }
    }
}
public static class PkcsObjectIdentifiers
{
    // PKCS#7 / CMS 内容类型
    public static readonly DerObjectIdentifier Data = new DerObjectIdentifier("1.2.840.113549.1.7.1");
    public static readonly DerObjectIdentifier SignedData = new DerObjectIdentifier("1.2.840.113549.1.7.2");

    // 摘要算法 OID
    public static readonly DerObjectIdentifier IdSha1 = new DerObjectIdentifier("1.3.14.3.2.26");
    public static readonly DerObjectIdentifier IdSha256 = new DerObjectIdentifier("2.16.840.1.101.3.4.2.1");

    // 签名算法标识符（RSA）
    public static readonly DerObjectIdentifier IdRsaEncryption = new DerObjectIdentifier("1.2.840.113549.1.1.1");
}