using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using FMO.Models;
using FMO.AMAC;
using Utilities;
using FMO.Utilities;

class Program
{
    static async Task Main(string[] args)
    {
        // 解析命令行参数：-name "名称" -id "ID" -code "Code" -dir "工作目录"
        string? name = null;
        string? id = null;
        string? code = null;
        string? workDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-name" when i + 1 < args.Length:
                    name = args[++i];
                    break;
                case "-id" when i + 1 < args.Length:
                    id = args[++i];
                    break;
                case "-code" when i + 1 < args.Length:
                    code = args[++i];
                    break;
                case "-dir" when i + 1 < args.Length:
                    workDir = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id))
        {
            Console.WriteLine("错误：必须提供 -name 和 -id 参数");
            Environment.Exit(1);
        }

        var program = new Program();
        await program.SetUpAsync(name, id, code ?? "", workDir);
    }

    public async Task SetUpAsync(string name, string id, string code, string? workDir = null)
    {
        // 如果传入了工作目录，切换到该目录
        if (!string.IsNullOrEmpty(workDir))
        {
            if (Directory.Exists(workDir))
            {
                Directory.SetCurrentDirectory(workDir);
                Console.WriteLine($"工作目录：{workDir}");
            }
            else
            {
                Console.WriteLine($"警告：工作目录不存在：{workDir}");
            }
        }

        var Manager = new Manager { AmacId = id, Name = name, RegisterNo = code };

        List<FundBasicInfo> funds = new List<FundBasicInfo>();
        await AmacHtml.CrawleManagerInfo(Manager, funds);


        ///保存数据库
        Manager.IsMaster = true;

#if RELEASE
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Nexus"))
#else
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Nexus\Debug"))
#endif
        {
            key.SetValue("Cap", AesHelper.Encrypt(Manager.Name));
            key.SetValue("Code", AesHelper.Encrypt(Manager.Identity!.Id));
        }

        //首次运行，记录Patch，以防错误运行
        DbHelper.Init();
        using var db = DbHelper.Base();
        db.GetCollection<Manager>().Insert(Manager);
        DatabaseAssist.InitPatch();

        db.GetCollection<Fund>().InsertBulk(funds.Select(x => new Fund
        {
            Name = x.Name!,
            Code = $"unset.{x.Name!.GetHashCode()}",
            ShortName = Fund.GetDefaultShortName(x.Name!),
            Url = "https://gs.amac.org.cn/amac-infodisc/res/pof" + x.Url,
            AsAdvisor = x.IsAdvisor,
            AmacID = Regex.Match(x.Url!, @"\d{5,}").Value
        }));

        Console.WriteLine($"初始化完成：{name} (ID: {id})");
    }
}
