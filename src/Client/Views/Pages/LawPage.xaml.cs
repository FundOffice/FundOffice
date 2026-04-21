using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;
using System.IO;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// LawPage.xaml 的交互逻辑
/// </summary>
public partial class LawPage : UserControl
{
    public LawPage()
    {
        InitializeComponent();
    }
}



public partial class LawPageViewModel : ObservableObject
{

    [ObservableProperty]
    public partial LawCategory[]? Catalog { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LawList))]
    public partial LawCategory? SelectedSection { get; set; }

    public string[]? LawList => SelectedSection?.Titles;


    [ObservableProperty]
    public partial string? SelectedLaw { get; set; }


    [ObservableProperty]
    public partial string? LawContent { get; set; }

    public LawPageViewModel()
    {
        // 检查数据库
        if (!File.Exists(@"data\laws.db"))
        {
            var rs = App.GetResourceStream(new Uri(@"res\laws.db", UriKind.Relative));
            using var fs = new FileStream(@"data\laws.db", FileMode.Create);
            rs.Stream.CopyTo(fs);
            fs.Flush();
            fs.Close();
        }

        // 获取目录
        using var db = new LiteDatabase(@"FileName=data\laws.db");
        Catalog = db.GetCollection<LawInfo>().Query().Select(x => new { Title = x.Title, Chapter = x.Chapter, Section = x.Section }).ToArray().GroupBy(x => x.Chapter + x.Section).Select(x => new LawCategory { Chapter = x.First().Chapter, Section = x.First().Section, Titles = x.Select(y => y.Title).ToArray() }).ToArray();
    }


    partial void OnSelectedLawChanged(string? value)
    {
        using var db = new LiteDatabase(@"FileName=data\laws.db");
        LawContent = db.GetCollection<LawInfo>().FindOne(x => x.Title == SelectedLaw)?.Body;
    }



}


public class LawCategory
{
    public required string[] Titles { get; set; }

    public required string Chapter { get; set; }

    public required string Section { get; set; }
}

public class LawInfo
{
    public required string Title { get; set; }

    public required string Chapter { get; set; }

    public required string Section { get; set; }

    public required string Body { get; set; }

    public DateOnly Date { get; set; }
    public DateOnly Expire { get; set; }
}
