using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace FMO;

/// <summary>
/// 股卡
/// </summary>
public partial class SecurityCardViewModel : ObservableObject
{
    public SecurityCardViewModel(SecurityCard x)
    {
        Id = x.Id;
        FundId = x.FundId;
        Name = x.Name;
        SerialNo = x.SerialNo;
        CardNo = x.CardNo;
        UniversalNo = x.UniversalNo;
        Group = x.Group >= GroupBrushes.Length ? 0 : x.Group;
        GroupBrush = GroupBrushes[Group];
        FundCode = x.FundCode;
        IsDeregistered = x.IsDeregistered;
        Date = x.Date;
        Type = x.Type;
        Tag = x.Type switch { SecurityCardType.ShangHai => "沪", SecurityCardType.ShenZhen => "深", _ => x.CardNo.StartsWith('B') ? "沪" : "深" };
        File = new FileInfo(@$"files\accounts\security\{SerialNo}-{CardNo}.pdf");
    }

    public string Id { get; }

    public int FundId { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    [ObservableProperty] public partial string SerialNo { get; set; }

    /// <summary>
    /// 子账户号
    /// </summary>
    [ObservableProperty] public partial string? CardNo { get; set; }

    /// <summary>
    /// 一码通
    /// </summary>
    [ObservableProperty] public partial string? UniversalNo { get; set; }

    [ObservableProperty] public partial string Name { get; set; }

    [ObservableProperty] public partial string? FundCode { get; set; }


    [ObservableProperty] public partial bool IsDeregistered { get; set; }

    [ObservableProperty] public partial bool ShowGroupPop { get; set; }

    [ObservableProperty] public partial int Group { get; set; }

    /// <summary>
    /// 可能有错，一码通和其它不一样
    /// </summary>
    [ObservableProperty] public partial bool MaybeError { get; set; }


    [ObservableProperty]
    public partial SolidColorBrush? GroupBrush { get; set; }

    public static SolidColorBrush[] GroupBrushes { get; } = [Brushes.Transparent, Brushes.Orange, Brushes.Green,
                                                            Brushes.RoyalBlue, Brushes.Purple, Brushes.Honeydew,
                                                            Brushes.Magenta, Brushes.Cyan,Brushes.Khaki];


    /// <summary>
    /// 申请日期
    /// </summary>
    public DateOnly Date { get; set; }
    public SecurityCardType Type { get; }
    public string Tag { get; set; }

    public FileInfo File { get; set; }


    partial void OnGroupChanged(int value)
    {
        using var db = DbHelper.Base();
        var card = db.GetCollection<SecurityCard>().FindById(Id);
        card.Group = value;
        db.GetCollection<SecurityCard>().Update(card);
        ShowGroupPop = false;
    }




    [RelayCommand]
    public void View()
    {
        if (File?.Exists ?? false)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(File.FullName) { UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void Deregister()
    {
        using var db = DbHelper.Base();
        var card = db.GetCollection<SecurityCard>().FindById(Id);

        if (card is null) return;

        if (Regex.IsMatch(card.CardNo, @"[^B0-9\s]"))
        {
            db.GetCollection<SecurityCard>().Delete(card.Id);
            return;
        }

        card.IsDeregistered = !card.IsDeregistered;
        IsDeregistered = card.IsDeregistered;
        db.GetCollection<SecurityCard>().Update(card);
    }


    [RelayCommand]
    public void SaveAs()
    {
        if (File is null || !File.Exists) return;

        try
        {
            var d = new SaveFileDialog();
            d.FileName = File.Name;
            d.DefaultExt = ".pdf";
            d.Filter = "Pdf文件|*.pdf";
            if (d.ShowDialog() == true)
                System.IO.File.Copy(File.FullName, d.FileName);
        }
        catch (Exception ex)
        {
            Log.Error($"文件另存为失败: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Copy()
    {
        Clipboard.SetDataObject(new DataObject(CardNo));
    }

    [RelayCommand]
    public void SetGroup()
    {
        ShowGroupPop = true;
    }
}



public partial class SecurityCardChangeViewModel : ObservableObject
{
    public SecurityCardChangeViewModel(SecurityCardChange x)
    {
        Id = x.Id;
        FundId = x.FundId;
        Name = x.Name;
        SerialNo = x.SerialNo;
        Date = x.Date;
        UniversalNo = x.UniversalNo;
        File = new FileInfo(@$"files\accounts\security\G-{SerialNo}.pdf");
    }


    [Obsolete]
    public int Group { get; } = -1;


    [Obsolete]
    public string? Tag { get; }

    public FileInfo File { get; set; }

    public string Id { get; }

    public int FundId { get; set; }

    public string Name { get; }

    /// <summary>
    /// 流水号
    /// </summary>
    public string SerialNo { get; set; }

    public DateOnly Date { get; }
    public string UniversalNo { get; set; }

    [ObservableProperty] public partial bool IsDeregistered { get; set; }

    /// <summary>
    /// 可能有错，一码通和其它不一样
    /// </summary>
    [ObservableProperty] public partial bool MaybeError { get; set; }


    [RelayCommand]
    public void View()
    {
        if (File?.Exists ?? false)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(File.FullName) { UseShellExecute = true }); } catch { }
    }



    [RelayCommand]
    public void SaveAs()
    {
        if (File is null || !File.Exists) return;

        try
        {
            var d = new SaveFileDialog();
            d.FileName = File.Name;
            d.DefaultExt = ".pdf";
            d.Filter = "Pdf文件|*.pdf";
            if (d.ShowDialog() == true)
                System.IO.File.Copy(File.FullName, d.FileName);
        }
        catch (Exception ex)
        {
            Log.Error($"文件另存为失败: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Deregister()
    {
        using var db = DbHelper.Base();
        var card = db.GetCollection<SecurityCardChange>().FindById(Id);
        IsDeregistered = true;
        if (card is not null)
            db.GetCollection<SecurityCardChange>().Delete(card.Id);
    }
}

