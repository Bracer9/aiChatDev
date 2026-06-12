# 《日留学部模拟器》SRS

Software Requirements Specification / 软件需求规格说明书

版本：0.2
定位：AI 演出型日本学部四年养成 SLG
目标平台：本地 Web Demo
主要技术方向：C# / .NET 8 / ASP.NET Core Minimal API / HTML + CSS + JavaScript / LLM API

---

# 1. 项目概述

《日留学部模拟器》是一款以日本大学学部四年生活为舞台的养成模拟游戏。

玩家扮演一名在日本读学部的留学生。游戏以“月”为主要推进单位，一局标准流程为 4 年，共 48 个月。玩家通过选择学部、seminar / ゼミ、月度方针、行动安排、技能成长、人际关系投入、机会选择和关键事件回应，逐渐走向不同的人生路线与结局。

本作不是纯现实留学模拟器，也不是开放式 AI 聊天游戏。它的核心定位是：

> **以真实日本学部生活制度、信息差与长期选择为骨架，加入浪漫、荒诞、隐藏路线和奇想展开的 AI 演出型养成 SLG。**

游戏的底层数值、技能、flag、事件触发、角色关系阶段、时间轴和结局判定全部由本地 GameState 管理。

AI 只负责在本地系统给定边界内生成文本演出，例如月度回顾、事件描写、角色对话、关系状态文本、结局文本等。

---

# 2. 产品目标

## 2.1 核心目标

第一阶段目标是验证以下游戏结构是否可行：

1. 玩家以月为单位规划日本学部生活。
2. 本地系统负责行动结算、数值变化、技能解锁、事件触发、关系阶段和 flag 管理。
3. 固定时间轴提供每年明确目标，例如考试、校园祭、seminar、就活、毕业制作等。
4. 角色关系和隐藏路线不是纯随机，而是由长期选择、数值、flag、前置事件和玩家回应共同推进。
5. AI 不负责游戏裁判，只负责文本表现。
6. 前端 UI 根据固定状态机轮换推进，避免游戏退化为聊天窗口。
7. 每类 AI 输出使用对应 schema，最终由本地前端渲染为稳定 HTML UI。
8. AI 文本素材可以在月度结算后批量生成并暂存，供后续 UI 调用。

## 2.2 游戏体验目标

玩家应感受到：

* 自己在安排四年学部生活，而不是和 AI 聊天。
* 每个月的选择都会积累到未来。
* 有些路线前期弱、回报慢，但后期可能爆炸。
* 日本学部生活中的真实信息差会影响结局。
* 现实制度和奇想路线并存。
* 关系不是抽奖，而是通过投入、回应和时机推进。
* AI 让有限选择产生更丰富、更有温度的演出。

---

# 3. 核心设计原则

## 3.1 GameState 主权原则

所有影响游戏性的事实，必须由本地 GameState 管理。

AI 不得决定或修改：

* 数值
* 技能
* 学部
* seminar / ゼミ
* 时间
* 行动结果
* 角色核心设定
* 可发展关系对象
* 角色关系阶段
* 关系数值
* 事件是否触发
* flag 是否开启
* 路线是否进入
* 结局是否达成
* 固定时间节点
* 隐藏身份是否揭示
* 玩家是否绕过条件获得高等级结果

AI 可以负责：

* 月度回顾文本
* 事件演出文本
* 角色台词
* 关系状态描述
* 机会介绍文本
* 氛围描写
* 心理描写
* 结局演出文本
* 对本地结算结果的文学化包装

---

## 3.2 AI 是智能文本函数，不是游戏主持人

AI 在本项目中被视为“智能文本处理函数”。

正确用法：

```txt
输入：本地状态 + 本地判定结果 + 当前事件上下文
输出：符合 schema 的演出文本
```

错误用法：

```txt
输入：玩家自由行动
输出：AI 自行决定世界、数值、路线和结局
```

AI 的职责是包装和演出，不是裁判和规划。

---

## 3.3 固定时间轴提供舞台，不写死玩家行为

游戏中存在必然发生的制度节点，例如考试、选课、校园祭、就活时期、毕业判定等。

但玩家是否参与、如何参与、以什么状态参与，由玩家选择和本地系统决定。

例如：

* 校园祭一定会发生。
* 玩家可以参加、不参加、旁观、打工错过、作为社团成员帮忙、作为创作者出展、作为关系事件舞台参与。
* 参加校园祭并不必然成功，结果由本地参数、技能、关系和准备情况判定。

---

## 3.4 选择的快乐

玩家的乐趣不应只来自“加点”和“排行动表”。

游戏必须提供多层选择：

1. 开局选择学部 / 初始方向。
2. 选择 seminar / ゼミ。
3. 每月选择发展方针。
4. 每月安排具体行动。
5. 同一行动可选择不同风格。
6. 选择将有限的关系余裕投入给谁。
7. 选择是否参加固定时间节点活动。
8. 出现多个机会时选择追哪条线。
9. 关键事件中选择回应态度。
10. 决定是否走正统路线、隐藏路线、创作路线、关系路线或摆烂路线。

---

## 3.5 有边界的自由

玩家可以自由表达意图，但不能自由改写结果。

例如玩家可以输入：

```txt
这个月我想重点研究魔法少女叙事，并试着和 seminar 上那个不起眼的女生多聊几句。
```

但系统不会因此直接解锁隐藏路线。
本地系统只会根据行动、参数、flag、关系阶段和时间节点判断是否推进。

---

# 4. 游戏世界与题材定位

## 4.1 基础题材

玩家是日本大学学部留学生，游戏时间跨度为四年。

题材包含：

* 日本大学学部生活
* 留学生身份
* 选课
* seminar / ゼミ
* GPA
* 出席率
* 课堂发表
* レポート
* 打工
* 社团 / サークル
* 校园祭
* 就活
* インターン
* 自我分析
* 业界分析
* ES
* 面试
* 升学
* 毕业论文 / 毕业制作
* 经济压力
* 孤独感
* 人际关系
* 创作与宅文化
* 隐藏关系与奇想路线

