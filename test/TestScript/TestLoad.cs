using FMO.Models;
using FMO.TPL;
using Initial;
using MiniExcelLibs;
using System.IO.Compression;
using System.Text;
using Utilities;

namespace TestScript;

[TestClass]
public class TestLoad
{

    [TestMethod]
    public async Task Encrypt()
    {
        var enc = AesHelper.Encrypt(File.ReadAllBytes(@"sharehold\defv1"));

        using var zip = await ZipArchive.CreateAsync(new FileStream("v1.tpl", FileMode.Create), ZipArchiveMode.Create, false, Encoding.UTF8);
        var entry = zip.CreateEntry("def");

        using (var stream = entry.Open())
        {
            stream.Write(enc);
            stream.Flush();
        }

        entry = zip.CreateEntry("tpl.xlsx");

        using (var stream = entry.Open())
        {
            stream.Write(File.ReadAllBytes(@"sharehold\default.xlsx"));
            stream.Flush();
        }

    }

    [TestMethod]
    public async Task LoadDef()
    {

        var tpl = await ExcelTemplate.Load("v1.tpl");

        DataInject.SetAsDebug();

        var g = await tpl!.Prepare(new TemplateGlobal
        {
            Funds = [new Fund { Name = "f", Id = 6 }],
            Dates = [new DateOnly(2026, 5, 1)]
        });


        var obj = tpl.Execute(g);


        MiniExcel.SaveAsByTemplate("outp.xlsx", tpl.Excel, g);


    }

}
