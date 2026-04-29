using FMO.Models;
using FMO.Utilities;
using LiteDB;

Console.WriteLine("开始生成演示数据...");

// 输入路径
string path = @"g:\xxsc";
//while (true)
//{
//    Console.WriteLine("请输入数据库文件所在路径：");
//    path = Console.ReadLine()?.Trim();
//    // 1. 路径校验
//    if (Directory.Exists(path))
//        break;

//    Console.WriteLine("❌ 数据库文件不存在！");
//}

Directory.SetCurrentDirectory(path);

Directory.CreateDirectory("data2");
// 复制data->data2
File.Copy(@"data\base.db", @"data2\base.db", true);
File.Copy(@"data\platform.db", @"data2\platform.db", true);
File.Copy(@"data\mission.db", @"data2\mission.db", true);

List<string> _lNames = new List<string>
    {
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴",
        "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗",
        "欧阳", "上官", "司马", "东方", "夏侯", "诸葛", "闻人", "拓跋"
    };

// 常用男名用字
 List<string> _fNames = new List<string>
    {
        "伟", "强", "磊", "军", "洋", "勇", "杰", "波", "明", "亮",
        "超", "浩", "凯", "健", "俊", "飞", "鹏", "峰", "旭", "晨" ,
        "芳", "娜", "敏", "静", "颖", "琳", "倩", "婷", "丽", "娟",
        "艳", "梅", "雪", "玲", "佳", "怡", "梦", "琪", "雨", "欣"
    };

using var db = DbHelper.Base();
var collection = db.GetCollection("Manager");
// 查询第一条 BsonDocument（FirstOrDefault 防止空数据报错）
var managerDoc = collection.Query().FirstOrDefault();

if (managerDoc != null)
{
    // ===================== 核心修改：直接修改 BsonDocument 字段 =====================
    // 继承自 Institution 的名称字段（直接赋值）
    managerDoc["Name"] = "暴富基金公司";

    // 必填字段赋值（对应实体类 required 属性）
    managerDoc["AmacId"] = "P12345678";
    managerDoc["RegisterNo"] = "P1234567";

    // 普通属性赋值（直接操作 BSON 键值对，与实体类字段名完全对应）
    managerDoc["IsMaster"] = true;
    managerDoc["SetupDate"] = BsonMapper.Global.ToDocument(DateOnly.FromDateTime(new DateTime(2018, 5, 20)));
    managerDoc["RegisterDate"] = BsonMapper.Global.ToDocument(DateOnly.FromDateTime(new DateTime(2018, 5, 20)));
    managerDoc[nameof(Manager.ArtificialPerson)] = "张三";
    managerDoc[nameof(Manager.Email)] = "zhangsan@example.com";
    managerDoc[nameof(Manager.WebSite)] = "www.example.com";

    managerDoc[nameof(Manager.Identity)] = BsonMapper.Global.ToDocument(new Identity { Id = "4324038084324234", Type = IDType.UnifiedSocialCreditCode });

    managerDoc[nameof(Manager.OfficeAddress)] = "中国上海";
    managerDoc[nameof(Manager.RegisterAddress)] = "中国上海";

    managerDoc["FundCount"] = 12;
    managerDoc["HasCreditTips"] = false;
    managerDoc["HasSpecialTips"] = false;
    managerDoc["MemberType"] = "普通会员";
    managerDoc["Advisorable"] = true;
    managerDoc["ScaleRange"] = "50-100亿元";
    managerDoc["RegisterCapitalAmac"] = 10000000.00m;
    managerDoc["RealCapitalAmac"] = 5000000.00m;
    managerDoc["Description"] = "专注于股票多头、量化对冲策略的私募基金管理人，致力于为客户创造稳健超额收益。";

    // ===================== 直接保存 BsonDocument 到数据库 =====================
    collection.Update(managerDoc);
}

db.FileStorage.Delete("icon.main");

ModifyFund(db);

ModifyParticipant(db);

void ModifyParticipant(BaseDatabase db)
{
    // ===================== 核心配置 =====================
    const int OffsetDays = 30;        // 日期偏移天数（正=延后，负=提前）
    const string NewPartName = "暴富投资顾问"; // 统一参与者名称

    // 获取 Participant 集合（非强类型 BsonDocument）
    var partCollection = db.GetCollection("Participant");
    var allParticipants = partCollection.Query().ToList();

    if (allParticipants.Count == 0)
    {
        Console.WriteLine("数据库中无参与者数据");
        return;
    }

    // 遍历全部参与者批量修改
    foreach (var partDoc in allParticipants)
    {
        try
        {
            // 1. 统一修改参与者名称
            partDoc["Name"] = $"{_lNames[Random.Shared.Next(_lNames.Count)]}{_fNames[Random.Shared.Next(_fNames.Count)]}";

            partDoc[nameof(Participant.Address)] = "中国上海";
            partDoc[nameof(Participant.Phone)] = "13912345678";
            partDoc[nameof(Participant.Email)] = "zhangsan@example.com";
            partDoc[nameof(Participant.Identity)] =  BsonMapper.Global.ToDocument(new Identity { Id = "123412341234", Type = IDType.IdentityCard });


            // ===================== 保存修改 =====================
            partCollection.Update(partDoc);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"参与者 ID {partDoc["Id"]} 修改失败：{ex.Message}");
        }
    }
}

var investors = db.GetCollection<Investor>().FindAll().ToList();



void ModifyFund(BaseDatabase db)
{

    // ===================== 核心配置（可自行修改） =====================
    // 日期偏移天数：正数=往后推，负数=往前推（例：30=后推30天，-7=前推7天）
    const int OffsetDays = 30;
    // 统一修改的基金名称
    const string NewFundName = "暴富系列基金";

    // 获取 Fund 集合（非强类型，直接操作BsonDocument）
    var fundCollection = db.GetCollection("Fund");
    // 查询【全部】基金文档
    var allFundDocs = fundCollection.Query().ToList();

    if (allFundDocs.Count == 0)
    {
        Console.WriteLine("数据库中无基金数据");
        return;
    }

    // 遍历每一只基金，批量修改
    foreach (var fundDoc in allFundDocs)
    {
        try
        {
            // 1. 统一修改基金名称
            fundDoc["Name"] = NewFundName;
            // 自动生成简称（可选）
            fundDoc["ShortName"] = "暴富基金";

 

            // --- 处理 DateOnly 类型日期（全部偏移，ClearDate 特殊判断）---
            // 发起日期
            fundDoc["InitiateDate"] = SafeOffsetDateTime(fundDoc["InitiateDate"]);
            // 成立日期
            fundDoc["SetupDate"] = SafeOffsetDateTime(fundDoc["SetupDate"]);
            // 备案日期
            fundDoc["AuditDate"] = SafeOffsetDateTime(fundDoc["AuditDate"]);



            // 4. 保存当前基金修改
            fundCollection.Update(fundDoc);
        }
        catch (Exception ex)
        {
            // 单条基金报错不影响整体，打印异常
            Console.WriteLine($"基金Id={fundDoc["Id"]} 修改失败：{ex.Message}");
        }
    }
}



BsonDocument SafeOffsetDateTime(BsonValue originalDt)
{
    var date = BsonMapper.Global.ToObject<DateOnly>(originalDt.AsDocument);
    if (date.Year < 2000 || date.Year > 2030) return originalDt.AsDocument;

    date = date.AddDays(Random.Shared.Next(-100, 100));

    var offsetDt = date.AddDays(100);
    return BsonMapper.Global.ToDocument(offsetDt);
}