using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Data;

namespace FMO.Disclosure;

/// <summary>
/// ChooseFundWindow.xaml 的交互逻辑
/// </summary>
public partial class ChooseFundWindow : Window
{
    public ChooseFundWindow()
    {
        InitializeComponent();
    }


}


public partial class ChooseFundWindowViewModel : ObservableObject
{
    public ChooseFundWindowViewModel(DisclosureWorkflowViewModel.FundSelectInfo[] funds)
    {
        Funds = funds;
        FundSource = new CollectionViewSource { Source = Funds };
        FundSource.Filter += FundSource_Filter;
    }

    private void FundSource_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is DisclosureWorkflowViewModel.FundSelectInfo info)
        {
            if (string.IsNullOrWhiteSpace(SearchKey))
            {
                e.Accepted = true;
            }
            else
            {
                e.Accepted = info.Name.Contains(SearchKey, StringComparison.OrdinalIgnoreCase) || info.Code.Contains(SearchKey, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [ObservableProperty]
    public partial string? SearchKey { get; set; }


    public DisclosureWorkflowViewModel.FundSelectInfo[] Funds { get; }

    public CollectionViewSource FundSource { get; }

    partial void OnSearchKeyChanged(string? value)
    {
        FundSource.View.Refresh();
    }

    [RelayCommand]
    public void Confirm(Window w)
    {
        w.DialogResult = true;
        w.Close();
    }
}