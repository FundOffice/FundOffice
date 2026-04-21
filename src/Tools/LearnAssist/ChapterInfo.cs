using CommunityToolkit.Mvvm.ComponentModel;

namespace FMO.LearnAssist;

public partial class ChapterInfo : ObservableObject
{
    public required string Name { get; set; }

    [ObservableProperty]
    public partial bool Learned { get; set; }


    [ObservableProperty]
    public partial double VideoProgress { get; set; }


    [ObservableProperty]
    public partial string? VideoTime { get; set; }

    public string? Url { get; internal set; }


    public string? TestUrl { get; set; }

}