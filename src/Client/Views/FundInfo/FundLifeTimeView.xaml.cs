using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// FundLifeTimeView.xaml 的交互逻辑
/// </summary>
public partial class FundLifeTimeView : UserControl
{
    public FundLifeTimeView()
    {
        InitializeComponent();
    }

}




public partial class CustomFileInfoViewModel : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }


    [ObservableProperty]
    public partial FileInfo? FileInfo { get; set; }


}
