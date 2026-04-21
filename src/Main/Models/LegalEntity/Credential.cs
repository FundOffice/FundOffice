using System.ComponentModel;
using System.Text.RegularExpressions;

namespace FMO.Models;

/// <summary>
/// 枚举表示不同的证件类型。
/// </summary>


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum IDType
{
    [Description("未知")] Unknown,

    /// <summary>
    /// 居民身份证。
    /// </summary>
    [Description("居民身份证")]
    IdentityCard,

    /// <summary>
    /// 中国护照。
    /// </summary>
    [Description("中国护照")]
    PassportChina,

    /// <summary>
    /// 军官证。
    /// </summary>
    [Description("军官证")]
    OfficerID,

    /// <summary>
    /// 士兵证。
    /// </summary>
    [Description("士兵证")]
    SoldierID,

    /// <summary>
    /// 港澳居民来往内地通行证。
    /// </summary>
    [Description("港澳居民来往内地通行证")]
    HongKongMacauPass,

    /// <summary>
    /// 户口本。
    /// </summary>
    [Description("户口本")]
    HouseholdRegister,

    /// <summary>
    /// 外国护照。
    /// </summary>
    [Description("外国护照")]
    PassportForeign,

    /// <summary>
    /// 文职证。
    /// </summary>
    [Description("文职证")]
    CivilianID,

    /// <summary>
    /// 警官证。
    /// </summary>
    [Description("警官证")]
    PoliceID,

    /// <summary>
    /// 台胞证。
    /// </summary>
    [Description("台胞证")]
    TaiwanCompatriotsID,

    /// <summary>
    /// 外国人永久居留身份证。
    /// </summary>
    [Description("外国人永久居留身份证")]
    ForeignPermanentResidentID,

    Institusion,

    [Description("统一社会信用代码")]
    UnifiedSocialCreditCode,

    [Description("组织机构代码证")]
    OrganizationCode, OrganizationCodeCertificate = OrganizationCode,

    [Description("营业执照号")]
    BusinessLicenseNumber,

    [Description("注册号")]
    RegistrationNumber,

    [Description("管理人登记编码")]
    ManagerRegistrationCode,

    [Description("产品备案编码")]
    ProductFilingCode,

    [Description("证券业务许可证")]
    SecuritiesBusinessLicense,

    [Description("批文")]
    Approval,


    [Description("产品登记编码")]
    ProductRegistrationCode,

    [Description("港澳台居民居住证")]
    ResidencePermitForHongKongMacaoAndTaiwanResidents,

    [Description("信托登记系统产品编码")]
    TrustRegistrationSystemProductCode,


    /// <summary>
    /// 其他证件。
    /// </summary>
    [Description("其他")]
    Other,
}


/// <summary>
/// 证件
/// </summary>
public record class Identity 
{
    /// <summary>
    /// 证件号码
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 证件类型
    /// </summary>
    public IDType Type { get; set; }


    public string? Other { get; set; }

     

    public override int GetHashCode()
    {
        return Type.GetHashCode() ^ (Id ?? "").GetHashCode();
    }
}


public class IdValidator
{
    private static readonly Dictionary<char, int> CharMap = new Dictionary<char, int>
    {
        {'0', 0}, {'1', 1}, {'2', 2}, {'3', 3}, {'4', 4}, {'5', 5}, {'6', 6}, {'7', 7}, {'8', 8}, {'9', 9},
        {'A', 10}, {'B', 11}, {'C', 12}, {'D', 13}, {'E', 14}, {'F', 15}, {'G', 16}, {'H', 17}, {'J', 18},
        {'K', 19}, {'L', 20}, {'M', 21}, {'N', 22}, {'P', 23}, {'Q', 24}, {'R', 25}, {'T', 26}, {'U', 27},
        {'W', 28}, {'X', 29}, {'Y', 30}
    };

    public static bool Validate(string idNumber, IDType idType)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
            return false;

