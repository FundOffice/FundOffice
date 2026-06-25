using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LiteDB;
using Vetting.Entity;
using Vetting.Models.Entities;

namespace Vetting.Data;

 
public class VettingDbContext : IDisposable
{
    private readonly LiteDatabase _db;
    public ILiteCollection<VettingReport> Reports => _db.GetCollection<VettingReport>();
    public ILiteCollection<AIProviderConfig> AIProviderConfigs => _db.GetCollection<AIProviderConfig>();
    public ILiteCollection<FileSpecialQuestion> FileSpecialQuestions => _db.GetCollection<FileSpecialQuestion>();

    public VettingDbContext()
    {
        var password = ComputeDiskSerialMd5();
        _db = new LiteDatabase($"Filename=data/vetting.db;Password={password};Connection=Shared");
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
}
