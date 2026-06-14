# Trigger 模块 (数据触发器/规则引擎)

## 概述

Trigger 模块实现了基于数据变化的规则触发机制。当 DataHub 中的数据事件发生时，自动执行预定义的规则检查，生成待办、信披报告或后台任务。

## 子项目

### DataTrigger (src/Trigger/DataTrigger/)

**命名空间**: FMO.Trigger

规则触发器的具体实现。通过 HookGenerator 源码生成器自动生成 Observer 代码。

**已实现的触发规则**:

| 规则类 | 说明 |
|--------|------|
| FundClearDateMissingRule | 基金清算日期缺失检查 |
| FundClearNotFinishedRule | 基金清算未完成检查 |
| FundDailyMissingRule | 基金每日数据缺失检查 |
| FundNearLiquidationAlertRule | 基金临近清算预警 |
| FundOverdueRule | 基金逾期检查 |
| FundScaleWarnRule | 基金规模预警 |
| FundSettlementMonitor | 基金清算监控 |
| FundSharePairRule | 基金份额配对规则 |
| FundStopPurchaseRule | 基金暂停申购规则 |
| HugeRedemptionMonitor | 大额赎回监控 |
| OrderValueIsWellMonitor | 订单金额异常监控 |
| PeriodicalUnreportedMonitor | 定期未报监控 |
| RequestMissingMonitor | 申请缺失监控 |

---

### HookGenerator (src/Trigger/HookRegister/)

**类型**: Roslyn Source Generator (netstandard2.0)

**命名空间**: TriggerGenerators

Hook 代码生成器，自动为触发规则生成：

- **ObserverGenerator** - 为标记的规则类生成 DataHub Observer 订阅代码
- **VerifySettingUnitSourceGenerator** - 为校验规则生成 SettingUnit 注册代码

## 工作原理

1. 开发者定义规则类，继承特定基类或标注特性
2. HookGenerator 在编译时扫描规则类
3. 自动生成 Observer 代码，订阅 DataHub 中对应的数据类型
4. 当 DataHub 发布数据事件时，Observer 自动调用规则检查
5. 规则触发后通过 TodoService 注册待办，或创建信披报告