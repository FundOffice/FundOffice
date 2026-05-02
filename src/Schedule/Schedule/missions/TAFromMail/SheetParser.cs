using ExcelDataReader;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using Serilog;
using System.Text.RegularExpressions;

namespace FMO.Schedule;

public class SheetParser
{
    public static TransferRecordType ParseType(string? str)
    {
        switch (str)
        {
            case "认购结果":
                return TransferRecordType.Subscription;

            case "申购确认":
                return TransferRecordType.Purchase;

            case string s when s.Contains("强制赎回"):
                return TransferRecordType.ForceRedemption;

            case string s when s.Contains("赎回"):
                return TransferRecordType.Redemption;

            case string s when s.Contains("调增"):
                return TransferRecordType.Increase;

            case string s when s.Contains("调减"):
                return TransferRecordType.Decrease;

            case string s when s.Contains("转入"):
                return TransferRecordType.MoveIn;

            case string s when s.Contains("转出"):
                return TransferRecordType.MoveOut;

            case string s when s.Contains("分红"):
                return TransferRecordType.Distribution;

            default:
                return TransferRecordType.UNK;
        }
    }
    public static string StringValue(object obj) => obj switch { string s => s, _ => obj?.ToString() ?? "" };

    public static decimal DecimalValue(object obj)
    {
        switch (obj)
        {
            case double s:
                return (decimal)s;

            case string s:
                if (Regex.Match(s, @"[\d,\.]+") is Match m && m.Success)
                    return decimal.Parse(m.Value);
                return -1;

            default:
                return -1;
        }
    }

    public static DateOnly DateOnlyValue(object o) => o switch { DateTime d => DateOnly.FromDateTime(d), DateOnly d => d, _ => DateTimeHelper.TryParse(o?.ToString(), out var d) ? d : default };


    protected void Check<TP, TE>(FieldInfo<TE, TP> field, IList<string> values) => field.Index = values.IndexOf(field.Header);

    protected void Fill<TE, TP>(FieldInfo<TE, TP> field, TE obj, IList<object> values)
    {
        if (field.Index >= 0 && values.Count > field.Index)
        {
            var v = field.GetValue(values[field.Index]);
            field.SetValue(obj, v);
        }
    }


    public virtual TransferRecord[] ParseTASheet(IExcelDataReader reader) => [];

    public virtual TransferRecord[] ParseTAConfirm(IExcelDataReader reader) => [];

    protected static void PostHandle(TransferRecord r, BaseDatabase db)
    {
        // 后处理
        // 对应fund id

        // 对应customer
        var customer = db.GetCollection<Investor>().FindOne(x => x.Name == r.InvestorName && x.Identity != null && x.Identity!.Id == r.InvestorIdentity);
        r.InvestorId = customer?.Id ?? 0;

        var fund = db.FindFund(r.FundCode);

        // 没找到对应的fund
        if (fund is null)
        {
            Log.Error($"{r.FundName}({r.FundCode}) {r.InvestorName} {r.ConfirmedDate} 未找到对应基金");
            return;
        }

        if (string.IsNullOrWhiteSpace(r.FundName))
        {
            Log.Error($"{r.FundName}({r.FundCode}) {r.InvestorName} {r.ConfirmedDate} 数据异常");
            return;
        }
        r.FundId = fund.Id;

        // 有子份额的
        var m = Regex.Match(r.FundName, @$"{fund.Name}(\w+)");
        if (m.Success)
            r.ShareClass = m.Groups[1].Value;

        r.Source = "Mail";
    }


    public static SheetParser Create(string identifier)
    {
        switch (identifier)
        {
            case "cmschina.com.cn":
                return new CMSParser();
            case "citics.com":
                return new CSTISCParser();
            case "csc.com.cn":
            case "csc108.com":
                return new CSCParser();

            default:
                return new SheetParser();
        }
    }
}



