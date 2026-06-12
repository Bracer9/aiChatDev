# 《日留学部模拟器》Demo HLD

High Level Design / 高层设计文档

版本：0.1
目标：开发一个可运行的 AI 演出型日本学部养成 SLG Demo
技术栈：C# / .NET 8 / ASP.NET Core Minimal API / HTML + CSS + JavaScript / DeepSeek 或 OpenRouter API

---

# 1. Demo 定位

《日留学部模拟器》Demo 是一个本地规则驱动、AI 负责文本演出的养成 SLG 原型。

玩家扮演一名在日本读学部的留学生，以“月”为单位安排学习、语言、职业、创作、生活、关系等行动。系统在本地结算 HP、MP、Money、核心参数、技能、事件和关系变化。AI 只根据本地结算结果生成月度回顾、事件演出、人际关系状态文本等内容。

本 Demo 的目标不是制作完整 4 年制游戏，而是验证核心闭环：

```txt
月度计划
↓
本地结算
↓
参数成长
↓
技能觉醒/升级
↓
事件与关系机会触发
↓
AI 生成文本 payload
↓
UI 轮换展示
↓
进入下个月
```

---

# 2. Demo 范围

## 2.1 Demo 时间长度

第一版 Demo 建议实现：

```txt
12 个月
```

后续架构应允许扩展到：

```txt
48 个月 / 4 年
```

## 2.2 Demo 内容范围

第一版包含：

```txt
1 个学部
2 个 seminar
6 个成长方向
3 个生存资源
10 个具体行动
10 个技能
8 个事件
2 个可发展角色
3 个结局
AI 月度回顾
AI 事件演出
AI 人际关系状态文本
```

## 2.3 Demo 不做

第一版不做：

```txt
完整 48 个月
复杂数据库
多存档
复杂前端框架
图片生成
BGM
复杂立绘
移动端深度适配
完整 tool calling
复杂细分参数系统
技能独立经验条
SP / Insight Point
```

---

# 3. 核心设计原则

## 3.1 本地规则拥有游戏性主权

所有影响游戏性的内容由本地 GameState 决定：

```txt
数值
技能
行动结果
事件触发
关系阶段
flag
结局判定
UI 状态机
```

AI 不得直接修改 GameState。

## 3.2 AI 只负责文本演出

AI 负责：

```txt
月度回顾文本
事件演出文本
关系状态文本
机会介绍文本
日志文本
结局演出文本
```

AI 不负责：

```txt
数值计算
行动是否成功
技能是否觉醒
事件是否触发
角色关系是否升级
结局是否达成
```

## 3.3 多 UI 轮换推进

游戏不是聊天窗口。
游戏由多个固定 UI 轮换推进。

Demo 中至少实现：

```txt
MonthStart / 月初主页
MonthlyPolicy / 月度方针
MonthPlan / 月计划
MonthResolution / 月度结算
OpportunitySelection / 机会选择
Relationship / 人际关系
EventScene / 事件演出
SkillBoard / 技能成长
MonthlyReview / 月度回顾
Archive / 日志
Ending / 结局
```

## 3.4 AI 文本批量生成并暂存

玩家提交月度计划后：

```txt
本地先结算
↓
AI 一次性生成本月多个 UI 会用到的 payload
↓
本地保存 storedAiPayloads
↓
后续 UI 按需读取
```

不要每切一个 UI 就调用一次 AI。

---

# 4. 系统整体架构

## 4.1 架构概览

```txt
Browser UI
  |
  | HTTP / fetch
  v
ASP.NET Core Minimal API
  |
  | GameState / Rules / Event System
  v
Local Game Engine
  |
  | PromptBuilder
  v
LLM API Client
  |
  | DeepSeek / OpenRouter
  v
AI Payload Bundle
  |
  | Validation / Storage
  v
storedAiPayloads
  |
  v
Browser UI Rendering
```

## 4.2 后端主要职责

后端负责：

```txt
GameState 管理
UI 状态机推进
行动合法性检查
月度行动结算
核心参数成长
技能觉醒/升级
事件触发
关系状态更新
机会生成
AI prompt 生成
AI API 调用
AI JSON 校验
AI payload 暂存
向前端提供 UI ViewModel
```

