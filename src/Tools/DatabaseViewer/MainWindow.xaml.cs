using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;


namespace DatabaseViewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}





public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        //Directory.SetCurrentDirectory(@"e:\fmo");
        Databases = [new("主数据库", () => DbHelper.Base()), new("平台", () => DbHelper.Platform()), new("平台Log", () => new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared")), new("Log", ()=> new LiteDatabase($@"FileName=logs.db;Connection=Shared"))];

        AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("Trustee"));
    }

    public DatabaseInfo[] Databases { get; set; }


    [ObservableProperty]
    public partial DatabaseInfo? SelectedDatabase { get; set; }


    [ObservableProperty]
    public partial IEnumerable<string>? Tables { get; set; }


    [ObservableProperty]
    public partial string? SelectedTable { get; set; }


    [ObservableProperty]
    public partial object? Data { get; set; }


    partial void OnSelectedDatabaseChanged(DatabaseInfo? value)
    {
        if (value is null)
        {
            Tables = null;
            Data = null;
            return;
        }

        using var db = value.GetDatabase();
        Tables = db.GetCollectionNames().Where(x => !x.StartsWith("_")).Order();
    }


    partial void OnSelectedTableChanged(string? value)
    {
        if (value is null || SelectedDatabase is null)
        {
            Data = null;
            return;
        }

        using var db = SelectedDatabase.GetDatabase();
        var doc = db.GetCollection(value).Query().OrderByDescending("_id").Limit(1000).ToList();

        if (doc is null)
        {
            Data = null;
            return;
        }


        if (value?.StartsWith("fv_") ?? false)
        {
            Data = doc!.Select(x => BsonMapper.Global.ToObject<DailyValue>(x));
            return;
        }

        if(value == "log")
        {
            Data = doc!.Select(x => BsonMapper.Global.ToObject<LogInfo>(x)).OrderByDescending(x => x.Time);
            return;
        }


        var types = AssemblyLoadContext.Default.Assemblies.SelectMany(x => x.GetTypes());

        if (types.FirstOrDefault(x => x.Name == value) is Type type)
            Data = doc.Select(x => BsonMapper.Global.ToObject(type, x));
        else Data = ToTable( doc).DefaultView;

    }

    private DataTable ToTable(List<BsonDocument> docs)
    {
        var table = new DataTable();
        if (docs == null || docs.Count == 0) return table;

        // 1. 收集所有不重复的键
        var allKeys = docs.SelectMany(d => d.Keys).Distinct().ToList();
        var columnTypes = new Dictionary<string, Type>();

        // 2. 安全类型推断
        foreach (var key in allKeys)
        {
            Type? colType = null;
            foreach (var doc in docs)
            {
                if (doc.TryGetValue(key, out var val) && !val.IsNull)
                {
                    Type currentType;

                    // 🔑 关键修复：数组、文档或 RawValue 为空时，列类型直接定为 string
                    if (val.IsArray || val.IsDocument || val.RawValue == null)
                    {
                        currentType = typeof(string);
                    }
                    else
                    {
                        currentType = val.RawValue.GetType();
                        var underlying = Nullable.GetUnderlyingType(currentType) ?? currentType;

                        // 过滤 LiteDB 专有类型（如 ObjectId）或集合类型，统一转 string
                        if (underlying.Namespace?.StartsWith("LiteDB") == true ||
                            underlying.Namespace?.StartsWith("System.Collections") == true)
                        {
                            currentType = typeof(string);
                        }
                        else
                        {
                            currentType = underlying;
                        }
                    }

                    if (colType == null)
                        colType = currentType;
                    else if (colType != currentType)
                    {
                        colType = typeof(string); // 类型冲突直接降级为 string
                        break;
                    }
                }
            }
            columnTypes[key] = colType ?? typeof(string);
        }

        // 3. 创建 DataTable 列
        foreach (var kvp in columnTypes)
            table.Columns.Add(kvp.Key, kvp.Value);

        // 4. 安全填充数据行
        foreach (var doc in docs)
        {
            var row = table.NewRow();
            foreach (DataColumn col in table.Columns)
            {
                var key = col.ColumnName;
                if (doc.TryGetValue(key, out var bsonVal) && !bsonVal.IsNull)
                {
                    try
                    {
                        if (bsonVal.IsArray || bsonVal.IsDocument)
                        {
                            // 此时该列类型必定是 string，安全赋值
                            row[col] = bsonVal.ToString();
                        }
                        else
                        {
                            var raw = bsonVal.RawValue;
                            if (raw != null)
                            {
                                if (col.DataType == typeof(string))
                                    row[col] = raw.ToString();
                                else if (col.DataType.IsInstanceOfType(raw))
                                    row[col] = raw; // 类型完全匹配，直接赋值
                                else
                                    row[col] = Convert.ChangeType(raw, col.DataType);
                            }
                        }
                    }
                    catch
                    {
                        // 兜底处理：转换失败时，若列为 string 则降级，否则填 DBNull 防崩溃
                        row[col] = col.DataType == typeof(string) ? bsonVal.ToString() : DBNull.Value;
                    }
                }
                else
                {
                    row[col] = DBNull.Value; // 缺失字段填 DBNull
                }
            }
            table.Rows.Add(row);
        }

        return table;
    }

}


public class DatabaseInfo
{
    [SetsRequiredMembers]
    public DatabaseInfo(string name, Func<ILiteDatabase> getDatabase)
    {
        Name = name;
        GetDatabase = getDatabase;
    }

    public required string Name { get; set; }


    public required Func<ILiteDatabase> GetDatabase { get; set; }

}