public class CMSParser : SheetParser
{
    public override TransferRecord[] ParseTASheet(IExcelDataReader reader)
    {
        FieldInfo<TransferRecord, string?> FundName = new FieldInfo<TransferRecord, string?>("产品名称", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundName = y);
        FieldInfo<TransferRecord, string?> FundCode = new FieldInfo<TransferRecord, string?>("产品代码", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundCode = y);
        FieldInfo<TransferRecord, TransferRecordType> Type = new FieldInfo<TransferRecord, TransferRecordType>("业务类型", o => ParseType(o?.ToString()), (x, y) => x.Type = y);
        FieldInfo<TransferRecord, DateOnly> RequestDate = new FieldInfo<TransferRecord, DateOnly>("申请日期", o => DateOnlyValue(o), (x, y) => x.RequestDate = y);
        FieldInfo<TransferRecord, decimal> RequestAmount = new FieldInfo<TransferRecord, decimal>("申请金额", o => DecimalValue(o), (x, y) => x.RequestAmount = y);
        FieldInfo<TransferRecord, decimal> RequestShare = new FieldInfo<TransferRecord, decimal>("申请份额", o => DecimalValue(o), (x, y) => x.RequestShare = y);

        FieldInfo<TransferRecord, DateOnly> ConfirmedDate = new FieldInfo<TransferRecord, DateOnly>("确认日期", o => DateOnlyValue(o), (x, y) => x.ConfirmedDate = y);
        FieldInfo<TransferRecord, decimal> ConfirmedShare = new("确认份额", o => DecimalValue(o), (x, y) => x.ConfirmedShare = y);
        FieldInfo<TransferRecord, decimal> ConfirmedAmount = new FieldInfo<TransferRecord, decimal>("确认金额", o => DecimalValue(o), (x, y) => x.ConfirmedAmount = y);
        FieldInfo<TransferRecord, decimal> ConfirmedNetAmount = new FieldInfo<TransferRecord, decimal>("确认净额", o => DecimalValue(o), (x, y) => x.ConfirmedNetAmount = y);


        FieldInfo<TransferRecord, decimal> Fee = new FieldInfo<TransferRecord, decimal>("手续费", o => DecimalValue(o), (x, y) => x.Fee = y);
        FieldInfo<TransferRecord, decimal> PerformanceFee = new FieldInfo<TransferRecord, decimal>("业绩报酬", o => DecimalValue(o), (x, y) => x.PerformanceFee = y);

        FieldInfo<TransferRecord, string> InvestorIdentity = new FieldInfo<TransferRecord, string>("证件号码", o => o switch { string s => s, _ => o.ToString()! }, (x, y) => x.InvestorIdentity = y);
        FieldInfo<TransferRecord, string> InvestorName = new FieldInfo<TransferRecord, string>("客户名称", o => StringValue(o), (x, y) => x.InvestorName = y);


        FieldInfo<TransferRecord, string> ExternalRequestId = new FieldInfo<TransferRecord, string>("申请单号", o => StringValue(o), (x, y) => x.ExternalRequestId = y, false);
        FieldInfo<TransferRecord, string> ExternalId = new FieldInfo<TransferRecord, string>("确认流水号", o => StringValue(o), (x, y) => x.ExternalId = y);

        reader.Reset();
        reader.Read();
        var head = new object[reader.FieldCount];
        reader.GetValues(head);

        List<TransferRecord> records = new List<TransferRecord>();

        var fields = head.Select(x => x?.ToString() ?? "").ToList();
        Check(FundName, fields);
        Check(FundCode, fields);
        Check(Type, fields);
        Check(RequestDate, fields);
        Check(RequestAmount, fields);
        Check(RequestShare, fields);
        Check(ConfirmedDate, fields);
        Check(ConfirmedShare, fields);
        Check(ConfirmedAmount, fields);
        Check(ConfirmedNetAmount, fields);
        Check(Fee, fields);
        Check(PerformanceFee, fields);
        Check(InvestorIdentity, fields);
        Check(InvestorName, fields);
        Check(ExternalRequestId, fields);
        Check(ExternalId, fields);

         

        List<string> errors = new List<string>();
        if (FundName.Index == -1) errors.Add("FundName");
        if (FundCode.Index == -1) errors.Add("FundCode");
        if (Type.Index == -1) errors.Add("Type");
        if (RequestDate.Index == -1) errors.Add("RequestDate");
        if (RequestAmount.Index == -1) errors.Add("RequestAmount");
        if (RequestShare.Index == -1) errors.Add("RequestShare");
        if (ConfirmedDate.Index == -1) errors.Add("ConfirmedDate");
        if (ConfirmedShare.Index == -1) errors.Add("ConfirmedShare");
        if (ConfirmedAmount.Index == -1) errors.Add("ConfirmedAmount");
        if (ConfirmedNetAmount.Index == -1) errors.Add("ConfirmedNetAmount");
        if (Fee.Index == -1) errors.Add("Fee");
        if (PerformanceFee.Index == -1) errors.Add("PerformanceFee");
        if (InvestorIdentity.Index == -1) errors.Add("InvestorIdentity");
        if (InvestorName.Index == -1) errors.Add("InvestorName");
        if (ExternalId.Index == -1) errors.Add("ExternalId");


        if (errors.Count > 0)
        {
            Log.Error($"TA表头无法解析 {string.Join(',', fields)}");
            return Array.Empty<TransferRecord>();
        }

        for (int i = 0; i < reader.RowCount - 1; i++)
        {
            reader.Read();
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            var r = new TransferRecord { InvestorIdentity = "", InvestorName = "" };

            Fill(FundName, r, values);
            Fill(FundCode, r, values);
            Fill(Type, r, values);
            Fill(RequestDate, r, values);
            Fill(RequestAmount, r, values);
            Fill(RequestShare, r, values);
            Fill(ConfirmedDate, r, values);
            Fill(ConfirmedShare, r, values);
            Fill(ConfirmedAmount, r, values);
            Fill(ConfirmedNetAmount, r, values);
            Fill(Fee, r, values);
            Fill(PerformanceFee, r, values);
            Fill(InvestorIdentity, r, values);
            Fill(InvestorName, r, values);
            Fill(ExternalId, r, values);
              
            records.Add(r);
        }


        using var db = DbHelper.Base();
        foreach (var r in records)
            PostHandle(r, db);

        return records.ToArray();
    }


}


