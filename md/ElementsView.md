# ElementsView 实现说明

## 1. 概述

`ElementsView` 是基金要素编辑/查看页面，位于 `src\Client\Views\FundInfo\ELementsView.xaml`，DataContext 为 `ElementsViewModel`（partial class，手写部分 + 生成器部分）。

---

## 2. 页面布局结构

采用两列 Grid 布局，外层 ScrollViewer 包裹：

```
Grid
+-- 左列 (StackPanel)
|   +-- 基本要素（绿色标签）：全称、简称、基金类型、风险等级、存续期、到期日、运作方式、封闭期、锁定期、基金份额
|   +-- 费用相关（蓝色标签）：管理费、支付方式、认购规则、申购规则、赎回费、业绩报酬、托管机构、外包机构
|   +-- 申赎相关（橙色标签）：开放规则、临时开放、巨额赎回、冷静期、回访
|   +-- 账户信息（棕色标签）：募集账户、托管账户
|
+-- 右列 (StackPanel)
    +-- 投资相关（橙色标签）：止损线、预警线、业绩比较基准、投资目标、投资范围、投资策略、投资经理
```

右上角悬浮编辑/查看切换按钮（ToggleButton）和保存按钮。

---

## 3. ElementsViewModel 架构

`ElementsViewModel` 是 `partial class`，由两部分组成：

### 3.1 手写部分（`ELementsView.xaml.cs`）

- 继承 `ObservableObject`，实现 `IRecipient<ElementChangedBackgroundMessage>`
- 静态枚举数组（供 ComboBox 绑定）：`RiskLevels`、`FundModes`、`FundFeeTypes`、`TrusteeNames` 等
- 手写属性（手动声明的双泛型或特殊类型）：`ManageFee`、`RedemptionFee`、`FundOpenRule`、`ExpirationDate` 等
- 业务逻辑：`OnFlowIdChanged()`（加载数据）、`Save()`、`GenerateBrochure()` 等

### 3.2 生成器部分（`ElementsViewModel.g.cs`）

由 `ElementsViewModelGenerator` 自动生成：
- 未在 `ELementsView.xaml.cs` 中手写但存在于 `FundFactors` 中的属性声明
- `FillBy(FundFactors factors, int flowId)` 方法：从 `FundFactors` 读取数据，初始化所有 ViewModel 属性

---

## 4. 控件模式

### 4.1 `ModifiableControl`（Singleton 因子）

用于 `SingletonFactorItem<T>` / `SingletonValueFactorItem<T>` 类型的属性，与份额无关。

```xml
<shared:ModifiableControl
    Header="基金全称"
    EditTemplate="{StaticResource DT.TextBox.Center}"
    Modifier="{Binding FullName}" />
```

对应 ViewModel 类型：`FactorModifiableViewModel<TValue>` 或 `FactorModifiableViewModel<TValue, TViewModel>`

### 4.2 `FactorModifiableControl`（按份额因子）

用于 `FactorItem<T>` 类型的属性，支持多份额分别编辑。

```xml
<shared:FactorModifiableControl
    Header="管理费"
    EditTemplate="{StaticResource DT.Fee}"
    Modifier="{Binding ManageFee}" />
```

对应 ViewModel 类型：`ShareFactorViewModel<TValue>` 或 `ShareFactorViewModel<TValue, TViewModel>`

### 4.3 默认显示模板

所有控件默认使用 `CopyableTextBlock` 作为只读显示（`ContentTemplate`），编辑时使用 `EditTemplate` 指定的 DataTemplate。

---

## 5. DataTemplate 来源

| 来源 | 文件 | 说明 |
|---|---|---|
| 共享模板 | `src\Client\Themes\FactorDataTemplates.xaml` | 大多数因子使用，通过 `x:Key` 或隐式 `DataType` 匹配 |
| 内联模板 | `ELementsView.xaml` Resources | 页面特有模板（如 `DT.Bank.Display`、`Modifiable.Bank` 样式）|
| 隐式匹配 | WPF 数据类型自动解析 | `DataType="{x:Type local:XxxViewModel}"` 无需 `x:Key`，控件自动应用 |

---

## 6. 生成器协作机制

### 6.1 `ElementsViewModelGenerator`

**输入**：`FundFactors` 类的所有属性（`FactorItem<T>` / `SingletonFactorItem<T>` 等）

**逻辑**：
1. 扫描 `FundFactors` 中所有属性，跳过 `ShareClasses`
2. 识别属性类型：Singleton -> `FactorModifiableViewModel`；非 Singleton -> `ShareFactorViewModel`
3. 检查是否有 `IViewModel<T, VM>` 实现，有则使用双泛型（`isDoubleTemplate = true`）
4. 检查 `ElementsViewModel` 中是否已手写该属性：
   - 已手写且为双泛型 -> 仅生成 `FillBy()` 代码，使用 `VMName.Trans()` 转换
   - 未手写 -> 同时生成属性声明 + `FillBy()` 代码

**输出**：`ElementsViewModel.g.cs`

### 6.2 `IViewModelIncrementalGenerator`

**输入**：实现了 `IViewModel<TValue, TViewModel>` 接口的类

**接口约定**：
```csharp
public interface IViewModel<TValue, TViewModel> : IEquatable<TValue>
    where TViewModel : IViewModel<TValue, TViewModel>
{
    static abstract TValue? Trans(TViewModel vm);   // VM -> Model
    static abstract TViewModel Trans(TValue? vm);   // Model -> VM
}
```

**输出**：为每个 VM 类生成属性映射代码（`{ClassName}.vm.g.cs`），自动从 Model 类同步属性到 VM 类。

### 6.3 `FundFactorsCtorGenerator`

**输入**：`FundFactors.Property.cs` 中声明的属性

**输出**：生成 `FundFactors` 构造函数，初始化所有 FactorItem 实例。

---

## 7. 添加新因子的步骤

1. **Model 层**：在 `FundFactors.Property.cs` 中声明属性（如 `FactorItem<NewType>`）
2. **ViewModel 层**：创建 `NewTypeViewModel` 实现 `IViewModel<NewType?, NewTypeViewModel>`
3. **手写属性**：在 `ELementsView.xaml.cs` 中声明 `ShareFactorViewModel<NewType?, NewTypeViewModel>`
4. **DataTemplate**：在 `FactorDataTemplates.xaml` 中添加 `DataType="{x:Type local:NewTypeViewModel}"` 模板
5. **XAML**：在 `ELementsView.xaml` 中添加 `FactorModifiableControl` 绑定

生成器会自动完成属性注册和 `FillBy()` 代码生成。