---

## 4.2 非纯现实模拟

游戏不追求完全写实。

现实制度是骨架，奇想展开是乐趣来源。

允许存在：

* 宅文化 seminar 隐藏路线
* 魔法少女研究 seminar
* 同人展 / CM 事件
* 隐藏富家角色路线
* AI Hatsune Miku 觉醒路线
* 音游逃避转创作路线
* 前期弱鸡后期爆炸路线
* 被包养 / 家庭主夫路线
* 现实失败但创作成功路线
* 普通就职之外的荒诞人生结局

---

## 4.3 题材优势

本游戏的独特价值不在于泛泛模拟“留学生活辛苦”，而在于：

* 利用真实日本学部经验。
* 体现只有亲历者或充分做过情报搜索的人才知道的信息差。
* 让早期看似无所谓的选择在后期产生巨大影响。
* 让“逃避”“宅文化”“创作”“音游”“自闭”等非正统路径也可能积累成特殊路线。
* 将真实制度压力与二次元式浪漫、荒诞和救赎结合。

---

# 5. 游戏时间结构

## 5.1 总时长

标准模式：

```txt
4 年 = 48 个月
```

Demo 可以先实现：

```txt
6 个月 / 12 个月
```

但架构必须支持扩展到 48 个月。

---

## 5.2 年度主题

### 第 1 年：信息差与路线种子

主题：

```txt
玩家以为自己还有很多时间，但现实系统已经开始筛人。
```

主要内容：

* 入学
* 选课
* 基础课程
* 初次考试
* 社团 / サークル接触
* 打工初体验
* 早期 career 情报
* 自我分析入门
* 业界研究入门
* 创作 / 宅文化 / 音游逃避路线种子

设计重点：

* 玩家可以不管就活，但系统会记录“不管”的后果。
* 玩家可以沉迷创作、音游、动漫，但这些不一定只是废路线。
* 大一不是单纯适应期，而是路线种子期。

---

### 第 2 年：路径依赖形成

主题：

```txt
玩家开始发现自己到底是在积累，还是只是在拖延。
```

主要内容：

* seminar / ゼミ预备或选择
* 专业课程加深
* 打工稳定化
* 创作积累
* 关系线推进
* 早期 internship 准备
* 隐藏路线初次显形

设计重点：

* 大二时玩家过去选择开始形成惯性。
* 如果早期积累了创作或宅文化参数，隐藏路线可以开始露头。
* 如果完全忽视就职准备，高级就职路线开始变窄。

---

### 第 3 年：外部系统正式筛选

主题：

```txt
实习、就活、研究、创作路线开始真正拉开差距。
```

主要内容：

* インターン
* 早期选考
* 本选考准备
* seminar 活动
* 作品集 / 项目发表
* 关系线关键分歧
* 隐藏路线正式展开

设计重点：

* 过去两年积累决定机会。
* 玩家开始面对“现在补还来不来得及”的压力。
* 一部分隐藏路线进入爆发期。

---

### 第 4 年：承担过去选择的结果

主题：

```txt
不是从现在开始选择，而是承受过去选择的后果。
```

主要内容：

* 内定 / 无内定
* 毕业论文 / 毕业制作
* 毕业判定
* 关系确认
* 是否留日
* 是否升学
* 是否就职
* 是否走创作路线
* 是否普通毕业
* 是否归国重启
* 结局收束

设计重点：

* 大四不是“开始就活”，而是过去积累的结果显现。
* 高级就职路线必须依赖大一、大二、大三的准备。
* 普通、奇想、创作、关系、失败、重启等路线都可以收束。

---

# 6. 固定时间节点

## 6.1 每年固定节点

以下节点由本地时间轴触发，不由 AI 决定。

| 月份   | 节点             | 是否必然发生 | 玩家是否可选择参与方式 |
| ---- | -------------- | -----: | ----------: |
| 4 月  | 新学期 / 入学 / 选课  |      是 |           是 |
| 5 月  | 新生活适应 / 社团接触   |      是 |           是 |
| 6 月  | 前期课程推进         |      是 |           是 |
| 7 月  | 前期考试 / 小发表     |      是 |          部分 |
| 8 月  | 夏休み            |      是 |           是 |
| 9 月  | 夏季活动 / 打工 / 实习 |      是 |           是 |
| 10 月 | 后期开始 / 交流活动    |      是 |           是 |
| 11 月 | 校园祭            |      是 |           是 |
| 12 月 | 年末事件           |      是 |           是 |
| 1 月  | 后期考试 / 年度评价    |      是 |          部分 |
| 2 月  | 春假 / 路线整理      |      是 |           是 |
| 3 月  | 年度总结           |      是 |           否 |

---

## 6.2 固定节点的参与方式

固定事件出现时，玩家可以选择不同参与方式。

例如校园祭：

```txt
参加社团摊位
进行音乐表演
进行美术展示
帮 seminar 做展示
和关系对象一起逛
只是旁观
打工错过
完全不参加
```

不同选择影响：

* 创作参数
* 关系事件
* MH
* Money
* hidden route flags
* 后续机会

---

## 6.3 就活时间轴原则

游戏必须体现现代日本新卒就活的提前化。

要求：

* 大一即可接触自我分析、业界研究、career center。
* 大二开始认真准备 internship 并不算早。
* 大三开始外部系统正式筛选。
* 大四才开始准备就活会锁掉高级就职路线。
* 大四仍可达成普通就职、非正统路线、归国、创作、升学或其他结局，但不能无条件进入顶级就职结局。

---

# 7. 学部与 seminar 系统

## 7.1 学部选择

开局玩家选择学部。
学部影响：

* 初始参数倾向
* 行动收益
* 可选课程
* 可接触角色
* seminar 候选
* 年度活动优势
* 隐藏路线倾向
* 结局候选

示例学部：

