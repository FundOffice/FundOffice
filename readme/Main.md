# Main 模块

## 概述

Main 是系统的核心业务层，包含领域模型定义、数据中心、AI 解析能力及代码分析器。

## 子项目

### Models (src/Main/Models/)

领域模型层，定义了整个系统的核心数据结构。

**命名空间**: FMO.Models

**主要模型目录**:

| 目录 | 说明 |
|------|------|
| Fund/ | 基金相关（Fund, FundElements, FundFlow, FundOpenDay, DailyValue等） |
| Investor/ | 投资者相关（Investor, InvestorQualification等） |
| Manager/ | 管理人相关（Manager） |
| TA/ | TA业务（TransferOrder, TransferRequest, TransferRecord等） |
| AMAC/ | 中基协数据模型 |
| Disclosure/ | 信披相关模型（IDisclosureNotice, DisclosureInstance等） |
| Bank/ | 银行相关（BankBalance, BankTransaction等） |
| Reports/ | 报告相关 |
| Setting/ | 设置模型（SettingUnit等） |
| Track/ | 数据追踪 |
| LegalEntity/ | 法人实体 |
| File/ | 文件存储 |
| Algo/ | 算法相关 |

**关键类型**:
- ErrorReturn - 统一的操作结果返回类型
- Days - 交易日计算（内嵌 day.csv 交易日历）
- Messages - 消息定义

**依赖**: SourceGenerator, ModelsGenerator (Analyzer), LiteDB, MoT.Logg

---

### Factor 要素数据模型 (Fund/Elements/)

基金的每个属性（名称、费用、规则等）建模为 **Factor（要素）**，由四元组唯一标识：

> **注意**：`FundElements` 已弃用，保留仅用于 Patch 迁移和 test 兼容。新增要素请添加到 `FundFactors` 中。

```
FundId . FlowId . ShareId . FactorId
```

| 字段 | 含义 |
|------|------|
| `FundId` | 基金 ID |
| `FlowId` | 变更流程编号，按时间递增，用于版本追溯 |
| `ShareId` | 份额标识；`Singleton` 表示全局统一值 |
| `FactorId` | 要素名称，如 FullName、ManageFee，对应 `FactorFields` 常量 |

#### 核心类型

| 类型 | 约束 | 用途 |
|------|------|------|
| `FundFactor<T>` | — | 最小存储单元，对应 LiteDB 一条记录 |
| `SingletonFactorItem<T>` | T : class | 与份额无关的全局统一要素 |
| `SingletonValueFactorItem<T>` | T : struct | 与份额无关的值类型要素（枚举、decimal、DateOnly） |
| `FactorItem<T>` | T : class | **与份额相关**的要素，支持按份额拆分与继承 |
| `ShareClassFactorItem` | — | 特殊 SingletonFactorItem，管理份额类别定义 |

#### 查询与继承规则（FactorItem）

FactorItem 内部按 FlowId **倒序**缓存数据，查询优先级：

1. **精确匹配**：当前 FlowId 中存在目标 ShareId 的记录
2. **Singleton 兜底**：当前 FlowId 仅有一条 Singleton 记录（多份额统一要素）
3. **Inherit 回溯**：按 InheritMap 向上追溯前一个版本的份额值

#### FundFactors 聚合容器

FundFactors（FundFactors.Property.cs）聚合所有要素属性：

```csharp
// 与份额无关
public SingletonFactorItem<FundModeInfo> FundModeInfo { get; private set; }
public SingletonValueFactorItem<RiskLevel> RiskLevel { get; private set; }

// 与份额相关
public FactorItem<FundFeeInfo> ManageFee { get; private set; }
public FactorItem<PerformanceFeeStandard> PerformanceFeeStandard { get; private set; }
```

#### 新增要素（Model 层步骤）

1. 在 FundFactors.Property.cs 添加属性，选择合适的 FactorItem 类型
2. 如需复杂类型，在 Models/Fund/Elements/ 下创建 Model 类
3. FactorFields 中添加对应的常量标识

---

### Data (src/Main/Data/)

数据中心层，实现事件驱动的数据发布/订阅。

**命名空间**: FMO.Utilities

**核心类**:
- **DataHub** - 数据事件总线，使用 [Hookable] 特性标记可订阅的数据类型
- **DataTracker** - 数据变化追踪器
- **FundTipList** - 基金提示列表
- **ThreadSafeList** - 线程安全集合

**Hookable 数据类型** (DataHub 可订阅的事件):
- IEnumerable<TransferOrder> - 批量订单
- IEnumerable<TransferRequest> - 批量申请
- IEnumerable<TransferRecord> - 批量确认
- IEnumerable<DailyValue> - 每日净值
- IDisclosureNotice - 信披报告
- NewDay - 新日期
- EntityChanged<Fund, DateOnly> - 基金实体变化
- FundFlow / EntityRemoved<FundFlow, int> - 资金流
- EntityChanged<FundElements, DateOnly, int> - 基金要素变化
- IEnumerable<FundShareRecordByDaily/ByTransfer> - 份额记录
- IEnumerable<FundOpenDay> - 开放日

---

### AI (src/Main/AI/)

AI 文档解析模块，利用大语言模型解析基金合同等文档。

**支持的 LLM 提供商**: OpenAI, Anthropic, DeepSeek, Google, Qwen, Zhipu, Moonshot, Doubao, Baichuan, MiMo

通过 AIGenerator 自动生成 ViewModel，支持 UI 端配置各提供商的 API Key。

**合同要素缓存**: `ContractParseRecord`（`Models/Fund/Elements/`）以文件 MD5 为主键缓存 `ReadonlyFundInfo` 的 JSON 序列化结果，供 UI 端对比展示使用。

---

### HookableGenerator (src/Main/HookableGenerator/)

Roslyn Source Generator，扫描 [Hookable] 特性标记，为 DataHub 自动生成 Subscribe/Unsubscribe/Publish 方法。

### CustomAnalyzer (src/Main/CustomAnalyzer/)

Roslyn Analyzer，检查 ViewModel 构造函数的规范性，提供 CodeFix 自动修复。
