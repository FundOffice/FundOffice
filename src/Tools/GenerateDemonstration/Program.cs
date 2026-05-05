using FMO.Models;
using FMO.Utilities;
using GenerateDemonstration;
using LiteDB;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

Console.OutputEncoding = System.Text.Encoding.UTF8;
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

Dictionary<string, string> _fundNameDic = [];
Dictionary<string, string> _fundCodeDic = [];


using var db = DbHelper.Base();


Dictionary<string, string> _nameDic = GenerateNameDic(db);
Dictionary<string, string> _idDic = GenerateCertDic(db);


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
    managerDoc[nameof(Manager.Phone)] = "021-12345678";
    managerDoc["Advisorable"] = true;
    managerDoc["ScaleRange"] = "50-100亿元";
    managerDoc["RegisterCapitalAmac"] = 10000000.00m;
    managerDoc["RealCapitalAmac"] = 5000000.00m;
    managerDoc["Description"] = "专注于股票多头、量化对冲策略的私募基金管理人，致力于为客户创造稳健超额收益。";

    // ===================== 直接保存 BsonDocument 到数据库 =====================
    collection.Update(managerDoc);
}

db.FileStorage.Delete("icon.main");

ModifyOwner(db);

ModifyInvestor(db);

ModifyFund(db);

ModifyParticipant(db);

ModifyFile(db);


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
            var old = partDoc["Name"].AsString;

            partDoc["Name"] = _nameDic[old];

            partDoc[nameof(Participant.Address)] = "中国上海";
            partDoc[nameof(Participant.Phone)] = "13912345678";
            partDoc[nameof(Participant.Email)] = "zhangsan@example.com";
            partDoc[nameof(Participant.Identity)] = BsonMapper.Global.ToDocument(new Identity { Id = "123412341234", Type = IDType.IdentityCard });


            // ===================== 保存修改 =====================
            partCollection.Update(partDoc);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"参与者 ID {partDoc["Id"]} 修改失败：{ex.Message}");
        }
    }
}

void ModifyOwner(BaseDatabase db)
{
    var ins = db.GetCollection<IEntity>().FindAll().ToArray();

    foreach (var partDoc in ins)
    {
        var old = partDoc.Name;
        partDoc.Name = _nameDic[old];

        partDoc.Identity = new Identity { Type = partDoc.Identity?.Type ?? default, Id = "1234567789" };

        if (partDoc is Institution cmp)
        {
            cmp.ArtificialPerson = _nameDic[cmp.ArtificialPerson];
            cmp.RegisterAddress = "中国";
            cmp.Telephone = "021-12345678";
            cmp.Identity?.Id = "3432432452342";
            cmp.Fax = "021-1234567";
            cmp.Email = "aa@bb.cc";
            cmp.OfficeAddress = "中国";
            cmp.WebSite = "www.example.com";
        }


    }
    db.GetCollection<IEntity>().Update(ins);
}



void ModifyInvestor(ILiteDatabase db)
{

    var investors = db.GetCollection<Investor>().FindAll().ToList();

    foreach (var c in investors)
    {
        c.Name = Map(c.Name, _nameDic);
        c.Identity?.Id = Map(c.Identity.Id, _idDic);

        c.Email = "aa@bb.cc";
        c.Address = Obfuscator.Suffix(c.Address);
    }

    db.GetCollection<Investor>().Update(investors);


    var orders = db.GetCollection<TransferOrder>().FindAll().ToArray();
    foreach (var c in orders)
    {
        c.FundName = Map(c.FundName, _nameDic);
        c.InvestorName = Map(c.InvestorName, _nameDic);
        c.InvestorIdentity = Map(c.InvestorIdentity, _idDic);
    }
    db.GetCollection<TransferOrder>().Update(orders);


    var records = db.GetCollection<TransferRecord>().FindAll().ToArray();
    foreach (var c in records)
    {
        c.FundName = Map(c.FundName, _nameDic);
        c.FundCode = Map(c.FundCode, _idDic);

        c.InvestorName = Map(c.InvestorName, _nameDic);
        c.InvestorIdentity = Map(c.InvestorIdentity, _idDic);
    }
    db.GetCollection<TransferRecord>().Update(records);


    var requests = db.GetCollection<TransferRequest>().FindAll().ToArray();
    foreach (var c in records)
    {
        c.FundName = Map(c.FundName, _nameDic);
        c.FundCode = Map(c.FundCode, _idDic);

        c.InvestorName = Map(c.InvestorName, _nameDic);
        c.InvestorIdentity = Map(c.InvestorIdentity, _idDic);
    }
    db.GetCollection<TransferRequest>().Update(requests);













}

