连接管理人、托管外包、基协、电子合同平台

仓库地址
https://github.com/iyumot/FundMiddleOffice


# 能做什么？
- **汇集所有产品的净值**
    
    痛点：对于产品数量众多的管理人，统计净值是比较麻烦的。
    
    解决方案：通过托管发送估值邮件到指定邮箱，在【任务】页中，设置邮件缓存、净值更新，
    可以每日更新产品的净值数据。
    
    这些数据可以用于自动生成各类报表，每日自动发送到群等自动化运营。


- **汇集TA数据**

  痛点：同样统计TA也是比较麻烦的。

  解决方案：
  1. 通过托管发送估值邮件到指定邮箱，在【任务】页中，设置邮件缓存、TA更新，
    可以每日更新产品的净值数据。
  2. 通过托管API，定时更新数据

  可以做什么？
    1. 生成各类报表
    2. 运营数据监控：比如申赎未到账提示（部分托管支持）

- **接入托管外包API**
  1. 定时监控募集户余额，提醒申赎未到账、无订单申购
  2. 同步基金费用，异常费用监控

- **接入协会**
  1. 同步员工数据
  2. 自动更新投资人信批账户


- <span style="color: green;">**辅助工具**</span>
  1. 费用计算器：支持按投资人份额计算每日管理费，可以把每月、季的管理费拆解到每个投资人。
  2. 报课助手：为员工批量在培训系统报课
  3. 学习助手：自动看培训课





# 首页
1. 历史规模图
2. 常用工具集
3. 各类提示信息
3. 托管提示信息


![Home](readme/home.png)


# 管理人
|      | 说明 | 状态 |
|----------|------|------|
| 管理人基本信息     | 可复制   | <span style="color: green;">*已完成*</span>   |
| 管理人证件     |    | <span style="color: gray;">*待完善*</span>   |
| 成员     | 支持从协会下载数据，可设置证件，属性可复制   | <span style="color: green;">*已完成*</span>   |
| 股权结构     |     | <span style="color: red;">*未完成*</span>   |


![Manager](readme/manager.png)


# 基金
|      | 说明 | 状态 |
|----------|------|------|
| 列表     | 可复制   | <span style="color: green;">*已完成*</span>   |
| 从协会更新数据     |    | <span style="color: green;">*已完成*</span>   |
| 各类信息预警     | 超期、份额数据异常、净值未更新等   | <span style="color: gray;">*待完善*</span>   

![Funds](readme/funds.png)


# 基金资料

![Funds](readme/fundinfo.png)
![Funds](readme/fundinfo2.png)
![Funds](readme/fundinfo-nv.png)
![Funds](readme/fundinfo-curve.png)
![Funds](readme/fundinfo-ele.png)





# 客户
|      | 说明 | 状态 |
|----------|------|------|
| 汇集投资人信息     | 可复制   | <span style="color: green;">*已完成*</span>   |
| 汇集合投资料     |    | <span style="color: green;">*已完成*</span>   |
| 从电签平台同步     | 同步资料   | <span style="color: gray;">*待完善*</span>   

 

![Investor](readme/investor.png)

# TA

|      | 说明 | 状态 |
|----------|------|------|
| 汇集交易申请（从托管）     | 托管目前支持招商、中信、建投   | <span style="color: gray;">*待完善*</span>   |
| 汇集交易确认（从托管）     |    | <span style="color: gray;">*待完善*</span>   |
| 从电签平台同步     | 同步资料   | <span style="color: gray;">*待完善*</span>   


![Ta](readme/ta.png)


# 协会、托管外包、电签平台
|      | 说明 | 状态 |
|----------|------|------|
| 同步协会数据     | 从业人员  | <span style="color: gray;">*待完善*</span>   |
| 托管API对接     | 已完成 招商、中信、建投   | <span style="color: gray;">*待完善*</span>   |
| 从电签平台同步     | 同步资料   | <span style="color: gray;">*待完善*</span>   |


![Platform](readme/platform.png)

# 自动化任务
|      | 说明 | 状态 |
|----------|------|------|
| 每日发送净值报表     |     | <span style="color: green;">*已完成*</span>   |
| 从邮件中更新估值表     |     | <span style="color: green;">*已完成*</span>   |
| 从邮件中更新TA报表     | 后续将被api替代  | <span style="color: green;">*已完成*</span>   |
| 定制功能     |  可委托开发  | <span style="color: green;">*已完成*</span>   |


![Task](readme/task.png)

# 报表 
|      | 说明 | 状态 |
|----------|------|------|
| 每日净值报表     |     | <span style="color: green;">*已完成*</span>   |
| 其它各类报表     | 支持模板化导出   | <span style="color: gray;">*待完善*</span>   |

![Report](readme/report.png)


