using FMO.Models;
using FMO.TPL;
using FMO.Utilities;

namespace SingleFundNetValueList;

public class Exporter : IExporter
{
    public string Id => "A176D642-C22E-4D46-81DF-56F09D71266B";

    public string Name => "单基金净值表";

    public string Description => "生成单一基金的净值列表";

    public ExportTypeFlag Suit => ExportTypeFlag.SingleFundNetValueList;

    public ExportParameterMeta[]? Meta => [new(nameof(Fund), false)];

    public ExportInfo Generate(object? parameter)
    {
        if (parameter is not int fundId)
            return new ExportInfo("参数错误");

        //文件名
        using var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(fundId);
        if (fund is null) return new ExportInfo("未找到此基金");

        var daily = db.GetDailyCollection(fundId).Find(x => x.NetValue != 0).OrderByDescending(x => x.Date).ToList();
        var last = daily.FirstOrDefault();
        if (last is null) return new ExportInfo("此基金没有净值数据");

        ExportInfo ExportInfo = new ExportInfo { FileName = $"{fund.Name}-每日净值-{last.Date:yyyy.MM.dd}.xlsx" };
        ExportInfo.Filter = "Excel|*.xlsx";
        ExportInfo.Data = new { dy = daily };

        return ExportInfo;
    }
}
