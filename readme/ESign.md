# ESign 模块 (电子签约)

## 概述

ESign 模块实现电子签约平台的对接，自动同步投资者、合格投资者认定和订单数据，并下载签约文件。

## 子项目

### ESigning 核心库 (src/ESign/ESigning/)

**命名空间**: FMO.ESigning

电子签约的核心框架和抽象层。

**核心类**:

- **ISigning** (interface) - 签约平台接口
  - QueryCustomerAsync() - 查询客户
  - QueryQualificationAsync() - 查询合格投资者认定
  - QueryOrderAsync() - 查询订单及下载签约文件

- **ISigningConfig** (interface) - 签约平台配置接口
  - IsEnable 属性

- **ESigningWorker** - 签约同步 Worker
  - SyncCustmersOnce() - 同步客户数据
  - SyncQualificationsOnce() - 同步合格投资者认定
  - SyncOrdersOnce() - 同步订单数据
  - Start() - 启动后台轮询（每分钟检查，工作日 8-18 点每 3 小时执行）
  - LoopOnce() - 单次同步循环
  - MergeCustomers() - 合并客户数据到本地数据库

- **ESignViewModelBase** - 签约配置 ViewModel 基类
- **Galley** - 签约平台集合（注册表）
- **SigningWorkRecord** - 同步工作记录（记录上次同步时间）
- **EsigningFundInfo** - 签约平台基金信息

---

### MeiShi (src/ESign/MeiShi/)

美市科技签约平台对接实现。

**核心类**:
- **MeiShiViewModel** - 美市配置 ViewModel
- **MeiShiAssit** - 美市辅助工具

**Json/ 子目录**:
- CustomerJson - 客户数据
- FundInfoJson - 基金信息
- LoginResultJson - 登录结果
- FileUploadDataDto - 文件上传

**Disclosure/ 子目录** (美市信披通道):
- **MeiShiDisclosureChannel** - 美市信披通道实现
- MeiShiChannelConfig - 通道配置
- MeiShiChannelConfigViewModel - 配置 ViewModel
- TemporaryOpenJson - 临时开放日数据

## 数据同步流程

1. ESigningWorker.Start() 启动后台定时轮询
2. LoopOnce() 仅在工作日 8-18 点、距上次执行 > 3 小时时触发
3. SyncCustmersOnce() - 从签约平台获取客户列表，合并到本地
4. SyncQualificationsOnce() - 获取合格投资者认定，去重后入库
5. SyncOrdersOnce() - 获取订单，匹配基金和投资者，下载签约文件
6. 通过 DataTracker.OnBatchTransferOrder() 发布订单数据到 DataHub