        return idType switch
        {
            IDType.IdentityCard => ValidateIdCard(idNumber),
            IDType.PassportChina => ValidateChinesePassport(idNumber),
            IDType.OfficerID => ValidateOfficerId(idNumber),
            IDType.SoldierID => ValidateSoldierId(idNumber),
            IDType.HongKongMacauPass => ValidateHongKongMacauPass(idNumber),
            IDType.PassportForeign => ValidateForeignPassport(idNumber),
            IDType.CivilianID => ValidateCivilianId(idNumber),
            IDType.PoliceID => ValidatePoliceId(idNumber),
            IDType.TaiwanCompatriotsID => ValidateTaiwanCompatriotsId(idNumber),
            IDType.ForeignPermanentResidentID => ValidateForeignPermanentResidentId(idNumber),
            IDType.UnifiedSocialCreditCode => ValidateUnifiedSocialCreditCode(idNumber),
            IDType.OrganizationCode => ValidateOrganizationCode(idNumber),
            IDType.BusinessLicenseNumber => ValidateBusinessLicenseNumber(idNumber),
            IDType.RegistrationNumber => ValidateRegistrationNumber(idNumber),
            IDType.ManagerRegistrationCode => ValidateManagerRegistrationCode(idNumber),
            IDType.ProductFilingCode => ValidateProductFilingCode(idNumber),
            IDType.ProductRegistrationCode => ValidateProductRegistrationCode(idNumber),
            IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents => ValidateHKMTResidencePermit(idNumber),
            IDType.TrustRegistrationSystemProductCode => ValidateTrustProductCode(idNumber),
            _ => true // 其他类型默认返回true
        };
    }

    #region 居民身份证验证
    private static bool ValidateIdCard(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        if (id.Length != 15 && id.Length != 18) return false;

        if (id.Length == 15 && !id.All(char.IsDigit)) return false;
        if (id.Length == 18 &&
            (!id.Substring(0, 17).All(char.IsDigit) ||
             !(char.IsDigit(id[17]) || id[17] == 'X')))
            return false;

        if (!ValidateBirthDate(id)) return false;

        return id.Length != 18 || ValidateIdCardCheckCode(id);
    }

    private static bool ValidateBirthDate(string id)
    {
        string birthDate = id.Length == 15 ? "19" + id.Substring(6, 6) : id.Substring(6, 8);
        return DateTime.TryParseExact(birthDate, "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.None, out _);
    }

    private static bool ValidateIdCardCheckCode(string id)
    {
        int[] weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
        char[] checkCodes = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

        int sum = 0;
        for (int i = 0; i < 17; i++)
        {
            sum += (id[i] - '0') * weights[i];
        }

        return id[17] == checkCodes[sum % 11];
    }
    #endregion

    #region 护照验证
    private static bool ValidateChinesePassport(string passport)
    {
        if (string.IsNullOrWhiteSpace(passport)) return false;
        passport = passport.Trim().ToUpper();

        // 中国大陆普通护照：E/D/S/P/G开头 + 8位数字
        return passport.Length == 9 &&
               "EDSPG".Contains(passport[0]) &&
               passport.Substring(1).All(char.IsDigit);
    }

    private static bool ValidateForeignPassport(string passport)
    {
        if (string.IsNullOrWhiteSpace(passport)) return false;
        passport = passport.Trim().ToUpper();

        // 外国护照：1-2个大写字母开头 + 6-10位数字
        return Regex.IsMatch(passport, @"^[A-Z]{1,2}\d{6,10}$");
    }
    #endregion

    #region 军官/警官/文职/士兵证验证
    private static bool ValidateOfficerId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 军官证：汉字"军"或拼音"JUN"开头 + 7-10位数字
        return Regex.IsMatch(id, @"^(军|JUN)\d{7,10}$");
    }

    private static bool ValidatePoliceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 警官证：汉字"警"或拼音"JING"开头 + 7-10位数字
        return Regex.IsMatch(id, @"^(警|JING)\d{7,10}$");
    }

    private static bool ValidateCivilianId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 文职证：汉字"文职"或拼音"WENZHI"开头 + 7-10位数字
        return Regex.IsMatch(id, @"^(文职|WENZHI)\d{7,10}$");
    }

    private static bool ValidateSoldierId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 士兵证：汉字"士"或拼音"SHI"开头 + 7-10位数字
        return Regex.IsMatch(id, @"^(士|SHI)\d{7,10}$");
    }
    #endregion

    #region 港澳台证件验证
    private static bool ValidateHongKongMacauPass(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 港澳通行证：H/M开头 + 10位数字
        return id.Length == 11 &&
               (id.StartsWith("H") || id.StartsWith("M")) &&
               id.Substring(1).All(char.IsDigit);
    }

    private static bool ValidateTaiwanCompatriotsId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 台胞证：8位数字或1位字母+7位数字
        return Regex.IsMatch(id, @"^(\d{8}|[A-Z]\d{7})$");
    }

    private static bool ValidateHKMTResidencePermit(string id)
    {
        // 港澳台居民居住证使用与大陆身份证相同的规则
        return ValidateIdCard(id);
    }
    #endregion

    #region 外国人证件验证
    private static bool ValidateForeignPermanentResidentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 外国人永久居留身份证：15位或18位数字
        return (id.Length == 15 || id.Length == 18) && id.All(char.IsDigit);
    }
    #endregion

    #region 组织机构代码验证
    private static bool ValidateOrganizationCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().ToUpper().Replace("-", "");

        if (code.Length != 9) return false;
        if (!Regex.IsMatch(code, @"^[A-Z0-9]{9}$")) return false;

        int[] weights = { 3, 7, 9, 10, 5, 8, 4, 2 };
        char[] checkCodes = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'X' };

        int sum = 0;
        for (int i = 0; i < 8; i++)
        {
            char c = code[i];
            int value = char.IsDigit(c) ? c - '0' : c - 'A' + 10;
            sum += value * weights[i];
        }

        int checkValue = 11 - (sum % 11);
        char expectedCheck = checkValue == 10 ? 'X' :
                            checkValue == 11 ? '0' :
                            (char)(checkValue + '0');

        return code[8] == expectedCheck;
    }
    #endregion

    #region 统一社会信用代码验证
    private static bool ValidateUnifiedSocialCreditCode(string uscc)
    {
        if (string.IsNullOrWhiteSpace(uscc)) return false;
        uscc = uscc.Trim().ToUpper();

        if (uscc.Length != 18) return false;
        if (!Regex.IsMatch(uscc, @"^[0-9A-HJ-NPQRTUWXY]{18}$")) return false;

        int[] weights = { 1, 3, 9, 27, 19, 26, 16, 17, 20, 29, 25, 13, 8, 24, 10, 30, 28 };
        char[] checkCodes = "0123456789ABCDEFGHJKLMNPQRTUWXY".ToCharArray();

        int sum = 0;
        for (int i = 0; i < 17; i++)
        {
            if (!CharMap.TryGetValue(uscc[i], out int value)) return false;
            sum += value * weights[i];
        }

        int mod = 31 - (sum % 31);
        mod = mod == 31 ? 0 : mod;

        return uscc[17] == checkCodes[mod];
    }
    #endregion

    #region 营业执照相关验证
    private static bool ValidateBusinessLicenseNumber(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 营业执照号：15位数字（旧版）或18位统一社会信用代码
        return (id.Length == 15 && id.All(char.IsDigit)) ||
               ValidateUnifiedSocialCreditCode(id);
    }

    private static bool ValidateRegistrationNumber(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 注册号：通常为13-15位数字
        return Regex.IsMatch(id, @"^\d{13,15}$");
    }
    #endregion

    #region 金融相关编码验证
    private static bool ValidateManagerRegistrationCode(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 管理人登记编码：P开头 + 6位数字
        return id.Length == 7 && id.StartsWith("P") && id.Substring(1).All(char.IsDigit);
    }

    private static bool ValidateProductFilingCode(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 产品备案编码：6-8位字母数字组合
        return Regex.IsMatch(id, @"^[A-Z0-9]{6,8}$");
    }

    private static bool ValidateProductRegistrationCode(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 产品登记编码：S开头 + 9位数字
        return id.Length == 10 && id.StartsWith("S") && id.Substring(1).All(char.IsDigit);
    }

    private static bool ValidateTrustProductCode(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpper();

        // 信托登记系统产品编码：15位字母数字组合
        return id.Length == 15 && Regex.IsMatch(id, @"^[A-Z0-9]{15}$");
    }
    #endregion
}
 

