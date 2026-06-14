# Utility 模块 (工具库)

## 概述

Utility 目录包含系统的各类基础工具和通用库，为上层业务模块提供底层支撑。

## 子项目

### Utilities (src/Utility/Utilities/)

**命名空间**: FMO.Utilities

通用工具库，提供系统级基础功能。

**核心类**:

| 类名 | 说明 |
|------|------|
| DbHelper | 数据库连接帮助（Base/Platform/Mission 等多种连接） |
| FileHelper | 文件操作工具 |
| FileIndexService | 文件索引服务 |
| FundHelper | 基金相关工具方法 |
| AesHelper | AES 加密/解密 |
| SecurityHelper | 安全工具 |
| DateTimeHelper | 日期时间工具 |
| NumberHelper | 数字格式化工具 |
| EnumHelper | 枚举工具 |
| ObjectExtension | 对象扩展方法 |
| Debouncer | 防抖器 |
| Toast | 消息提示 |
| ValuationSheetHelper | 估值表工具 |
| ZipSplitter | ZIP 分卷压缩 |
| MissionDatabase | 任务数据库工具 |
| PlatformSynchronizeTime | 平台同步时间 |

---

### FileTemplate (src/Utility/FileTemplate/)

文件模板引擎，支持 Excel 和 Word 模板填充。

**核心类**:
- **IFileTemplate** (interface) - 文件模板接口
- **Tpl** - 模板基类
- **ExcelTpl** - Excel 模板
- **WordTpl** - Word 模板
- **Excel/ExcelTemplate** - Excel 模板处理器
- **Excel/TemplateFileHandler** - 模板文件处理器
- **Excel/TemplateMeta** - 模板元数据
- **Excel/InputInfo** - 输入信息
- **Excel/ScriptGlobal** - 脚本全局变量

---

### OCR (src/Utility/OCR/)

OCR 文字识别模块，基于 PaddleOCR。

**核心类**:
- **OCRWorker** - OCR 工作器

---

### Patch (src/Utility/Patch/)

数据库补丁/迁移模块。

**核心类**:
- **Patch** - 数据库补丁执行器
- **DatabaseAssist** - 数据库辅助工具

---

### PdfExt (src/Utility/PDF/)

PDF 处理扩展库，基于 PDFiumSharp。

---

### SourceGenerator (src/Utility/SourceGenerator/)

**类型**: Roslyn Source Generator (netstandard2.0)

通用源码生成器集合。

**生成器类**:

| 类名 | 说明 |
|------|------|
| AutoChangeableViewModelGenerator | 可变更 ViewModel 生成 |
| AutoViewModelIncrementalGenerator | 自动 ViewModel 增量生成 |
| CheckInvokerGenerator | 调用检查生成 |
| EntityModifiableIncrementalGenerator | 实体可修改增量生成 |
| IViewModelGenerator | ViewModel 接口生成 |
| VerifyRulesInitGenerator | 校验规则初始化生成 |
| CloneFrom | 克隆方法生成 |

---

### ElementGenerator (src/Utility/ElementGenerator/)

**类型**: Roslyn Source Generator (netstandard2.0)

基金要素 ViewModel 生成器。

**核心类**:
- **ElementsViewModelGenerator** - 为基金要素模型生成对应的 ViewModel

---

### Logging (src/Utility/Logging/)

日志库封装，基于 MoT.Logg。