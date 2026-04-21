using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;

namespace MultiFundElementSheet;




public class Exporter : IExporter
{
    public string Id => "8F9D1C3E-6A42-4B7C-9E1D-0A3B7C8D2E4F";

    public string Name => "基金要素汇总表";

    public string Description => "生成基金的要素汇总表";

    public ExportTypeFlag Suit => ExportTypeFlag.MultiFundElementSheet;

    public ExportParameterMeta[]? Meta => [new(nameof(Fund))];

    public ExportInfo Generate(object? parameter)
    {
        if (parameter is not int[] fundIds)
            return new ExportInfo("参数错误");

        //文件名
        using var db = DbHelper.Base();
        var funds = db.GetCollection<Fund>().Query().Where(Query.In("_id", fundIds.Select(x=>new BsonValue(x)))).ToArray();

        var data = funds.SelectMany(x =>
        {
            var elements = db.GetCollection<FundElements>().FindById(x.Id) ?? FundElements.Create(x.Id, 0);
            return ReadonlyFundInfo.Load(x, elements);
        }).ToArray();


        ExportInfo ExportInfo = new ExportInfo { FileName = $"要素汇总表-{DateTime.Now:yyyy.MM.dd}.xlsx" };
        ExportInfo.Filter = "Excel|*.xlsx";
        var obj = ObjectExtension.ExpandToDictionary(new { f = data });
        ObjectExtension.ReplaceNullsWithPlaceholder(obj);//.ReplaceNullsWithPlaceholder();
        ExportInfo.Data = obj;

        return ExportInfo;
    }
}