## 4.3 前端主要职责

前端负责：

```txt
显示当前 UI
提交玩家选择
渲染状态栏
渲染月度计划
渲染结算结果
渲染机会选择
渲染人际关系
渲染事件演出
渲染技能界面
渲染月度回顾
渲染日志和结局
```

前端不直接调用 AI API。

---

# 5. 推荐项目结构

```txt
NichiryuSim/
  Program.cs
  appsettings.json

  Data/
    actions.json
    skills.json
    events.json
    characters.json
    seminars.json
    endings.json

  Prompts/
    monthly_payload_system_prompt.txt

  Models/
    GameState.cs
    PlayerStats.cs
    CoreParameters.cs
    SkillState.cs
    CharacterRelationship.cs
    MonthPlan.cs
    MonthResolution.cs
    ActionDefinition.cs
    EventDefinition.cs
    AiPayloadBundle.cs

  Services/
    GameStateService.cs
    MonthSimulationService.cs
    ActionResolver.cs
    SkillService.cs
    EventService.cs
    RelationshipService.cs
    OpportunityService.cs
    PromptBuilder.cs
    LlmClient.cs
    AiPayloadValidator.cs
    UiStateService.cs

  wwwroot/
    index.html
    style.css
    app.js
```

---

# 6. GameState 设计

## 6.1 GameState

```csharp
public class GameState
{
    public int CurrentMonth { get; set; }
    public int MaxMonth { get; set; } = 12;

    public string CurrentUiState { get; set; } = "MonthStart";

    public string FacultyId { get; set; } = "default_faculty";
    public string? SeminarId { get; set; }

    public PlayerStats Stats { get; set; } = new();
    public CoreParameters Core { get; set; } = new();

    public Dictionary<string, SkillState> Skills { get; set; } = new();
    public Dictionary<string, int> ActionCounters { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();

    public Dictionary<string, CharacterRelationship> Relationships { get; set; } = new();

    public List<string> TriggeredEventIds { get; set; } = new();
    public List<string> PendingEventIds { get; set; } = new();
    public List<string> PendingOpportunityIds { get; set; } = new();

    public AiPayloadBundle? StoredAiPayloads { get; set; }

    public List<string> MonthlyLogs { get; set; } = new();
}
```

---

## 6.2 PlayerStats

```csharp
public class PlayerStats
{
    public int HP { get; set; } = 100;
    public int MaxHP { get; set; } = 100;

    public int MP { get; set; } = 100;
    public int MaxMP { get; set; } = 100;

    public int Money { get; set; } = 50000;
}
```

规则：

```txt
HP <= 0：强制入院
MP <= 0：强制摆烂一个月
Money 可以暂时为负，但会触发经济危机事件
```

---

## 6.3 CoreParameters

Demo 只保留 6 个核心成长方向。

```csharp
public class CoreParameters
{
    public int AcademicExp { get; set; }
    public int LanguageExp { get; set; }
    public int CareerExp { get; set; }
    public int CreationExp { get; set; }
    public int LifeExp { get; set; }
    public int RelationshipExp { get; set; }

    public int AcademicLv => AcademicExp / 100;
    public int LanguageLv => LanguageExp / 100;
    public int CareerLv => CareerExp / 100;
    public int CreationLv => CreationExp / 100;
    public int LifeLv => LifeExp / 100;
    public int RelationshipLv => RelationshipExp / 100;
}
```

Demo 阶段不做细分参数。

---

# 7. 行动系统

## 7.1 行动大类

Demo 中存在 7 个行动大类：

```txt
Academic / 学业
Language / 语言
Career / 职业
Creation / 创作
Life / 生活
Relationship / 关系
Free / 自由行动
```

其中：

* 前 6 个对应 6 个核心参数。
* Free 行动必须先选择所属大类，再输入补充文本。
* 自由文本只影响 AI 演出，不直接决定数值。

---

## 7.2 月度行动结构

一个月由 4 周组成，每周 3 个行动位：

```txt
1 个月 = 4 周 × 3 行动 = 12 行动
```

前端 UI 应显示 12 个行动槽。

---

## 7.3 ActionDefinition