1. 语言文化学部
2. 信息 / 数据学部
3. 商业 / 经营学部
4. 艺术表现学部
5. 国际交流学部
6. 宅文化 / 媒体研究方向，可作为特殊 seminar 或隐藏学部方向出现

---

## 7.2 Seminar / ゼミ选择

seminar 是路线分化核心。

seminar 影响：

* 专属事件
* 专属导师 / 教员
* 可发展关系对象
* 毕业论文 / 毕业制作方向
* 隐藏路线
* 后期结局

示例 seminar：

---

### 魔法少女研究 Seminar

研究内容：

```txt
魔法少女叙事、宅文化、消费社会、少女幻想、二次创作文化
```

可能路线：

* 宅文化研究路线
* 同人创作路线
* CM 事件
* 隐藏富家角色路线
* 内容产业路线
* 创作结局

---

### 数据分析 Seminar

研究内容：

```txt
数据分析、Python、社会调查、文本挖掘、用户行为
```

可能路线：

* IT 就职
* 数据分析就职
* 项目发表
* 实习路线
* 研究生路线

---

### 商业日本语 / Career Seminar

研究内容：

```txt
商业沟通、企业研究、就职实践、跨文化职场
```

可能路线：

* 日企就职
* 外资就职
* 面试路线
* 会社适应路线

---

### 艺术创作 Seminar

研究内容：

```txt
音乐、绘画、剧本、影像、校园祭创作
```

可能路线：

* 编曲人
* 同人音乐
* 美术展示
* 校园祭舞台
* AI Miku 路线

---

# 8. GameState 数据主权

## 8.1 GameState 必须管理的内容

GameState 至少包含：

```txt
当前年月
学部
seminar
HP
MH
Money
核心显示参数
细分参数
技能
flag
事件历史
角色关系
已触发事件
隐藏路线进度
月度日志
年度日志
结局候选
pendingEvents
pendingDialogues
pendingOpportunities
pendingRelationshipActions
storedAiPayloads
currentUiState
```

---

## 8.2 AI 不得覆盖 GameState

AI 返回的内容不得直接写入 GameState。
所有 AI 返回必须经过本地解析、校验和筛选。

例如：

AI 可以返回：

```txt
memoryUpdate: "本月玩家在校园祭中通过编曲展示获得小小认可。"
```

但本地只有在对应事件确实触发时，才能记录该记忆。

---

# 9. 参数系统

## 9.1 参数分层原则

玩家不应看到过多细分参数。
系统内部可以维护大量细分参数，但 UI 默认只展示大方向关键词。

---

## 9.2 玩家可见核心参数

推荐显示：

```txt
HP
MH
Money
学业
语言
职业
创作
关系
生活
```

其中：

* HP：身体状态
* MH：精神状态
* Money：经济状态
* 学业：课程、GPA、报告、考试、专业理解
* 语言：日语听说读写、敬语、闲谈、发表
* 职业：自我分析、业界研究、ES、面试、实习、商业日本语
* 创作：编曲、绘画、剧本、审美、同人、作品完成力
* 关系：人际能力、主动性、共情、边界感、亲密关系能力
* 生活：料理、整理、时间管理、财务管理、睡眠、压力管理、打工适应

---

## 9.3 内部细分参数

### 学业 Academic

```txt
Attendance
GPA
ReportWriting
LectureComprehension
MajorKnowledge
SeminarContribution
ExamSkill
```

### 语言 Communication

```txt
Listening
Speaking
Keigo
SmallTalk
Presentation
Reading
WritingJapanese
```

示例加权：

```txt
语言显示值 =
Listening * 0.20
+ Speaking * 0.25
+ Keigo * 0.15
+ SmallTalk * 0.15
+ Presentation * 0.15
+ Reading * 0.10
```

不同事件可使用不同权重。
例如社交闲聊事件中，SmallTalk 权重应明显提高。

### 职业 Career

```txt
SelfAnalysis
IndustryResearch
EntrySheet
Interview
Internship
BusinessJapanese
Portfolio
CompanyLiteracy
```

### 创作 Creation

```txt
Composition
Drawing
ScenarioWriting
Sense
OtakuCulture
DoujinActivity
Performance
Completion
```

### 关系 Relation

```txt
Initiative
SocialListening
Empathy
Boundary
Humor
TrustGeneral
Intimacy
```

### 生活 Survival

```txt
Cooking
Cleaning
TimeManagement
Finance
Sleep
StressManagement
WorkAdaptation
```

---

## 9.4 参数显示方式

默认 UI 显示大方向等级，例如：

```txt
学业 B
语言 C+
职业 D
创作 B-
关系 C
生活 C+
```

点击展开后可显示部分细分参数。

行动选择界面不显示完整细账，只显示主要影响方向：

```txt
【参加 Career Center 说明会】
主要影响：职业 ↑↑，语言 ↑，MH ↓
可能积累：自我分析、业界研究、商业日本语
```

---

# 10. 行动系统

## 10.1 月行动单位

游戏以月为单位安排行动。

推荐结构：

```txt
1 个月 = 4 周
每周 3 个行动格
共 12 个行动格
```

---

## 10.2 行动分类

行动分为：

1. 学习
2. 语言
3. 职业
4. 创作
5. 关系
6. 生活
7. 打工
8. 休息
9. 特殊行动

---

## 10.3 行动风格

同一个行动可提供不同风格。

### 自习

```txt
稳定复习：SP 中等，MH 消耗低
高强度刷题：SP 高，HP/MH 消耗高
轻量整理笔记：SP 低，MH 消耗低
```

### 打工

```txt
正常排班：收益普通，消耗普通
多接班赚钱：Money 高，HP/MH 消耗大
尽量和同事交流：Money 普通，关系/语言有收益
只求平稳结束：收益低，消耗低
```

### 社交

```txt
主动聊天
倾听别人
请求帮助
轻松闲聊
保持距离
```

---

## 10.4 连续行动惩罚

系统必须支持连续行动惩罚。

例如：

