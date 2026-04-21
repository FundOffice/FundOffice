using FMO.Models;
using FMO.TPL;
using FMO.Utilities;

namespace FundHolderSheet;

public class Exporter : IExporter
{
    public string Id => "DF3CEE4F-EF22-E7F7-8238-6CFEE4605326";

    public string Name => "持有人名册";

    public string Description => "基金的持有人名册";

    public ExportTypeFlag Suit => ExportTypeFlag.SingleFundHolderSheet;

    public ExportParameterMeta[]? Meta => [new(nameof(Fund)), new(nameof(DateOnly), false)];

    public ExportInfo Generate(object? parameter = null)
    {
        if (parameter is not (int FundId, DateOnly Date))
            return new ExportInfo("参数错误");


        //文件名
        using var db = DbHelper.Base();
        var fund = db.GetCollection<Fund>().FindById(FundId);

        if (fund is null)
            return new("基金不存在");

        ExportInfo ExportInfo = new ExportInfo { FileName = $"{fund.Name}-持有人名册-{Date:yyyy.MM.dd}.xlsx" };
        ExportInfo.Filter = "Excel|*.xlsx";

        var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == FundId).ToList().GroupBy(x => x.InvestorId).ToDictionary(x => x.Key, x => x.ToArray());
        var cid = ta.Keys.ToArray();
        var cs = db.GetCollection<InvestorHolding>(nameof(Investor)).Find(x => cid.Contains(x.Id)).ToList();
        cs.ForEach(x => x.Records = ta[x.Id]);

        var nv = db.GetDailyCollection(FundId).Query().OrderByDescending(x => x.Date).Where(x => x.Date <= Date).FirstOrDefault()?.NetValue ?? 0;
        cs.ForEach(x => x.Init(nv));
        var totalShare = cs.Sum(x => x.Share);
        cs.ForEach(x => x.Proportion = x.Share / totalShare);

        var obj = ObjectExtension.ExpandToDictionary(new { c = cs.Where(x => x.Proportion > 0).ToList() });
        ExportInfo.Data = obj;

        return ExportInfo;

    }
}


public class InvestorHolding : Investor
{
    public IList<TransferRecord>? Records { get; set; }

    public string IdentityType => EnumDescriptionTypeConverter.GetEnumDescription(Identity?.Type ?? default);

    public string IdentityId => Identity?.Id ?? "";

    public decimal Share { get; set; }

    public decimal Asset { get; private set; }
    public decimal Deposit { get; private set; }
    public decimal Withdraw { get; private set; }
    public decimal Profit { get; private set; }
    public decimal Proportion { get; set; }

    public void Init(decimal nv)
    {
        if (Records is null || Records.Count == 0) return;

        Share = Records.Sum(x => x.ShareChange());
        Asset = Share * nv;
        Deposit = Records.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
        Withdraw = Records.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.Redemption or TransferRecordType.MoveOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
        Profit = Asset + Withdraw - Deposit;

    }


}