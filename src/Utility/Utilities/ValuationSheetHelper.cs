using ExcelDataReader;
using FMO.Models;
using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FMO.Utilities;

public static class ValuationSheetHelper
{

    public static DailyValue[] ParseMany(FileInfo[] files)
    {
        List<Fund> funds;
        using (var db = DbHelper.Base())
            funds = db.GetCollection<Fund>().FindAll().ToList();

        var zip = ZipFile.OpenRead("");

        return Array.Empty<DailyValue>();
    }

    public static DailyValue[] ParseMany(FileInfo zipfiles)
    {
        List<Fund> funds;
        using (var db = DbHelper.Base())
            funds = db.GetCollection<Fund>().FindAll().ToList();

        var zip = ZipFile.OpenRead("");

        return Array.Empty<DailyValue>();
    }


    public static (string? fn, string? code, DailyValue? dy) ParseExcel(Stream stream)
    {
        if (stream is null || stream.Length == 0) return (null, null, null);

        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);
        //Console.WriteLine($"\n===============================================================\n");

        var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet();
        var table = dataset.Tables[0];

        //Console.WriteLine($"Table:  {table.Rows[0]["Column0"]}");

        string? fundname = null, code = null;
        DailyValue daily = new DailyValue();
        //find product name
        //Locate
        int Rowid_AccountStart = 0;
        int Rowid_AccountField = 0;
        int Rowid_AccountEnd = 0;
        int ColId_AccountId = 0;
        int ColId_AccountName = 0;
        int ColId_Amount = 0;
        int ColId_UnitCost = 0;
        int ColId_TotalCost = 0;
        int ColId_MarketPrice = 0;
        int ColId_MarketValue = 0;

        int Rowid_PaidInCapital = 0;
        int Rowid_Equity = 0;
        int Rowid_Assets = 0;
        int Rowid_NetValue = 0;
        int Rowid_AccumulatedNetValue = 0;
        int Rowid_CashDividend = 0;

        var regexpatten = @"([A-Z][A-Z\d]{5})?.*?([\u4e00-\u9fa5\da-zA-Z]{6,}?投资基金)";
        //var regexpatten2 = "([\\u4e00-\\u9fa5]+)私募证券投资基金";
        int tocol = table.Columns.Count;
        for (int i = 0; i < table.Rows.Count; i++)
        {
            for (int j = 0; j < tocol; j++)
            {
                var cell = table.Rows[i][j];
                if (cell is DBNull || cell == null)
                    continue;

                var tmp = cell.ToString() ?? "";

                if (fundname is null)
                {
                    var mm = Regex.Match(tmp, regexpatten);
                    if (mm.Success)
                    {
                        fundname = mm.Groups[2].Value;
                        code = mm.Groups[1].Value;

                        break;
                    }
                }

                if (tmp.Trim() == "科目代码")
                {
                    Rowid_AccountField = i;
                    ColId_AccountId = j;
                    goto _next;
                }
            }
        }
    _next:

        if (Rowid_AccountField == 0)
            return (null, null, null);

        //抓取日期
        for (int j = 0; j < table.Columns.Count; j++)
        {
            var datetmp = table.Rows[Rowid_AccountField - 1][j];
            if (datetmp is not DBNull && datetmp is not null && datetmp.ToString() is string sdate)
            {
                var mm = Regex.Match(sdate, @"(\d{4})[-/]*(\d{2})[-/]*(\d{2})");
                if (mm.Success)
                {
                    daily.Date = DateOnly.ParseExact($"{mm.Groups[1]}{mm.Groups[2]:00}{mm.Groups[3]:00}", "yyyyMMdd", null);
                    break;
                }
            }
        }

        ColId_AccountName = ColId_AccountId + 1;
        for (int j = ColId_AccountId + 1; j < table.Columns.Count; j++)
        {
            var cell = table.Rows[Rowid_AccountField][j];
            var tmp = cell.ToString() ?? "";
            //if (tmp.Trim() == "科目名称")
            // ColId_AccountName = j;
            if (tmp.Trim() == "数量")
                ColId_Amount = j;
            if (tmp.Trim() == "单位成本")
                ColId_UnitCost = j;
            if (Regex.IsMatch(tmp.Trim(), "^成本(?:-本币)*$"))
            {
                var cellne = table.Rows[Rowid_AccountField][j + 1];
                if (cellne is DBNull || cellne == null || string.IsNullOrWhiteSpace(cellne.ToString()))
                    ColId_TotalCost = j + 1;
                else
                    ColId_TotalCost = j;
            }
            if (tmp.Trim() == "市价" || tmp.Trim() == "行情")
                ColId_MarketPrice = j;
            if (Regex.IsMatch(tmp.Trim(), "^市值(?:-本币)*$"))
            {
                var cellne = table.Rows[Rowid_AccountField][j + 1];
                if (cellne is DBNull || cellne == null || string.IsNullOrWhiteSpace(cellne.ToString()))
                    ColId_MarketValue = j + 1;
                else
                    ColId_MarketValue = j;
            }
        }
        for (int i = Rowid_AccountField + 1; i < table.Rows.Count; i++)
        {
            var cell = table.Rows[i][ColId_AccountId];
            if (cell is DBNull || cell == null)
            {
                if (Rowid_AccountStart > 0 && Rowid_AccountEnd == 0)
                    Rowid_AccountEnd = i;
                continue;
            }


            var tmp = cell.ToString() ?? "";
            if (!Regex.IsMatch(tmp, "^\\d") && Rowid_AccountStart > 0 && Rowid_AccountEnd == 0)
                Rowid_AccountEnd = i;

            if (Rowid_AccountStart == 0 && Regex.IsMatch(tmp, "^\\d"))
            {
                Rowid_AccountStart = i;
            }

            if (tmp.Length > 20) continue;

            if (tmp.Trim() == "实收资本" && Rowid_PaidInCapital == 0)
            {
                Rowid_PaidInCapital = i;
                continue;
            }
            if (tmp.Contains("资产净值") && Rowid_Equity == 0)
            {
                Rowid_Equity = i;
                continue;
            }
            if (Regex.IsMatch(tmp, @"资产\w*合计") && Rowid_Assets == 0)
            {
                Rowid_Assets = i;
                continue;
            }
            if (tmp == "单位净值" || Regex.IsMatch(tmp, "^(?:今日|基金)*单位净值"))
            {
                Rowid_NetValue = i;
                continue;
            }

            if (tmp.StartsWith("累计单位净值"))
            {
                Rowid_AccumulatedNetValue = i;
                continue;
            }
            if (tmp.StartsWith("累计派现金额"))
            {
                Rowid_CashDividend = i;
                continue;
            }
        }

