using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace FMO.Shared;

/// <summary>
/// RenameWindow.xaml 的交互逻辑
/// </summary>
public partial class RenameWindow : Window
{
    public RenameWindow()
    {
        InitializeComponent();
    }
}

public partial class RenameWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? NewName { get; set; }

    public bool CanConfirm => !string.IsNullOrWhiteSpace(NewName);


    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window window)
    {
        window.DialogResult = true;
        window.Close();
    }
}
