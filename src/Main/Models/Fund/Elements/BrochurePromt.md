# 角色设定
你是一位资深的前端工程师和 UI 设计师，精通 HTML/CSS/JS 页面重构、响应式布局、打印样式优化以及数据驱动视图的开发。

# 任务目标
请为我生成一个高质量的、数据驱动的基金宣传册（Brochure）HTML/CSS/JS 模板片段。该模板将用于展示 `BrochureFactor` 数据。

# 运行环境与三视图切换规范（核心痛点解决）
模板会被嵌入到一个外层包裹容器中，该容器会通过 JS 动态切换 `.mode-original`、`.mode-a4` 或 `.mode-wide` 类名来实现视图切换。**为防止切换失效，请严格遵守以下 CSS 编写铁律：**
1. **CSS 选择器必须挂载在父级模式下**：所有布局控制样式必须带有父级类名前缀（如 `.mode-original .brochure-container`），**严禁**使用 `@media` 屏幕宽度查询来替代视图模式切换。
2. **绝对禁止内部写死宽度**：模板内部的任何区块、表格、卡片、Flex/Grid 子项，**严禁使用固定的 `width: XXXpx`**！必须使用 `width: 100%`、`max-width`、`flex: 1` 或 `1fr`，确保内容能随外层容器自由缩放。
3. **三种模式的严格定义**：
   - **`.mode-original` (原始尺寸)**：根容器 `max-width: 1000px; width: 100%; margin: 0 auto;`，保留阴影和圆角。
   - **`.mode-a4` (A4纸张)**：根容器强制 `width: 210mm; min-height: 297mm; padding: 15mm; box-shadow: none; border-radius: 0; background: #fff;`。内部元素尺寸建议使用 `%` 或 `mm`，强化 `break-inside: avoid;` 防截断。
   - **`.mode-wide` (多折页)**：利用 CSS Grid 将垂直堆叠的 Section 转为横向多列。例如：`.mode-wide .brochure-main { display: grid; grid-template-columns: repeat(2, 1fr); gap: 20px; }`。子元素必须设置 `width: 100%` 以填满网格列。

# 布局与 CSS 核心规范（严格遵守）
1. **基础重置**：使用 `box-sizing: border-box`，清除默认 margin/padding。
2. **防截断保护**：所有重要的块级元素（如 `.brochure-section`, `.brochure-manager`, `.brochure-table`）必须添加 `break-inside: avoid; page-break-inside: avoid;`。
3. **多折页兼容**：模板内容**绝对不能**依赖固定的绝对高度（如 `height: 500px`），必须允许内容自然撑开。背景色或背景图应能自然延伸，配合系统外层的“动态高度补长”机制。
4. **打印优化**：避免使用会导致打印空白或渲染异常的复杂 CSS 滤镜（如 `filter: blur`）或极端的负 margin。使用 `@media print` 确保打印时背景色保留（`-webkit-print-color-adjust: exact`）。
5. **UI 设计**：风格需专业、金融感、简洁大气。类名需语义化（推荐 BEM 命名规范，如 `.brochure-section__title`）。
6. **底部声明**：在模板最底部必须包含一个“风险提示与免责声明”区块，内容严格如下：
   1、本宣传资料仅为产品信息展示，不构成任何投资邀约、推介或交易要约； 
   2、私募基金存在市场风险、流动性风险、信用风险等多重风险，过往业绩不代表未来收益； 
   3、投资者应当具备相应风险识别能力和风险承担能力，自行审慎评估自身财务状况与风险承受能力； 
   4、请仔细阅读基金合同、风险揭示书、投资者适当性匹配文件等全部法律文件，独立做出投资决策； 
   5、管理人、托管人及外包机构不对投资本金及收益作出任何保本、保收益承诺； 
   6、本资料内容仅供内部参考，不得作为公开募集、对外推销使用，复制传播前须经管理人书面许可。 

# 数据处理与业务逻辑（JS 部分，极其重要）
1. **执行顺序保证**：`<script>` 标签必须放在 `<div>` 标签**之后**。由于本模板是作为片段嵌入宿主 HTML，**禁止**使用 `DOMContentLoaded` 或 `window.onload`。必须使用 IIFE（立即执行函数表达式）封装。
2. **必须实现的数据校验工具**（在 IIFE 内部定义，不可省略）：
   - `isValid(val)`：过滤 `null` / `undefined` / `""` / `"未设置"`；若为数组，需递归过滤无效项并返回长度 > 0 的布尔值。
   - `isValidDate(val)`：在 `isValid` 基础上，额外过滤 `"0001-01-01"` / `"1900-01-01"` 等 C# 默认日期。
   - `escapeHtml(text)`：基础 XSS 防护。