```csharp
public class ActionDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";

    public int HpCost { get; set; }
    public int MpCost { get; set; }
    public int MoneyDelta { get; set; }

    public Dictionary<string, int> CoreExpDelta { get; set; } = new();

    public List<string> SkillTags { get; set; } = new();

    public List<string> RequiredFlags { get; set; } = new();
    public List<string> RequiredSkills { get; set; } = new();

    public int CooldownMonths { get; set; } = 0;
    public bool CannotRepeatInSameMonth { get; set; } = false;
}
```

---

## 7.4 Demo 行动列表

### 学业

```txt
在家学习
HP -4 / MP -5 / 学业 +8
SkillTags: 学习习惯

去图书馆学习
HP -6 / MP -6 / 学业 +12
SkillTags: 学习习惯, 学术写作

使用 AI 学习
HP -4 / MP -7 / 学业 +10
SkillTags: 学习习惯, 学术写作

和朋友一起学习
HP -6 / MP -5 / 学业 +8 / 语言 +3 / 关系 +3
SkillTags: 学习习惯, 日语会话
前置：至少一个角色 relationshipStage >= acquaintance
```

### 语言

```txt
看教材背单词
HP -3 / MP -5 / 语言 +8
SkillTags: 日语会话

影子跟读
HP -4 / MP -6 / 语言 +12
SkillTags: 日语会话

和 seminar 同学聊天
HP -4 / MP -6 / 语言 +8 / 关系 +5
SkillTags: 日语会话, 闲谈能力
前置：seminar 已选择
```

### 职业

```txt
业界分析
HP -4 / MP -7 / 职业 +10
SkillTags: 自我分析

自我分析
HP -3 / MP -8 / 职业 +12
SkillTags: 自我分析

预约学校就职中心
HP -5 / MP -8 / 职业 +14
SkillTags: 自我分析, 面试技巧
```

### 创作

```txt
看剧场版动画片
HP -2 / MP +4 / Money -1800 / 创作 +6
SkillTags: 宅文化研究, 扒谱能力

宅家打游戏
HP -1 / MP +6 / 创作 +4
SkillTags: 宅文化研究, 扒谱能力

绘画练习
HP -4 / MP -6 / 创作 +10
SkillTags: 宅文化研究

编曲练习
HP -5 / MP -8 / 创作 +14
SkillTags: 扒谱能力
前置：扒谱能力 Lv1
```

### 生活

```txt
研究做饭
HP +6 / MP +3 / Money -800 / 生活 +8
SkillTags: 生活自理

整理房间
HP -2 / MP +8 / 生活 +8
SkillTags: 生活自理
CD：同月不可连续选择

去健身房
HP -8 / MP +4 / Money -1200 / 生活 +12
SkillTags: 强健体魄

便利店打工
HP -12 / MP -8 / Money +5000 / 生活 +5
SkillTags: 强健体魄, 生活自理
前置：便利店打工录用 flag
```

### 关系

```txt
和朋友打电话
HP -2 / MP +8 / 关系 +8
SkillTags: 闲谈能力

约人参加活动
HP -6 / MP -4 / Money -2000 / 关系 +12
SkillTags: 闲谈能力
前置：当前有可参加活动，并且有 acquaintance 以上角色

研究时尚杂志和化妆
HP -2 / MP -3 / Money -1000 / 关系 +8
SkillTags: 闲谈能力
```

### 自由行动

自由行动不单独定义固定收益。
玩家必须先选择大类。

例如：

```txt
自由行动类别：创作
自定义说明：研究魔法少女动画里的配乐结构
```

本地按对应自由行动模板结算：

```txt
自由学业行动：HP -5 / MP -6 / 学业 +8
自由语言行动：HP -4 / MP -6 / 语言 +8
自由职业行动：HP -4 / MP -7 / 职业 +8
自由创作行动：HP -5 / MP -7 / 创作 +8
自由生活行动：HP +4 / MP +4 / 生活 +5
自由关系行动：HP -3 / MP -4 / 关系 +8
```

---

# 8. 行动结算流程

## 8.1 结算流程

每个行动按以下步骤处理：

