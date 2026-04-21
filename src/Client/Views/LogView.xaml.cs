using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;
using System.Linq;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// LogView.xaml 的交互逻辑
/// </summary>
public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
    }
}


public partial class LogViewModel : ObservableObject
{

    [ObservableProperty]
    public partial IEnumerable<LogMessage>? CommonLogs { get; set; }

    public LogViewModel()
    {
        using var db = new LiteDatabase($@"FileName=logs.db;Connection=Shared");

        CommonLogs = db.GetCollection("logex").Query().OrderByDescending(x => x["_t"].AsDateTime)
                    .Limit(1000).ToList().Select(x => new LogMessage(x["_t"].AsDateTime, x[nameof(LogMessage.File)].AsString, x[nameof(LogMessage.Method)].AsString, x[nameof(LogMessage.Line)].AsInt32, x["_m"].AsString));

    }

    public record LogMessage(DateTime Time, string File, string Method, int Line, string Message);
}

