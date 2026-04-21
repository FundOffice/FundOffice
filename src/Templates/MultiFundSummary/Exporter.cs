using FMO.Models;
using FMO.TPL;
using FMO.Utilities;

namespace MultiFundSummary;


public class Exporter : IExporter
{
    public string Id => "114319F6-4CCF-4A1C-907C-C58E68A04C25";

    public string Name => "基金数据汇总表";

    public string Description => "生成基金的数据汇总表";

    public ExportTypeFlag Suit => ExportTypeFlag.MultiFundSummary;

    public ExportParameterMeta[]? Meta => [new(nameof(Fund))];

    public ExportInfo Generate(object? parameter)
    {
        if (parameter is not int[] fundIds)
            return new ExportInfo("参数错误");

        //文件名
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Find(x => fundIds.Contains(x.Id)).ToArray();

        var data = funds.Select(x => new
        {
            Id = x.Id,
            Name = x.Name,
            Code = x.Code,
            Latest = db.GetDailyCollection(x.Id).Query().OrderByDescending(y => y.Date).Where(x => x.NetValue != 0).FirstOrDefault()
        });


        ExportInfo ExportInfo = new ExportInfo { FileName = $"基金汇总表-{DateTime.Now:yyyy.MM.dd}.xlsx" };
        ExportInfo.Filter = "Excel|*.xlsx";
        var obj = new { f = data.Select(x => x.ExpandToDictionary().ReplaceNullsWithPlaceholder()).ToList() };
        //obj.ReplaceNullsWithPlaceholder();
        ExportInfo.Data = obj;

        return ExportInfo;
    }
}