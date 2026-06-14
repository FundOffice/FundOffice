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

### ViewModel/

| 目录/文件 | 说明 |
|-----------|------|
| Elements/ | 基金要素 ViewModel（ElementItemViewModel等） |

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

## 关键 NuGet 包

- CommunityToolkit.Mvvm 8.4.2
- HandyControl 3.5.1
- LiteDB 5.0.21
- OxyPlot.Wpf 2.2.0
- ClosedXML 0.105.0