public class CSTISCParser : SheetParser
{
    public override TransferRecord[] ParseTASheet(IExcelDataReader reader)
    {
        FieldInfo<TransferRecord, string?> FundName = new FieldInfo<TransferRecord, string?>("产品名称", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundName = y);
        FieldInfo<TransferRecord, string?> FundCode = new FieldInfo<TransferRecord, string?>("产品代码", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundCode = y);
        FieldInfo<TransferRecord, TransferRecordType> Type = new FieldInfo<TransferRecord, TransferRecordType>("业务类型", o => ParseType(o?.ToString()), (x, y) => x.Type = y);
        FieldInfo<TransferRecord, DateOnly> RequestDate = new FieldInfo<TransferRecord, DateOnly>("申请日期", o => DateOnlyValue(o), (x, y) => x.RequestDate = y);
        FieldInfo<TransferRecord, decimal> RequestAmount = new FieldInfo<TransferRecord, decimal>("申请金额", o => DecimalValue(o), (x, y) => x.RequestAmount = y);
        FieldInfo<TransferRecord, decimal> RequestShare = new FieldInfo<TransferRecord, decimal>("申请份额", o => DecimalValue(o), (x, y) => x.RequestShare = y);

        FieldInfo<TransferRecord, DateOnly> ConfirmedDate = new FieldInfo<TransferRecord, DateOnly>("确认日期", o => DateOnlyValue(o), (x, y) => x.ConfirmedDate = y);
        FieldInfo<TransferRecord, decimal> ConfirmedShare = new("确认份额", o => DecimalValue(o), (x, y) => x.ConfirmedShare = y);
        FieldInfo<TransferRecord, decimal> ConfirmedAmount = new FieldInfo<TransferRecord, decimal>("确认金额", o => DecimalValue(o), (x, y) => x.ConfirmedAmount = y);
        FieldInfo<TransferRecord, decimal> ConfirmedNetAmount = new FieldInfo<TransferRecord, decimal>("净认/申购金额", o => DecimalValue(o), (x, y) => x.ConfirmedNetAmount = y);


        FieldInfo<TransferRecord, decimal> Fee = new FieldInfo<TransferRecord, decimal>("交易费", o => DecimalValue(o), (x, y) => x.Fee = y);
        FieldInfo<TransferRecord, decimal> PerformanceFee = new FieldInfo<TransferRecord, decimal>("业绩报酬", o => DecimalValue(o), (x, y) => x.PerformanceFee = y);

        FieldInfo<TransferRecord, string> InvestorIdentity = new FieldInfo<TransferRecord, string>("证件号码", o => o switch { string s => s, _ => o.ToString()! }, (x, y) => x.InvestorIdentity = y);
        FieldInfo<TransferRecord, string> InvestorName = new FieldInfo<TransferRecord, string>("客户名称", o => StringValue(o), (x, y) => x.InvestorName = y);

        FieldInfo<TransferRecord, string> ExternalId = new FieldInfo<TransferRecord, string>("TA确认号", o => StringValue(o), (x, y) => x.ExternalId = y);


        reader.Read();
        var head = new object[reader.FieldCount];
        reader.GetValues(head);

        List<TransferRecord> records = new List<TransferRecord>();

        var fields = head.Select(x => x?.ToString() ?? "").ToList();
        Check(FundName, fields);
        Check(FundCode, fields);
        Check(Type, fields);
        Check(RequestDate, fields);
        Check(RequestAmount, fields);
        Check(RequestShare, fields);
        Check(ConfirmedDate, fields);
        Check(ConfirmedShare, fields);
        Check(ConfirmedAmount, fields);
        Check(ConfirmedNetAmount, fields);
        Check(Fee, fields);
        Check(PerformanceFee, fields);
        Check(InvestorIdentity, fields);
        Check(InvestorName, fields);
        Check(ExternalId, fields);

        List<string> errors = new List<string>();
        if (FundName.Index == -1) errors.Add("FundName");
        if (FundCode.Index == -1) errors.Add("FundCode");
        if (Type.Index == -1) errors.Add("Type");
        if (RequestDate.Index == -1) errors.Add("RequestDate");
        if (RequestAmount.Index == -1) errors.Add("RequestAmount");
        if (RequestShare.Index == -1) errors.Add("RequestShare");
        if (ConfirmedDate.Index == -1) errors.Add("ConfirmedDate");
        if (ConfirmedShare.Index == -1) errors.Add("ConfirmedShare");
        if (ConfirmedAmount.Index == -1) errors.Add("ConfirmedAmount");
        if (ConfirmedNetAmount.Index == -1) errors.Add("ConfirmedNetAmount");
        if (Fee.Index == -1) errors.Add("Fee");
        if (PerformanceFee.Index == -1) errors.Add("PerformanceFee");
        if (InvestorIdentity.Index == -1) errors.Add("InvestorIdentity");
        if (InvestorName.Index == -1) errors.Add("InvestorName");
        if (ExternalId.Index == -1) errors.Add("ExternalId");


        if (errors.Count > 0)
        {
            Log.Error($"TA表头无法解析 {string.Join(',', fields)}");
            return Array.Empty<TransferRecord>();
        }

        for (int i = 0; i < reader.RowCount - 1; i++)
        {
            reader.Read();
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            var r = new TransferRecord { InvestorIdentity = "", InvestorName = "" };

            Fill(FundName, r, values);
            Fill(FundCode, r, values);
            Fill(Type, r, values);
            Fill(RequestDate, r, values);
            Fill(RequestAmount, r, values);
            Fill(RequestShare, r, values);
            Fill(ConfirmedDate, r, values);
            Fill(ConfirmedShare, r, values);
            Fill(ConfirmedAmount, r, values);
            Fill(ConfirmedNetAmount, r, values);
            Fill(Fee, r, values);
            Fill(PerformanceFee, r, values);
            Fill(InvestorIdentity, r, values);
            Fill(InvestorName, r, values);
            Fill(ExternalId, r, values);

            // 特殊情况
            if (r.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => true, _ => false } && r.ConfirmedNetAmount <= 0)
                r.ConfirmedNetAmount = r.ConfirmedAmount;


            records.Add(r);
        }