* 连续自习导致 MH 消耗增加。
* 连续打工导致 HP/MH 消耗增加。
* 连续休息导致学业/职业进度落后。
* 长期不社交导致关系事件触发率下降。
* 长期不处理就活导致高级就职路线被锁。

---

# 11. 月度方针系统

每个月开始时，玩家选择本月方针。

## 11.1 本月主目标

选项示例：

```txt
学习优先
打工优先
恢复身心
创作优先
关系优先
就职准备
seminar 准备
自由探索
```

## 11.2 本月关系关注对象

玩家可选择本月想投入关系余裕的对象。

```txt
无特别关注
同班同学
seminar 成员
打工前辈
老师 / 教授
社团对象
家人
隐藏角色
```

## 11.3 本月风险策略

```txt
稳健
稍微冒险
拼一把
摆烂
```

该选择影响事件池、压力变化和部分判定。

---

# 12. 技能系统

## 12.1 技能用途

技能不是单纯变强，而是：

* 降低行动惩罚
* 提高特定行动收益
* 解锁新行动
* 解锁事件
* 影响关系推进
* 影响路线判定
* 影响结局条件

---

## 12.2 示例技能

### 日语会话 Lv1

效果：

```txt
社交行动成功率提升
打工面试失败率下降
解锁部分同学关系事件
```

### 学术写作 Lv1

效果：

```txt
报告收益提升
期末レポート事件更容易成功
升学路线开放
```

### 自我分析 Lv1

效果：

```txt
就职路线早期事件开放
Career Center 行动收益提升
```

### 编曲 Lv1

效果：

```txt
创作路线开放
校园祭音乐事件开放
AI Miku 路线前置条件之一
```

### 宅文化研究 Lv1

效果：

```txt
魔法少女研究 seminar 事件收益提升
CM 事件前置条件之一
隐藏内容产业路线开放
```

---

# 13. 关系系统

## 13.1 可发展关系对象

可发展关系对象必须由本地数据定义。
AI 不得凭空创建核心攻略对象。

每个角色应包含：

```txt
角色 ID
姓名
核心设定
所属路线
可推进阶段
关系轴
可触发事件
不可突破边界
隐藏信息揭示条件
```

---

## 13.2 关系轴

单一好感度不足以支撑关系深度。
推荐每个可发展角色包含：

```txt
Trust
Closeness
Respect
Dependency
Tension
Stage
Route
```

关系阶段示例：

```txt
stranger
acquaintance
friendly
trusted
close
romantic
distant
broken
```

---

## 13.3 关系推进原则

关系推进由本地决定，依据：

* 玩家是否投入关系行动
* 玩家在事件中的回应态度
* 对应角色的核心设定
* 关系轴数值
* 相关 flag
* 玩家状态
* 时间节点
* 技能条件

AI 不得擅自让角色突然爱上玩家。

---

## 13.4 事件回应选择

关键关系事件必须提供玩家选择。

例如：

```txt
A. 坦率说自己最近很累
B. 开玩笑带过
C. 礼貌感谢但不深入
D. 反过来关心对方
```

不同选择改变不同关系轴。

---

## 13.5 关系不是随机奖励

关系推进不得主要依赖随机。

随机只能用于：

```txt
1. 某个机会是否在本月出现
2. 同等级事件池中抽取哪一个事件
3. 轻微文本变化
```

关系能否进入下一阶段，必须主要由以下因素决定：

```txt
长期投入
角色相关行动次数
事件回应选择
关系轴数值
技能条件
特定 flag
时间节点
玩家是否主动选择该角色路线
```

例如：

```txt
从 acquaintance 进入 friendly：
- Closeness >= 10
- Trust >= 5
- 至少触发过 1 次共同活动事件
- 本月或上月选择过该角色作为关系关注对象
```

---

# 14. 事件与 flag 系统

## 14.1 事件类型

```txt
制度事件
月度事件
路线事件
关系事件
危机事件
隐藏事件
结局事件
```

---

## 14.2 事件触发原则

事件触发必须由本地系统决定。

触发条件可包括：

* 时间
* 学部
* seminar
* 参数
* 技能
* flag
* 行动次数
* 关系轴
* 随机数
* 机会选择
* 玩家是否参与固定节点活动

---

## 14.3 事件结构

事件数据建议包含：

```txt
eventId
eventName
eventType
eventLevel
timeWindow
conditions
effects
aiInstruction
exclusiveGroup
repeatable
```

---

## 14.4 机会选择

月度结算后，系统可生成多个机会。
玩家不能全部处理，必须选择。

例如：

```txt
本月机会：
1. 同学邀请一起吃饭
2. Career Center 开放早期实习说明会
3. 社团请求玩家帮校园祭做音乐
```

玩家只能选择有限数量。
未选择机会可能消失或延后。

---

# 15. 隐藏路线设计原则

隐藏路线必须长期铺垫，不得突然发生。

---

## 15.1 宅文化 Seminar × 富家隐藏角色路线

前置条件示例：

```txt
选择魔法少女研究 seminar
OtakuCulture 达到阈值
Composition / Drawing / ScenarioWriting 至少一项达到阈值
多次与不起眼 seminar 女生互动
workshop 组队次数达到条件
大三前触发 CM 邀请事件
```

后期可能结果：

```txt
内容产业路线
隐藏富家关系路线
创作者路线
被包养 / 家庭主夫路线
出版相关就职路线
```

---

## 15.2 自闭音游 × AI Miku × 编曲人路线

前置条件示例：

```txt
大一 GPA 低于阈值
旷课打音游事件 >= 3
动漫 / 编曲 / 宅家娱乐行动次数达到阈值
Composition 持续成长
社交投入低
```

大二触发：

```txt
中古市场买到 Hatsune Miku 软件
安装后发现其中搭载独立人格 AI 试作品
```

系统效果：

```txt
创作类事件回报提升
编曲路线加速
孤独负面事件部分缓冲
解锁虚拟搭档创作路线
```

后期可能结果：