3. **错误处理与调试**（强制）：
   - IIFE 第一行必须包含：`console.log('[Brochure] broData:', window.broData);`
   - 若 `!window.broData`，直接在根容器内渲染 `<p style="color:red;text-align:center;">暂无数据 (window.broData 未定义)</p>` 并 `return`。
   - 整个渲染逻辑必须包裹在 `try { ... } catch(e) { 根容器显示红色错误信息: e.message }` 中。
4. **DOM 预构建与显隐控制**：在 `<div>` 中预先构建所有可能的 DOM 元素结构，在 `<script>` 中根据 `window.broData` 的数据进行赋值、隐藏（`style.display = 'none'`）或修改操作。
5. **空区块整块隐藏策略**：每个 section 在渲染前，先拼接其内部所有 row 的 HTML。如果拼接结果为空字符串（即该区块所有字段都无效），则**整个 section 不渲染**（而非渲染一个只有标题的空区块）。
6. **特定业务规则**：
   - **份额类型 (ShareClasses)**：如果数组长度 ≤ 1（或 Name 为空），则**完全隐藏**“份额类型”相关区块。
   - **数组多元素映射展示 (Class Property)**：对于按份额区分的数组类型属性（如 `ManageFee`, `OpenInfo`, `SubscriptionRule`, `RedemptionFee`, `PurchasRule` 等），如果过滤后**包含多个元素**（>1），则必须与 `ShareClasses` 数组按索引一一对应，展示为“**[份额名称]**：[对应属性值]”的键值对（Class Property）形式（例如：“A类份额：1.5%”），而非简单的无序列表。
   - **单元素数组降级**：对于上述数组属性，如果过滤后**只有 1 个元素**，则表现与普通文本一致（不显示列表符号，也不显示份额名称前缀）。
   - **多行文本处理**：对于 `CollectionAccount`、`CustodyAccount`、`ManagerProfile` 等文本，需保留换行符（在 JS 中将 `\n` 替换为 `<br>`，或在 CSS 中使用 `white-space: pre-wrap`）。
   - **图片处理**：`ManagerLogo` 和 `BrochureInvestManager.Photo` 为 Base64 字符串或 Byte 数组。必须处理**空值情况**：如果为空/undefined，则隐藏对应的 `<img>` 标签，不要显示裂图图标。若投资经理无 Photo，则仅显示姓名和简介。

# 输出格式严格约束（极其重要）
1. **仅输出三个标签**：你的输出**只能**包含 `<style>...</style>`、`<div class="brochure-container">...</div>` 和 `<script>...</script>` 这三个标签。
2. **禁止额外内容**：**绝对不要**输出任何 Markdown 标记（如 ```html）、解释性文字、前言或后语。直接以 `<style>` 开头，以 `</script>` 结尾。
3. **代码质量**：代码需整洁、结构清晰，包含必要的中文注释。

# 数据结构参考 (C# 定义)
以下是 `window.broData` 对应的 C# 数据结构，属性名在 JS 中必须保持 **PascalCase**（与 C# 完全一致）：

```csharp
/// <summary>
/// 社交账号
/// 微信 微博等
/// </summary>
public class SocialAccount
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public byte[]? QRCode { get; set; }
}

/// <summary>
/// 基金经理介绍
/// </summary>
/// <param name="Name"></param>
/// <param name="Profile"></param>
public record BrochureInvestManager(string Name, string Profile, byte[] Photo);

/// <summary>
/// 份额类型
/// </summary>
/// <param name="Name"></param>
/// <param name="Requirement"></param>
public record BrochureShareClass(string Name, string Requirement);


/// <summary>
/// 宣传用的要素
/// </summary>
public class BrochureFactor
{
    #region 管理人
    /// <summary>
    /// 管理人名称
    /// </summary>
    public required string ManagerName { get; set; }

    /// <summary>
    /// logo
    /// </summary>
    public byte[]? ManagerLogo { get; set; }

    /// <summary>
    /// 管理人英文名称
    /// </summary>
    public string? ManagerEnglishName { get; set; }

    /// <summary>
    /// 管理人备案号
    /// </summary>
    public required string ManagerAmacCode { get; set; }

    public string? ManagerProfile { get; set; }

    /// <summary>
    /// 公众账号
    /// </summary>
    public SocialAccount[]? ManagerSocialAccounts { get; set; }
    #endregion