```txt
1. 检查是否可执行
2. 计算实际 HP / MP / Money 变化
3. 计算成功分
4. 判定结果等级
5. 根据结果倍率应用成长
6. 累计 SkillTag 行动次数
7. 检查技能觉醒/升级
8. 检查事件触发
9. 更新 GameState
```

---

## 8.2 可执行检查

检查：

```txt
HP 是否足够
MP 是否足够
Money 是否足够
RequiredFlags 是否满足
RequiredSkills 是否满足
是否 CD 中
是否违反 CannotRepeatInSameMonth
```

如果不满足，前端应将行动显示为不可选。

---

## 8.3 成功分计算

简化公式：

```txt
成功分 =
50
+ 对应核心参数等级 × 3
+ 相关技能最高等级 × 5
+ MP 状态修正
+ 随机值(-15 到 +15)
```

MP 状态修正：

```txt
MP >= 70：+5
MP 40-69：0
MP 20-39：-8
MP < 20：-15
```

---

## 8.4 结果等级

```txt
成功分 < 40：失败
40 <= 成功分 < 75：普通
成功分 >= 75：大成功
```

---

## 8.5 结果倍率

```txt
失败：收益 × 0.5，MP 额外 -3
普通：收益 × 1.0
大成功：收益 × 1.5，SkillTag 次数 +2
```

普通情况下 SkillTag 次数 +1。

---

# 9. HP / MP 归零规则

## 9.1 HP 归零

当 HP <= 0：

```txt
触发 hospitalized flag
本月剩余行动取消
Money -10000
HP 恢复到 50
MP -10
生成入院事件
```

可能影响：

```txt
考试缺席
打工排班中断
关系对象探望事件
隐藏路线开启或关闭
```

---

## 9.2 MP 归零

当 MP <= 0：

```txt
触发 burnout flag
强制摆烂一个月
拒绝一切非强制行动
制度事件可能失败或缺席
MP 恢复到 50
HP +10
```

MP 归零不一定只代表坏结局。
部分隐藏路线可利用该状态。

例如：

```txt
burnout flag
+ 音游/宅家/创作相关行动次数高
+ 创作 Lv 达标
= 可能触发 AI Miku 路线前置事件
```

---

# 10. 技能系统

## 10.1 Demo 技能原则

Demo 不做复杂 SkillExp。
技能通过以下条件觉醒和升级：

```txt
核心参数等级
相关 SkillTag 行动次数
关键 flag
关键事件结果
```

技能状态：

```txt
Hidden / 未发现
Available / 可觉醒
Awakened / 已觉醒
```

技能等级：

```txt
Lv1
Lv2
Lv3
```

---

## 10.2 SkillState

```csharp
public class SkillState
{
    public string SkillId { get; set; } = "";
    public string Status { get; set; } = "Hidden"; // Hidden, Available, Awakened
    public int Level { get; set; } = 0;
}
```

---

## 10.3 Demo 技能列表

### 学业

#### 学术写作

```txt
Lv1 觉醒条件：
学业 Lv2
学术写作 tag 行动次数 >= 3
触发第一次レポート事件

Lv1 效果：
レポート事件成功率 +15
学业行动 MP 消耗 -1

Lv2 条件：
学业 Lv4
レポート事件成功 1 次

Lv2 效果：
レポート事件成功率 +25

Lv3 条件：
学业 Lv6
seminar 小发表成功

Lv3 效果：
升学路线开放
```

#### 学习习惯

```txt
Lv1 条件：
学业 Lv2
学习习惯 tag 行动次数 >= 4

效果：
学业行动 MP 消耗 -1

Lv2 条件：
学业 Lv4
连续两个月至少安排 3 次学业行动

效果：
学业行动成功分 +5

Lv3 条件：
学业 Lv6
期末考试成功

效果：
连续学习惩罚降低
```

---

### 语言

#### 日语会话

```txt
Lv1 条件：
语言 Lv2
日语会话 tag 行动次数 >= 3

效果：
关系行动成功分 +5
打工面试成功率 +10

Lv2 条件：
语言 Lv4
seminar 同学聊天行动 >= 3

效果：
关系事件成功分 +10

Lv3 条件：
语言 Lv6
校园祭/发表类事件成功

效果：
高级关系事件开放
```

#### 闲谈能力

