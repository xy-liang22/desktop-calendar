# 离线历法资料与更新

P7 将个人业务文件与知识资料分离。程序内置 `Core/calendar.json`（仅公开日历事实），本机主动更新保存 `calendar-knowledge.json`。正常启动不联网。

- 农历：Microsoft .NET `ChineseLunisolarCalendar`，1901-02-19 至 2101-01-28，支持闰月；越界明确提示，传统节日只为非闰月相应日期提供标签，不冒充公众放假安排。
- 二十四节气：香港天文台公历与农历对照表，2020–2030，每年24项、UTC+8民用日期。没有自行套用近似分钟公式。支持范围外显示未覆盖。天文台提醒远期靠近午夜的预测可能出现一日差异，不能当作秒级星历。
- 内地：国务院办公厅2025、2026年放假调休通知；CN为完整放假安排（包含连休），不是把所有连休日都称作法定节日本日；CNWork为明确补班。2027及其他无包年份不推测调休。
- 香港：香港特区政府1823公开数据，2025–2027，英文/简体中文按日期配对、每年17个具名公众假期；完整日期属性仍含星期日公众假期，0.8.1紧凑月格不额外展示该通用标签。不与《雇佣条例》法定假日混同。
- 黄历宜忌：暂无经过核验且授权清晰的日资料，未启用；保留知识事件扩展边界，不伪造权威建议。

来源与权利归属：香港特别行政区政府、香港天文台、1823以及 DATA.GOV.HK；资料知识产权归原提供者。本项目转换的日期事实非第三方代码，不含官方网页、图表、Logo或全文转载。香港资料按 DATA.GOV.HK 公开使用条款提供署名及来源，无官方背书或准确性保证。内地仅将官方通知中的日期安排转录为结构化事实。

- 香港天文台：[公历农历对照与预测说明](https://www.hko.gov.hk/en/gts/time/conversion.htm)，[2026文本](https://www.hko.gov.hk/en/gts/time/calendar/text/files/T2026e.txt)。
- 香港政府：[1823数据说明](https://www.1823.gov.hk/en/hong-kong-public-holidays-ical)，[英文JSON](https://www.1823.gov.hk/common/ical/en.json)、[中文JSON](https://www.1823.gov.hk/common/ical/sc.json)，[资料使用条款](https://data.gov.hk/en/terms-and-conditions)。
- 内地：[2025通知](https://www.gov.cn/zhengce/zhengceku/202411/content_6986383.htm)、[2026通知](https://www.gov.cn/zhengce/zhengceku/202511/content_7047091.htm)。
- [.NET农历实现](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.chineselunisolarcalendar)。

## 主动更新

设置 → 检查/更新日历资料，先看范围/来源/资料时间，再明确确认联网。当前白名单仅访问天文台各内置年份文本、1823两种语言JSON；禁重定向、请求超时、每源1MB上限。新的内地年份须经过核验后通过本地包导入或后续程序更新取得，不爬取搜索结果/猜测新公告URL。下载后校验整包，任一失败保留既有资料。导入本地包也先验证再确认；来源URL字段只是出处，不是数字签名，只导入自行核对可信来源的包。

窗口与脚本共用 Core.CalendarKnowledge，不存在另一套PS解析器：

```powershell
.\Update-CalendarData.ps1 -Action Status -Configuration P8
.\Update-CalendarData.ps1 -Action Update -Online -Configuration P8
.\Update-CalendarData.ps1 -Action Import -PackagePath C:\trusted\calendar.json -Configuration P8
```

默认Status离线；Update必须显式Online；只写知识文件及其备份，不读写 planner-data.json。格式含SchemaVersion、UpdatedAt、各资料覆盖年份、带Date/Kind/Zh/En/Source的Events。24节气/每年17个具名HK假期、年份、重复、可信来源和体积均校验；同目录临时文件刷盘再替换，旧文件保留.bak。较新schema拒绝覆盖；损坏本机文件可从.bak或内置包继续显示，但导入/更新不悄悄覆盖损坏原件。

脚本更新后可重新打开资料面板或重启读取。维护内置公开包时，在artifacts独立目录主动更新、核对来源和新旧差异后才替换Core/calendar.json；新内地公告必须人工核对，不把推测写成正式调休。更新文件锁也与ZIP维护共用，冲突时保留旧资料并提示失败。

## 0.8.1 月格展示规则

农历计算及完整详情不变；月格只显示初一月份（闰月保留）、其他日日期。冬月/腊月初一简写为十一月/十二月。公历与信息/+N同一个21DIP日期头，不额外占第二行。节气优先于传统节日，具名假日/补班随后，完整事件在悬停/详情保留。On()额外补出的普通Sunday通用HK事件只在紧凑月格过滤，具名节日从不按周日过滤；官方387条原始知识事实不改，周日底色和完整日属性保留。
