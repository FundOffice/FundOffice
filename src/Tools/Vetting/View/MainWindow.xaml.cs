using System.Windows;
using Vetting.ViewModel;

namespace Vetting.View;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