```txt
Lv1 条件：
关系 Lv2 或 语言 Lv2
闲谈能力 tag 行动次数 >= 3

效果：
acquaintance 角色更容易出现关系行动

Lv2 条件：
关系 Lv4
至少触发 1 次朋友事件

效果：
关系事件好感收益增加

Lv3 条件：
关系 Lv6
至少一名角色达到 friend

效果：
特殊关系事件开放
```

---

### 职业

#### 自我分析

```txt
Lv1 条件：
职业 Lv2
自我分析 tag 行动次数 >= 3

效果：
就职相关机会开放

Lv2 条件：
职业 Lv4
Career Center 相关行动 >= 2

效果：
实习事件开放

Lv3 条件：
职业 Lv6
实习事件成功

效果：
高级就职路线开放
```

#### 面试技巧

```txt
Lv1 条件：
职业 Lv3
面试技巧 tag 行动次数 >= 2

效果：
面试类事件成功分 +10

Lv2 条件：
职业 Lv5
至少 1 次模拟面试成功

效果：
面试类事件成功分 +20

Lv3 条件：
职业 Lv7
实习或本选考事件成功

效果：
优秀就职结局候选开放
```

---

### 创作

#### 扒谱能力

```txt
Lv1 条件：
创作 Lv2
扒谱能力 tag 行动次数 >= 5
触发“听出旋律结构”事件

效果：
解锁“编曲练习”
AI Miku 路线前置条件之一

Lv2 条件：
创作 Lv4
编曲练习 >= 3

效果：
创作行动成功分 +5

Lv3 条件：
创作 Lv6
校园祭音乐事件成功 或 AI Miku 前置事件触发

效果：
ボカロP路线开放
```

#### 宅文化研究

```txt
Lv1 条件：
创作 Lv2
宅文化研究 tag 行动次数 >= 4

效果：
魔法少女研究 seminar 事件收益增加

Lv2 条件：
创作 Lv4
选择魔法少女研究 seminar

效果：
CM 机会事件开放

Lv3 条件：
创作 Lv6
CM 事件触发

效果：
内容产业隐藏路线开放
```

---

### 生活

#### 强健体魄

```txt
Lv1 条件：
生活 Lv2
强健体魄 tag 行动次数 >= 3

效果：
HP 消耗 -1

Lv2 条件：
生活 Lv4
去健身房行动 >= 3

效果：
HP 消耗 -2

Lv3 条件：
生活 Lv6
最近 3 个月 HP 未归零

效果：
病倒事件概率下降
```

#### 生活自理

```txt
Lv1 条件：
生活 Lv2
生活自理 tag 行动次数 >= 3

效果：
做饭/整理房间效果提升

Lv2 条件：
生活 Lv4
Money 未低于 0 且 HP 未归零

效果：
每月基础生活消耗降低

Lv3 条件：
生活 Lv6
连续 3 个月未触发生活危机

效果：
生活类行动 MP 恢复增加
```

---

# 11. 关系系统 Demo

## 11.1 简化关系值

Demo 只做两个关系数值：

```txt
Affection / 好感度
Trust / 信赖度
```

## 11.2 关系阶段

```txt
stranger
acquaintance
friend
close
special
```

## 11.3 CharacterRelationship

```csharp
public class CharacterRelationship
{
    public string CharacterId { get; set; } = "";
    public string Stage { get; set; } = "stranger";

    public int Affection { get; set; } = 0;
    public int Trust { get; set; } = 0;

    public List<string> Flags { get; set; } = new();
}
```

## 11.4 阶段升级条件

```txt
stranger → acquaintance：
触发初见事件

acquaintance → friend：
Affection >= 20
Trust >= 10
触发共同活动事件

friend → close：
Affection >= 45
Trust >= 35
触发关键关系事件

close → special：
Affection >= 70
Trust >= 60
角色路线 flag 满足
```

---

# 12. Demo 角色

## 12.1 Seminar 不起眼女生

用途：

```txt
魔法少女研究 seminar
宅文化隐藏路线
CM 事件
富家隐藏角色路线
```

初始：

```txt
Stage: stranger
```

出现条件：

```txt
选择魔法少女研究 seminar 后触发初见
```

关键路线：

