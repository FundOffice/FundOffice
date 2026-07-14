# Plugin 模块 (插件系统)

## 概述

Plugin 模块实现了基于 AssemblyLoadContext 的插件加载系统，允许在不修改主程序的情况下扩展功能。

## 项目路径

src/Plugin/Plugin/

**命名空间**: FMO.Plugin

## 核心类型

### IPlugin (interface)

插件接口定义：

- **Title** - 插件标题
- **Description** - 插件描述
- **Icon** - 插件图标 (Stream)
- **OnLoad()** - 插件加载回调
- **OnUnload()** - 插件卸载回调

### PluginDefinition

插件定义模型（从 def.json 反序列化）：

- Folder - 插件目录
- EndPoint - 入口 DLL 文件名

### PluginLoadContext

自定义 AssemblyLoadContext，用于隔离加载插件程序集。

### PluginManager (static)

插件管理器：

- **LoadAll()** - 扫描 plugins/ 目录下所有子文件夹
- 读取每个子目录的 def.json 配置
- 加载入口 DLL 及其依赖
- 查找实现 IPlugin 的类型并实例化
- 调用 OnLoad() 初始化

## 插件目录结构

```
plugins/
├── PluginA/
│   ├── def.json        # {"EndPoint": "PluginA.dll"}
│   ├── PluginA.dll     # 插件入口
│   └── *.dll           # 插件依赖
└── PluginB/
    ├── def.json
    └── ...
```

## 加载流程

1. 应用启动时调用 PluginManager.Init()
2. 扫描 plugins/ 目录下的所有子文件夹
3. 解析 def.json 获取入口 DLL
4. 通过 AssemblyLoadContext 加载程序集
5. 查找并实例化所有 IPlugin 实现类
6. 调用 OnLoad() 完成初始化