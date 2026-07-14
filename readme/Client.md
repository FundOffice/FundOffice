# Client 模块

## 概述

Client 是 WPF 桌面客户端入口项目，编译输出为 Thor.exe。负责应用启动、主窗口导航、页面路由及 UI 交互。

**命名空间**: FMO
**目标框架**: net10.0-windows (WPF)
**程序集名**: Thor.exe

## 启动流程

App.xaml.cs 中的启动逻辑：

1. 单例检查（Release模式下通过 Mutex 防止多开）
2. 首次运行检测 → 显示 InitWindow 初始化向导
3. 注册全局异常处理
4. 创建必要的目录结构（data, config, files, plugins）
5. 初始化数据库 (DbHelper.Init)
6. 加载插件 (PluginManager.Init)
7. 延迟加载 (DelayLoader.Load)
8. 显示主窗口 (MainWindow.xaml)

## 目录结构

### Views/

| 目录/文件 | 说明 |
|-----------|------|
| Home/ | 首页视图 |
| FundInfo/ | 基金详情视图 |
| Customer/ | 客户/投资者视图 |
| Manager/ | 管理人视图 |
| Disclosure/ | 信息披露视图 |
| Setting/ | 设置视图 |
| TA/ | TA业务视图 |
| Automations/ | 自动化操作视图 |
| Pages/ | 独立页面（FundsPage, LawPage, PlatformPage, StatementPage） |
| LogView | 日志查看 |
| TrusteeWorkerSettingView | 托管Worker配置 |
| TransferRequestPage | 转让申请页 |
| WorkEvent/ | 工作事项（工作事项列表与详情页） |

### ViewModel/

| 目录/文件 | 说明 |
|-----------|------|
| Elements/ | 基金要素 ViewModel（ElementItemViewModel等） |
| WorkEvent/ | 工作事项 ViewModel（WorkEventViewModel及子类） |

#### Factor 要素 UI 层

基金要素的编辑 UI 由以下组件组成：

| 组件 | 文件 | 说明 |
|------|------|------|
| ModifiableControl | SharedUI | 绑定 FactorModifiableViewModel，单要素编辑（确认/回退/清空按钮） |
| FactorModifiableControl | SharedUI | 绑定 ShareFactorViewModel，多份额要素编辑（拆分/合并按钮） |
| FactorDataTemplates.xaml | Themes/ | 各 ViewModel 类型的 DataTemplate 定义 |
| ElementsView.xaml | Views/FundInfo/ | 基金要素页面，绑定 ElementsViewModel |

**新增要素完整步骤**：

1. **Model**：在 FundFactors.Property.cs 添加属性（选择合适的 FactorItem 类型）
2. **数据结构**：如需复杂类型，在 Models/Fund/Elements/ 下创建 Model 类
3. **ViewModel**：创建 ViewModel 类，实现 `IViewModel<TValue, TViewModel>`，提供 `Trans()` 双向转换
4. **生成器**：ElementsViewModelGenerator 自动生成属性和 FillBy（手写属性需确保类型匹配）
5. **DataTemplate**：在 FactorDataTemplates.xaml 中添加编辑 UI 模板
6. **View 绑定**：在 ELementsView.xaml 中用 ModifiableControl 或 FactorModifiableControl 绑定
| Flow/ | 业务流程 ViewModel（基金设立、合同修改、分红、清算等） |
| Home/ | 首页数据观察 |
| Disclosure/ | 信披 ViewModel |
| ElementInfoViewModel | 要素信息 |
| ShareClassViewModel | 份额类别 |
| UnitViewModel | 单元视图模型 |
| FileViewModel | 文件视图模型 |

#### Factor 要素 UI 层

基金要素的编辑 UI 由以下组件组成：

| 组件 | 文件 | 说明 |
|------|------|------|
| ModifiableControl | SharedUI | 绑定 FactorModifiableViewModel，单要素编辑（确认/回退/清空按钮） |
| FactorModifiableControl | SharedUI | 绑定 ShareFactorViewModel，多份额要素编辑（拆分/合并按钮） |
| FactorDataTemplates.xaml | Themes/ | 各 ViewModel 类型的 DataTemplate 定义 |
| ElementsView.xaml | Views/FundInfo/ | 基金要素页面，绑定 ElementsViewModel |

**新增要素完整步骤**：

