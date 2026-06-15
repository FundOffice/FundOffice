# Disclosure 模块 (信息披露系统)

## 概述

Disclosure 模块实现了私募基金信息披露的自动化管理系统，支持多通道（邮件、PFID、中基协、季度更新等）的信披报告生成、分发和追踪。

## 子项目

### Disclosure 核心库 (src/Disclosure/Disclosure/)

**命名空间**: FMO.Disclosure

**核心类**:

- **DisclosureService** (static partial) - 信披服务核心
  - 管理工作流配置 (DisclosureWorkflow)
  - 管理信披实例队列 (DisclosureInstance)
  - 注册/管理信披通道 (IDisclosureChannel)
  - 创建信批报告 (RegisterNotice)
  - 执行信披任务 (ExecuteDisclosureAsync)
  - 后台 Worker (StartWorker) - 每 60 秒轮询 + 每工作日 8-18 点定时重试

- **DisclosureInstance** - 信披执行实例
  - 状态: Waiting → Processing → Successed/Failed
  - 支持自动重试（失败次数 < 5 次）

- **DisclosureWorkflow** - 信披工作流配置
  - 关联通道和报告类型
  - 配置适用基金范围

- **Enums** - 枚举定义（DisclosureType, DisclosureStatus 等）

**通道实现** (channel/ 目录):

| 通道类 | 说明 |
|--------|------|
| EmailDisclosureChannel | 邮件信披通道 |
| PFIDDisclosureChannel | PFID 平台信披通道 |
| QuarterlyUpdateChannel | 季度更新通道 |

**通道配置** (configs/ 目录):
- AMACChannelConfig - 中基协通道配置
- EmailChannelConfig - 邮件通道配置
- PfidChannelConfig - PFID 通道配置
- CustomChannelConfig - 自定义通道配置
- QuarterlyUpdateChannelConfig - 季度更新通道配置

---

### Disclosure.UI (src/Disclosure/Disclosure.UI/)

信披模块的 UI 层。

**核心类**:
- **ChannelConfigViewModel** - 通道配置 ViewModel
- **ChooseFundWindow** - 基金选择窗口

## 信披类型 (DisclosureType)

支持多种信披报告类型：
- 季度报告
- 半年度报告
- 年度报告
- 季度更新 (QuarterlyUpdate)
- 临时公告 (Temporary)
- 管理人级别 (ManagerLevel)

## 工作流程

1. 系统通过 DataTrigger 或手动操作生成信披报告 (IDisclosureNotice)
2. RegisterNotice() 注册报告到数据库
3. CreateInstance() 为适用的工作流创建执行实例
4. AddToQueue() 将实例加入执行队列
5. LoopOnce() 按通道分组并行执行
6. ExecuteDisclosureAsync() 加载数据 → 校验 → 调用通道发送
7. 失败自动重试（最多 5 次），工作日 8-18 点每小时重新入队