        using var db = DbHelper.Base();
        foreach (var r in records)
            PostHandle(r, db);

        return records.ToArray();
    }
}


public class CSCParser : SheetParser
{
    public override TransferRecord[] ParseTASheet(IExcelDataReader reader)
    {
        FieldInfo<TransferRecord, string?> FundName = new FieldInfo<TransferRecord, string?>("产品名称", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundName = y);
        FieldInfo<TransferRecord, string?> FundCode = new FieldInfo<TransferRecord, string?>("产品代码", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundCode = y);
        FieldInfo<TransferRecord, TransferRecordType> Type = new FieldInfo<TransferRecord, TransferRecordType>("业务类型", o => ParseType(o?.ToString()), (x, y) => x.Type = y);
        FieldInfo<TransferRecord, DateOnly> RequestDate = new FieldInfo<TransferRecord, DateOnly>("申请日期", o => DateOnlyValue(o), (x, y) => x.RequestDate = y);
        FieldInfo<TransferRecord, decimal> RequestAmount = new FieldInfo<TransferRecord, decimal>("申请金额", o => DecimalValue(o), (x, y) => x.RequestAmount = y);
        FieldInfo<TransferRecord, decimal> RequestShare = new FieldInfo<TransferRecord, decimal>("申请份额", o => DecimalValue(o), (x, y) => x.RequestShare = y);

        FieldInfo<TransferRecord, DateOnly> ConfirmedDate = new FieldInfo<TransferRecord, DateOnly>("确认日期", o => DateOnlyValue(o), (x, y) => x.ConfirmedDate = y);
        FieldInfo<TransferRecord, decimal> ConfirmedShare = new("确认份额", o => DecimalValue(o), (x, y) => x.ConfirmedShare = y);
        FieldInfo<TransferRecord, decimal> ConfirmedAmount = new FieldInfo<TransferRecord, decimal>("确认金额", o => DecimalValue(o), (x, y) => x.ConfirmedAmount = y);
        FieldInfo<TransferRecord, decimal> ConfirmedNetAmount = new FieldInfo<TransferRecord, decimal>("净认/申购金额", o => DecimalValue(o), (x, y) => x.ConfirmedNetAmount = y);


        FieldInfo<TransferRecord, decimal> Fee = new FieldInfo<TransferRecord, decimal>("交易费", o => DecimalValue(o), (x, y) => x.Fee = y);
        FieldInfo<TransferRecord, decimal> PerformanceFee = new FieldInfo<TransferRecord, decimal>("业绩报酬", o => DecimalValue(o), (x, y) => x.PerformanceFee = y);

        FieldInfo<TransferRecord, string> InvestorIdentity = new FieldInfo<TransferRecord, string>("证件号码", o => o switch { string s => s, _ => o.ToString()! }, (x, y) => x.InvestorIdentity = y);
        FieldInfo<TransferRecord, string> InvestorName = new FieldInfo<TransferRecord, string>("客户名称", o => StringValue(o), (x, y) => x.InvestorName = y);

        FieldInfo<TransferRecord, string> ExternalId = new FieldInfo<TransferRecord, string>("TA确认号", o => StringValue(o), (x, y) => x.ExternalId = y);

        reader.Read();
        var head = new object[reader.FieldCount];
        reader.GetValues(head);

        List<TransferRecord> records = new List<TransferRecord>();

        var fields = head.Select(x => x?.ToString() ?? "").ToList();
        Check(FundName, fields);
        Check(FundCode, fields);
        Check(Type, fields);
        Check(RequestDate, fields);
        Check(RequestAmount, fields);
        Check(RequestShare, fields);
        Check(ConfirmedDate, fields);
        Check(ConfirmedShare, fields);
        Check(ConfirmedAmount, fields);
        Check(ConfirmedNetAmount, fields);
        Check(Fee, fields);
        Check(PerformanceFee, fields);
        Check(InvestorIdentity, fields);
        Check(InvestorName, fields);
        Check(ExternalId, fields);

        List<string> errors = new List<string>();
        if (FundName.Index == -1) errors.Add("FundName");
        if (FundCode.Index == -1) errors.Add("FundCode");
        if (Type.Index == -1) errors.Add("Type");
        if (RequestDate.Index == -1) errors.Add("RequestDate");
        if (RequestAmount.Index == -1) errors.Add("RequestAmount");
        if (RequestShare.Index == -1) errors.Add("RequestShare");
        if (ConfirmedDate.Index == -1) errors.Add("ConfirmedDate");
        if (ConfirmedShare.Index == -1) errors.Add("ConfirmedShare");
        if (ConfirmedAmount.Index == -1) errors.Add("ConfirmedAmount");
        if (ConfirmedNetAmount.Index == -1) errors.Add("ConfirmedNetAmount");
        if (Fee.Index == -1) errors.Add("Fee");
        if (PerformanceFee.Index == -1) errors.Add("PerformanceFee");
        if (InvestorIdentity.Index == -1) errors.Add("InvestorIdentity");
        if (InvestorName.Index == -1) errors.Add("InvestorName");
        if (ExternalId.Index == -1) errors.Add("ExternalId");


        if (errors.Count > 0)
        {
            Log.Error($"TA表头无法解析 {string.Join(',', fields)}");
            return Array.Empty<TransferRecord>();
        }

        for (int i = 0; i < reader.RowCount - 1; i++)
        {
            reader.Read();
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            var r = new TransferRecord { InvestorIdentity = "", InvestorName = "" };

            Fill(FundName, r, values);
            Fill(FundCode, r, values);
            Fill(Type, r, values);
            Fill(RequestDate, r, values);
            Fill(RequestAmount, r, values);
            Fill(RequestShare, r, values);
            Fill(ConfirmedDate, r, values);
            Fill(ConfirmedShare, r, values);
            Fill(ConfirmedAmount, r, values);
            Fill(ConfirmedNetAmount, r, values);
            Fill(Fee, r, values);
            Fill(PerformanceFee, r, values);
            Fill(InvestorIdentity, r, values);
            Fill(InvestorName, r, values);
            Fill(ExternalId, r, values);

            // 特殊情况
            if (r.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => true, _ => false } && r.ConfirmedNetAmount <= 0)
                r.ConfirmedNetAmount = r.ConfirmedAmount;


            records.Add(r);
        }


