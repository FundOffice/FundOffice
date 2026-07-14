using FMO.Models;
using System.Windows;

namespace FMO;

/// <summary>
/// ContractParseJsonViewWindow.xaml 的交互逻辑
/// </summary>
public partial class ContractParseJsonViewWindow : Window
{
    public ContractParseJsonViewWindow(ContractParseHistory history)
    {
        InitializeComponent();
        DataContext = new ContractParseJsonViewViewModel(history);
    }
}