```txt
ボカロP
专业编曲人
同人音乐路线
现实失败但创作成功结局
AI Miku 共生结局
```

---

# 16. 结局系统

## 16.1 结局由本地判定

AI 不得自行判定结局。

结局依据：

* 4 年时间结束
* 学业状态
* 职业状态
* 创作状态
* 关系状态
* Money
* HP/MH
* 学部
* seminar
* 技能
* 关键 flag
* 隐藏路线进度

---

## 16.2 示例结局

```txt
优秀就职结局
普通就职结局
升学成功结局
研究生路线结局
内容产业路线结局
创作者结局
ボカロP结局
被包养 / 家庭主夫结局
社交充实结局
普通毕业结局
归国重启结局
打工人生结局
身心崩溃 Bad End
经济危机 Bad End
无内定焦虑结局
现实失败但创作成功结局
```

---

# 17. UI 状态机

## 17.1 多 UI 反复出现原则

本游戏不是单一聊天窗口，也不是一次性线性剧情页面。
游戏流程由多个功能明确的 UI 反复轮换推进。

核心 UI 包括：

```txt
MonthStart / 月初主页
MonthlyPolicy / 月度方针
MonthPlan / 月计划
MonthResolution / 月度结算
OpportunitySelection / 机会选择
Relationship / 人际关系
EventScene / 事件演出
DialogueScene / 日常对话
SkillBoard / 技能成长
MonthlyReview / 月度回顾
Archive / 人物与日志
YearSummary / 年度总结
Ending / 结局
```

这些 UI 会在 48 个月流程中反复出现，而不是一次性使用。

每个 UI 都有明确功能：

* 月初主页：确认当前状态与本月风险。
* 月度方针：选择本月发展重心。
* 月计划：安排本月行动。
* 月度结算：展示本地数值结算结果。
* 机会选择：从本月出现的机会中选择追哪条线。
* 人际关系：查看并推进已建立关系的角色。
* 事件演出：展示本地触发事件的 AI 文本演出。
* 日常对话：处理角色互动与玩家回应选择。
* 技能成长：消耗 SP 解锁技能。
* 月度回顾：展示 AI 生成的本月生活总结。
* 人物与日志：查看历史、人物关系和路线记录。
* 年度总结：每年末展示路线倾向与年度反馈。
* 结局：本地判定结局后，由 AI 生成结局演出。

UI 的切换由本地状态机控制。
AI 不决定下一个 UI 是什么。

---

## 17.2 UI 状态机主权

本地系统必须维护当前 UI 状态，例如：

```txt
currentUiState
currentMonth
pendingEvents
pendingDialogues
pendingRelationshipActions
pendingOpportunities
availableSkillUnlocks
storedAiPayloads
```

UI 状态推进由本地 GameState 和流程规则决定。

标准月循环：

```txt
MonthStart
↓
MonthlyPolicy
↓
MonthPlan
↓
LocalMonthSimulation
↓
MonthResolution
↓
OpportunitySelection
↓
Relationship
↓
EventSceneQueue
↓
DialogueSceneQueue
↓
SkillBoard
↓
MonthlyReview
↓
Archive optional
↓
NextMonth / YearSummary / Ending
```

AI 只能为某些 UI 提供文本内容，不能改变 UI 状态机。

---

## 17.3 本地 UI 与 AI UI 区分

本地 UI：

```txt
月初主页
月度方针
月计划
月度结算
机会选择
技能成长
人物/日志
年度总结中的数值部分
```

AI UI：

```txt
月度回顾
事件演出
日常对话
人际关系状态文本
年度总结演出
结局演出
```

---

# 18. 人际关系 UI

## 18.1 人际关系 UI 的作用

游戏必须包含独立的人际关系 UI。

该 UI 显示已经与玩家建立最低关系阶段的角色。
默认最低显示条件为：

```txt
relationshipStage >= acquaintance
```

人际关系 UI 不是单纯查看好感度的页面，而是玩家主动投入关系资源、选择关系推进方向的重要 UI。

---

## 18.2 人际关系 UI 的可用行动条件

人际关系 UI 中，玩家能对某个角色做什么，不是由 AI 决定，而是由本地系统根据以下条件决定：

```txt
1. 该角色当前关系阶段
2. Trust / Closeness / Respect / Dependency / Tension 等关系轴
3. 当前好感或关系数值
4. 已触发前置事件
5. 已解锁技能
6. 当前月份与时间节点
7. 当前学部 / seminar / 社团 / 打工场所等上下文
8. 特定 flag 状态
9. 角色自身路线限制
10. 玩家当前 HP / MH / Money / 行动余裕
```

示例：

```txt
角色：seminar 里不起眼的女生
关系阶段：acquaintance

可用行动：
- 打招呼
- 讨论 workshop
- 询问她画的角色

暂不可用行动：
- 邀请一起去 CM
  条件不足：OtakuCulture 未达到阈值 / workshop 组队事件未触发
- 深入谈家庭背景
  条件不足：Trust 不足 / 隐藏身份 flag 未开放
```

---

## 18.3 人际关系 UI 的行动类型

人际关系 UI 中可出现的行动类型包括：

```txt
轻量互动：打招呼、闲聊、发消息
共同活动：一起吃饭、一起做 workshop、一起参加活动
求助：向对方请教、请求建议、请求协助
支持：倾听对方烦恼、帮忙、陪伴
试探：询问兴趣、深入话题、邀请参加活动
关系确认：表达感谢、确认距离、推进友情/恋爱/合作关系
保持距离：礼貌回避、拒绝邀约、降低投入
```

每个行动是否出现、是否可选、选后结果如何，均由本地条件决定。

AI 只能生成该行动对应的文本演出或对话，不得自行开放高等级关系行动。

---

# 19. AI 批量返回与本地暂存原则

## 19.1 月度结算时集中生成 AI 文本素材

标准流程中，玩家提交月度计划后，本地系统先完成所有数值、flag、事件、关系和技能判定。