        using var db = DbHelper.Base();
        foreach (var r in records)
            PostHandle(r, db);

        return records.ToArray();
    }

    public TransferRecord[] ParseTA(IList<string> fields, IEnumerable<IList<object>> data)
    {
        FieldInfo<TransferRecord, string?> FundName = new FieldInfo<TransferRecord, string?>("基金名称", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundName = y);
        FieldInfo<TransferRecord, string?> FundCode = new FieldInfo<TransferRecord, string?>("基金代码", o => o switch { string s => s, _ => o.ToString() }, (x, y) => x.FundCode = y);
        FieldInfo<TransferRecord, TransferRecordType> Type = new FieldInfo<TransferRecord, TransferRecordType>("业务类型", o => ParseType(o?.ToString()), (x, y) => x.Type = y);
        FieldInfo<TransferRecord, DateOnly> RequestDate = new FieldInfo<TransferRecord, DateOnly>("申请日期", o => DateOnlyValue(o), (x, y) => x.RequestDate = y);
        FieldInfo<TransferRecord, decimal> RequestAmount = new FieldInfo<TransferRecord, decimal>("申请金额", o => DecimalValue(o), (x, y) => x.RequestAmount = y);
        FieldInfo<TransferRecord, decimal> RequestShare = new FieldInfo<TransferRecord, decimal>("申请份额", o => DecimalValue(o), (x, y) => x.RequestShare = y);

        FieldInfo<TransferRecord, DateOnly> ConfirmedDate = new FieldInfo<TransferRecord, DateOnly>("确认日期", o => DateOnlyValue(o), (x, y) => x.ConfirmedDate = y);
        FieldInfo<TransferRecord, decimal> ConfirmedShare = new("确认份额", o => DecimalValue(o), (x, y) => x.ConfirmedShare = y);
        FieldInfo<TransferRecord, decimal> ConfirmedAmount = new FieldInfo<TransferRecord, decimal>("确认金额", o => DecimalValue(o), (x, y) => x.ConfirmedAmount = y);
        FieldInfo<TransferRecord, decimal> ConfirmedNetAmount = new FieldInfo<TransferRecord, decimal>("确认金额净额", o => DecimalValue(o), (x, y) => x.ConfirmedNetAmount = y);


        FieldInfo<TransferRecord, decimal> Fee = new FieldInfo<TransferRecord, decimal>("交易费用", o => DecimalValue(o), (x, y) => x.Fee = y);
        FieldInfo<TransferRecord, decimal> PerformanceFee = new FieldInfo<TransferRecord, decimal>("业绩报酬", o => DecimalValue(o), (x, y) => x.PerformanceFee = y);

        FieldInfo<TransferRecord, string> InvestorIdentity = new FieldInfo<TransferRecord, string>("证件号码", o => o switch { string s => s, _ => o.ToString()! }, (x, y) => x.InvestorIdentity = y);
        FieldInfo<TransferRecord, string> InvestorIdType = new FieldInfo<TransferRecord, string>("证件类型", o => o switch { string s => s, _ => o.ToString()! }, (x, y) => x.InvestorIdentity = y);
        FieldInfo<TransferRecord, string> InvestorName = new FieldInfo<TransferRecord, string>("投资人名称", o => StringValue(o), (x, y) => x.InvestorName = y);

        FieldInfo<TransferRecord, string> ExternalId = new FieldInfo<TransferRecord, string>("TA确认号", o => StringValue(o), (x, y) => x.ExternalId = y);

        List<TransferRecord> records = new List<TransferRecord>();

        Check(FundName, fields);
        Check(FundCode, fields);
        Check(Type, fields);
        Check(RequestDate, fields);
        Check(RequestAmount, fields);
        Check(RequestShare, fields);
        Check(ConfirmedDate, fields);
        Check(ConfirmedShare, fields);
        Check(ConfirmedAmount, fields);
        Check(ConfirmedNetAmount, fields);
        Check(Fee, fields);
        Check(PerformanceFee, fields);
        Check(InvestorIdentity, fields);
        Check(InvestorIdType, fields);
        Check(InvestorName, fields);
        Check(ExternalId, fields);

        List<string> errors = new List<string>();
        if (FundName.Index == -1) errors.Add("FundName");
        if (FundCode.Index == -1) errors.Add("FundCode");
        if (Type.Index == -1) errors.Add("Type");
        if (RequestDate.Index == -1) errors.Add("RequestDate");
        if (RequestAmount.Index == -1) errors.Add("RequestAmount");
        if (RequestShare.Index == -1) errors.Add("RequestShare");
        if (ConfirmedDate.Index == -1) errors.Add("ConfirmedDate");
        if (ConfirmedShare.Index == -1) errors.Add("ConfirmedShare");
        if (ConfirmedAmount.Index == -1) errors.Add("ConfirmedAmount");
        if (ConfirmedNetAmount.Index == -1) errors.Add("ConfirmedNetAmount");
        if (Fee.Index == -1) errors.Add("Fee");
        if (PerformanceFee.Index == -1) errors.Add("PerformanceFee");
        if (InvestorIdentity.Index == -1) errors.Add("InvestorIdentity");
        if (InvestorName.Index == -1) errors.Add("InvestorName");
        if (ExternalId.Index == -1) errors.Add("ExternalId");


        if (errors.Count > 0)
        {
            Log.Error($"TA表头无法解析 {string.Join(',', fields)}");
            return Array.Empty<TransferRecord>();
        }

        foreach (var values in data)
        {
            var r = new TransferRecord { InvestorIdentity = "", InvestorName = "" };

            Fill(FundName, r, values);
            Fill(FundCode, r, values);
            Fill(Type, r, values);
            Fill(RequestDate, r, values);
            Fill(RequestAmount, r, values);
            Fill(RequestShare, r, values);
            Fill(ConfirmedDate, r, values);
            Fill(ConfirmedShare, r, values);
            Fill(ConfirmedAmount, r, values);
            Fill(ConfirmedNetAmount, r, values);
            Fill(Fee, r, values);
            Fill(PerformanceFee, r, values);
            Fill(InvestorIdentity, r, values);
            if (!Regex.IsMatch(r.InvestorIdentity, @"\d+"))
                Fill(InvestorIdType, r, values);
            Fill(InvestorName, r, values);
            Fill(ExternalId, r, values);


            records.Add(r);
        }


        using var db = DbHelper.Base();
        foreach (var r in records)
            PostHandle(r, db);

        return records.ToArray();
    }


    public override TransferRecord[] ParseTAConfirm(IExcelDataReader reader)
    {
        //
        List<string> head = new();
        List<List<object>> tas = new();

        do
        {
            bool first = head.Count == 0;
            var vl = new List<object>();
            while (reader.Read())
            {
                if (reader.FieldCount < 2) continue;

                var values = new object[reader.FieldCount];
                reader.GetValues(values);

                // 两两一组
                for (int i = 0; i <= reader.FieldCount / 2; i += 2)
                {
                    if (values[i] is string s && values[i + 1] is not null)
                    {
                        if (first)
                            head.Add(s.Trim());
                        vl.Add(values[i + 1]);
                    }
                }
            }
            tas.Add(vl);
        } while (reader.NextResult());

        return ParseTA(head, tas);
    }
}
