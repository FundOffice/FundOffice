using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Loader;
using System.Windows;


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


        var types = AssemblyLoadContext.Default.Assemblies.Where(x => x.FullName!.Contains("FMO")).SelectMany(x => x.GetTypes());

        if (types.FirstOrDefault(x => x.Name == value) is Type type)
            Data = doc.Select(x => BsonMapper.Global.ToObject(type, x));
        else Data = doc;

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