void ModifyFund(BaseDatabase db)
{

    // ===================== 核心配置（可自行修改） =====================
    // 日期偏移天数：正数=往后推，负数=往前推（例：30=后推30天，-7=前推7天）
    const int OffsetDays = 30;
    // 统一修改的基金名称
    const string NewFundName = "暴富系列基金";

    // 获取 Fund 集合（非强类型，直接操作BsonDocument）
    var fundCollection = db.GetCollection<Fund>();
    // 查询【全部】基金文档
    var allFunds = fundCollection.Query().ToList();
    var eles = db.GetCollection<FundElements>().FindAll().ToArray();


    int fid = 0;
    var fnames = allFunds.Select(x => x.Name).Union(allFunds.Select(x => x.ShortName)).
        Union(eles.SelectMany(x => x.FullName.Changes.Values)).Union(eles.SelectMany(x => x.ShortName.Changes.Values)).Distinct().Where(x => x is not null);
    _fundNameDic = fnames.Select(x => (x!, Regex.Replace(x, ".*号", $"暴富{++fid}号"))).ToDictionary();

    if (allFunds.Count == 0)
    {
        Console.WriteLine("数据库中无基金数据");
        return;
    }

    // 遍历每一只基金，批量修改
    foreach (var fundDoc in allFunds)
    {
        try
        {

            _fundCodeDic[fundDoc.Code!] = $"SSSSS{fid}"[^6..];

            // 1. 统一修改基金名称
            fundDoc.Name = _nameDic[fundDoc.Name];
            // 自动生成简称（可选）
            fundDoc.ShortName = _nameDic[fundDoc.ShortName];



            // --- 处理 DateOnly 类型日期（全部偏移，ClearDate 特殊判断）---
            // 发起日期
            fundDoc.InitiateDate = SafeOffsetDateOnly(fundDoc.InitiateDate);
            // 成立日期
            fundDoc.SetupDate = SafeOffsetDateOnly(fundDoc.SetupDate);
            // 备案日期
            fundDoc.AuditDate = SafeOffsetDateOnly(fundDoc.AuditDate);

            fundDoc.Code = _fundCodeDic[fundDoc.Code!];





            // 4. 保存当前基金修改
            fundCollection.Update(fundDoc);
        }
        catch (Exception ex)
        {
            // 单条基金报错不影响整体，打印异常
            Console.WriteLine($"基金Id={fundDoc.Id} 修改失败：{ex.Message}");
        }
    }

    fundCollection.Update(allFunds);

    Parallel.ForEach(eles, ele =>
    {
        foreach (var item in ele.FullName.Changes.ToArray())
        {
            if (_fundNameDic.TryGetValue(item.Value, out var nv))
                ele.FullName.SetValue(nv, item.Key);
        }

        foreach (var item in ele.ShortName.Changes.ToArray())
        {
            if (_fundNameDic.TryGetValue(item.Value, out var nv))
                ele.ShortName.SetValue(nv, item.Key);
        }

        foreach (var item in ele.CollectionAccount.Changes.ToArray())
        {
            item.Value.Name = Map(item.Value.Name, _nameDic);
            item.Value.Number = Obfuscator.Suffix(item.Value.Number);
            ele.CollectionAccount.SetValue(item.Value, item.Key);
        }
        foreach (var item in ele.CustodyAccount.Changes.ToArray())
        {
            item.Value.Name = Map(item.Value.Name, _nameDic);
            item.Value.Number = Obfuscator.Suffix(item.Value.Number);
            ele.CustodyAccount.SetValue(item.Value, item.Key);
        }

        foreach (var item in ele.InvestmentManager.Changes.ToArray())
        {
            ele.InvestmentManager.SetValue(Map(item.Value, _nameDic), item.Key);
        }
    });

    db.GetCollection<FundElements>().Update(eles);
}


string ReplaceFundName(string old)
{
    foreach (var item in _fundNameDic)
    {
        old = old.Replace(item.Key, item.Value);
    }
    return old;
}

