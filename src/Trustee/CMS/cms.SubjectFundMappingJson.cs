using FMO.Models;
using System.Text.Json.Serialization;


namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class SubjectFundMappingJson : JsonBase
{
    [JsonPropertyName("gradeNo")]
    public string GradeNo { get; set; }

    [JsonPropertyName("gradeName")]
    public string GradeName { get; set; }

    [JsonPropertyName("productNo")]
    public string ProductNo { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; }

    [JsonPropertyName("isGrade")]
    public string IsGrade { get; set; }

    [JsonPropertyName("registerNo")]
    public string RegisterNo { get; set; }

    public SubjectFundMapping ToObject()
    {
        var sc = IsGrade == "1" ? GradeName.Replace(ProductName, "") : "";
        return new SubjectFundMapping { FundCode = GradeNo, FundName = GradeName, MasterCode = ProductNo, AmacCode = ProductNo, MasterName = ProductName, ShareClass = sc };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。