using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using System.Windows;

namespace FMO;

/// <summary>
/// AddInvestorWindow.xaml 的交互逻辑
/// </summary>
public partial class AddInvestorWindow : Window
{
    public AddInvestorWindow()
    {
        InitializeComponent();
    }
}


public partial class AddInvestorWindowViewModel : ObservableObject
{
    public AddInvestorWindowViewModel()
    {
        using var db = DbHelper.Base();
        exists = db.GetCollection<Investor>().FindAll().ToArray();
    }


    private Investor[] exists;


    [ObservableProperty]
    public partial string? Name { get; set; }


    public IDType[] IdTypes { get; } = Enum.GetValues<IDType>().Where(x => x != IDType.Unknown && x != IDType.Institusion).ToArray();


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial AmacInvestorType Type { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial IDType? IdType { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? IdNo { get; set; }


    [ObservableProperty]
    public partial string? Tip { get; set; }

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name) && IdType is not null && !string.IsNullOrWhiteSpace(IdNo);


    public Investor? Investor { get; set; }







    partial void OnNameChanged(string? value) => UpdateTip();

    partial void OnIdNoChanged(string? value) => UpdateTip();



    private void UpdateTip()
    {
        Tip = exists.FirstOrDefault(x => x.Name == Name || x.Identity?.Id == IdNo) is Investor c ?
             $"已存在投资人：{c.Name}，证件号：{c.Identity?.Id}" : null;

        if (IdType is not null  && IdNo is not null && !IdValidator.Validate(IdNo, IdType!.Value))
            Tip += "证件号可能不正确";
    }



    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window window)
    {
        // CanConfirm 后，一定不符合
        if (Name is null || IdType is null || IdNo is null) return;

        Investor = new Investor
        {
            Name = Name,
            Identity = new Identity
            {
                Type = IdType.Value,
                Id = IdNo
            }
        };

        window.DialogResult = true;
        window.Close();
    }
}