# Templates 模块 (报表导出模板)

## 概述

Templates 模块提供各类基金报表的 Excel 导出功能，定义了多种报表模板和对应的导出器。

## 项目路径

src/Templates/

## 报表模板

| 目录 | 说明 | 导出器 |
|------|------|--------|
| FundHolderSheet/ | 基金份额持有人名册 | Exporter |
| MultiFundElementSheet/ | 多基金要素汇总表 | Exporter |
| MultiFundSummary/ | 多基金概况汇总 | Exporter |
| SingleFundNetValueList/ | 单基金净值列表 | Exporter |

## 工作原理

每个模板目录下包含：
- 报表数据模型定义
- Exporter 导出器（通常使用 ClosedXML/MiniExcel 生成 Excel）
- 模板格式配置

---

# Tools 模块 (开发工具)

## 概述

Tools 目录包含开发辅助工具。

## 子项目

### DatabaseViewer (src/Tools/DatabaseViewer/)

独立的 WPF 应用，用于查看和浏览 LiteDB 数据库内容。

- App.xaml.cs - 应用入口
- MainWindow.xaml.cs - 主窗口

---

# Generators 模块

## 概述

独立的源码生成器项目。

### AIGenerator (src/Generators/AIGenerator/)

**类型**: Roslyn Source Generator (netstandard2.0)

AI 相关的源码生成器。

**核心类**:
- **TokenProviderViewModelGenerator** - 为 TokenProvider 子类自动生成 ViewModel，支持 UI 端配置 API Key