# Todo 模块 (待办事项系统)

## 概述

Todo 模块实现了系统级的待办事项/提醒管理，支持多种待办类型和自动注册机制。

## 子项目

### Todo 核心库 (src/Todo/Todo/)

**命名空间**: FMO.Todo

**核心接口**:

- **ITodo** - 待办项接口
  - Id, CreateTime, FinishTime
  - JustNotify - 仅通知（无需处理）
  - Status (TotoStatus 枚举)
  - UniqueId - 唯一标识（用于去重/更新）

**核心服务**:

- **TodoService** (static) - 待办服务
  - Register<T>() - 注册新待办（支持 UniqueId 去重）
  - Unregister() - 完成/忽略待办
  - GetAll() - 获取所有未处理待办
  - Initialize() - 从数据库加载待办

**待办类型**:

| 类名 | 说明 |
|------|------|
| FundElementFillTodo | 基金要素补填待办 |
| HugeRedemptionTodo | 大额赎回待办 |
| JustNotifyTodo | 纯通知类待办 |
| PeriodicalUnreportedTodo | 定期未报待办 |
| Todo | 通用待办 |

---

### Todo.UI (src/Todo/Todo.UI/)

待办模块的 UI 层。

**核心类**:
- **TodoViewModel** - 待办列表 ViewModel
- **TodoViewModelFactory** - ViewModel 工厂
- **ViewModel** - 基础 ViewModel

---

### TodoViewModelAutoRegister (src/Todo/TodoViewModelAutoRegister/)

**类型**: Roslyn Source Generator (netstandard2.0)

自动为 ITodo 实现类生成对应的 ViewModel 注册代码。

## 工作机制

1. 各模块通过 TodoService.Register() 创建待办
2. 待办持久化到 base.db 的 ITodo 集合
3. 具有 UniqueId 的待办支持更新（同类新待办覆盖旧待办）
4. 通过 WeakReferenceMessenger 广播待办变化
5. Todo.UI 展示待办列表和处理界面