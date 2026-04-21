using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.Windows;

namespace FMO;

/// <summary>
/// IssueNewFundWindow.xaml 的交互逻辑
/// </summary>
public partial class IssueNewFundWindow : Window
{
    public IssueNewFundWindow()
    {
        InitializeComponent();
    }
}


public partial class IssueNewFundWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Name { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? ShortName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Code { get; set; }


    public bool CanConfirm => Name?.Length > 5 && ShortName?.Length > 3 && Code?.Length == 6;


    partial void OnNameChanged(string? value)
    {
        if (ShortName is null && value is not null)
            ShortName = Fund.GetDefaultShortName(value);
    }

    [RelayCommand(CanExecute =nameof(CanConfirm))]
    public void Confirm(Window window)
    {
        window.DialogResult = true;
        window.Close();
    }
}