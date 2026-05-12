namespace FMO.ESigning;






/// <summary>
/// 基金资料
/// </summary>
/// <param name="Name"></param>
/// <param name="Code"></param>
/// <param name="Id"></param>
/// <param name="Class"> 份额类别 </param>
/// <param name="SetupDate"></param>
public record EsigningFundInfo(string Id, string Name, string Code, string Class, DateOnly SetupDate);