string Map(string? old, Dictionary<string, string> dic)
{
    if (old is null) return "";

    if (dic.TryGetValue(old, out var v))
        return v;

    foreach (var item in dic)
    {
        old = old.Replace(item.Key, item.Value);
    }
    return old;
}




BsonDocument SafeOffsetDateTime(BsonValue originalDt)
{
    var date = BsonMapper.Global.ToObject<DateOnly>(originalDt.AsDocument);
    if (date.Year < 2000 || date.Year > 2030) return originalDt.AsDocument;

    date = date.AddDays(Random.Shared.Next(-100, 100));

    var offsetDt = date.AddDays(100);
    return BsonMapper.Global.ToDocument(offsetDt);
}

DateOnly SafeOffsetDateOnly(DateOnly date)
{
    if (date.Year < 2000 || date.Year > 2030) return date;

    date = date.AddDays(Random.Shared.Next(-100, 100));

    return date.AddDays(100);
}


Dictionary<string, string> GenerateNameDic(ILiteDatabase db)
{
    Dictionary<string, string> dic = [];
    var names = db.GetCollection<Manager>().Query().Select(x => x.Name).ToArray();
    foreach (var n in names)
        dic.TryAdd(n, Obfuscator.GenerateCompany(n));

    var entity = db.GetCollection<IEntity>().FindAll().ToArray();
    foreach (var e in entity)
    {
        if (e is Institution)
            dic.TryAdd(e.Name, Obfuscator.GenerateCompany(e.Name));
        else dic.TryAdd(e.Name, Obfuscator.GeneratePerson());
    }


    int id = 0;
    foreach (var n in db.GetCollection<Fund>().Query().Select(x => new { x.Name, x.ShortName }).ToArray())
    {
        dic.TryAdd(n.Name, Regex.Replace(n.Name, ".*号", $"暴富{++id}号"));
        dic.TryAdd(n.ShortName, Regex.Replace(n.ShortName, ".*号", $"暴富{++id}号"));
    }

    foreach (var n in db.GetCollection<Participant>().Query().Select(x => x.Name).ToArray())
        dic.TryAdd(n!, Obfuscator.GeneratePerson());

    id = 0;
    foreach (var c in db.GetCollection<Investor>().FindAll().ToArray())
    {
        if (c.EntityType == EntityType.Natural)
            dic.TryAdd(c.Name, Obfuscator.GeneratePerson());
        else if (c.EntityType == EntityType.Institution)
            dic.TryAdd(c.Name, Obfuscator.GenerateCompany(c.Name));
        else if (c.EntityType == EntityType.Product)
            dic.TryAdd(c.Name, $"FOF{++id}号");
    }


    return dic;
}

Dictionary<string, string> GenerateCertDic(ILiteDatabase db)
{
    Dictionary<string, string> dic = [];
    var names = db.GetCollection<Manager>().Query().Select(x => x.Identity.Id).ToArray();
    foreach (var n in names)
        dic.TryAdd(n, Obfuscator.Suffix(n));


    int id = 0;
    foreach (var n in db.GetCollection<Fund>().Query().Select(x => x.Code).ToArray())
        dic.TryAdd(n, Obfuscator.Suffix(n));

    foreach (var n in db.GetCollection<Participant>().Query().Select(x => x.Identity.Id).ToArray())
        dic.TryAdd(n, Obfuscator.Suffix(n));

    id = 0;
    foreach (var n in db.GetCollection<Investor>().Query().Select(x => x.Identity.Id).ToArray())
        dic.TryAdd(n, Obfuscator.Suffix(n));


    return dic;
}









