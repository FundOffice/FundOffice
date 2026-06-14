# Amac 模块

## 概述

Amac 模块负责与中国证券投资基金业协会（AMAC）的数据对接，包括直连上报、公开数据爬取和 RPA 自动化操作。

## 子项目

### Amac.Direct (src/Amac/Direct/)

**命名空间**: FMO.Amac.Direct

中基协直连上报模块，通过 API 直接向中基协系统提交数据。

**核心类**:
- **DirectReporter** - 直连上报执行器
- **Example** - 上报示例
- **ResultJson** / **SM** - 上报结果 JSON 模型

---

### Amac.Public (src/Amac/Public/)

**命名空间**: FMO.Amac.Public

中基协公开数据爬取模块，从 AMAC 官网获取公开信息。

**核心类**:
- **AmacHtml** - HTML 页面解析
- **FundBasicInfo** - 基金基本信息爬取

**json/ 子目录**:
- ManagerInfo - 管理人信息模型
- QueryManagers - 管理人查询
- SortItem - 排序项

---

### Amac.RPA (src/Amac/RPA/)

**命名空间**: FMO.Amac.RPA

RPA（机器人流程自动化）模块，使用 Microsoft.Playwright 自动化操作中基协系统。

**核心类**:
- **amac** - AMAC 系统自动化主类
- **AmacHuman** - 模拟人工操作
- **AmbersAssist** - Amber 系统助手
- **FundNameUrl** - 基金名称/URL 映射
- **PfidAssist** - PFID 系统助手

**JsonModels/ 子目录**:
- Employee - 员工信息
- ManagerInfo - 管理人信息
- PendingReportInfo - 待上报信息