/// <summary>
/// 证件类型枚举扩展（硬编码，无反射）
/// </summary>
public static class IDTypeExtensions
{
    /// <summary>
    /// 枚举 => 中文描述
    /// </summary>
    public static string ToAmacString(this IDType idType)
    {
        return idType switch
        {
            IDType.Unknown => "未知",
            IDType.IdentityCard => "身份证",
            IDType.PassportChina => "护照",
            IDType.OfficerID => "军官证",
            IDType.SoldierID => "士兵证",
            IDType.HongKongMacauPass => "港澳居民来往内地通行证",
            IDType.HouseholdRegister => "户口本",
            IDType.PassportForeign => "外国护照",
            IDType.CivilianID => "文职证",
            IDType.PoliceID => "警官证",
            IDType.TaiwanCompatriotsID => "台胞证",
            IDType.ForeignPermanentResidentID => "外国人永久居留身份证",
            IDType.UnifiedSocialCreditCode => "统一社会信用代码",
            IDType.OrganizationCode => "组织机构代码证",
            IDType.BusinessLicenseNumber => "营业执照号",
            IDType.RegistrationNumber => "注册号",
            IDType.ManagerRegistrationCode => "管理人登记编码",
            IDType.ProductFilingCode => "产品备案编码",
            IDType.SecuritiesBusinessLicense => "证券业务许可证",
            IDType.Approval => "批文",
            IDType.ProductRegistrationCode => "产品登记编码",
            IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents => "港澳台居民居住证",
            IDType.TrustRegistrationSystemProductCode => "信托登记系统产品编码",
            IDType.Other => "其他",
            _ => idType.ToString()
        };
    }

