using System.Windows;
using Vetting.ViewModel;

namespace Vetting.View;
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }
}
