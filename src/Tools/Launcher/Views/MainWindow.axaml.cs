using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Launcher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 关闭按钮
            var closeBtn = this.FindControl<Button>("CloseBtn");
            if (closeBtn != null)
            {
                closeBtn.Click += (_, _) => Close();
            }
        }

        // 整体拖拽：在非按钮区域按下左键时拖动窗口
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
                && e.Source is not Button)
            {
                BeginMoveDrag(e);
            }
        }
    }
}
