using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FMO;

/// <summary>
/// TransferRequestPage.xaml 的交互逻辑
/// </summary>
public partial class TransferRequestPage : UserControl
{
    public TransferRequestPage()
    {
        InitializeComponent();
    }
}


public partial class TransferRequestPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<TransferRequest>? Records { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;


    public TransferRequestPageViewModel()
    {
        using var db = DbHelper.Base();
        var l = db.GetCollection<TransferRequest>().FindAll().OrderByDescending(x=>x.RequestDate).ToList();

        Records = new ObservableCollection<TransferRequest>(l);

        IsLoading = false;
    }












}