using FMO.Models;
using FMO.Schedule;
using FMO.Trustee;
using LiteDB;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FMO.Utilities;


public static partial class DatabaseAssist
{
    private static Dictionary<int, Action<BaseDatabase>> patchs = new()
    {
        [42] = UpdateTASchcame,

        //[44] = MapTA

        [49] = ChangeAPIConfig,
        [51] = ChangeAmacAccount,
        [54] = ChangeAmacAccount2,

        [55] = FixLogInfo,
        [62] = RebuildFundShareRecord,
        [64] = ChangeOrderFilePathToRelative,
        [65] = UpdateLiqudatingTA,
        [66] = ChangeAPIAndClearData,

        [67] = Customer2Investor,
        [71] = PlatformTable,
        //[68] = AddManualLink
        [72] = MiggrateInstitutionCertifications,
        [73] = MiggigrateFileInInvestor,
        [74] = MiggrateQualification,
        [75] = MiggrateRisk,
        [78] = MiggrateOrderFile,
        [84] = MiggrateFlow,
        [85] = MiggrateTradeAccount,
        [86] = MiggrateAnnounce,
        [87] = ChangeQuaterlyId,
        [88] = UpdateManageScale,
        [89] = UpdatePolicy,
        [92] = UpdateSecurityCard,
        [93] = UpdateInvestorBalance,
        [97] = ClearTaBecauseSCBug,
        [98] = RecalcInvestorBalance,
        [100] = FixNv,
        [104] = FixNv2,
        [106] = FixMeishi,
        [107] = FixFund,
        [113] = FixMove,
        [117] = FixChannelConfig,
    };

    private static void FixChannelConfig(BaseDatabase database)
    {
        database.DropCollection("IDisclosureChannelConfig");
        database.DropCollection("DisclosureWorkflow");
    }

    /// <summary>
    /// 修复迁移时的type名称不对
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void FixMove(BaseDatabase database)
    {
        FixMovedDb(database);
        using (var db = DbHelper.Platform())
            FixMovedDb(db);

        using (var db = new MissionDatabase())
            FixMovedDb(db);
    }

   static  void TraverseBson(BsonValue bson, Action<BsonDocument> visit)
    {
        switch (bson.Type)
        {
            case BsonType.Document:
                var doc = bson.AsDocument;
                visit(doc); // 处理当前文档

                // 递归遍历所有字段
                foreach (var field in doc.Values)
                    TraverseBson(field, visit);
                break;

            case BsonType.Array:
                foreach (var item in bson.AsArray)
                    TraverseBson(item, visit);
                break;

            case BsonType.Null:
            case BsonType.Int32:
            case BsonType.Int64:
            case BsonType.Double:
            case BsonType.String:
            case BsonType.Boolean:
            case BsonType.DateTime:
            case BsonType.Guid:
            case BsonType.Binary:
                break; // 基础类型无需遍历
        }
    }