        var badvalueaddr = false;
        if (Rowid_NetValue == 0 && Rowid_AccumulatedNetValue == 0)
        {
            badvalueaddr = true;
            for (int i = Rowid_AccountField + 1; i < table.Rows.Count; i++)
            {
                var cell = table.Rows[i][0];
                if (cell is DBNull || cell == null)
                {
                    continue;
                }

                var tmp = cell.ToString() ?? "";

                if (tmp == "单位净值" || Regex.IsMatch(tmp, "^(?:今日|基金)*单位净值"))
                {
                    Rowid_NetValue = i;
                    continue;
                }

                if (tmp.StartsWith("累计单位净值"))
                {
                    Rowid_AccumulatedNetValue = i;
                    continue;
                }
                if (tmp.StartsWith("累计派现金额"))
                {
                    Rowid_CashDividend = i;
                    continue;
                }
            }
        }



        //Console.WriteLine($"ProductName:    {daily.ProductName}");
        daily.Share = GetDouble(table.Rows[Rowid_PaidInCapital][ColId_TotalCost]);
        daily.NetAsset = GetDouble(table.Rows[Rowid_Equity][ColId_MarketValue]);
        daily.Asset = GetDouble(table.Rows[Rowid_Assets][ColId_MarketValue]);

        var valtmp = table.Rows[Rowid_NetValue][badvalueaddr ? 1 : ColId_AccountName];
        daily.NetValue = GetDouble(valtmp);

        valtmp = table.Rows[Rowid_AccumulatedNetValue][badvalueaddr ? 1 : ColId_AccountName];
        daily.CumNetValue = GetDouble(valtmp);

        //valtmp = table.Rows[Rowid_CashDividend][badvalueaddr ? 1 : ColId_AccountName];
        //daily.CashDividend = GetDouble(valtmp);

        if (Rowid_AccountStart == 0)
            throw new Exception("Rowid_AccountStart == 0");


        daily.Source = DailySource.Sheet;
        //ParseInvestments(daily);
        return (fundname, code, daily);
    }



    public class AccountItem
    {

        public string? Id { get; set; }


        public string? Name { get; set; }


        public int Amount { get; set; }


        public double UnitCost { get; set; }


        public double TotalCost { get; set; }


        public double MarketPrice { get; set; }


        public double MarketValue { get; set; }

        [JsonIgnore]
        public double AppraisementChange => MarketValue - TotalCost;

    }

    private static Investment ParseStock(AccountItem ai, string id)
    {
        Investment investment = new Investment();
        investment.InvestmentType = InvestmentType.Stock;
        investment.ProductId = id;
        investment.Name = ai.Name;
        investment.Amount = ai.Amount;
        investment.UnitCost = ai.UnitCost;
        investment.TotalCost = ai.TotalCost;
        investment.MarketPrice = ai.MarketPrice;
        investment.MarketValue = ai.MarketValue;

        return investment;
    }
    private static Investment ParseFuture(AccountItem ai, string dk, string pid, string serail)
    {
        Investment investment = new Investment();
        investment.InvestmentType = InvestmentType.Future;
        investment.ProductId = pid;
        if (int.TryParse(serail, out int dd))
            investment.Serial = dd;
        if (int.TryParse(dk, out int ddk))
            if (ddk % 2 == 0)
                investment.LongOrShort = false;
        investment.Name = ai.Name;
        investment.Amount = ai.Amount;
        investment.UnitCost = ai.UnitCost;
        investment.TotalCost = ai.TotalCost;
        investment.MarketPrice = ai.MarketPrice;
        investment.MarketValue = ai.MarketValue;

        return investment;
    }

    private static decimal GetDouble(object valtmp)
    {
        if (valtmp is string && decimal.TryParse(valtmp as string, out decimal dd))
            return dd;
        else if (valtmp is double)
            return Convert.ToDecimal(valtmp);
        else if (valtmp is decimal)
            return (decimal)valtmp;
        else if (valtmp is int)
            return (decimal)valtmp;
        return 0;
    }
}



public enum InvestmentType { Unknown, Cash, Stock, Future }

public class Investment
{

    public string? ProductId { get; set; }


    public int Serial { get; set; }

    [JsonIgnore]
    public string? Contract => Serial == 0 ? ProductId : ProductId + Serial;


    public int Amount { get; set; }


    public double UnitCost { get; set; }


    public double TotalCost { get; set; }


    public double MarketPrice { get; set; }


    public double MarketValue { get; set; }

    [JsonIgnore]
    public double AppraisementChange => MarketValue - TotalCost;


    public bool LongOrShort { get; set; } = true;


    public string? Name { get; set; }


    public InvestmentType InvestmentType { get; set; }
}