随后调用 AI 时，不应只要求 AI 返回“立即展示的月度回顾”。
AI 应根据本地提供的本月结算结果，返回多个后续 UI 可能使用的结构化文本素材。

这些素材包括但不限于：

```txt
1. monthlyReviewPayload：月度回顾 UI 使用
2. eventScenePayloads：事件演出 UI 使用
3. dialogueScenePayloads：日常对话 UI 使用
4. relationshipPayloads：人际关系 UI 使用
5. opportunityPayloads：机会选择 UI 使用
6. skillUnlockFlavorPayloads：技能成长 UI 可选使用
7. archiveMemoryPayload：人物/日志 UI 使用
```

AI 返回后，这些 payload 不一定马上全部展示。
本地系统应将它们暂存在 GameState 或本地缓存中，等玩家进入对应 UI 时再调用。

---

## 19.2 AI Payload 暂存机制

本地系统应维护：

```txt
storedAiPayloads: {
  monthlyReviewPayload,
  eventScenePayloads,
  dialogueScenePayloads,
  relationshipPayloads,
  opportunityPayloads,
  skillUnlockFlavorPayloads,
  archiveMemoryPayload
}
```

每个 payload 必须包含：

```txt
payloadId
payloadType
month
relatedEventId
relatedCharacterId
relatedUiState
displayCondition
content
consumed
```

其中：

* `payloadId`：本地唯一 ID。
* `payloadType`：文本素材类型。
* `month`：生成月份。
* `relatedEventId`：关联事件，没有则为空。
* `relatedCharacterId`：关联角色，没有则为空。
* `relatedUiState`：该 payload 应在哪个 UI 使用。
* `displayCondition`：展示条件，由本地决定。
* `content`：AI 生成文本内容。
* `consumed`：是否已经展示过。

---

## 19.3 月度结算 UI 的特殊地位

月度结算 UI 不依赖 AI 文本。
月度结算 UI 必须优先展示本地硬结果，例如：

```txt
HP 变化
MH 变化
Money 变化
核心参数变化
技能可解锁状态
事件触发列表
机会候选列表
关系变化
风险 flag
```

AI payload 可以在月度结算后被生成并暂存，但月度结算本身必须由本地 ViewModel 渲染。

---

## 19.4 非立即展示 payload 的原则

AI 返回的内容不必马上全部展示。

例如：

玩家月度计划结算后，AI 返回：

```txt
1. 本月月度回顾文本
2. 同班同学A的关心事件文本
3. 人际关系 UI 中同班同学A可显示的一段状态描述
4. 校园祭机会的一段介绍文本
5. 日志用记忆摘要
```

本地系统应这样处理：

```txt
1. 月度结算 UI 先显示本地结果。
2. opportunityPayloads 暂存在本地，进入机会选择 UI 时展示。
3. relationshipPayloads 暂存在本地，进入人际关系 UI 时展示。
4. eventScenePayloads 暂存在本地，玩家选择查看对应事件时展示。
5. monthlyReviewPayload 暂存在本地，进入月度回顾 UI 时展示。
6. archiveMemoryPayload 暂存在本地，写入日志或人物记录。
```

这样可以减少 AI 调用次数，并保证 UI 轮换过程中仍然有丰富文本。

---

## 19.5 AI 不得通过 payload 改变事实

AI payload 中出现的任何内容都不能直接改变 GameState。

例如，AI payload 写道：

```txt
“她似乎已经把你当成非常重要的人。”
```

但如果本地关系阶段仍为 `acquaintance`，则该文本应视为非法或需要在 prompt 层避免。

本地系统必须以 GameState 为准：

```txt
relationshipStage = acquaintance
Trust = 6
Closeness = 4
```

那么 AI 文本只能表现为：

```txt
“她开始能自然地和你打招呼，但你们仍只是普通同学。”
```

---

## 19.6 AI 批量返回 schema 示例

月度 AI 返回可以采用总 envelope：

```json
{
  "type": "monthly_ai_payload_bundle",
  "month": 5,
  "monthlyReviewPayload": {
    "payloadId": "mr_005",
    "payloadType": "monthly_review",
    "relatedUiState": "MonthlyReview",
    "title": "五月的微妙偏移",
    "summary": "这个月你在学习和打工之间摇摆，但也开始和 seminar 的同学有了更多接触。",
    "paragraphs": [
      "五月的雨来得比想象中频繁。",
      "你开始习惯在课后留下来讨论 workshop，虽然大多数时候只是听别人说。"
    ],
    "memoryUpdate": "第5个月，玩家开始参与 seminar workshop，并与不起眼女生关系略有推进。"
  },
  "eventScenePayloads": [
    {
      "payloadId": "ev_005_01",
      "payloadType": "event_scene",
      "relatedUiState": "EventScene",
      "relatedEventId": "seminar_workshop_first_team",
      "relatedCharacterId": "seminar_girl_01",
      "displayCondition": "event_triggered",
      "title": "第一次组队 workshop",
      "story": [
        "你们被分到同一组时，她只是很轻地看了你一眼。",
        "她的笔记本角落画着一个像魔法少女又像企业吉祥物的角色。"
      ],
      "dialogue": [
        {
          "speaker": "不起眼的 seminar 女生",
          "text": "如果你不介意的话，资料整理我可以先做一版。"
        }
      ],
      "consumed": false
    }
  ],
  "relationshipPayloads": [
    {
      "payloadId": "rel_005_01",
      "payloadType": "relationship_status_text",
      "relatedUiState": "Relationship",
      "relatedCharacterId": "seminar_girl_01",
      "displayCondition": "relationshipStage >= acquaintance",
      "statusText": "她仍然话不多，但已经不会在你靠近时立刻把笔记本合上。",
      "availableActionHints": [
        "可以讨论 workshop",
        "可以询问她画的角色",
        "暂时不适合深入私人话题"
      ],
      "consumed": false
    }
  ],
  "opportunityPayloads": [
    {
      "payloadId": "opp_005_01",
      "payloadType": "opportunity_text",
      "relatedUiState": "OpportunitySelection",
      "relatedEventId": "campus_festival_preparation",
      "title": "校园祭准备的早期机会",
      "description": "社团开始征集能够帮忙制作音乐或视觉素材的人。如果你现在投入创作，十一月或许会有展示机会。",
      "consumed": false
    }
  ],
  "archiveMemoryPayload": {
    "payloadId": "mem_005",
    "payloadType": "archive_memory",
    "relatedUiState": "Archive",
    "memoryText": "第5个月，玩家开始参与 seminar workshop，与不起眼女生建立 acquaintance 关系，并出现校园祭创作机会。",
    "consumed": false
  }
}
```

