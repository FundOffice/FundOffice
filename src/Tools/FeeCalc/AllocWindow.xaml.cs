using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using LiteDB;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for AllocWindow.xaml
/// </summary>
public partial class AllocWindow : Window
{
    public AllocWindow()
    {
        InitializeComponent();
    }
}


public partial class AllocWindowViewModel : ObservableObject
{
    [SetsRequiredMembers]
    public AllocWindowViewModel(LiteDatabase db)
    {
        FeeDB = db;

        Recipients = [.. FeeDB.GetCollection<string>("Recipient").FindAll().Distinct().ToArray()];

        if (!Recipients.Contains("管理人"))
            Recipients.Insert(0, "管理人");

        var list = FeeDB.GetCollection<TransferRecord>().Query().Select(x => x.InvestorId).ToList();

        Investors = FeeDB.GetCollection<Investor>().Query().Where(Query.In("_id", list.Select(x => new BsonValue(x)))).ToArray();

    }

    public required LiteDatabase FeeDB { get; set; }



    public ObservableCollection<string> Recipients { get; }

    [ObservableProperty]
    public partial string? SearchInvestor { get; set; }


    public Investor[] Investors { get; set; }



    [ObservableProperty]
    public partial string? NewOwner { get; set; }





    [RelayCommand]
    public void AddOwner()
    {
        if (string.IsNullOrWhiteSpace(NewOwner)) return;

        if (Recipients.Contains(NewOwner))
            HandyControl.Controls.Growl.Warning($"已存在{NewOwner}");
        else
            FeeDB.GetCollection<string>("Recipient").Insert(NewOwner);

        NewOwner = null;
    }










}