```txt
workshop 组队
关系达到 friend
CM 邀请事件
隐藏身份伏笔
```

---

## 12.2 同班朋友

用途：

```txt
普通关系线
学习辅助
日语会话
关系系统基础验证
```

初始：

```txt
Stage: acquaintance
```

关键路线：

```txt
一起学习
电话聊天
校园祭一起行动
普通朋友/close关系
```

---

# 13. 事件系统 Demo

## 13.1 EventDefinition

```csharp
public class EventDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int StartMonth { get; set; }
    public int EndMonth { get; set; }

    public List<string> RequiredFlags { get; set; } = new();
    public List<string> ForbiddenFlags { get; set; } = new();

    public Dictionary<string, int> RequiredCoreLv { get; set; } = new();
    public Dictionary<string, int> RequiredSkillLv { get; set; } = new();

    public List<string> SetFlagsOnTrigger { get; set; } = new();

    public string AiInstruction { get; set; } = "";
}
```

---

## 13.2 Demo 事件 8 个

```txt
1. 第一次レポート事件
类型：制度事件
时间：Month 2
作用：学术写作觉醒前置

2. 便利店打工录用事件
类型：生活/打工事件
时间：Month 1-3
条件：生活 Lv1 或 Money < 30000
作用：解锁便利店打工

3. 听出旋律结构事件
类型：创作事件
条件：创作 Lv2，扒谱能力 tag 次数 >= 5
作用：扒谱能力觉醒前置

4. Seminar 初见事件
类型：关系/seminar事件
条件：选择魔法少女研究 seminar
作用：不起眼女生变为 acquaintance

5. Workshop 组队事件
类型：关系事件
条件：不起眼女生 acquaintance，创作 Lv2 或 学业 Lv2
作用：关系推进，CM路线前置

6. 校园祭准备事件
类型：固定时间节点
时间：Month 8-10
条件：创作 Lv2 或 关系 Lv2
作用：校园祭参与机会

7. AI Miku 前置事件
类型：隐藏事件
条件：burnout flag 或 旷课/宅家/创作次数高，创作 Lv3，扒谱能力 Lv1
作用：AI Miku路线种子

8. Career Center 早期说明会
类型：职业事件
时间：Month 3-6
条件：职业 Lv1 或月度方针为就职准备
作用：自我分析觉醒前置
```

---

# 14. 机会选择系统

月度结算后，系统根据事件与状态生成机会。

示例：

```txt
本月机会：
- 校园祭准备：投入创作可在 11 月展示作品
- Seminar 女生 workshop：投入关系可推进隐藏路线
- Career Center 说明会：投入职业可开启就职路线
```

玩家不能全部选择。
Demo 中每月最多选择：

```txt
1 个机会
```

选择机会后：

```txt
设置对应 flag
触发对应 EventScenePayload
可能改变角色关系
```

---

# 15. AI Payload Bundle

## 15.1 调用时机

每月行动结算完成后调用一次 AI。

输入：

```txt
当前月份
本月行动
本地结算结果
触发事件
可用机会
关系变化
技能变化
GameState 摘要
```

输出：

```txt
monthly_ai_payload_bundle
```

---

## 15.2 AiPayloadBundle

```csharp
public class AiPayloadBundle
{
    public string Type { get; set; } = "monthly_ai_payload_bundle";
    public int Month { get; set; }

    public MonthlyReviewPayload? MonthlyReviewPayload { get; set; }
    public List<EventScenePayload> EventScenePayloads { get; set; } = new();
    public List<RelationshipPayload> RelationshipPayloads { get; set; } = new();
    public List<OpportunityPayload> OpportunityPayloads { get; set; } = new();
    public ArchiveMemoryPayload? ArchiveMemoryPayload { get; set; }
}
```

---

## 15.3 Payload 基本字段

所有 payload 应包含：

```txt
payloadId
payloadType
relatedUiState
month
consumed
```

---

## 15.4 AI 不得返回数值修改

AI payload 中不应包含：

```txt
HP变化
MP变化
Money变化
技能解锁
关系阶段修改
flag修改
```

如出现，后端应忽略。

---

# 16. UI 设计

## 16.1 UI 状态列表

