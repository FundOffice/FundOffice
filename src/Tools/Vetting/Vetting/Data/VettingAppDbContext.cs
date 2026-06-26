using LiteDB;
using Vetting.Copilot.Data;
using Vetting.Entity;

namespace Vetting.Data;

/// <summary>
/// Vetting 应用专用 DbContext，扩展 Copilot 的 VettingDbContext，添加 VettingReport 集合
/// </summary>
public class VettingAppDbContext : VettingDbContext
{
    public ILiteCollection<VettingReport> Reports => _db.GetCollection<VettingReport>();

    public VettingAppDbContext() : base() { }
    public VettingAppDbContext(string dbPath) : base(dbPath) { }
    public VettingAppDbContext(string dbPath, bool noPassword) : base(dbPath, noPassword) { }
}
