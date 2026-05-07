using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.Windows;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// AddTemporaryOpenWindow.xaml 的交互逻辑
/// </summary>
public partial class AddTemporaryOpenWindow : Window
{
    public AddTemporaryOpenWindow()
    {
        InitializeComponent();
    }
}


public partial class AddTemporaryWindowViewModel : ObservableObject
{
    public CollectionViewSource FundSource { get; } = new();


    public Fund[] Funds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    public partial string? SearchText { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Fund? SelectedFund { get; set; }

    [ObservableProperty]
    public partial bool ShowList { get; set; }



    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime PublishTime { get; set; } = DateTime.Now;

    public virtual bool CanConfirm => false;// CanConfirmOverrride();

    public virtual bool CanConfirmOverrride() => false;

    public AddTemporaryWindowViewModel(Fund[] names)
    {
        Funds = names;
        FundSource.Source = Funds;
        FundSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchText) ? true : e.Item switch { Fund ss => ss.Name.Contains(SearchText) || (ss.Code?.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ?? false), _ => true };
    }

    partial void OnSearchTextChanged(string? value)
    {
        FundSource.View?.Refresh();
        if (!Funds.Any(x => x.Name == value))
            ShowList = true;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window window)
    {
        window.DialogResult = true;
        window.Close();
    }
}



public partial class AddTemporaryOpenWindowViewModel : AddTemporaryWindowViewModel
{



    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool AllowBuy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool AllowSell { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime OpenDate { get; set; }


    public override bool CanConfirm => SelectedFund is not null && OpenDate.Year > 2000 && PublishTime.Year > 2000 && (AllowBuy || AllowSell);

    public AddTemporaryOpenWindowViewModel(Fund[] names) : base(names)
    {
    }


}