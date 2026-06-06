using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for TipWindow.xaml
/// </summary>
public partial class TipWindow : Window
{
    public TipWindow()
    {
        InitializeComponent();
    }
}

public partial class TipWindowViewModel : ObservableObject
{
    public int SuccessCount => Items.Count(x => x.ItemType == ImportItemType.Success);


    public int SkipCount => Items.Count(x => x.ItemType == ImportItemType.Skip);


    public int ErrorCount => Items.Count(x => x.ItemType == ImportItemType.Error);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuccessCount))]
    [NotifyPropertyChangedFor(nameof(SkipCount))]
    [NotifyPropertyChangedFor(nameof(ErrorCount))]
    public partial ImportItemInfo[] Items { get; set; }




}

public enum ImportItemType
{
    Success, //绿色
    Skip,    //橙黄
    Error,    //红色
    Info
}

public class ImportItemInfo
{
    public ImportItemInfo(int rowNo, ImportItemType type, string v)
    {
        RowNum = rowNo;
        ItemType = type;
        Msg = v;
    }

    public int RowNum { get; set; }

    public ImportItemType ItemType { get; set; }

    public string? Msg { get; set; }
}