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
    public ILiteCollection<FundBinding> FundBindings => _db.GetCollection<FundBinding>();

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
    public ILiteCollection<ProductLine> ProductLines => _db.GetCollection<ProductLine>();
    public ILiteCollection<Shareholder> Shareholders => _db.GetCollection<Shareholder>();
    public ILiteCollection<Staff> Staffs => _db.GetCollection<Staff>();
    public ILiteCollection<Strategy> Strategies => _db.GetCollection<Strategy>();

    // 图片
    public ILiteCollection<PhotoMap> PhotoMaps => _db.GetCollection<PhotoMap>();
    public ILiteStorage<string> PhotoStorage => _db.FileStorage;

    private static string _machine = GetDiskSerial();

    public VettingDbContext() : this("data/vetting.db") { }

    public VettingDbContext(string dbPath)
    {
        var password = ComputeDiskSerialMd5();
        _db = new LiteDatabase($"Filename={dbPath};Password={password};Connection=Shared");
    }

    public VettingDbContext(string dbPath, bool noPassword)
    {
        _db = noPassword
            ? new LiteDatabase($"Filename={dbPath};Connection=Shared")
            : new LiteDatabase($"Filename={dbPath};Password={ComputeDiskSerialMd5()};Connection=Shared");
    }

    private static string ComputeDiskSerialMd5()
    {
        var serial = _machine;
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(serial))).ToLowerInvariant();
    }

    private static string GetDiskSerial()
    {
        try
        {
            var psi = new ProcessStartInfo("wmic", "diskdrive get serialnumber")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.SystemDirectory
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

    // ── 图片操作 ─────────────────────────────────────

    /// <summary>上传图片并创建 PhotoMap 记录</summary>
    public PhotoMap UploadPhoto(string filePath, string? description = null)
    {
        var fileInfo = new FileInfo(filePath);
        var fileId = Guid.NewGuid().ToString();
        var contentType = GetContentType(filePath);

        // 读取图片尺寸
        var (width, height) = GetImageDimensions(filePath);

        // 上传到 FileStorage
        using var stream = File.OpenRead(filePath);
        _db.FileStorage.Upload(fileId, fileInfo.Name, stream);

        // 创建 PhotoMap 记录
        var photo = new PhotoMap
        {
            FileName = fileInfo.Name,
            FileId = fileId,
            ContentType = contentType,
            Size = fileInfo.Length,
            CreatedAt = DateTime.Now,
            Description = description,
            Width = width,
            Height = height
        };

        PhotoMaps.Insert(photo);
        return photo;
    }

    /// <summary>从 FileStorage 获取图片流</summary>
    public Stream? GetPhotoStream(string fileId)
    {
        var file = _db.FileStorage.FindById(fileId);
        return file?.OpenRead();
    }

    /// <summary>删除图片及其 PhotoMap 记录</summary>
    public void DeletePhoto(int photoId)
    {
        var photo = PhotoMaps.FindById(photoId);
        if (photo?.FileId != null)
        {
            _db.FileStorage.Delete(photo.FileId);
        }
        PhotoMaps.Delete(photoId);
    }

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static (int width, int height) GetImageDimensions(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            return (image.Width, image.Height);
        }
        catch
        {
            return (0, 0);
        }
    }
}