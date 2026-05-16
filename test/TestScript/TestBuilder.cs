using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace TestScript;


[TestClass]
public class TestBuilder
{
    [TestMethod]
    public void TestGen()
    {
        xxxBuilder xx = new xxxBuilder();

        var sc =  xx.GenerateScript();
    }
}




public class xxxBuilder : TemplateBuilder, IBuilder<TransferRecord>
{
    public xxxBuilder()
    {

        TemplateMeta meta = new TemplateMeta
        {
            Name = "示例模板",
            Input = [new(TemplateInputs.Fund, ChooseType.Single), new(TemplateInputs.Date, ChooseType.Single)],
            //ReferenceInfo = [new ReferenceInfo(nameof(TransferRecord), queryExp.ToString())],

        };

    }
     


    public TransferRecord[] QueryRefer(ILiteQueryable<TransferRecord> query)
    {
        return query.Where(Query.In(nameof(TransferRecord.FundId), Funds.Select(x => new BsonValue(x.Id)))).ToArray();
    }

 

    public override object ScriptFunc(TemplateGlobal g)
    {
        var cur = g.Records.GroupBy(x => x.InvestorId)
       .Select(x => new
       {
           Id = x.Key,
           x.First().FundId,
           Name = x.First().InvestorName,
           Record = x,
           Share = x.Sum(y => y.ShareChange())
       }).OrderByDescending(x => x.Share);

        var date = g.Dates[0];
        var nv = g.Dailies[0].NetValue;
        var totalShare = cur.Sum(x => x.Share);
        return new
        {
            c = cur.Select(x => new
            {
                Name = x.Name,
                Share = x.Share,
                Asset = x.Share * nv,
                Deposit = x.Record.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount),
                Withdraw = x.Record.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.Redemption or TransferRecordType.MoveOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount),
                Proportion = x.Share == 0 ? 0 : x.Share / totalShare
            })
        };
    }


    //gen
    Expression<Func<ILiteQueryable<TransferRecord>, TransferRecord[]>>? queryExp;

}