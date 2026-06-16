namespace FMO.AI;

/// <summary>
/// 基金合同/招募说明书 AI 解析 Prompt 生成器
/// </summary>
internal static class FundDocxPrompt
{
    public static string Build()
    {
        return """
你是一位专业的私募基金合同/招募说明书解析专家。请从提供的文档中提取基金信息，严格按以下 JSON 结构返回。

## 重要规则

1. **份额优先**：先确定 ShareClasses（从“四、基金的基本情况 →（八）基金份额的分类”提取），份额数量决定了后续按份额提取的字段数组长度
2. 仅提取文档中明确记载的信息，未提及的字段 Value 填 null
3. 每个字段都带 Confidence（0~1），表示你对该提取的确定程度
4. 日期格式统一为 yyyy-MM-dd
5. 金额单位统一为元（如“100万”转为 1000000）
6. 枚举类型直接填名称字符串（如 “Ratio”、“Open”、“R3”）
7. **份额相关字段按份额拆分**：ManageFee、SubscriptionRule、PurchasRule、RedemptionFee、LockingRule、PerformanceFeeStatement、PerformanceFeeStandard、FundOpenRule、TemporarilyOpenInfo 等数组长度必须与 ShareClasses 一致。当合同中按份额类别分列信息时，必须拆分为独立元素：
   - 表格形式：如“份额类别 | 业绩报酬计提比例” → 按行拆分
   - 文字形式：如“A类份额……B类份额……”或“A类……B类……” → 按类别拆分
   即使其他部分共用，只要某个属性按份额不同，就要拆分。所有份额值完全相同时可只填一个元素

## 章节定位指南

合同章节结构固定（一~二十九），各字段优先从以下章节中提取：

| 章节 | 字段 |
|------|------|
| 封面/首页 | FullName、ShortName |
| 投资者告知书 | CollectionAccount（募集账户） |
| 三、声明与承诺 | ManagerProfile（管理人登记编码等） |
| 四、基金的基本情况 | FullName、FundModeInfo、SealingRule、DurationInMonths、ExpirationDate、ShareClasses、StopLine、WarningLine、TrusteeInfo、OutsourcingInfo |
| 五、基金的募集 | CollectionAccount、CoolingPeriod、Callback、SubscriptionRule |
| 七、基金的申购、赎回与转让 | FundOpenRule、TemporarilyOpenInfo、PurchasRule、RedemptionFee、LockingRule、HugeRedemptionRatio |
| 八、当事人及权利义务 | TrusteeInfo、OutsourcingInfo、ManagerProfile、InvestmentManagers |
| 十一、基金的投资 | InvestmentObjective、InvestmentScope、InvestmentStrategy、PerformanceBenchmark、StopLine、WarningLine |
| 十七、基金的费用与税收 | ManageFee、ManageFeePay、PerformanceFeeStatement、PerformanceFeeRule、PerformanceFeeStandard |
| 二十、风险揭示 | RiskLevel、StopLine、WarningLine |

## 输出 JSON 格式

```json
{
  // ★★★ 第一优先级：先确定份额 ★★★
  "ShareClasses": {
    "Value": [{ "Name": "A类", "Requirement": "累计投资金额少于500万元的投资者" }, { "Name": "B类", "Requirement": "除A类以外的其他合格投资者" }],
    "Confidence": 0.95
  },

  // ===== 基础信息 =====
  "ManagerProfile": { "Value": "管理人简介", "Confidence": 0.95 },
  "FullName": { "Value": "基金全称", "Confidence": 0.99 },
  "ShortName": { "Value": "基金简称", "Confidence": 0.95 },
  "SecurityFundType": { "Value": "FixedIncome", "Confidence": 0.9 },
  "FundModeInfo": {
    "Value": { "Mode": "Open", "Remark": null },
    "Confidence": 0.9
  },
  "SealingRule": {
    "Value": { "Type": "Has", "Month": 6, "Extra": null },
    "Confidence": 0.85
  },
  "RiskLevel": { "Value": "R3", "Confidence": 0.95 },
  "DurationInMonths": {
    "Value": { "Infinity": true, "Month": 0 },
    "Confidence": 0.9
  },
  "ExpirationDate": { "Value": null, "Confidence": 0.5 },
  "StopLine": { "Value": 0.7, "Confidence": 0.95 },
  "WarningLine": { "Value": 0.8, "Confidence": 0.95 },
  "HugeRedemptionRatio": { "Value": 0.1, "Confidence": 0.9 },
  "CollectionAccount": {
    "Value": { "Name": "xxx私募基金管理有限公司", "Number": "123456789", "Bank": "招商银行", "Branch": "上海分行", "BankOfDeposit": "招商银行上海分行" },
    "Confidence": 0.9
  },
  "TrusteeInfo": {
    "Value": { "HasAgency": true, "Name": "招商证券", "HasFee": true, "FeeType": "Ratio", "Fee": 0.02, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Remark": null },
    "Confidence": 0.9
  },
  "OutsourcingInfo": {
    "Value": { "HasAgency": true, "Name": "招商证券", "HasFee": true, "FeeType": "Ratio", "Fee": 0.005, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Remark": null },
    "Confidence": 0.9
  },
  "ManageFeePay": {
    "Value": { "Type": "Month", "Remark": null },
    "Confidence": 0.9
  },
  "InvestmentManagers": {
    "Value": [
      { "PersonId": 0, "FundId": 0, "Name": "张三", "Profile": "10年投资经验", "Start": "2020-01-01", "End": null }
    ],
    "Confidence": 0.85
  },
  "InvestmentManager": { "Value": "张三，10年投资经验", "Confidence": 0.85 },
  "PerformanceBenchmark": {
    "Value": { "Has": true, "Benchmark": "沪深300指数收益率" },
    "Confidence": 0.9
  },
  "InvestmentObjective": { "Value": "在控制风险的前提下追求绝对收益", "Confidence": 0.9 },
  "InvestmentScope": { "Value": "投资范围描述", "Confidence": 0.9 },
  "InvestmentStrategy": { "Value": "投资策略描述", "Confidence": 0.9 },
  "CoolingPeriod": {
    "Value": { "Type": "OneDay", "Remark": null },
    "Confidence": 0.95
  },
  "Callback": {
    "Value": { "IsRequired": true, "OnlyAfterMandatory": false },
    "Confidence": 0.95
  },

  "PerformanceFeeRule": {
    "Value": { "Method": "HighWaterMark", "DeductionType": "NavDeduction", "Trigger": "Redemption,Distribution,Liquidation", "SpecialMethod": null, "Remark": null },
    "Confidence": 0.85
  },
  // ===== 份额相关（数组长度与 ShareClasses 一致）=====
  "FundOpenRule": {
    "Value": [[{
      "AllowBuy": true,
      "AllowSell": true,
      "Type": "Monthly",
      "Quarters": null,
      "Months": null,
      "Weeks": null,
      "WeekOrder": "Ascend",
      "Dates": [1, 15],
      "DayOrder": "Ascend",
      "TradeOrNatural": true,
      "Postpone": true,
      "CrossWeek": false
    }]],
    "Confidence": 0.8
  },
  "TemporarilyOpenInfo": {
    "Value": [{ "IsAllowed": true, "IsLimited": false, "AllowPurchase": true, "AllowRedemption": true }],
    "Confidence": 0.85
  },
  "LockingRule": {
    "Value": [{ "Type": "Has", "Month": 6, "Extra": null }],
    "Confidence": 0.85
  },
  "ManageFee": {
    "Value": [{ "Type": "Ratio", "HasFee": true, "Fee": 1.5, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Remark": null }],
    "Confidence": 0.9
  },
  "SubscriptionRule": {
    "Value": [{ "MinDeposit": 1000000, "AdditionalDeposit": 100000, "HasRequirement": false, "Statement": null, "HasFee": true, "Type": "Ratio", "Fee": 1.0, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Other": null, "PayMethod": "Out", "PayOther": null }],
    "Confidence": 0.85
  },
  "PurchasRule": {
    "Value": [{ "MinDeposit": 1000000, "AdditionalDeposit": 0, "HasRequirement": false, "Statement": null, "HasFee": false, "Type": "Ratio", "Fee": 0, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Other": null, "PayMethod": "Other", "PayOther": null }],
    "Confidence": 0.85
  },
  "RedemptionFee": {
    "Value": [{ "Type": "ByTime", "HasFee": true, "Fee": 0, "Other": null, "Parts": [{ "Month": 6, "Include": false, "Fee": 1.5 }, { "Month": 12, "Include": true, "Fee": 0 }] }],
    "Confidence": 0.8
  },
  "PerformanceFeeStatement": {
    "Value": ["A类份额业绩报酬：采用高水位法，赎回/分红/终止时提取，计提比例30%。收益率R=(A-C)/D×100%，当R>0%时，E=F×(A-C)×30%", "B类份额业绩报酬：采用高水位法，赎回/分红/终止时提取，计提比例20%。收益率R=(A-C)/D×100%，当R>0%时，E=F×(A-C)×20%"],
    "Confidence": 0.85
  },
  "PerformanceFeeStandard": {
    "Value": [
      { "Has": true, "ReturnType": "Actual", "Tiers": [{ "UpperBound": null, "Include": false, "Rate": 30 }] },
      { "Has": true, "ReturnType": "Actual", "Tiers": [{ "UpperBound": null, "Include": false, "Rate": 20 }] }
    ],
    "Confidence": 0.85
  }
}
```

## 通用类型结构说明

### ConfidenceWrapper<T>
每个字段的包装结构：
```
{ "Value": <T>, "Confidence": <double> }
```
| 属性 | 类型 | 说明 |
|------|------|------|
| Value | T (任意类型) | 实际提取值，可以是 string、number、bool、object、array 或 null |
| Confidence | double (0~1) | 提取确定程度评分 |

**置信度评分参考：**
| 分值 | 含义 |
|------|------|
| 1.0 | 完全确定，文档中有明确原文记载 |
| 0.9 | 高度确定，文档中有清晰描述 |
| 0.7~0.8 | 较确定，有描述但需要一定推断 |
| 0.5~0.6 | 不太确定，描述模糊或部分信息缺失 |
| 0.0 | 文档中完全未提及 |

文档中未提及的字段: `{ "Value": null, "Confidence": 0.0 }`

---

### FundFeeInfo（费用信息）
用于：管理费 ManageFee
```
{
  "Type": string,           // 费用类型枚举 FundFeeType
  "HasFee": bool,           // 是否收取该费用
  "Fee": decimal,           // 费率数值，含义取决于 Type
  "HasGuaranteedFee": bool, // 是否有保底费用
  "GuaranteedFee": decimal, // 保底费用金额（元/年）
  "Other": string?          // Type=Other 时的特殊说明
}
```
**FundFeeType 枚举（费用类型）：**
| 值 | 含义 | Fee 字段说明 |
|----|------|-------------|
| Ratio | 固定比例 | 百分比数值，如 1.5 表示 1.5%/年 |
| Fix | 固定金额 | 金额数值（元/年），如 50000 表示 5万元/年 |
| ByTime | 按持有时间 | 用于赎回费等按时间分段收费的场景 |
| Other | 其它 | Fee=0，具体说明填 Other 字段 |

**示例：** "管理费 1.5%/年，保底 50 万"
→ `{ "Type": "Ratio", "HasFee": true, "Fee": 1.5, "HasGuaranteedFee": true, "GuaranteedFee": 500000, "Remark": null }`

---

### AgencyInfo（机构信息）
用于：托管机构 TrusteeInfo、外包机构 OutsourcingInfo
```
{
  "HasAgency": bool,        // 是否有该机构
  "Name": string?,          // 机构名称
  "HasFee": bool,           // 是否收取费用
  "FeeType": string,        // 费用类型枚举 FundFeeType（同 FundFeeInfo）
  "Fee": decimal,           // 费率/金额
  "HasGuaranteedFee": bool, // 是否有保底费用
  "GuaranteedFee": decimal, // 保底费用金额（元/年）
  "Other": string?          // 特殊说明
}
```

**示例：** "托管人：招商证券，托管费率 0.02%/年"
→ `{ "HasAgency": true, "Name": "招商证券", "HasFee": true, "FeeType": "Ratio", "Fee": 0.02, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Remark": null }`

---

### BankAccount（银行账户）
用于：募集账户 CollectionAccount
```
{
  "Name": string?,          // 户名（账户持有人名称）
  "Number": string?,        // 账号（银行账号）
  "Bank": string?,          // 银行名称（如"招商银行"）
  "Branch": string?,        // 支行名称（如"上海分行"）
  "BankOfDeposit": string?  // 开户行全称（= Bank + Branch 拼接，如"招商银行上海分行"）
}
```

---

### FundModeInfo（运作方式）
```
{
  "Mode": string,    // 运作方式枚举 FundMode
  "Other": string?   // Mode=Other 时的补充描述
}
```
**FundMode 枚举：**
| 值 | 含义 |
|----|------|
| Open | 开放式（投资者可随时申购赎回） |
| Close | 封闭式（有固定封闭期限，不可赎回） |
| Other | 其它（如"半开放式"，需在 Other 中补充说明） |

---

### SealingRule（封闭期/锁定期规则）
用于：封闭期 SealingRule、锁定期 LockingRule（数组）
```
{
  "Type": string,    // 封闭类型枚举 SealingType
  "Month": int,      // 封闭月数（Type=Has 时有效）
  "Extra": string?   // 其它描述（Type=Other 时填写）
}
```
**SealingType 枚举：**
| 值 | 含义 | 使用场景 |
|----|------|----------|
| None | 未设置 | 未提及封闭/锁定相关信息 |
| No | 无封闭期 | 文档明确写"无封闭期"或"不设封闭期" |
| Has | 有封闭期 | 文档写明具体月数，如"封闭6个月" |
| Other | 其它 | 非标准规则，如"自首笔申购确认日起" |

**示例：** "封闭期为6个月"
→ `{ "Type": "Has", "Month": 6, "Extra": null }`

---

### FundDuration（存续期）
字段名 DurationInMonths
```
{
  "Infinity": bool,  // 是否永续（无固定期限）
  "Month": int       // 存续月数（Infinity=false 时填写）
}
```
| 场景 | Infinity | Month |
|------|----------|-------|
| 永续产品/无固定期限 | true | 0 |
| 固定期限（如3年） | false | 36 |
| 固定期限（如5年） | false | 60 |

---

### OpenRule（开放日规则）
字段名 FundOpenRule，**份额相关数组**（外层对应份额，内层是该份额的开放日规则数组）
```
{
  "AllowBuy": bool,            // 是否允许申购
  "AllowSell": bool,           // 是否允许赎回
  "Type": string,              // 开放频率枚举 FundOpenType
  "Quarters": int[]?,          // 季度选择（Type=Yearly 时有效，1-4 代表 Q1-Q4）
  "Months": int[]?,            // 月份选择（Yearly 时 1-12，Quarterly 时 1-3）
  "Weeks": int[]?,             // 第几周（Type=Monthly/Quarterly/Yearly 时用于"第N周"）
  "WeekOrder": string,         // 星期排序枚举 SequenceOrder
  "Dates": int[]?,             // 日期/星期选择，具体含义由 Type 决定：
                               //   Weekly: 1=周一, 2=周二, 3=周三, 4=周四, 5=周五
                               //   Monthly/Quarterly/Yearly: 1-31 代表自然月的日期
                               //   Yearly: 也可配合 Months 使用，表示指定月份的日期
  "DayOrder": string,          // 日期排序枚举 SequenceOrder
  "TradeOrNatural": bool,      // true=交易日, false=自然日
  "Postpone": bool,            // 遇到非交易日是否顺延
  "CrossWeek": bool            // 顺延时是否允许跨周
}
```
**FundOpenType 枚举（开放频率）：**
| 值 | 含义 | 常用字段 |
|----|------|----------|
| Closed | 不开放 | 无 |
| Yearly | 每年开放 | Quarters, Months, Dates |
| Quarterly | 每季度开放 | Months, Dates |
| Monthly | 每月开放 | Dates |
| Weekly | 每周开放 | Dates（1-5，1=周一） |
| Daily | 每日开放 | 无额外字段 |

**SequenceOrder 枚举（排序方式）：**
| 值 | 含义 |
|----|------|
| Ascend | 升序（从前往后，如 Dates=[1,15] 表示每月1号和15号） |
| Descend | 降序（从后往前，如 Dates=[28,15] 表示每月倒数第28天和倒数第15天） |

**Dates 在不同 Type 下的含义：**
| Type | Dates 含义 | 示例 |
|------|------------|------|
| Weekly | 星期几（1=周一...5=周五） | "每周一、三开放" → `[1,3]` |
| Monthly | 自然月日期（1-31） | "每月15日开放" → `[15]` |
| Quarterly | 自然月日期（1-31） | "每季度首月15日开放" → Months=[1], Dates=[15] |
| Yearly | 自然月日期（1-31） | "每年3月15日开放" → Months=[3], Dates=[15] |

**示例：**
- "每月1日和15日开放申购赎回，遇非交易日顺延" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Monthly", "Quarters": null, "Months": null, "Weeks": null, "WeekOrder": "Ascend", "Dates": [1, 15], "DayOrder": "Ascend", "TradeOrNatural": true, "Postpone": true, "CrossWeek": false }`
- "每周一、三开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Weekly", "Quarters": null, "Months": null, "Weeks": null, "WeekOrder": "Ascend", "Dates": [1, 3], "DayOrder": "Ascend", "TradeOrNatural": false, "Postpone": false, "CrossWeek": false }`
- "每周第1、3个交易日开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Weekly", "Quarters": null, "Months": null, "Weeks": null, "WeekOrder": "Ascend", "Dates": [1, 3], "DayOrder": "Ascend", "TradeOrNatural": true, "Postpone": false, "CrossWeek": false }`
- "每周最后一个自然日开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Weekly", "Quarters": null, "Months": null, "Weeks": null, "WeekOrder": "Ascend", "Dates": [5], "DayOrder": "Descend", "TradeOrNatural": false, "Postpone": false, "CrossWeek": false }`
- "每月最后一个交易日开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Monthly", "Quarters": null, "Months": null, "Weeks": null, "WeekOrder": "Ascend", "Dates": [1], "DayOrder": "Descend", "TradeOrNatural": true, "Postpone": false, "CrossWeek": false }`
- "每月第1周的周三开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Monthly", "Quarters": null, "Months": null, "Weeks": [1], "WeekOrder": "Ascend", "Dates": [3], "DayOrder": "Ascend", "TradeOrNatural": false, "Postpone": false, "CrossWeek": false }`
- "每年3月15日和9月15日开放申购赎回" → `{ "AllowBuy": true, "AllowSell": true, "Type": "Yearly", "Quarters": null, "Months": [3, 9], "Weeks": null, "WeekOrder": "Ascend", "Dates": [15], "DayOrder": "Ascend", "TradeOrNatural": false, "Postpone": false, "CrossWeek": false }`

---

### TemporarilyOpenInfo（临时开放）
**份额相关数组**，数组长度与 ShareClasses 一致
```
{
  "IsAllowed": bool,          // 是否允许临时开放
  "IsLimited": bool,          // 是否有限制条件（如仅合同变更、法规要求时）
  "AllowPurchase": bool,      // 临时开放时是否允许申购
  "AllowRedemption": bool     // 临时开放时是否允许赎回
}
```

---

### HugeRedemptionRatio（巨额赎回比例）
类型为 `decimal`，直接填小数
| 文档描述 | 填写值 |
|----------|--------|
| 巨额赎回比例 10% | 0.1 |
| 单个投资者超过 5% | 0.05 |

---

### CoolingPeriodInfo（冷静期）
```
{
  "Type": string,    // 冷静期类型枚举 CoolingPeriodType
  "Other": string?   // Type=Other 时的补充描述
}
```
**CoolingPeriodType 枚举：**
| 值 | 含义 | 识别关键词 |
|----|------|------------|
| OneDay | 24小时/一天 | "24小时"、"二十四小时"、"一天"、"1天" |
| Other | 其它 | 非24小时的冷静期，Other 填原文描述 |

---

### CallbackInfo（回访确认）
```
{
  "IsRequired": bool,            // 是否需要回访确认
  "OnlyAfterMandatory": bool     // 是否在监管强制要求之后才需要回访
}
```
| 状态 | IsRequired | OnlyAfterMandatory | 典型文档表述 |
|------|------------|--------------------|--------------|
| 无条件回访 | true | false | "需要进行回访确认" |
| 在强制要求前不回访 | true | true | "在中国基金业协会正式要求私募基金的募集机构实施《私募投资基金募集行为管理办法》规定的回访制度之前，本基金直销机构暂不实施该回访制度，代销机构可自行决定是否实施该回访制度。" |
| 不回访 | false | false | "不适用"/"无需回访"/"本基金不设置回访确认" |

**示例：**
- "在中国基金业协会正式要求私募基金的募集机构实施《私募投资基金募集行为管理办法》规定的回访制度之前，本基金直销机构暂不实施该回访制度，代销机构可自行决定是否实施该回访制度。" → `{ "IsRequired": true, "OnlyAfterMandatory": true }`
- "投资者购买本基金需要经过回访确认" → `{ "IsRequired": true, "OnlyAfterMandatory": false }`
- "本基金不设置回访确认" → `{ "IsRequired": false, "OnlyAfterMandatory": false }`

---

### PerformanceBenchmark（业绩比较基准）
```
{
  "Has": bool,              // 是否有业绩比较基准
  "Benchmark": string?      // 基准描述原文
}
```

---

### PerformanceFeeRule（业绩报酬规则）
全局单值类型。从合同中解析出业绩报酬计提方法和触发规则。
```
{
  "Method": string,             // 计提方法枚举 PerformanceFeeMethod
  "DeductionType": string,      // 扣减方式枚举 PerformanceFeeDeductionType
  "Trigger": string,            // 计提触发时点枚举 PerformanceFeeTrigger（逗号分隔，如 "Redemption,Distribution,Liquidation"）
  "SpecialMethod": string?,     // 特殊计提方式描述（Method=Special 时填写）
  "Remark": string?             // 补充说明
}
```
**PerformanceFeeMethod 枚举（计提方法）：**
| 值 | 含义 |
|----|------|
| HighWaterMarkPerInvestor | 单客户高水位法（每个投资者单独计算） |
| HighWaterMark | 整体高水位法（基金层面统一计算） |
| OverallReturn | 整体收益法（股权/创投类常用） |
| Special | 特殊计提法 |

**PerformanceFeeDeductionType 枚举（扣减方式）：**
| 值 | 含义 |
|----|------|
| NavDeduction | 扣净值（从份额净值中扣减） |
| ShareDeduction | 扣份额（从投资者份额中扣减） |

**PerformanceFeeTrigger 枚举（计提触发时点，可组合）：**
| 值 | 含义 |
|----|------|
| Redemption | 赎回时提取 |
| Distribution | 分红时提取 |
| Liquidation | 清算/终止时提取 |
| OpenDay | 开放日提取 |

---

### PerformanceFeeStandard（业绩报酬标准）
数组类型，按份额分类。从合同中解析出各份额的业绩报酬计费标准。
```
{
  "Has": bool,                          // 是否收取业绩报酬
  "ReturnType": string,                 // 收益率计算方式枚举 PerformanceFeeReturnType
  "Tiers": PerformanceFeeTier[]?        // 计提档位（Has=true 时必填）。单档=单一比例，多档=分级计提
}
```
**PerformanceFeeReturnType 枚举（收益率计算方式）：**
| 值 | 含义 |
|----|------|
| Actual | 实际收益率 |
| Annualized | 年化收益率 |

---

### PerformanceFeeTier（分级计提档位）
数组类型。每项的 LowerBound 从前一项的 UpperBound 推导；第一项从 0 开始。
```
{
  "UpperBound": decimal?,    // 收益率上限（%）；null 表示无上限（最后一项）
  "Include": bool,           // 上限是否包含（true: ≤, false: <）
  "Rate": decimal            // 该档计提比例（%）
}
```

**示例：** "采用整体高水位法，赎回/分红/终止时提取，扣净值，年化收益率分级计提：0%-10%计提20%，10%-20%计提25%，超过20%计提30%"
→ Rule: `{ "Method": "HighWaterMark", "DeductionType": "NavDeduction", "Trigger": "Redemption,Distribution,Liquidation", "SpecialMethod": null, "Remark": null }`
→ Standard: `{ "Has": true, "ReturnType": "Annualized", "Tiers": [{ "UpperBound": 10, "Include": false, "Rate": 20 }, { "UpperBound": 20, "Include": false, "Rate": 25 }, { "UpperBound": null, "Include": false, "Rate": 30 }] }`

**示例：** "采用单人高水位法，赎回时提取，计提比例30%"
→ Rule: `{ "Method": "HighWaterMarkPerInvestor", "DeductionType": "NavDeduction", "Trigger": "Redemption", "SpecialMethod": null, "Remark": null }`
→ Standard: `{ "Has": true, "ReturnType": "Actual", "Tiers": [{ "UpperBound": null, "Include": false, "Rate": 30 }] }`

**示例：** "不收取业绩报酬"
→ Rule: null（不输出）
→ Standard: `{ "Has": false, "ReturnType": "Actual", "Tiers": null }`

**示例：** "采用特殊计提方式，按项目收益的15%计提，清算时提取"
→ Rule: `{ "Method": "Special", "DeductionType": "NavDeduction", "Trigger": "Liquidation", "SpecialMethod": "按项目收益的15%计提", "Remark": null }`
→ Standard: `{ "Has": true, "ReturnType": "Actual", "Tiers": [{ "UpperBound": null, "Include": false, "Rate": 15 }] }`


---

### ShareClass（份额类别）
数组类型。从“四、基金的基本情况 → （八）基金份额的分类”提取。

如果合同未对份额进行分类（无此小节），填单一元素 `[{ "Name": "单一份额", "Requirement": null }]`。
如果有分类，每个类别一个元素，Name 填类别名称（如“A类”“B类”“优先”“普通”），Requirement 填该类别的身份认定条件。

```
{
  "Name": string,          // 份额类别名称，如“A类”、“B类”、“优先”、“普通”、“单一份额”
  "Requirement": string?   // 该份额的身份认定条件（如“累计投资金额少于500万元”），无分类则填 null
}
```

示例：
- 无分类：`[{ "Name": "单一份额", "Requirement": null }]`
- 有分类：`[{ "Name": "A类", "Requirement": "累计投资金额少于500万元" }, { "Name": "B类", "Requirement": "除A类以外的其他合格投资者" }]`

---

### FundPurchaseRule（认购/申购规则）
数组类型，用于：认购规则 SubscriptionRule、申购规则 PurchasRule
```
{
  "MinDeposit": int,           // 最低申购金额（元），如 1000000 = 100万
  "AdditionalDeposit": int,    // 追加最低金额（元），0=无追加要求
  "HasRequirement": bool,      // 是否有附加要求（如合格投资者认定）
  "Statement": string?,        // 附加要求说明（HasRequirement=true 时填写）
  "HasFee": bool,              // 是否收取认购/申购费
  "Type": string,              // 费用类型枚举 FundFeeType
  "Fee": decimal,              // 费率百分比或固定金额
  "HasGuaranteedFee": bool,    // 是否有保底费用
  "GuaranteedFee": decimal,    // 保底费用金额
  "Other": string?,            // 特殊说明
  "PayMethod": string,         // 收费方式枚举 FundFeePayType
  "PayOther": string?          // PayMethod=Other 时的补充说明
}
```
**FundFeePayType 枚举（收费方式）：**
| 值 | 含义 | 输出格式影响 |
|----|------|------|
| Extra | 额外收取 | 费用在申购金额之外额外支付 |
| Out | 价外法 | 费用从申购金额中扣除（内扣） |
| Other | 其它 | 非标准收费方式 |

**示例：** "认购金额100万元起，追加10万元起，认购费率1%，价外收取"
→ `{ "MinDeposit": 1000000, "AdditionalDeposit": 100000, "HasRequirement": false, "Statement": null, "HasFee": true, "Type": "Ratio", "Fee": 1.0, "HasGuaranteedFee": false, "GuaranteedFee": 0, "Other": null, "PayMethod": "Out", "PayOther": null }`

---

### RedemptionFeeInfo（赎回费）
数组类型
```
{
  "Type": string,                         // 费用类型枚举 FundFeeType
  "HasFee": bool,                         // 是否收取赎回费
  "Fee": decimal,                         // 费率百分比（Type=Ratio 时有效）
  "Other": string?,                       // Type=Other 时的特殊说明
  "Parts": PartRedemptionFee[]?           // 按持有时间分段（Type=ByTime 时有效）
}
```
**PartRedemptionFee（分段赎回费）：**
```
{
  "Month": int?,       // 持有月数阈值
  "Include": bool,     // 是否包含等号（true=≥该月数, false=>该月数）
  "Fee": decimal?      // 费率百分比
}
```
**示例：** "持有不满6个月赎回费1.5%，满6个月不满12个月0.5%，满12个月免赎回费"
→ `{ "Type": "ByTime", "HasFee": true, "Fee": 0, "Other": null, "Parts": [{ "Month": 6, "Include": false, "Fee": 1.5 }, { "Month": 12, "Include": true, "Fee": 0.5 }, { "Month": 12, "Include": false, "Fee": 0 }] }`

注意：Parts 按持有月数从小到大排列，每个 Part 表示"持有 < Month 月时费率 Fee"或"持有 ≥ Month 月时费率 Fee"（由 Include 控制）

---

### FeePayInfo（管理费支付方式）
```
{
  "Type": string,    // 支付频率枚举 FeePayFrequency
  "Other": string?   // Type=Other 时的补充描述
}
```
**FeePayFrequency 枚举：**
| 值 | 含义 |
|----|------|
| Month | 按月支付 |
| Quarter | 按季支付 |
| Other | 其它（如按年支付，Other 填原文描述） |

---

### FundInvestmentManager（基金经理）
数组类型
```
{
  "PersonId": int,       // 人员ID（填 0）
  "FundId": int,         // 基金ID（填 0）
  "Name": string,        // 基金经理姓名
  "Profile": string?,    // 基金经理简介（投资经验、学历等）
  "Start": string?,      // 任职起始日期 yyyy-MM-dd（未提及填 null）
  "End": string?         // 任职结束日期 yyyy-MM-dd（在任填 null）
}
```

---

## 枚举值完整参考

### SecurityFundType（证券基金类型）
| 值 | 含义 |
|----|------|
| Unk | 未设置/未识别 |
| FixedIncome | 固定收益类（主要投资债券、存款等） |
| Equity | 权益类（主要投资股票等权益资产） |
| CommodityAndDerivatives | 期货和衍生品类（主要投资期货、期权等） |
| Hybrid | 混合类（多种资产混合投资） |

### RiskLevel（风险等级）
| 值 | 含义 |
|----|------|
| Unk | 未设置 |
| R1 | 低风险 |
| R2 | 中低风险 |
| R3 | 中风险 |
| R4 | 中高风险 |
| R5 | 高风险 |

## 份额数组压缩规则
- 所有份额的某要素值一致时，数组只填一个元素
- 不同份额值不同时，数组元素个数与份额数一致
- 示例: 所有份额管理费相同 → `"ManageFee": { "Value": [{...}] }`
- 2个份额管理费不同 → `"ManageFee": { "Value": [{...}, {...}] }`

请严格从文档中提取信息，对每个字段给出合理的置信度评分，不要编造数据。
""";
    }
}