void ModifyFile(BaseDatabase db, string[] fields, Dictionary<string,string> maps)
{
    var liteDb = db as LiteDatabase ?? throw new ArgumentException("db 必须是 LiteDatabase 实例");

    // 🔧 性能调优参数
    const int BATCH_SIZE = 200;              // 每批处理文档数（建议 100~500）
    const int MAX_DEGREE_OF_PARALLELISM = 12; // 并发集合数（非文档数，避免锁竞争）

    var totalDocs = 0;
    var processedDocs = 0;
    var startTime = Stopwatch.StartNew();

    Console.WriteLine("📦 正在扫描数据库...");

    // 1. 按集合分组预加载文档（避免枚举期间修改 + 便于按集合批量处理）
    var collectionsData = new Dictionary<string, List<BsonDocument>>();

    foreach (var colName in liteDb.GetCollectionNames())
    {
        var collection = liteDb.GetCollection(colName);
        var docs = collection.FindAll().ToList();

        if (docs.Count > 0)
        {
            collectionsData[colName] = docs;
            totalDocs += docs.Count;
            Console.WriteLine($"   ├─ {colName}: {docs.Count} 条");
        }
    }

    if (totalDocs == 0)
    {
        Console.WriteLine("✅ 数据库为空，无需处理。");
        return;
    }

    Console.WriteLine($"\n📊 共 {totalDocs} 条文档，开始批量处理 (Batch={BATCH_SIZE}, Threads={MAX_DEGREE_OF_PARALLELISM})\n");

    // 2. 按集合并行处理（集合间并行，集合内串行批量）
    Parallel.ForEach(collectionsData, new ParallelOptions { MaxDegreeOfParallelism = MAX_DEGREE_OF_PARALLELISM }, kvp =>
    {
        var colName = kvp.Key;
        var docs = kvp.Value;
        var collection = liteDb.GetCollection(colName);

        // 按批次处理当前集合的文档
        for (int i = 0; i < docs.Count; i += BATCH_SIZE)
        {
            var batch = docs.Skip(i).Take(BATCH_SIZE).ToList();

            // ✅ 核心优化：事务包裹批量更新，大幅减少磁盘 IO
            var trans = liteDb.BeginTrans();
            try
            {
                foreach (var doc in batch)
                {
                    ProcessBsonDocument(doc); // 内存中处理，无 IO
                }

                // 批量写回（单次事务内）
                collection.Update(batch);
                liteDb.Commit();

                // 原子更新全局计数
                var current = Interlocked.Add(ref processedDocs, batch.Count);

                // 🔽 降低进度刷新频率：每批或每 2 秒刷新一次，避免 Console 输出成为瓶颈
                if (current % (BATCH_SIZE * 5) == 0 || current == totalDocs)
                {
                    ShowProgress(current, totalDocs, startTime);
                }
            }
            catch (Exception ex)
            {
                liteDb.Rollback();
                Console.WriteLine($"\n❌ 批次处理失败 [{colName} 第 {i / BATCH_SIZE + 1} 批]: {ex.Message}");
            }
        }
    });

    // 3. 最终输出
    startTime.Stop();
    ShowProgress(totalDocs, totalDocs, startTime, true);
    Console.WriteLine($"\n\n✅ 全部完成！总耗时: {startTime.Elapsed:mm\\:ss\\.ff}");
}

/// <summary>
/// 递归处理 BsonDocument（逻辑不变，仅内存操作，极快）
/// </summary>
void ProcessBsonDocument(BsonDocument doc)
{
    if (doc.ContainsKey("Name") && doc["Name"].IsString)
    {
        var originalName = doc["Name"].AsString;
        doc["Name"] = Map(originalName, _nameDic) ?? originalName;
    }

    foreach (var field in doc)
    {
        if (field.Key == "Name") continue;
        ProcessBsonValue(field.Value);
    }
}

void ProcessBsonValue(BsonValue value)
{
    if (value.IsDocument)
    {
        ProcessBsonDocument(value.AsDocument);
    }
    else if (value.IsArray)
    {
        foreach (var item in value.AsArray)
        {
            ProcessBsonValue(item);
        }
    }
}

/// <summary>
/// 精简版进度显示（减少 Console 调用频率）
/// </summary>
void ShowProgress(int current, int total, Stopwatch timer, bool isFinal = false)
{
    var percent = (double)current / total * 100;
    var elapsed = timer.Elapsed;
    var speed = elapsed.TotalSeconds > 0 ? current / elapsed.TotalSeconds : 0;

    var barWidth = 30;
    var filled = (int)(barWidth * percent / 100);
    var bar = new string('█', filled) + new string('░', barWidth - filled);

    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(isFinal
        ? $"[{bar}] 100% ({total}/{total}) | {speed:F0} 条/秒 | 耗时: {elapsed:mm\\:ss\\.ff}"
        : $"[{bar}] {percent:F1}% ({current}/{total}) | {speed:F0} 条/秒 | 剩余: {(total - current) / Math.Max(speed, 1):F0}秒");

    if (isFinal) Console.WriteLine();
}