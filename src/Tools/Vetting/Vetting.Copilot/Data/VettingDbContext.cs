using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LiteDB;
using Vetting.Copilot.Models;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot.Data;

public class VettingDbContext : IDisposable
{
    protected readonly LiteDatabase _db;
    public ILiteCollection<AIProviderConfig> AIProviderConfigs => _db.GetCollection<AIProviderConfig>();
    public ILiteCollection<FileSpecialQuestion> FileSpecialQuestions => _db.GetCollection<FileSpecialQuestion>();
    public ILiteCollection<SpecialAnswer> SpecialAnswers => _db.GetCollection<SpecialAnswer>();
    public ILiteCollection<QA> QA => _db.GetCollection<QA>();
    public ILiteCollection<TemplateRecommend> TemplateRecommends => _db.GetCollection<TemplateRecommend>();

    // 唯一项
    public ILiteCollection<Manager> Managers => _db.GetCollection<Manager>();
    public ILiteCollection<CreditStanding> CreditStandings => _db.GetCollection<CreditStanding>();
    public ILiteCollection<InvestmentInfo> InvestmentInfos => _db.GetCollection<InvestmentInfo>();
    public ILiteCollection<RiskControl> RiskControls => _db.GetCollection<RiskControl>();

    // 列表项
    public ILiteCollection<AUM> AUMs => _db.GetCollection<AUM>();
    public ILiteCollection<Award> Awards => _db.GetCollection<Award>();
    public ILiteCollection<Department> Departments => _db.GetCollection<Department>();
    public ILiteCollection<DrawdownRecord> DrawdownRecords => _db.GetCollection<DrawdownRecord>();
    public ILiteCollection<FinancialStatement> FinancialStatements => _db.GetCollection<FinancialStatement>();
    public ILiteCollection<FundInfo> FundInfos => _db.GetCollection<FundInfo>();
    public ILiteCollection<Shareholder> Shareholders => _db.GetCollection<Shareholder>();
    public ILiteCollection<Staff> Staffs => _db.GetCollection<Staff>();
    public ILiteCollection<Strategy> Strategies => _db.GetCollection<Strategy>();

    /// <summary>
    /// 默认构造：使用 data/vetting.db，密码基于磁盘序列号
    /// </summary>
    public VettingDbContext() : this("data/vetting.db") { }

    /// <summary>
    /// 指定数据库路径构造（用于测试）
    /// </summary>
    public VettingDbContext(string dbPath)
    {
        var password = ComputeDiskSerialMd5();
        _db = new LiteDatabase($"Filename={dbPath};Password={password};Connection=Shared");
    }

    /// <summary>
    /// 无密码构造（仅测试用）
    /// </summary>
    public VettingDbContext(string dbPath, bool noPassword)
    {
        _db = noPassword
            ? new LiteDatabase($"Filename={dbPath};Connection=Shared")
            : new LiteDatabase($"Filename={dbPath};Password={ComputeDiskSerialMd5()};Connection=Shared");
    }

    private static string ComputeDiskSerialMd5()
    {
        var serial = GetDiskSerial();
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(serial))).ToLowerInvariant();
    }

    private static string GetDiskSerial()
    {
        try
        {
            var psi = new ProcessStartInfo("wmic", "diskdrive get serialnumber")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return Environment.MachineName;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var serial = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).FirstOrDefault()?.Trim();
            return string.IsNullOrEmpty(serial) ? Environment.MachineName : serial;
        }
        catch { return Environment.MachineName; }
    }

    public void Dispose() => _db.Dispose();

    private static readonly MethodInfo GetCollectionGeneric = typeof(LiteDatabase)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .First(m => m.Name == "GetCollection" && m.IsGenericMethod && m.GetParameters().Length == 0);

    public void UpsertEntity<T>(T entity)
    {
        _db.GetCollection<T>().Upsert(entity);
    }

    public void DeleteEntity(Type type, int id)
    {
        var method = GetCollectionGeneric.MakeGenericMethod(type);
        dynamic col = method.Invoke(_db, null)!;
        col.Delete(id);
    }

    public void DropCollection(string name) => _db.DropCollection(name);
}
