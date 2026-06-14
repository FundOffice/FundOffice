# Schedule 模块 (任务调度系统)

## 概述

Schedule 模块实现了一个通用的后台任务调度框架，支持定时任务和一次性任务，提供任务注册、调度、执行、日志记录和 UI 管理功能。

## 子项目

### Mission (src/Schedule/Mission/)

**命名空间**: FMO.Schedule

任务框架核心库。

**核心类**:

- **Mission** (abstract) - 任务基类
  - 属性: Id, LastRun, NextRun, IsEnabled, IsWorking, IsAborted
  - 方法: OnTime() 定时触发, Work() 执行任务, WorkOverride() 子类实现, SetNextRun() 计算下次执行时间
  - 执行记录持久化到 mission.db

- **OnceMission** (abstract) - 一次性任务基类
  - 属性: IsFinished, Name, Description
  - 完成后自动从调度器中移除

- **MissionSchedule** - 任务调度器，管理所有注册的任务
- **MissionManager** - 任务管理器
- **MissionViewModel** - 任务 UI ViewModel（支持删除、手动设置下次执行、查看日志）
- **MissionRecord** - 任务执行记录
- **MissionMessage** / **MissionWorkMessage** / **MissionFailedMessage** - 消息类型
- **MissionInfoAttribute** - 任务信息标注

---

### Mission.Background (src/Schedule/Mission.Background/)

后台任务的具体实现。

**核心类**:
- **OrderEntryMonitorMission** - 订单录入监控任务
- **SettlementMonitorMission** - 清算监控任务

---

### Mission.Predefined (src/Schedule/Schedule/)

预定义任务集合，包含系统默认的定时任务。

**核心类**:
- BooleanToBrushConverter - UI 转换器

---

### MissionRegisterGenerator (src/Schedule/MissionRegisterGenerator/)

**类型**: Roslyn Source Generator (netstandard2.0)

任务自动注册源码生成器，扫描 Mission 子类并自动生成注册代码。

**生成器类**:
- **MissionRegistrationGenerator** - 任务注册代码生成
- **MissionViewModelInitAnalyzer** - ViewModel 初始化分析器
- **MissionViewModelSyncGenerator** - ViewModel 同步代码生成

## 任务执行流程

1. MissionSchedule 维护任务注册表
2. 定时循环检查任务是否到达 NextRun 时间
3. 调用 Mission.Work() 执行任务
4. Work() 内部调用子类的 WorkOverride()
5. 执行结果写入 MissionRecord
6. 通过 WeakReferenceMessenger 广播状态变化
7. 根据结果计算下次执行时间