```txt
MonthStart
MonthlyPolicy
MonthPlan
MonthResolution
OpportunitySelection
Relationship
EventScene
SkillBoard
MonthlyReview
Archive
Ending
```

---

## 16.2 月循环状态流

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
AI Payload Bundle Generation
↓
OpportunitySelection
↓
Relationship
↓
EventScene
↓
SkillBoard
↓
MonthlyReview
↓
Archive optional
↓
NextMonth or Ending
```

---

## 16.3 MonthStart UI

显示：

```txt
当前月份
HP / MP / Money
6 个核心参数等级
已觉醒技能
本月提醒
```

---

## 16.4 MonthlyPolicy UI

玩家选择：

```txt
本月主目标
本月关系关注对象
本月风险策略
```

示例：

```txt
主目标：创作优先
关系关注：Seminar 不起眼女生
风险策略：稍微冒险
```

---

## 16.5 MonthPlan UI

显示：

```txt
4 周 × 3 行动格
行动大类下拉菜单
具体行动下拉菜单
自由行动补充说明
预计消耗
预计收益
```

---

## 16.6 MonthResolution UI

纯本地显示：

```txt
HP 变化
MP 变化
Money 变化
核心参数经验变化
技能可觉醒/升级
触发事件
生成机会
关系变化
```

---

## 16.7 OpportunitySelection UI

显示本月可选机会。

每个机会显示：

```txt
机会标题
AI 生成介绍文本
本地显示风险
本地显示可能收益
选择按钮
```

---

## 16.8 Relationship UI

显示 relationshipStage >= acquaintance 的角色。

每个角色显示：

```txt
角色名
关系阶段
好感度
信赖度
AI 生成状态文本
可用关系行动
不可用行动提示
```

可用行动由本地决定。

---

## 16.9 EventScene UI

展示已触发事件的 AI 文本。

显示：

```txt
事件标题
地点
角色
事件正文
台词
结果说明
```

---

## 16.10 SkillBoard UI

显示技能：

```txt
未发现
可觉醒
已觉醒 Lv1-Lv3
觉醒条件
升级条件
技能效果
```

玩家可以点击觉醒或升级。
Demo 中不消耗 SP。

---

## 16.11 MonthlyReview UI

展示 AI 月度回顾：

```txt
标题
本月总结
段落文本
关键词
```

---

## 16.12 Archive UI

显示：

```txt
月度日志
已触发事件
角色关系历史
隐藏路线线索
```

---

# 17. API 设计

## 17.1 获取当前状态

```http
GET /api/state
```

返回：

```txt
GameState 简要
当前 UI
当前月份
状态栏数据
```

---

## 17.2 提交月度方针

```http
POST /api/month/policy
```

请求：

```json
{
  "mainGoal": "creation",
  "focusedCharacterId": "seminar_girl",
  "riskStyle": "slightly_risky"
}
```

---

## 17.3 提交月度计划

```http
POST /api/month/plan
```

请求：

```json
{
  "actions": [
    {
      "week": 1,
      "slot": 1,
      "actionId": "library_study",
      "customNote": ""
    }
  ]
}
```

后端执行：

```txt
保存计划
执行月度结算
生成 MonthResolution
调用 AI
保存 storedAiPayloads
切换 UI 到 MonthResolution
```

---

## 17.4 获取指定 UI 数据

```http
GET /api/ui/{uiState}
```

示例：

```http
GET /api/ui/relationship
GET /api/ui/month-resolution
GET /api/ui/monthly-review
```

后端根据 GameState 和 storedAiPayloads 返回对应 ViewModel。

---

## 17.5 选择机会

```http
POST /api/opportunity/select
```

请求：

```json
{
  "opportunityId": "campus_festival_preparation"
}
```

---

## 17.6 关系行动

```http
POST /api/relationship/action
```

请求：

```json
{
  "characterId": "seminar_girl",
  "relationshipActionId": "discuss_workshop"
}
```

---

## 17.7 技能觉醒/升级

```http
POST /api/skill/activate
```

请求：

```json
{
  "skillId": "ear_copying"
}
```

---

## 17.8 下个月

```http
POST /api/month/next
```

进入下个月。

---

# 18. LLM PromptBuilder

## 18.1 Prompt 输入内容

PromptBuilder 应传给 AI：

```txt
当前月份
当前学部 / seminar
本月方针
本月行动列表
本地结算前状态
本地结算后状态
触发事件列表
生成机会列表
角色关系变化
技能觉醒/升级信息
当前 GameState 摘要
AI 输出 schema 要求
```

---

## 18.2 System Prompt 原则

System Prompt 必须强调：

```txt
你只负责文本演出
不得修改数值
不得新增事件事实
不得提升角色关系阶段
不得输出 HTML
必须输出合法 JSON
必须符合 monthly_ai_payload_bundle schema
文本必须与本地结算一致
```

---

# 19. AI JSON 校验

后端校验：

```txt
是否能 parse JSON
type 是否为 monthly_ai_payload_bundle
month 是否等于当前月份
payload 类型是否合法
必要字段是否存在
是否包含非法数值修改字段
relationship 文本是否明显越过当前关系阶段
```

失败处理：

```txt
第一次失败：重试一次
第二次失败：使用 fallback payload
保存错误日志
不影响 GameState
```

---

# 20. 结局系统 Demo

Demo 先做 3 个结局。

## 20.1 普通学部生活结局

条件：

```txt
12个月结束
无隐藏路线
HP/MP未严重崩坏
核心参数平均
```

## 20.2 创作路线种子结局

条件：

```txt
创作 Lv >= 5
扒谱能力 Lv >= 2
校园祭准备或 AI Miku 前置事件触发
```

## 20.3 崩溃重启结局

条件：

```txt
HP归零次数 >= 2
或 MP归零次数 >= 2
或 Money < -30000
```

结局类型由本地判定，AI 只负责结局文本。

---

# 21. 开发顺序建议

## Phase 1：Mock 版闭环

目标：不接 AI，先跑通 UI 和本地逻辑。

实现：

```txt
GameState
行动数据
月度方针
月计划
行动结算
MonthResolution UI
Mock AiPayloadBundle
MonthlyReview UI
NextMonth
```

## Phase 2：技能与事件

实现：

```txt
SkillService
技能觉醒/升级
EventService
8 个 Demo 事件
OpportunitySelection UI
SkillBoard UI
```

## Phase 3：关系系统

实现：

```txt
2 个角色
Affection / Trust
Relationship UI
关系行动
关系阶段升级
```

## Phase 4：接入 AI

实现：

```txt
PromptBuilder
LlmClient
AiPayloadValidator
真实 monthly_ai_payload_bundle
fallback 机制
```

## Phase 5：隐藏路线与结局

实现：

```txt
AI Miku 前置路线
魔法少女 seminar 路线
3 个结局
Ending UI
```

---

# 22. 验收标准

Demo 完成时应满足：

```txt
1. 可以启动本地 Web 应用
2. 可以创建新游戏
3. 可以选择月度方针
4. 可以安排 12 个行动
5. 行动会影响 HP / MP / Money / 6 个核心参数
6. 行动会累计 SkillTag 次数
7. 技能可根据条件觉醒/升级
8. 事件可根据本地条件触发
9. 机会选择 UI 可显示本地机会和 AI 文本
10. 人际关系 UI 可显示 acquaintance 以上角色
11. 关系行动由本地条件决定是否可用
12. AI payload 可在月度结算后生成并暂存
13. 后续 UI 从 storedAiPayloads 读取文本
14. AI 不直接修改 GameState
15. 可以连续推进至少 6 个月
16. 可以达成至少 1 个结局
```

---

# 23. 对 Coding Agent 的明确要求

Coding Agent 必须遵守：

```txt
不要引入复杂前端框架
不要引入数据库
不要让 AI 输出 HTML
不要让 AI 修改 GameState
不要设计细分参数系统
不要设计 SkillExp 经验条
不要设计 SP 系统
不要每个 UI 都请求 AI
先实现 Mock，再接真实 API
每个功能完成后保证项目可运行
```

---

# 24. 一句话总结

本 Demo 的核心不是做一个完整游戏，而是验证：

> **玩家用月度计划塑造人生，本地系统结算选择的代价，AI 把这些结果包装成有真实感、浪漫感和奇想感的日本学部生活演出。**
