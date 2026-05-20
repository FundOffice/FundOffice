using FMO.Models;
using LiteDB;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using Utilities;
namespace FMO.Utilities;



public class BaseDatabase : LiteDatabase
{


    public BaseDatabase(string con) : base(con, null) { }

    public Fund? FindFund(string? fundCode)
    {
        var c = GetCollection<Fund>();

        if (fundCode?.Length > 0)
        {
            // code匹配
            var f = c.FindOne(x => x.Code != null && fundCode.Contains(x.Code!));
            if (f is not null) return f;

            // SNN111 NN111A/B SNN111A/B 这类
            f = c.FindAll().Where(x => x.Code is not null && fundCode.Contains(x.Code![1..])).FirstOrDefault();
            if (f is not null) return f;
        }
        return null;
    }

    public (Fund? Fund, string? Class) FindFundByCode(string? fundCode)
    {
        var c = GetCollection<Fund>();

        if (fundCode?.Length > 0)
        {
            // code匹配
            var f = c.FindOne(x => x.Code != null && fundCode == x.Code!);
            if (f is not null) return (f, null);

            // SNN111 NN111A/B SNN111A/B 这类
            f = c.FindAll().Where(x => x.Code is not null && fundCode.StartsWith(x.Code![1..])).FirstOrDefault();
            if (f is not null) return (f, fundCode[5..]);
        }
        return default;
    }



    public (Fund? Fund, string? Class) FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return default;

        var fund = GetCollection<Fund>().FindOne(x => x.Name == name);
        if (fund is not null) return (fund, null);

        // 尝试通过名称包含来查找 xxA xxB等子份额
        var poss = GetCollection<Fund>().Find(x => name.StartsWith(x.Name)).ToArray();
        if (poss.Length == 1)
            return (poss[0], name[poss[0].Name.Length..]);

        // 曾用名
        var ava = GetCollection<FundElements>().FindAll().Select(x => x.FullName.Changes.Select(y => new { id = x.Id, fn = y.Value })).ToList().SelectMany(x => x);

        var old = ava.FirstOrDefault(x => name.StartsWith(x.fn));
        if (old is not null)
            return (GetCollection<Fund>().FindById(old.id), name == old.fn ? null : name[old.fn.Length..]);
        return default;
    }


    public ILiteCollection<DailyValue> GetDailyCollection(int fid, string? shareClas = null)
    {
        return string.IsNullOrWhiteSpace(shareClas) ? GetCollection<DailyValue>($"fv_{fid}") : GetCollection<DailyValue>($"fv_{fid}_{shareClas}");
    }

    public FundElements QueryElements(int fid) => FundElements.From(GetCollection<IFundFactor>().Find(x => x.FundId == fid).ToArray());

    public FundElements QueryElements(int fid, params string[] fields) => FundElements.From(GetCollection<IFundFactor>().Query().Where(x => x.FundId == fid).Where(Query.In(nameof(IFundFactor.FactorId), fields.Select(x => new BsonValue(x)))).ToArray());

    public T[] QueryFundFact<T>(int fid, string field) => GetCollection<IFundFactor>().Find(x => x.FundId == fid && x.FactorId == field).OrderByDescending(x=>x.FlowId).OfType<FundFactor<T>>().Select(x => x.Data).ToArray();
}

public static class DbHelper
{
    private static string _password = "";


    private const string _dbfolder = "data";

    private readonly static string _exeFolder;

    static DbHelper()
    {
        _exeFolder = AppDomain.CurrentDomain.BaseDirectory;
    }

    public static void Init()
    {
        Directory.CreateDirectory(_dbfolder);


#pragma warning disable CA1416 // 验证平台兼容性
#if RELEASE
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus"))       
#else
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
#endif        
        {
            if (key is not null && key.GetValue("Code") is string s)
                _password = AesHelper.Decrypt(s);
            else _password = "";// DateTime.Now.Ticks.ToString();//"fjd32890f5djflds";
        }
#pragma warning restore CA1416 // 验证平台兼容性

        _password += "jgkfld9024039284jrwe";

        using (MD5 sha256 = MD5.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(_password);
            byte[] hashBytes = sha256.ComputeHash(bytes);
            _password = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }


    public static BaseDatabase Base() => new BaseDatabase(@$"FileName={_dbfolder}\base.db;Password={_password};Connection=Shared");

    public static BaseDatabase ShareClass() => new BaseDatabase(@$"FileName={_dbfolder}\sc.db;Password={_password};Connection=Shared");



    public static LiteDatabase Platform() => new LiteDatabase(@$"FileName={_dbfolder}\platform.db;Password={_password};Connection=Shared");


    public static LiteDatabase Mission() => new LiteDatabase(@$"FileName={_dbfolder}\mission.db;Password=891uiu89f41uf9dij432u89;Connection=Shared");


    public static LiteDatabase Setting() => new LiteDatabase(@$"FileName={_exeFolder}\setting.db;Password={_password};Connection=Shared");


    public static LiteDatabase Tracker() => new LiteDatabase(@$"FileName={_dbfolder}\tracker.db;Password={_password};Connection=Shared");


    public static LiteDatabase Template() => new LiteDatabase(@$"FileName={_dbfolder}\template.db;Password={_password};Connection=Shared");


    public static string[] ListAllFileId()
    {
        List<string> ids = [];
        using (var db = Base())
        {
            foreach (var tn in db.GetCollectionNames())
            {
                foreach (var doc in db.GetCollection(tn).FindAll().ToList())
                {
                    if (doc.IsDocument)
                        FindFileInDocument(doc, ids);
                }
            }
        }
        return ids.Distinct().ToArray();
    }

    private static void FindFileInDocument(BsonDocument doc, List<string> ids)
    {
        if (doc.ContainsKey("_id") && doc["_id"].IsString && doc.ContainsKey("Hash"))
        {
            ids.Add(doc["_id"].AsString);
            return;
        }

        foreach (var (k, v) in doc)
        {
            if (v.IsDocument)
                FindFileInDocument(v.AsDocument, ids);
            else if (v.IsArray)
                foreach (var item in v.AsArray)
                    if (item.IsDocument)
                        FindFileInDocument(item.AsDocument, ids);
        }
    }

    //public static bool RebuildFundShareRecord(this ILiteDatabase db, int fundid)
    //{
    //    try
    //    {
    //        if (fundid == 0) return false;


    //        var data = db.GetCollection<TransferRecord>().Find(x => x.FundId == fundid).GroupBy(x => x.ConfirmedDate).OrderBy(x => x.Key);
    //        var list = new List<FundShareRecord>();
    //        foreach (var item in data)
    //            list.Add(new FundShareRecord(fundid, item.Key, item.Sum(x => x.ShareChange()) + (list.Count > 0 ? list[^1].Share : 0)));

    //        db.GetCollection<FundShareRecord>().DeleteMany(x => x.FundId == fundid);
    //        db.GetCollection<FundShareRecord>().Insert(list);

    //        return true;
    //    }
    //    catch (Exception e)
    //    {
    //        LogEx.Error($"BuildFundShareRecord {e.Message}");
    //        return false;
    //    }
    //}

    //public static void RebuildFundShareRecord(this ILiteDatabase db, params int[] fundids)
    //{
    //    foreach (var fundid in fundids)
    //        RebuildFundShareRecord(db, fundid);
    //}




}


public static class Settings
{

}