---

## 19.7 Payload 调用方式

前端进入某个 UI 时，不直接请求 AI。
前端应向本地后端请求该 UI 所需的 payload。

例如：

```txt
GET /api/ui/relationship
```

后端根据 GameState 返回：

```txt
角色列表
每个角色当前关系阶段
可用行动
本地判定条件
已暂存的 relationshipPayload
```

如果没有可用 AI payload，UI 应使用本地 fallback 文本。

---

## 19.8 Payload 生命周期

AI payload 生命周期：

```txt
Generated
↓
Stored
↓
Available
↓
Displayed
↓
Consumed / Archived
```

规则：

* 重要事件 payload 展示后标记为 consumed。
* 月度回顾 payload 展示后写入月志。
* relationshipPayload 可在当月多次显示，也可在下月被新 payload 替换。
* archiveMemoryPayload 可永久写入日志。
* 过期机会 payload 在时间窗口结束后失效。

---

## 19.9 设计目的

该机制的目的：

1. 降低 AI 调用次数。
2. 避免每个 UI 都即时请求 AI。
3. 保证 UI 轮换时仍有丰富文本。
4. 保证 AI 文本和本地结算绑定。
5. 防止 AI 在后续 UI 中擅自改写事实。
6. 将 AI 生成内容变成本地可管理资源，而不是即时聊天回复。

---

# 20. AI 输出 schema 原则

## 20.1 不使用单一巨型 JSON 作为所有 UI 的最终形式

游戏可在月度结算后使用一个总 envelope 批量返回多个 payload。

但每个 payload 仍然必须对应明确 UI 和明确 schema。

示例：

```txt
MonthlyReviewPayload
EventScenePayload
DialogueScenePayload
RelationshipPayload
OpportunityPayload
ArchiveMemoryPayload
EndingScenePayload
```

---

## 20.2 MonthlyReviewPayload

```json
{
  "payloadId": "mr_001",
  "payloadType": "monthly_review",
  "relatedUiState": "MonthlyReview",
  "month": 1,
  "title": "标题",
  "tone": "normal",
  "summary": "本月总结",
  "paragraphs": ["段落1", "段落2"],
  "keywords": ["关键词1", "关键词2"],
  "memoryUpdate": "供本地参考的简短记忆",
  "consumed": false
}
```

---

## 20.3 EventScenePayload

```json
{
  "payloadId": "ev_001",
  "payloadType": "event_scene",
  "relatedUiState": "EventScene",
  "eventId": "event_id",
  "eventLevel": "minor",
  "title": "事件标题",
  "location": "地点",
  "characters": [
    {
      "id": "character_id",
      "name": "角色名",
      "mood": "情绪"
    }
  ],
  "story": ["描写1", "描写2"],
  "dialogue": [
    {
      "speaker": "角色名",
      "text": "台词"
    }
  ],
  "resultText": "事件结果演出",
  "memoryUpdate": "事件记忆",
  "consumed": false
}
```

---

## 20.4 DialogueScenePayload

```json
{
  "payloadId": "dlg_001",
  "payloadType": "dialogue_scene",
  "relatedUiState": "DialogueScene",
  "dialogueId": "dialogue_id",
  "characterId": "character_id",
  "characterName": "角色名",
  "relationshipStage": "acquaintance",
  "mood": "mood",
  "opening": [
    {
      "speaker": "角色名",
      "text": "台词"
    }
  ],
  "playerOptions": [
    {
      "optionId": "honest",
      "text": "玩家选项"
    }
  ],
  "memoryUpdate": "对话记忆",
  "consumed": false
}
```

---

## 20.5 RelationshipPayload

```json
{
  "payloadId": "rel_001",
  "payloadType": "relationship_status_text",
  "relatedUiState": "Relationship",
  "relatedCharacterId": "character_id",
  "displayCondition": "relationshipStage >= acquaintance",
  "statusText": "角色当前与玩家关系的自然语言描述",
  "availableActionHints": [
    "可以闲聊",
    "可以讨论课堂内容",
    "暂时不适合深入私人话题"
  ],
  "consumed": false
}
```

---

## 20.6 OpportunityPayload

```json
{
  "payloadId": "opp_001",
  "payloadType": "opportunity_text",
  "relatedUiState": "OpportunitySelection",
  "relatedEventId": "event_id",
  "title": "机会标题",
  "description": "机会介绍文本",
  "riskHint": "可能风险",
  "rewardHint": "可能收益",
  "consumed": false
}
```

---

## 20.7 AI 输出校验

本地系统必须：

1. 尝试解析 JSON。
2. 校验 type。
3. 校验必要字段。
4. 丢弃 AI 返回中的非法数值修改。
5. 丢弃不符合当前 GameState 的文本。
6. 解析失败时显示 fallback。
7. 必要时重试一次。
8. 连续失败时显示调试文本，不影响 GameState 主权。

---

# 21. 前端渲染原则

AI 不直接输出 HTML。
AI 只输出结构化文本数据。
HTML 由本地前端固定模板渲染。

目的：

* 保证 UI 稳定。
* 避免模型破坏格式。
* 避免每次输出信息过量。
* 让不同 UI part 目的清晰。
* 允许同一 payload 在不同 UI 中复用。

---

# 22. 第一阶段 MVP 范围

## 22.1 MVP 目标

先做可运行 Demo，不追求完整 48 个月。

