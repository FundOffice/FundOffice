using System.Windows;
using System.Windows.Controls;

namespace FMO.Schedule
{
    /// <summary>
    /// MissionShellView.xaml 的交互逻辑
    /// </summary>
    public partial class MissionShellView : UserControl
    {
        public MissionShellView()
        {
            InitializeComponent();
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Content = DataContext is AutomationViewModelBase vm ? MissionTemplateManager.MakeView(vm.MissionType) ?? new TextBlock { Text = $"无法显示此任务 {vm.MissionType.Name}", VerticalAlignment = VerticalAlignment.Center } : "无法显示此任务";
        }
    }
}
