using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FMO
{
    /// <summary>
    /// SupplementaryOrderWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SupplementaryOrderWindow : Window
    {
        public SupplementaryOrderWindow()
        {
            InitializeComponent();
        }
 

        private void MultiFile_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is SupplementaryOrderWindowViewModel v)
                v.OnBatchFile(e.Data.GetData(DataFormats.FileDrop) as string[]);
        }
    }
}