建议 MVP：

```txt
12 个月
1 个学部
2 个 seminar
6 个核心参数
10 个行动
5 个技能
8 个事件
2 个可发展角色
3 个结局
AI 月度回顾
AI 事件演出
AI 人际关系状态文本
```

---

## 22.2 MVP 必须包含

* 月度主循环
* 本地 GameState
* 月度方针
* 月计划 UI
* 本地结算
* 技能解锁
* 固定时间节点
* 事件触发
* 人际关系 UI
* 机会选择 UI
* AI 月度 payload bundle
* AI 月度回顾
* AI 事件演出
* AI 关系状态文本
* 至少一个隐藏路线种子
* JSON schema 校验
* 本地 HTML 渲染

---

## 22.3 MVP 不做

* 完整 48 个月
* 多存档系统
* 复杂角色立绘
* 图片生成
* BGM
* 复杂战斗
* 在线用户系统
* 云同步
* 完整 tool calling
* 完整移动端适配

---

# 23. 技术要求

## 23.1 技术栈

```txt
C# / .NET 8
ASP.NET Core Minimal API
HTML / CSS / JavaScript
JSON 文件存储初期数据
DeepSeek API 或 OpenRouter API
Git / GitHub
VS Code
```

---

## 23.2 后端职责

后端负责：

* GameState 管理
* UI 状态机
* 月度结算
* 参数计算
* 技能系统
* 事件触发
* flag 管理
* 角色关系系统
* 结局判定
* storedAiPayloads 管理
* 读取 prompt
* 调用 LLM API
* 校验 AI JSON
* 向前端返回 ViewModel

---

## 23.3 前端职责

前端负责：

* 显示 UI 状态机
* 提供月度行动选择
* 提供方针选择
* 展示数值变化
* 展示事件机会
* 展示人际关系 UI
* 展示 AI 演出文本
* 展示技能树
* 展示人物/日志
* 渲染固定 HTML 模板

---

## 23.4 AI 调用职责

AI 负责生成：

* MonthlyReviewPayload
* EventScenePayload
* DialogueScenePayload
* RelationshipPayload
* OpportunityPayload
* ArchiveMemoryPayload
* YearSummaryPayload
* EndingScenePayload

AI 不负责：

* GameState
* 数值计算
* 事件触发
* 技能解锁
* flag
* 路线进入
* 结局判定
* UI 状态机

---

# 24. 非功能需求

## 24.1 可维护性

* 游戏规则与文本演出分离。
* 行动、技能、事件、角色应尽量数据化。
* AI prompt 与代码分离。
* 每个 UI 使用独立 ViewModel / Response schema。
* AI payload 必须可保存、可调用、可失效。

---

## 24.2 可扩展性

系统应支持：

* 增加学部
* 增加 seminar
* 增加行动
* 增加技能
* 增加角色
* 增加隐藏路线
* 增加结局
* 扩展到 48 个月
* 替换 JSON 输出为 tool calling 或 structured outputs

---

## 24.3 稳定性

* AI JSON 解析失败不能破坏存档。
* AI 返回非法内容不能修改 GameState。
* AI payload 过期后不能继续触发旧事件。
* 本地结算必须可复现。
* 每个月推进前后必须有明确状态。
* 前端进入任意 UI 时，应优先使用本地 GameState，再附加 AI payload。

---

## 24.4 成本控制

* 不应每个小动作都调用 AI。
* 标准月循环中 AI 调用应集中在月度结算后的 payload bundle 生成。
* 本地系统能处理的数值和判定不得交给 AI。
* 后续 UI 主要调用已暂存 payload，不即时请求 AI。

---

# 25. 验收标准

第一阶段完成标准：

1. 启动本地 Web 应用。
2. 创建新游戏。
3. 选择学部。
4. 进入第 1 月。
5. 选择本月方针。
6. 安排本月行动。
7. 后端完成本地结算。
8. 显示月度结算 UI。
9. 根据本地条件触发事件。
10. 调用 AI 生成 monthly_ai_payload_bundle。
11. 本地保存 monthlyReviewPayload。
12. 本地保存 eventScenePayloads。
13. 本地保存 relationshipPayloads。
14. 本地保存 opportunityPayloads。
15. 进入机会选择 UI 时显示本地机会与 AI opportunity 文本。
16. 进入人际关系 UI 时显示 acquaintance 以上角色与可用行动。
17. 进入事件演出 UI 时展示已暂存 event payload。
18. 进入月度回顾 UI 时展示已暂存 monthly review。
19. GameState 正确推进到下个月。
20. 技能和 flag 不受 AI 非法输出影响。
21. 至少可以连续推进 6 个月。
22. 至少存在 1 条隐藏路线种子。
23. 至少存在 1 个非随机、由长期选择触发的角色事件。
24. 至少存在 1 个固定时间节点，例如考试或校园祭。
25. 至少存在 1 个结局判定 Demo。

---

# 26. 对 Coding Agent 的要求

开发时必须遵守：

1. 不要把 AI 当游戏裁判。
2. 不要让 AI 直接修改 GameState。
3. 不要让 AI 输出 HTML。
4. 不要一开始做复杂前端框架。
5. 不要引入数据库作为第一版依赖。
6. 不要一次性实现完整 48 个月。
7. 先跑通最小月循环。
8. 所有重要规则必须本地实现。
9. 事件和角色路线必须数据化。
10. UI 状态机必须清晰。
11. 每次完成一个功能后确保项目可运行。
12. 不得因为方便而把数值判定交给 prompt。
13. 不要每个 UI 都即时请求 AI。
14. AI 文本应在月度结算后批量生成并暂存。
15. 前端进入 UI 时应调用本地 payload，而不是直接调用 AI。

---

# 27. 一句话定位

《日留学部模拟器》不是 AI 聊天游戏，而是：

> **本地规则驱动的日本学部四年养成 SLG，AI 只负责把玩家选择造成的后果写得有温度、有浪漫、有荒诞感。**