    #region 基金全局属性
    public required string FundName { get; set; }
    public required string ShortName { get; set; }

    /// <summary>
    /// 份额类型
    /// </summary>
    public required BrochureShareClass[] ShareClasses { get; set; }

    /// <summary>
    /// 成立日期 yyyy-MM-dd
    /// </summary>
    public required string SetupDate { get; set; }

    /// <summary>
    /// 备案日期  yyyy-MM-dd
    /// </summary>
    public string? AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 结束日期  yyyy-MM-dd
    /// </summary>
    public required string ExpirationDate { get; set; }

    /// <summary>
    /// 存续期
    /// </summary>
    public required string Duration { get; set; }

    /// <summary>
    /// 是否结构化
    /// </summary>
    public bool IsStructured { get; set; }

    /// <summary>
    /// 基金类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 证券投资基金类型
    /// </summary>
    public string? SecurityFundType { get; set; }

    /// <summary>
    /// 运作方式（全份额共用，普通类型）封闭、开放
    /// </summary>
    public string? FundModeInfo { get; set; }

    /// <summary>
    /// 锁定期规则
    /// </summary>
    public required string[] LockingRule { get; set; }

    /// <summary>
    /// 整体封闭期规则（基金全局规则）
    /// </summary>
    public required string SealingRule { get; set; }

    /// <summary>
    /// 风险等级
    /// </summary>
    public required string RiskLevel { get; set; }
    #endregion

    #region 账户信息（基金全局账户）
    /// <summary>
    /// 主募集账户（全份额共用）- 多行文本
    /// </summary>
    public required string CollectionAccount { get; set; }

    /// <summary>
    /// 主托管账户（全份额共用）- 多行文本
    /// </summary>
    public string? CustodyAccount { get; set; }
    #endregion

    #region 风控线（按份额区分，数组）
    /// <summary>
    /// 止损线
    /// </summary>
    public decimal? StopLine { get; set; }

    /// <summary>
    /// 预警线
    /// </summary>
    public decimal? WarningLine { get; set; }

    /// <summary>
    /// 巨额赎回规则
    /// </summary>
    public string? HugeRedemption { get; set; }
    #endregion

    #region 开放/赎回规则
    /// <summary>
    /// 开放日规则
    /// </summary>
    public required string[] OpenInfo { get; set; }

    /// <summary>
    /// 临时开放信息
    /// </summary>
    public string[]? TemporarilyOpenInfo { get; set; }

    /// <summary>
    /// 冷静期信息
    /// </summary>
    public required string CoolingPeriod { get; set; }

    /// <summary>
    /// 回访信息
    /// </summary>
    public required string Callback { get; set; }

    /// <summary>
    /// 认购规则
    /// </summary>
    public required string[] SubscriptionRule { get; set; }

    /// <summary>
    /// 申购规则
    /// </summary>
    public required string[] PurchasRule { get; set; }
    #endregion

    #region 机构信息
    /// <summary>
    /// 托管机构信息
    /// </summary>
    public required string TrusteeInfo { get; set; }

    /// <summary>
    /// 托管机构费用信息
    /// </summary>
    public required string TrusteeFee { get; set; }

    /// <summary>
    /// 外包机构信息
    /// </summary>
    public required string OutsourcingInfo { get; set; }

    /// <summary>
    /// 外包机构费用信息
    /// </summary>
    public required string OutsourcingFee { get; set; }
    #endregion

    #region 费用信息
    /// <summary>
    /// 管理费支付方式
    /// </summary>
    public string? ManageFeePay { get; set; }

    /// <summary>
    /// 管理费
    /// </summary>
    public string[]? ManageFee { get; set; }

    /// <summary>
    /// 赎回费
    /// </summary>
    public string[]? RedemptionFee { get; set; }

    /// <summary>
    /// 业绩报酬说明
    /// </summary>
    public string[]? PerformanceFeeStatement { get; set; }
    #endregion

    #region 投资经理/投资策略
    /// <summary>
    /// 基金经理列表
    /// </summary>
    public required BrochureInvestManager[] InvestmentManagers { get; set; }

    /// <summary>
    /// 业绩比较基准
    /// </summary>
    public string? PerformanceBenchmark { get; set; }

    /// <summary>
    /// 投资目标
    /// </summary>
    public string? InvestmentObjective { get; set; }

    /// <summary>
    /// 投资范围
    /// </summary>
    public string? InvestmentScope { get; set; }

    /// <summary>
    /// 投资策略
    /// </summary>
    public string? InvestmentStrategy { get; set; }
    #endregion
}