1. **Model**：在 FundFactors.Property.cs 添加属性（选择合适的 FactorItem 类型）
2. **数据结构**：如需复杂类型，在 Models/Fund/Elements/ 下创建 Model 类
3. **ViewModel**：创建 ViewModel 类，实现 `IViewModel<TValue, TViewModel>`，提供 `Trans()` 双向转换
4. **生成器**：ElementsViewModelGenerator 自动生成属性和 FillBy（手写属性需确保类型匹配）
5. **DataTemplate**：在 FactorDataTemplates.xaml 中添加编辑 UI 模板
6. **View 绑定**：在 ELementsView.xaml 中用 ModifiableControl 或 FactorModifiableControl 绑定

### Controls/

自定义 WPF 控件：
- BottomUpPanel - 底部弹出面板
- CopyableControl - 可复制控件
- EquityStructureDiagram - 股权结构图
- FileDisplay - 文件展示
- HomePageHeader - 首页头部
- OpenRuleEditor - 开放日规则编辑器
- ValidationItemView - 校验项视图
- WatermarkService - 水印服务

## 工作事项 (WorkEvent)

工作事项模块用于记录和管理日常事务（开户、账户变更、销户、尽调、自查、管理人事务等）。

### 文件位置

- **视图**：`src/Client/Views/WorkEvent/WorkEventPage.xaml`
- **视图模型**：`src/Client/ViewModel/WorkEvent/`
- **领域模型**：`src/Main/Models/WorkEvent/`

### 支持的工作事项类型

| 类型枚举 | 显示名称 | 默认关联 |
|----------|----------|----------|
| `Custom` | 自定义 | 无 |
| `AccountOpening` | 开户 | 基金 |
| `AccountInfoChange` | 账户资料变更 | 基金 + 账户 |
| `AccountCancellation` | 销户 | 基金 + 账户 |
| `AccountOther` | 账户其它 | 基金 + 账户 |
| `DueDiligence` | 尽调 | 无 |
| `SelfInspection` | 自查 | 无 |
| `ManagerAffairs` | 管理人事务 | 无 |

### 主要功能

1. **快捷创建**：顶部工具栏提供开户、变更、销户、其它、尽调、自查、管理人变更、其它等按钮，按钮使用不同颜色区分。
2. **列表与详情**：左侧卡片列表展示状态、类型、标题、标签；右侧详情面板可编辑标题、状态、截止时间、描述、标签。
3. **标签管理**：详情面板使用 HandyControl `Tag` 控件展示标签，支持回车添加、逗号/分号批量添加、去重、点击关闭删除。
4. **关联对象**：
   - 可勾选关联管理人、基金、账户。
   - 选择基金后，账户列表仅显示所选基金下的交易账户。
   - 账户 checkbox 只在已选至少一只基金时显示。
5. **文件与文件夹**：
   - 详情页右侧提供"原始文件夹"和"用印文件夹"按钮。
   - 点击打开 `files\events\{id}\原始文件` / `用印文件`。
   - 支持拖拽文件到按钮区域保存，重名时自动重命名。
6. **状态与类型显示**：状态/类型变更后左侧卡片实时刷新。

## 业务流程 (Flow)

ViewModel/Flow/ 中定义了基金全生命周期的业务流程 ViewModel：

- **FundInitiateFlowViewModel** - 基金设立流程
- **SetupFlowViewModel** - 产品成立流程
- **RegistrationFlowViewModel** - 备案流程
- **ContractModifyFlowViewModel** - 合同变更流程
- **ContractFinalizeFlowViewModel** - 合同终止流程
- **DividendFlowViewModel** - 分红流程
- **LiquidationFlowModel** - 清算流程
- **ModifyByAnnounceFlowViewModel** - 公告变更流程

### 合同 AI 要素解析与对比

`ContractRelatedFlowViewModel`（合同定稿/合同变更的基类）提供 `ParseContractElementsCommand`，调用 AI 解析合同文件中的基金要素：

1. 使用 `FundDocxAiParser` 解析合同 docx，得到 `ReadonlyFundInfo`
2. 解析结果以 `ContractParseRecord`（Id = 文件 MD5）缓存到数据库
3. 自动查找上一个合同流程的解析记录，打开 `ContractElementsCompareWindow` 对比展示
4. 变化的要素以红色标注，显示 "旧值 → 新值"

## 关键 NuGet 包

- CommunityToolkit.Mvvm 8.4.2
- HandyControl 3.5.1
- LiteDB 5.0.21
- OxyPlot.Wpf 2.2.0
- ClosedXML 0.105.0