    private static void FixMovedDb(ILiteDatabase db)
    {
        var regexPattern = @"(.*\.\w+),\s*FMO\.([\w\.]+)";

        // ======================================
        // 第一步：全库遍历，收集所有 groups[2] 的值（去重）
        // ======================================
        HashSet<string> allGroup2Values = new HashSet<string>(); 
         
        foreach (var colName in db.GetCollectionNames())
        {
            if (colName.StartsWith("$")) continue;

            try
            {
                var docs = db.GetCollection(colName).FindAll().ToArray();
                foreach (var doc in docs)
                {
                    TraverseBson(doc, node =>
                    {
                        if (node.ContainsKey("_type"))
                        {
                            var match = Regex.Match(node["_type"].AsString, regexPattern);
                            if (match.Success)
                                allGroup2Values.Add(match.Groups[2].Value);
                        }
                    });
                }
            }
            catch { }
        }

        // ======================================
        // 输出所有抓到的 group2 值（你可以复制下来）
        // ======================================
        Debug.WriteLine("======== 全库所有 groups[2] 值 ========");
        foreach (var val in allGroup2Values)
        {
            Debug.WriteLine(val);
        }
        Debug.WriteLine("======================================");

        // ======================================
        // 第二步：建立替换字典（你在这里写规则）
        // ======================================
        Dictionary<string, string> replaceMap = new Dictionary<string, string>();
        foreach (var v in allGroup2Values)
        {
            if (v.StartsWith("Trustee"))
                replaceMap[v] = v[8..];
            else if (v.StartsWith("ESigning."))
                replaceMap[v] = v[9..];
        }
        // 示例规则：
        // replaceMap["OldValue1"] = "NewValue1";
        // replaceMap["OldValue2"] = "NewValue2";

        // ======================================
        // 第三步：全库批量替换并保存
        foreach (var colName in db.GetCollectionNames())
        {
            if (colName.StartsWith("$")) continue;

            try
            {
                var col = db.GetCollection(colName);
                var docs = col.FindAll().ToArray();
                int updateCount = 0;

                foreach (var doc in docs)
                {
                    bool modified = false;

                    TraverseBson(doc, node =>
                    {
                        if (!node.ContainsKey("_type")) return;

                        var match = Regex.Match(node["_type"].AsString, regexPattern);
                        if (!match.Success) return;

                        var g1 = match.Groups[1].Value;
                        var g2 = match.Groups[2].Value;

                        // 替换逻辑
                        string newType = replaceMap.TryGetValue(g2, out var newG2)
                            ? $"{g1}, {newG2}"
                            : $"{g1}, {g2}";

                        node["_type"] = newType;
                        modified = true;
                    });

                    if (modified) updateCount++;
                }

                if (updateCount > 0)
                {
                    col.Update(docs);
                    Debug.WriteLine($"✅ {colName} → 更新 {updateCount} 条");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ {colName} 失败：{ex.Message}");
            }
        }

        Debug.WriteLine("✅ 全库批量替换完成！");
    }






    private static void FixFund(BaseDatabase database)
    {
        var d = database.GetCollection("Fund").FindAll().ToArray();
        foreach (var item in d)
        {
            if (item.ContainsKey("AmacId") && item["AmacId"].IsInt64)
            {
                item.Remove("AmacId");
                item.Add("AmacID", "");
            }
        }
        database.GetCollection("Fund").Update(d);
    }

    private static void FixMeishi(BaseDatabase database)
    {
        using var db = DbHelper.Platform();
        var list = db.GetCollection("SigningWorkRecord").FindAll().ToArray();
        foreach (var item in list)
        {
            item["QueryOrderTime"] = new DateTime(2025, 12, 22);
        }
        db.GetCollection("SigningWorkRecord").Update(list);
    }

    /// <summary>
    /// sb 数据库，id都能重复
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void FixNv2(BaseDatabase db)
    {
        var fvCollections = db.GetCollectionNames()
                        .Where(c => Regex.IsMatch(c, @"^fv_", RegexOptions.IgnoreCase))
                        .ToList();

        foreach (var collectionName in fvCollections)
        {
            var collection = db.GetCollection<DailyValue>(collectionName);
            var allDocs = collection.FindAll().ToList();

            if (!allDocs.Any())
                continue;

            // 按 Id 分组，保留每组中最后一个文档（按 FindAll 返回顺序）
            var uniqueDocs = allDocs
                .GroupBy(x => x.Id)
                .Select(g => g.Last()) // 保留最后一个
                .ToList();

            db.DropCollection(collectionName);

            // 重新插入去重后的数据
            if (uniqueDocs.Any())
            {
                db.GetCollection<DailyValue>(collectionName).InsertBulk(uniqueDocs);
            }
        }
    }


    private static void FixNv(BaseDatabase db)
    {
        var fvCollections = db.GetCollectionNames().Where(c => Regex.IsMatch(c, @"fv_")).ToList();
        foreach (var fv in fvCollections)
        {
            var docs = db.GetCollection(fv).FindAll().ToArray();
            //foreach (var item in docs)
            //{
            //    item["_id"] = item["Date"]["DayNumber"];
            //}
            db.GetCollection(fv).DeleteAll();
            db.GetCollection(fv).Insert(docs.DistinctBy(x => x["Date"].AsDocument["DayNumber"]));
        }

        UpdateManageScale(db);
    }

    private static void RecalcInvestorBalance(BaseDatabase db)
    {
        var ta = db.GetCollection<TransferRecord>().FindAll().ToArray();
        var list = new List<InvestorBalance>();
        foreach (var c in ta.GroupBy(x => x.InvestorId))
        {
            foreach (var tf in c.GroupBy(x => x.FundId))
            {
                var share = tf.Sum(x => x.ShareChange());
                var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
                var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

                var cur = new InvestorBalance { FundId = tf.Key, InvestorId = c.Key, Share = share, Deposit = deposit, Withdraw = withdraw, Date = tf.Max(x => x.ConfirmedDate) };

                list.Add(cur);
            }
        }
        db.GetCollection<InvestorBalance>().DeleteAll();
        db.GetCollection<InvestorBalance>().Insert(list);
    }

    /// <summary>
    /// 由于shareclass 错误，清空TA
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void ClearTaBecauseSCBug(BaseDatabase database)
    {
        using var db = DbHelper.Platform();
        db.GetCollection<TrusteeMethodShotRange>().DeleteMany(x => x.Id.Contains("QueryTransferRequests"));
        db.GetCollection<TrusteeMethodShotRange>().DeleteMany(x => x.Id.Contains("QueryTransferRecords"));
    }

    private static void UpdateInvestorBalance(BaseDatabase db)
    {
        var ta = db.GetCollection<TransferRecord>().FindAll().ToArray();
        var list = new List<InvestorBalance>();
        var list2 = new List<InvestorFundEntry>();
        foreach (var c in ta.GroupBy(x => x.InvestorId))
        {
            foreach (var tf in c.GroupBy(x => x.FundId))
            {
                var share = tf.Sum(x => x.ShareChange());
                var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
                var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

                var cur = new InvestorBalance { FundId = tf.Key, InvestorId = c.Key, Share = share, Deposit = deposit, Withdraw = withdraw, Date = tf.Max(x => x.ConfirmedDate) };

                list.Add(cur);

                share = 0;
                InvestorFundEntry fe = new() { FundId = tf.Key, InvestorId = c.Key };
                foreach (var item in tf.OrderBy(x => x.ConfirmedDate).GroupBy(x => x.ConfirmedDate))
                {
                    var sc = item.Sum(x => x.ShareChange());
                    if (fe.FirstBuy == default && share == 0 && sc > 0)
                    {
                        fe.FirstBuy = item.Key;
                        list2.Add(fe);
                    }
                    else if (fe.FirstBuy != default && fe.SellOut == default && share > 0 && sc + share == 0)
                    {
                        fe.SellOut = item.Key;
                        fe = new() { FundId = tf.Key, InvestorId = c.Key };
                    }
                    share += sc;
                }
            }
        }
        db.GetCollection<InvestorBalance>().DeleteAll();
        db.GetCollection<InvestorBalance>().Insert(list);

        db.GetCollection<InvestorFundEntry>().DeleteAll();
        db.GetCollection<InvestorFundEntry>().Insert(list2);
    }

    /// <summary>
    /// 变更股卡结构
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void UpdateSecurityCard(BaseDatabase db)
    {
        var a = db.GetCollection(nameof(SecurityCard)).FindAll().ToArray();
        foreach (var item in a)
            item["_id"] = item[nameof(SecurityCard.CardNo)];
        db.GetCollection(nameof(SecurityCard)).DeleteAll();
        db.GetCollection(nameof(SecurityCard)).DropIndex(nameof(SecurityCard.CardNo));
        db.GetCollection(nameof(SecurityCard)).Insert(a.DistinctBy(x => x["_id"]));

        var b = db.GetCollection(nameof(SecurityCardChange)).FindAll().ToArray();
        foreach (var item in b)
            item["_id"] = item[nameof(SecurityCardChange.SerialNo)];
        db.GetCollection(nameof(SecurityCardChange)).DeleteAll();
        db.GetCollection(nameof(SecurityCardChange)).DropIndex(nameof(SecurityCard.CardNo));
        db.GetCollection(nameof(SecurityCardChange)).Insert(b.DistinctBy(x => x["_id"]));
    }

    private static void UpdatePolicy(BaseDatabase database)
    {
        database.DropCollection(nameof(PolicyDocument));
    }

    private static void UpdateManageScale(BaseDatabase db)
    {
        db.DropCollection(nameof(DailyManageSacle));
        // 获取所有名称包含"fv_开关"的集合（表）名称
        var fvCollections = db.GetCollectionNames().Where(c => Regex.IsMatch(c, @"fv_\d+$")).ToList();

        Dictionary<DateOnly, decimal> assets = new();
        List<DailyManageSacle> dys = new();

        // 遍历每个符合条件的集合，执行查询并转换结果
        foreach (var collectionName in fvCollections)
        {
            var collection = db.GetCollection<DailyValue>(collectionName);

            dys.AddRange(collection.FindAll().Select(x => new DailyManageSacle(x.Date, x.NetAsset)));

        }

        var result = dys.GroupBy(x => x.Date).Select(x => new DailyManageSacle(x.Key, x.Sum(y => y.Scale)));
        var tmp = dys.Where(x => x.Date.DayNumber > 739400).GroupBy(x => x.Date).ToArray();
        db.GetCollection<DailyManageSacle>().Upsert(result);
    }

    private static void ChangeQuaterlyId(BaseDatabase database)
    {
        database.BeginTrans();
        var dd = database.GetCollection<FundQuarterlyUpdate>().FindAll().ToArray();
        database.GetCollection<FundQuarterlyUpdate>().DeleteAll();
        database.GetCollection<FundQuarterlyUpdate>().InsertBulk(dd);
        database.Commit();
    }

    private static void MiggrateAnnounce(BaseDatabase db)
    {
        var list = db.GetCollection(nameof(FundAnnouncement)).FindAll().ToList();
        foreach (var item in list)
        {
            var normal = FromStorate(item["File"].AsDocument);
            var seal = FromStorate(item["Sealed"].AsDocument);

            item["File"] = BsonMapper.Global.ToDocument(new DualFile
            {
                Label = item["Title"].AsString,
                File = normal,
                Another = seal
            });
        }
        db.GetCollection(nameof(FundAnnouncement)).Update(list);
    }

    private static void MiggrateTradeAccount(BaseDatabase db)
    {
        void move(BsonDocument item, string key, string name)
        {
            item[key] = BsonMapper.Global.ToDocument(new SimpleFile { Label = name, File = FromStorate(item[key].AsDocument) });
        }
        var stock = db.GetCollection(nameof(StockAccount)).FindAll().ToList();
        foreach (var item in stock)
        {
            move(item["Common"].AsDocument, nameof(StockAccount.Common.BankLetter), "银行函");
            move(item["Common"].AsDocument, nameof(StockAccount.Common.ServiceAgreement), "经服协议");
        }
        db.GetCollection(nameof(StockAccount)).Update(stock);

        var future = db.GetCollection(nameof(FutureAccount)).FindAll().ToList();
        foreach (var item in stock)
        {
            move(item["Common"].AsDocument, nameof(FutureAccount.Common.BankLetter), "银行函");
            move(item["Common"].AsDocument, nameof(FutureAccount.Common.ServiceAgreement), "经服协议");
            move(item["Common"].AsDocument, nameof(FutureAccount.Common.AccountLetter), "账户信息函");
        }
        db.GetCollection(nameof(FutureAccount)).Update(future);


    }

    //private static void MiggrateFlow2(BaseDatabase db)
    //{
    //    var list = db.GetCollection(nameof(FundFlow)).Find("_type=\"FMO.Models.RegistrationFlow, FMO.Models\"").ToList();
    //    foreach (var item in list)
    //    {
    //        item[]
    //    }
    //}

    private static void MiggrateFlow(BaseDatabase db)
    {
        void move(BsonDocument item, string key, string name)
        {
            item[key] = BsonMapper.Global.ToDocument(new SimpleFile { Label = name, File = FromStorate(item[key].AsDocument) });
        }
        void single2dual(BsonDocument item, string key, string name)
        {
            item[key] = BsonMapper.Global.ToDocument(new DualFile { Label = name, File = FromStorate(item[key].AsDocument), Another = item.ContainsKey("Sealed" + key) ? FromStorate(item["Sealed" + key].AsDocument) : null });
        }

        void movemul(BsonDocument item, string key, string name)
        {
            if (item is null || !item.ContainsKey(key)) return;
            item[key] = BsonMapper.Global.ToDocument(new MultiFile { Label = name, Files = [.. item[key]["Files"].AsArray.Select(x => FromStorate(x.AsDocument))] });
        }

        if (!db.CollectionExists(nameof(FundFlow) + "_bak"))
            db.GetCollection(nameof(FundFlow) + "_bak").InsertBulk(db.GetCollection(nameof(FundFlow)).FindAll().ToList());

        var list = db.GetCollection(nameof(FundFlow) + "_bak").FindAll().ToList();

        foreach (var item in list)
        {
            var type = Regex.Match(item["_type"].AsString, @"\.(\w+),").Groups[1].Value;

            switch (type)
            {
                case nameof(InitiateFlow):
                    item[nameof(InitiateFlow.ElementFiles)] = BsonMapper.Global.ToDocument(new MultiFile { Label = "基金要素", Files = [.. item[nameof(InitiateFlow.ElementFiles)]["Files"].AsArray.Select(x => FromStorate(x.AsDocument))] });
                    item[nameof(InitiateFlow.ContractFiles)] = BsonMapper.Global.ToDocument(new MultiFile { Label = "基金合同", Files = [.. item[nameof(InitiateFlow.ContractFiles)]["Files"].AsArray.Select(x => FromStorate(x.AsDocument))] });
                    break;


                case nameof(ContractFinalizeFlow):
                    move(item, nameof(ContractFinalizeFlow.ContractFile), "基金合同");
                    move(item, nameof(ContractFinalizeFlow.RiskDisclosureDocument), "风险揭示书");
                    move(item, nameof(ContractFinalizeFlow.CollectionAccountFile), "募集账户函");
                    move(item, nameof(ContractFinalizeFlow.CustodyAccountFile), "托管账户函");
                    break;

                case nameof(ContractModifyFlow):
                    move(item, nameof(ContractFinalizeFlow.ContractFile), "基金合同");
                    move(item, nameof(ContractFinalizeFlow.RiskDisclosureDocument), "风险揭示书");
                    move(item, nameof(ContractFinalizeFlow.CollectionAccountFile), "募集账户函");
                    move(item, nameof(ContractFinalizeFlow.CustodyAccountFile), "托管账户函");

                    movemul(item, nameof(ContractModifyFlow.SupplementaryFile), "补充协议");
                    //item[nameof(ContractModifyFlow.SupplementaryFile)] = BsonMapper.Global.ToDocument(new MultiFile { Label = "补充协议", Files = [.. item[nameof(ContractModifyFlow.SupplementaryFile)]["Files"].AsArray.Select(x => FromStorate(x.AsDocument))] });
                    single2dual(item, nameof(ContractModifyFlow.RegistrationLetter), "备案函");
                    single2dual(item, nameof(ContractModifyFlow.Announcement), "变更公告");
                    single2dual(item, nameof(ContractModifyFlow.CommitmentLetter), "变更承诺函");
                    item[nameof(ContractModifyFlow.SignedSupplementary)] = BsonMapper.Global.ToDocument(new MultiFile { Label = "签署的协议", Files = [FromStorate(item[nameof(ContractModifyFlow.SignedSupplementary)].AsDocument)] });

                    break;

                case nameof(ModifyByAnnounceFlow):
                    single2dual(item, nameof(ModifyByAnnounceFlow.Announcement), "变更公告");
                    break;

                case nameof(SetupFlow):

                    move(item, nameof(SetupFlow.PaidInCapitalProof), "实缴出资证明");
                    single2dual(item, nameof(SetupFlow.EstablishmentAnnouncement), "成立公告");
                    break;

                case nameof(RegistrationFlow):
                    single2dual(item, nameof(RegistrationFlow.CommitmentLetter), "备案承诺函");
                    single2dual(item, nameof(RegistrationFlow.Prospectus), "招募说明书");

                    move(item, nameof(RegistrationFlow.SealedContract), "用印的基金合同");
                    move(item, nameof(RegistrationFlow.SealedAccountOversightProtocol), "募集账户监督协议");
                    move(item, nameof(RegistrationFlow.SealedInvestorList), "投资者明细");


                    single2dual(item, nameof(RegistrationFlow.Prospectus), "招募说明书");
                    single2dual(item, nameof(RegistrationFlow.StructureGraph), "产品结构图");

                    single2dual(item, nameof(RegistrationFlow.NestedCommitmentLetter), "嵌套承诺函");

                    single2dual(item, nameof(RegistrationFlow.RegistrationLetter), "备案函");
                    break;


                case nameof(DividendFlow):

                    single2dual(item, nameof(DividendFlow.Announcement), "分红公告");


                    break;

                default:
                    break;
            }

            item.Remove("CustomFiles");

        }


        db.GetCollection(nameof(FundFlow)).DeleteAll();
        db.GetCollection(nameof(FundFlow)).InsertBulk(list);

        var nnn = db.GetCollection<FundFlow>().Find(x => x.FundId == 6).ToList();
    }

    private static void MiggrateOrderFile(BaseDatabase db)
    {
        if (!db.CollectionExists(nameof(TransferOrder) + "_bak"))
            db.GetCollection(nameof(TransferOrder) + "_bak").InsertBulk(db.GetCollection(nameof(TransferOrder)).FindAll().ToList());

        var list = db.GetCollection(nameof(TransferOrder) + "_bak").FindAll().ToList();
        foreach (var item in list)
        {
            foreach (var (k, v) in item)
            {
                if (v.Type == BsonType.Array)
                {
                    if (v.AsArray.FirstOrDefault() is BsonValue bv && bv.Type == BsonType.Document && bv.AsDocument.Keys.Intersect(["Path", "Time",]).Count() == 2)
                    {
                        var label = v.AsArray.Select(x => x.AsDocument.TryGetValue("Title", out var t) ? t : null).FirstOrDefault()?.AsString;
                        var meta = v.AsArray.Select(x => FromStorate(x.AsDocument)).Where(x => x is not null);
                        item[k] = BsonMapper.Global.ToDocument(new MultiFile { Label = label, Files = [.. meta] });
                    }
                }
                else if (v.Type == BsonType.Document && v.AsDocument.Keys.Intersect(["Path", "Time"]).Count() == 2)
                    item[k] = FromStorate(v.AsDocument) is FileMeta me ? BsonMapper.Global.ToDocument(new SimpleFile { File = me }) : null;
            }
        }

        db.GetCollection(nameof(TransferOrder)).DeleteAll();
        db.GetCollection(nameof(TransferOrder)).Insert(list);


    }

    private static void MiggrateRisk(BaseDatabase db)
    {
        var list = db.GetCollection<RiskAssessment>().FindAll().ToList();
        foreach (var item in list)
        {
            if (item.Path is not null && File.Exists(item.Path))
                item.File = FileMeta.Create(item.Path);
        }
        db.GetCollection<RiskAssessment>().Update(list);
    }

    private static void MiggrateQualification(BaseDatabase db)
    {
        var list = db.GetCollection(nameof(InvestorQualification)).FindAll().ToList();
        if (!db.CollectionExists(nameof(InvestorQualification) + "_bak"))
            db.GetCollection(nameof(InvestorQualification) + "_bak").InsertBulk(list);

        foreach (var item in list)
        {
            foreach (var (k, v) in item)
            {
                if (v.Type == BsonType.Array)
                {
                    if (v.AsArray.FirstOrDefault() is BsonValue bv && bv.Type == BsonType.Document && bv.AsDocument.Keys.Intersect(["Path", "Time", "Hash"]).Count() == 3)
                    {
                        var label = v.AsArray.Select(x => x.AsDocument.TryGetValue("Title", out var t) ? t : null).FirstOrDefault()?.AsString;
                        var meta = v.AsArray.Select(x => FromStorate(x.AsDocument)).Where(x => x is not null);
                        item[k] = BsonMapper.Global.ToDocument(new MultiFile { Label = label, Files = [.. meta] });
                    }
                }
                else if (v.Type == BsonType.Document && v.AsDocument.Keys.Intersect(["Path", "Time", "Hash"]).Count() == 3)
                    item[k] = FromStorate(v.AsDocument) is FileMeta me ? BsonMapper.Global.ToDocument(new SimpleFile { File = me }) : null;
            }
        }

        db.GetCollection(nameof(InvestorQualification)).DeleteAll();
        db.GetCollection(nameof(InvestorQualification)).Insert(list);


    }

    /// <summary>
    /// 迁移投资人中的文件
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void MiggigrateFileInInvestor(BaseDatabase db)
    {
        var list = db.GetCollection(nameof(Investor)).Find("IDCards != null").ToList().Select(x => (Id: x["_id"], IDCards: x["IDCards"].AsArray)).ToList();
        foreach (var (c, cards) in list)
        {
            List<FileMeta> fileMetas = [];
            foreach (var fsi in cards)
            {
                var path = fsi["Path"].AsString;
                var m = Regex.Match(path!, "files.*");
                if (m.Success) path = m.Value;

                if (File.Exists(path))
                    fileMetas.Add(FileMeta.Create(path) with { Time = fsi["Time"].AsDateTime });
            }
            db.GetCollection<InvestorCertifications>().Upsert(new InvestorCertifications { Id = c, Files = fileMetas });
        }


        //var cus = db.GetCollection<Investor>().Query().Where(x => x.IDCards != null).Select(x => new { x.Id, x.IDCards }).ToList().
        //    Select(x => new { x.Id, IDCards = x.IDCards.Select(x => new { x.Name, p = x.Path!, m = Regex.Match(x.Path!, "files.*") }).Select(x => new { x.Name, Path = x.m.Success ? x.m.Value : x.p }) }).ToList();
        //var mig = cus.Select(x => new InvestorCertifications { Id = x.Id, Files = x.IDCards?.Where(x => x.Path?.Length > 5).Select(y => FileMeta.Create(y.Path, y.Name!)).ToList() });
        //db.GetCollection<InvestorCertifications>().DeleteAll();
        //db.GetCollection<InvestorCertifications>().InsertBulk(mig.Where(x => x.Files is not null && x.Files.Count > 0));

    }

    /// <summary>
    /// 迁移InstitutionCertifications
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void MiggrateInstitutionCertifications(BaseDatabase db)
    {
        var list = db.GetCollection(nameof(InstitutionCertifications)).FindAll().ToList();
        if (!db.CollectionExists(nameof(InstitutionCertifications) + "_bak"))
            db.GetCollection(nameof(InstitutionCertifications) + "_bak").InsertBulk(list);

        foreach (var item in list)
        {
            foreach (var (k, v) in item)
            {
                if (v.Type == BsonType.Array)
                {
                    if (v.AsArray.FirstOrDefault() is BsonValue bv && bv.Type == BsonType.Document && bv.AsDocument.Keys.Intersect(["Path", "Time", "Hash"]).Count() == 3)
                    {
                        var label = v.AsArray.Select(x => x.AsDocument.TryGetValue("Title", out var t) ? t : null).FirstOrDefault()?.AsString;
                        var meta = v.AsArray.Select(x => FromStorate(x.AsDocument)).Where(x => x is not null);
                        item[k] = BsonMapper.Global.ToDocument(new MultiDualFile { Label = label, Files = [.. meta.Select(x => new DualFileMeta { Normal = x })] });
                    }
                }
                else if (v.Type == BsonType.Document && v.AsDocument.Keys.Intersect(["Path", "Time", "Hash"]).Count() == 3)
                    item[k] = BsonMapper.Global.ToDocument(FromStorate(v.AsDocument));
            }
        }

        db.GetCollection(nameof(InstitutionCertifications)).DeleteAll();
        db.GetCollection(nameof(InstitutionCertifications)).Insert(list);
    }

    private static FileMeta? FromStorate(BsonDocument? fileStorageInfo)
    {
        if (fileStorageInfo is null) return null;

        if (fileStorageInfo.Keys.Intersect(["Path", "Time"]).Count() != 2)
            return null;

        var path = fileStorageInfo["Path"].AsString;
        var m = Regex.Match(path!, "files.*");
        if (m.Success) path = m.Value;

        if (!File.Exists(path))
        {
            Debug.WriteLine(path);
            return null;
        }

        return FileMeta.Create(path, fileStorageInfo["Name"].AsString)
            with
        { Time = fileStorageInfo["Time"].AsDateTime };
    }



    private static void PlatformTable(BaseDatabase database)
    {
        using var db = DbHelper.Platform();
        foreach (var item in db.GetCollectionNames().Where(x => x.StartsWith("trustee_")))
        {
            db.DropCollection(item);
        }

        var data = db.GetCollection<TrusteeMethodShotRange>().FindAll().ToList();

        var good = data.Where(x => x.Id.Contains('.')).ToList();
        var bad = data.Where(x => !x.Id.Contains(".")).Select(x => new { Id = Regex.Replace(x.Id, "trustee_([a-z]+)Q", "trustee_$1.Q"), Item = x }).ToList();

        var mod = bad.Select(x => new TrusteeMethodShotRange(x.Id, x.Item.Begin, x.Item.End)).ExceptBy(good.Select(x => x.Id), x => x.Id).ToList();

        var bi = bad.Select(x => x.Item.Id).ToList();
        db.GetCollection<TrusteeMethodShotRange>().DeleteMany(x => bi.Contains(x.Id));
        db.GetCollection<TrusteeMethodShotRange>().Upsert(mod);
    }

    //private static void AddManualLink(BaseDatabase db)
    //{ 
    //    var recs = db.GetCollection("ta_record_bak").FindAll().ToList();

    //    db.GetCollection<ManualLinkOrder>().InsertBulk(recs.Select(x => new ManualLinkOrder(x["_id"], x["OrderId"], x["ExternalId"])));
    //}

    private static void Customer2Investor(BaseDatabase db)
    {
        var req = db.GetCollection(nameof(TransferRequest)).FindAll().ToArray();
        foreach (var item in req)
        {
            var modi = item.Keys.Where(x => x.StartsWith("Customer")).ToList();
            foreach (var m in modi)
            {
                item[m.Replace("Customer", "Investor")] = item[m];
            }
        }
        db.GetCollection(nameof(TransferRequest)).Update(req);


        var rec = db.GetCollection(nameof(TransferRecord)).FindAll().ToArray();
        foreach (var item in rec)
        {
            var modi = item.Keys.Where(x => x.StartsWith("Customer")).ToList();
            foreach (var m in modi)
            {
                item[m.Replace("Customer", "Investor")] = item[m];
            }
        }
        db.GetCollection(nameof(TransferRecord)).Update(rec);
    }


    /// <summary>
    /// 修改API的结构，需要重新获取数据
    /// </summary>
    /// <param name="database"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static void ChangeAPIAndClearData(BaseDatabase database)
    {
        // 备份
        ILiteCollection<BsonDocument> creq = database.GetCollection("ta_request_bak");
        creq.DeleteAll();
        creq.InsertBulk(database.GetCollection(nameof(TransferRequest)).FindAll().ToList());

        ILiteCollection<BsonDocument> crec = database.GetCollection("ta_record_bak");
        crec.DeleteAll();
        crec.InsertBulk(database.GetCollection(nameof(TransferRecord)).FindAll().ToList());

        database.DropCollection(nameof(TransferRecord));
        database.DropCollection(nameof(TransferRequest));

        using var db = DbHelper.Platform();
        db.GetCollection<TrusteeMethodShotRange>().DeleteMany(x => x.Id.EndsWith(nameof(ITrustee.QueryTransferRecords)));
        db.GetCollection<TrusteeMethodShotRange>().DeleteMany(x => x.Id.EndsWith(nameof(ITrustee.QueryTransferRequests)));

    }

    /// <summary>
    ///  检查基金是否份额是否为0，如果是这样，最后的ta设为清盘
    /// </summary>
    /// <param name="db"></param>
    private static void UpdateLiqudatingTA(BaseDatabase db)
    {
        // 检查基金是否份额是否为0，如果是这样，最后的ta设为清盘
        var fsr = db.GetCollection<FundShareRecordByTransfer>().FindAll().ToList().OrderByDescending(x => x.Date).GroupBy(x => x.FundId).Select(x => new { FundId = x.Key, Item = x.First() }).Where(x => x.Item.Share == 0);
        foreach (var f in fsr)
        {
            var last = f.Item.Date;
            db.BeginTrans();
            foreach (var item in db.GetCollection<TransferRecord>().Find(x => x.FundId == f.FundId && x.ConfirmedDate == last).ToList())
            {
                if (item.Type != TransferRecordType.Redemption && item.Type != TransferRecordType.ForceRedemption)
                {
                    db.Rollback(); // 异常
                    break;
                }
                item.IsLiquidating = true;
                db.GetCollection<TransferRecord>().Update(item);
            }
            db.Commit();
        }
    }



    private static void ChangeOrderFilePathToRelative(BaseDatabase db)
    {
        //void Modify(FileStorageInfo? fileStorageInfo)
        //{
        //    if (fileStorageInfo?.Path is not null && !fileStorageInfo.Exists && Regex.Match(fileStorageInfo.Path, @"\w:(?:\\\w+\\)+files") is Match m && m.Success && fileStorageInfo.Path.Replace(m.Value, "files") is string newp && File.Exists(newp))
        //        fileStorageInfo.Path = newp;
        //}

        //var orders = db.GetCollection<TransferOrder>().FindAll().ToList();
        //orders.ForEach(o =>
        //{
        //    Modify(o.OrderSheet);
        //    Modify(o.Contract);
        //    Modify(o.RiskDiscloure);
        //    Modify(o.RiskPair);
        //    Modify(o.Videotape);
        //    Modify(o.Review);
        //});
        //db.GetCollection<TransferOrder>().Update(orders);
    }

    private static void RebuildFundShareRecord(BaseDatabase db)
    {
        void UpdateFundShareRecordByTA(ILiteCollection<TransferRecord> table, ILiteCollection<FundShareRecordByTransfer> tableSR, int fundId, DateOnly from = default)
        {
            var old = tableSR.Query().OrderByDescending(x => x.Date).Where(x => x.FundId == fundId && x.Date < from).FirstOrDefault();

            var data = table.Find(x => x.FundId == fundId).OrderBy(x => x.ConfirmedDate).GroupBy(x => x.ConfirmedDate);
            var list = new List<FundShareRecordByTransfer>();
            if (old is not null) list.Add(old);
            foreach (var item in data)
            {
                if (item.Sum(x => x.ShareChange()) is decimal change && change != 0)
                    list.Add(new FundShareRecordByTransfer(fundId, item.First().RequestDate, item.Key, change + (list.Count > 0 ? list[^1].Share : 0)));
            }
            tableSR.DeleteMany(x => x.FundId == fundId && x.Date >= from);
            tableSR.Upsert(list);
        }
        void UpdateInvestorBalance(ILiteCollection<TransferRecord> table, ILiteCollection<InvestorBalance> tableIB, int investorId, int fundId, DateOnly from = default)
        {
            var old = tableIB.Query().OrderByDescending(x => x.Date).Where(x => x.Date < from).FirstOrDefault();
            var data = table.Find(x => x.FundId == fundId && x.InvestorId == investorId && x.ConfirmedDate >= from).GroupBy(x => x.ConfirmedDate).OrderBy(x => x.Key);
            var list = new List<InvestorBalance>();
            if (old is not null) list.Add(old);
            foreach (var tf in data)
            {
                var share = tf.Sum(x => x.ShareChange());
                var deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn or TransferRecordType.SwitchIn or TransferRecordType.TransferIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
                var withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption or TransferRecordType.MoveOut or TransferRecordType.SwitchOut or TransferRecordType.TransferOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

                var last = list.LastOrDefault() ?? new();
                var cur = new InvestorBalance { FundId = fundId, InvestorId = investorId, Share = share + last.Share, Deposit = deposit + last.Deposit, Withdraw = withdraw + last.Withdraw, Date = tf.Key };
                list.Add(cur);
            }

            tableIB.DeleteMany(x => x.FundId == fundId && x.InvestorId == investorId && x.Date >= from);
            tableIB.Upsert(list);
        }

        var funds = db.GetCollection<Fund>().Query().Select(f => f.Id).ToList();

        var t1 = db.GetCollection<TransferRecord>();
        var t2 = db.GetCollection<FundShareRecordByTransfer>();
        var t3 = db.GetCollection<FundShareRecordByDaily>();
        var t4 = db.GetCollection<InvestorBalance>();

        db.BeginTrans();
        try
        {
            t2.DeleteAll();
            t3.DeleteAll();
            t4.DeleteAll();

            foreach (var fid in funds)
            {
                UpdateFundShareRecordByTA(t1, t2, fid);

                var dailyValues = db.GetDailyCollection(fid).Query().Where(x => x.Share > 0).OrderBy(x => x.Date).ToList();
                if (dailyValues.Count > 0) t3.Insert(new FundShareRecordByDaily(fid, dailyValues[0].Date, dailyValues[0].Share));
                for (var i = 1; i < dailyValues.Count; i++)
                {
                    if (dailyValues[i].Share != dailyValues[i - 1].Share)
                        t3.Insert(new FundShareRecordByDaily(fid, dailyValues[i].Date, dailyValues[i].Share));
                }

                foreach (var cid in t1.Query().Select(x => x.InvestorId).ToList().Distinct())
                    UpdateInvestorBalance(t1, t4, cid, fid);

            }
            db.Commit();
        }
        catch { db.Rollback(); }
    }

    private static void FixLogInfo(BaseDatabase database)
    {
        using var db = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");
        db.GetCollection("LogInfo").DeleteMany(x => !x["_id"].IsInt32);
    }

    private static void ChangeAmacAccount2(BaseDatabase database)
    {
        database.GetCollection<AmacReportAccount>().Delete("pmg");

        if (database.GetCollection<AmacReportAccount>().FindById("pfiddirect") is AmacReportAccount a)
        {
            database.GetCollection<AmacReportAccount>().Insert(a with { Id = "pmg" });

            database.GetCollection<AmacReportAccount>().Delete("pfiddirect");
        }
    }

    private static void ChangeAmacAccount(BaseDatabase database)
    {
        var config = database.GetCollection("AmacAccount").FindAll().ToArray();
        foreach (var item in config)
        {
            if (!item.ContainsKey("_type")) continue;
            var str = item["_type"].AsString.Split(',');
            item["_type"] = $"{str[0].Replace("IO.AMAC", "Models")},FMO.Models";
        }


        database.GetCollection("AmacAccount").Update(config);
    }

    private static void ChangeAPIConfig(BaseDatabase database)
    {
        using var db = DbHelper.Platform();
        var config = db.GetCollection("IAPIConfig").FindAll().ToArray();
        foreach (var item in config)
        {
            var idf = item["_id"].AsString.Split('_')[1];
            item["_type"] = $"FMO.Trustee.APIConfig,FMO.Trustee.{idf.ToUpper()}";
        }


        db.GetCollection("IAPIConfig").Update(config);
    }
}
