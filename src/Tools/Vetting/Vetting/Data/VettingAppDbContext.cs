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
    public ILiteCollection<AppSetting> AppSettings => _db.GetCollection<AppSetting>();

    public VettingAppDbContext() : base() { }
    public VettingAppDbContext(string dbPath) : base(dbPath) { }
    public VettingAppDbContext(string dbPath, bool noPassword) : base(dbPath, noPassword) { }

    /// <summary>获取全局设置（自动确保存在）</summary>
    public AppSetting GetSettings() =>
        AppSettings.FindById(1) ?? new AppSetting();
}
