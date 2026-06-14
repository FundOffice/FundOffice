# Trustee 模块

## 概述

Trustee 模块负责与托管机构（券商/银行）的 API 对接，实现基金净值、份额、交易、投资者等数据的自动同步。

## 子项目

### Trustee 核心库 (src/Trustee/Trustee/)

**命名空间**: FMO.Trustee

提供托管机构对接的基础框架。

**核心类**:
- **TrusteeApiBase** - 托管 API 基类，封装 HTTP 请求和认证逻辑
- **TrusteeGallay** - 托管机构集合/注册表（Gallery 模式）
- **TrusteeWorker** - 托管同步 Worker，定时拉取托管数据
- **TrusteeViewModelBase** - 托管配置 ViewModel 基类
- **JsonBase** - JSON 响应基类
- **TrusteeJsonUnexpected** - JSON 异常处理
- **UnusualType** - 异常类型枚举

**CITISC/ 子目录** (内嵌的中信证券实现):
中信证券的 JSON 数据模型和 API 实现，作为 Trustee 核心库的一部分。

---

### CITICS (src/Trustee/CITICS/)

**命名空间**: FMO.Trustee.CITICS

中信证券托管对接，实现中信证券的 API 数据获取。

**数据模型**:

| 类名 | 说明 |
|------|------|
| BankBalanceJson | 银行余额 |
| BankTransactionJson | 银行交易记录 |
| CustodialAccountJson | 托管账户 |
| CustodialTransactionJson/2 | 托管交易记录 |
| DistrubutionJson | 分配信息 |
| FundDailyFeeJson | 每日费用 |
| InvestorJson | 投资者信息 |
| NetValueJson | 净值数据 |
| OpenDayJson | 开放日 |
| PerformanceJson | 业绩数据 |
| RaisingBalanceJson | 募集余额 |
| TransferRecordJson | 转让记录 |
| TransferRequestJson | 转让请求 |
| VirtualNetValueJson | 虚拟净值 |
| ProductInfoJson | 产品信息 |

**核心类**:
- **CITISC** - 中信证券 API 实现
- **CITISCViewModel** - 配置 ViewModel

---

### CMS (src/Trustee/CMS/)

招商证券托管对接。

---

### CSC (src/Trustee/CSC/)

中信建投托管对接。

---

### XYZQ (src/Trustee/XYZQ/)

兴业证券托管对接。

## 工作原理

1. TrusteeWorker 定时运行（每工作日 8-18 点，每 3 小时间隔）
2. 遍历注册的托管平台实例
3. 调用各平台 API 获取净值、份额、交易、投资者等数据
4. 与本地数据库合并，通过 DataHub 发布数据变化事件
5. 触发 DataTrigger 规则引擎进行数据校验和预警