    /// <summary>
    /// 中文描述/字符串 => 枚举（硬编码解析）
    /// 匹配不到返回 Unknown
    /// </summary>
    public static IDType FromString(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return IDType.Unknown;

        return text.Trim() switch
        {
            "未知" => IDType.Unknown,
            "身份证" => IDType.IdentityCard,
            "护照" => IDType.PassportChina,
            "军官证" => IDType.OfficerID,
            "士兵证" => IDType.SoldierID,
            "港澳居民来往内地通行证" => IDType.HongKongMacauPass,
            "户口本" => IDType.HouseholdRegister,
            "外国护照" => IDType.PassportForeign,
            "文职证" => IDType.CivilianID,
            "警官证" => IDType.PoliceID,
            "台胞证" => IDType.TaiwanCompatriotsID,
            "外国人永久居留身份证" => IDType.ForeignPermanentResidentID,
            "Institusion" => IDType.Institusion,
            "统一社会信用代码" => IDType.UnifiedSocialCreditCode,
            "组织机构代码证" => IDType.OrganizationCode,
            "营业执照号" => IDType.BusinessLicenseNumber,
            "注册号" => IDType.RegistrationNumber,
            "管理人登记编码" => IDType.ManagerRegistrationCode,
            "产品备案编码" => IDType.ProductFilingCode,
            "证券业务许可证" => IDType.SecuritiesBusinessLicense,
            "批文" => IDType.Approval,
            "产品登记编码" => IDType.ProductRegistrationCode,
            "港澳台居民居住证" => IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents,
            "信托登记系统产品编码" => IDType.TrustRegistrationSystemProductCode,
            "其他" => IDType.Other,
            _ => IDType.Unknown // 兜底
        };
    }
}