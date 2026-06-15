# Setting 模块 (设置管理)

## 概述

Setting 模块提供系统级配置管理功能，支持功能开关（Ability）、设置单元（SettingUnit）的注册、持久化和 UI 配置。

## 子项目

### Settings 核心库 (src/Setting/Settings/)

**命名空间**: FMO.Settings

**核心类**:

- **SettingService** (static partial) - 设置服务
  - Initialize() - 初始化，从数据库加载设置
  - RegisterAbility() - 注册功能开关（带 ISettingFunction 实例）
  - RegisterSwitch() - 注册简单开关
  - EnableAbility() / DisableAbility() - 启用/禁用功能
  - GetUnits() / GetValue() / GetUnit<T>() - 获取设置值
  - Save() - 持久化设置

- **SettingUnit** - 设置单元基类
  - Id (Section.Name 格式)
  - Name, Section, Title, Description, IsEnabled

- **SwitchUnit** - 开关类型设置单元
- **AbilityUnit** - 功能类型设置单元

- **ISettingFunction** (interface) - 功能接口
  - Init(), Start(), Stop()

- **IVerifyRule** (interface) - 校验规则接口
- **IUnitViewModel** (interface) - 单元 ViewModel 接口

**数据存储**: data\settings (LiteDB)

---

### Settings.UI (src/Setting/Settings.UI/)

设置模块的 UI 层。

**核心类**:
- VerifyRuleUnitViewModel - 校验规则单元 ViewModel

---

### SettingGenerator (src/Setting/SettingGenerator/)

**类型**: Roslyn Source Generator (netstandard2.0)

设置相关的源码生成器。

**生成器类**:
- **SettingServiceGenerator** - 设置服务代码生成
- **SettingViewModelRegisterGenerator** - 设置 ViewModel 注册代码生成

## 设计模式

Setting 模块采用注册模式：
1. 各功能模块在初始化时调用 SettingService.RegisterAbility() 注册功能开关
2. SettingService 管理开关状态，控制功能的启停
3. 通过 SettingGenerator 自动生成注册代码，减少手动配置