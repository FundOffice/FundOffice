using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO.LearnAssist;

public partial class ClassLearnInfo : ObservableObject
{
    public required string Name { get; set; }


    [ObservableProperty]
    public partial decimal Progress { get; set; }

    [ObservableProperty]
    public partial ChapterInfo[] Chapters { get; set; } = [];

    [ObservableProperty]
    public partial TestInfo[] Tests { get; set; } = [];


    public int TypeId { get; set; }

    public decimal Hour { get; set; }

    public string? Url { get; internal set; }
}


public partial class TestInfo : ObservableObject
{
    public required string Url { get; set; }

    [ObservableProperty]
    public partial int Score { get; set; }

}