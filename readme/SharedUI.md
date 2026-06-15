# SharedUI 模块 (共享UI组件库)

## 概述

SharedUI 提供跨模块复用的 WPF UI 组件、控件和转换器，被 Client 和各业务模块的 UI 层引用。

**命名空间**: FMO.Shared
**目标框架**: net10.0-windows (WPF)

## 核心组件

### 通用控件

| 类名 | 说明 |
|------|------|
| AbbreviationText | 缩略文本控件 |
| CopyableAttach | 可复制附加属性 |
| CopyableTextblock | 可复制 TextBlock |
| CopyableControl | 可复制控件 |
| HeaderWrapPanel | 头部换行面板 |
| YearCalender | 年历控件 |
| BooleanDate | 布尔日期控件 |
| RenameWindow | 重命名窗口 |
| DailyReportGridView | 日报网格视图 |
| MaskService | 遮罩服务 |

### 可编辑模块 (Editable/)

| 类名 | 说明 |
|------|------|
| ModifiableControl | 可修改控件 |
| ModifiableViewModel | 可修改 ViewModel |
| FactModifiableViewModel | 实际可修改 ViewModel |

#### Factor ViewModel 类型体系

要素编辑的 ViewModel 层，提供三值追踪（OldValue / NewValue / FallbackValue）和自动持久化：

```
ModifiableViewModel<TValue>                        ← 基类：三值追踪、变更状态机
├── FactorModifiableViewModel<TValue>              ← 单份额要素，LiteDB Upsert 持久化
└── FactorModifiableViewModel<TValue, TViewModel>  ← 双模板版本，通过 TViewModel.Trans() 转换

ShareFactorViewModel<TValue>                       ← 多份额容器（ObservableCollection<Data>）
└── ShareFactorViewModel<TValue, TViewModel>       ← 双模板版本
```

| 属性/方法 | 说明 |
|-----------|------|
| OldValue | 数据库中的当前值（基准） |
| NewValue | 用户编辑中的值 |
| FallbackValue | 继承值（前一版本 / 上层份额的值） |
| CanConfirm | NewValue != OldValue 时可提交 |
| CanReset | 有未保存修改时可回退 |
| CanClear | 可清空回继承值 |
| ChangeKind | None / Added / Modified / Deleted |
| IsInherited | 当前值是否继承自 FallbackValue |

#### IViewModel 接口

```csharp
public interface IViewModel<TValue, TViewModel> : IEquatable<TValue>
    where TViewModel : IViewModel<TValue, TViewModel>
{
    static abstract TValue? Trans(TViewModel vm);    // ViewModel -> Model
    static abstract TViewModel Trans(TValue? vm);    // Model -> ViewModel
}
```

每个复杂类型的 ViewModel 必须实现此接口，提供 Model 与 ViewModel 双向转换。

#### 分割与合并（ShareFactorViewModel）

多份额要素支持 **拆分**（Divide）和 **合并**（Unify）：

- **拆分**：将统一的 Singleton 值拆为每个份额独立的值
- **合并**：将所有份额的值统一为一个 Singleton 值

#### 源生成器映射（ElementsViewModelGenerator）

| FundFactors 属性类型 | 生成的 ViewModel 属性类型 |
|----------------------|---------------------------|
| SingletonFactorItem<T> | FactorModifiableViewModel<T?> |
| SingletonValueFactorItem<T> | FactorModifiableViewModel<T?> |
| FactorItem<T> | ShareFactorViewModel<T?> |
| 有 IViewModel 的类型 | 追加第二泛型参数 TViewModel |

手写属性注意事项：

- 属性类型必须使用正确的 ViewModel 类型
- 生成器检查手写属性的类型，仅匹配时才纳入 FillBy
- IViewModel 第一个泛型参数带 ? 时，可能与 FundFactors 中的类型名不匹配

### 文件控件 (FileCtrl/)

| 类名 | 说明 |
|------|------|
| FileViewModel | 文件视图模型 |
| SimpleFileView | 简单文件视图 |
| DelayMouseOverExpandBehavior | 延迟悬停展开行为 |

### 网格过滤 (GridFilter/)

| 类名 | 说明 |
|------|------|
| GridFilter | 网格过滤器 |
| FilterColumn | 过滤列定义 |

### 转换器

| 类名 | 说明 |
|------|------|
| BooleanToVisibilityReverseConverter | 布尔转可见性（反向） |
| ObjectDesplayConverter | 对象显示转换器 |

### 特性标记

| 类名 | 说明 |
|------|------|
| AutoChangeableViewModelAttribute | 自动可变更 ViewModel 标记 |

## 依赖

- CommunityToolkit.Mvvm 8.4.2
- HandyControl 3.5.1
- Models, Utilities
