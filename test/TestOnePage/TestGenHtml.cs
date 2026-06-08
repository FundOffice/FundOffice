using FMO.Models;
using FMO.Utilities;
using Initial;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestOnePage;

[TestClass]
public sealed class TestGenHtml
{
    [TestInitialize]
    public void TestInit()
    {
        // This method is called before each test method.
        DataInject.SetAsDebug();
    }

    [TestMethod]
    public void TestMethod1()
    {
        int FundId = 9, FlowId = 68;
        // 读取资源  
        var path = @"D:\Project\FundOffice\src\Client\res\onepage.html";
        var stream = new FileStream(path, FileMode.Open);// Assembly.GetExecutingAssembly().GetManifestResourceStream("FMO.res.onepage.html");
        using var sr = new StreamReader(stream!);
        var html = sr.ReadToEnd();

        // 写json
        using var db = DbHelper.Base();
        var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
        using var ms = new MemoryStream();
        if (db.FileStorage.Exists("icon.main"))
            db.FileStorage.Download("icon.main", ms);
        var logo = ms.ToArray();
         
        var fund = db.GetCollection<Fund>().FindById(FundId);
        var factors = db.QueryFactor(FundId);
        BrochureInvestManager[] investManagers = [new("张三", "介绍", [])];
        var bro = BrochureFactor.Create(manager, logo, [], investManagers, fund, factors, FlowId);

        ///json
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        }
        ;

        var json = JsonSerializer.Serialize(bro, jsonOptions);

        html = html.Replace("###DATA###", json);

        // 读取模板 <div>...</div>
        var templateFiles = new DirectoryInfo(@"files\onepage").GetFiles("*.html");

        // 获取所有模板文件（按文件名排序，保证索引稳定）
        var listItemHtmlSb = new StringBuilder();       // 左侧文件名列表HTML
        var templateContentList = new List<string>();   // 模板内容集合

        foreach (var file in templateFiles)
        {
            // 读取单个模板内容
            string tplContent = File.ReadAllText(file.FullName, Encoding.UTF8);
            templateContentList.Add(tplContent);

            // 生成左侧列表项：显示【文件名】 
            listItemHtmlSb.AppendLine($"<div class=\"list-item\">{Path.GetFileNameWithoutExtension(file.Name)}</div>");
        }

        // 替换占位符1：###LIST### 左侧模板名称列表
        html = html.Replace("###LIST###", listItemHtmlSb.ToString());

        // 4. 拼接 JS 数组字符串（重点：转义HTML，防止JS语法错误）
        string templateArrayJs = JsonSerializer.Serialize(templateContentList, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        html = html.Replace("###TEMPLATE_ARR###", templateArrayJs);



        var fileInfo = new FileInfo(@$"temp\{FundId}\onepage.html");
        if (!fileInfo.Directory!.Exists)
            fileInfo.Directory.Create();

        using var sw = new StreamWriter(fileInfo.FullName);
        sw.Write(html);
        sw.Flush();


        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = fileInfo.FullName, UseShellExecute = true });
    }
}
