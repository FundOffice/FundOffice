namespace FMO.ESigning;






/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Name">子基金名称</param>
/// <param name="ShareCode">子基金代码</param>
/// <param name="MajorName">主基金名称</param>
/// <param name="MajorCode">主基金代码</param>
/// <param name="SetupDate"></param>
public record EsigningFundInfo(string Id, string Name,  string ShareCode, string MajorName, string MajorCode, DateOnly SetupDate);