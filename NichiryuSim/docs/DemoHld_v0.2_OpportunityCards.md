# NichiryuSim Demo HLD：核心属性与卡片牌组

## 1. 当前设计目标

玩家扮演一名在日本读学部的留学生，以月为单位安排生活。

游戏由以下核心机制构成：

- 6 个核心属性
- 卡片与牌组
- 人生经验点数
- HP、MP、Money
- 住房、生活费与学费
- 学部与研究会
- 事件 flag
- 人际关系与角色记忆
- 本地规则结算与 AI 文本演出

本地代码决定全部规则、数值、解锁、事件、关系和结局。AI 只根据已经确定的本地结果生成自然中文演出文本。

## 2. 六个核心属性

统一使用 `CoreAttribute`：

- Academic / 学业
- Language / 语言
- Career / 职业
- Creation / 创作
- Life / 生活
- Relationship / 关系

核心属性保存累计 EXP。等级根据累计 EXP 自动计算，等级越高，升到下一级需要的 EXP 越多。

核心属性用于：

- 解锁卡片
- 影响对应卡片的成功判定
- 触发事件
- 推进路线与结局

## 3. 人生经验点数

`LifeExperiencePoints` 是玩家可自由分配的成长资源，与生活核心属性无关。

- 每张成功、普通或失败执行的卡片都会给予人生经验点数。
- 玩家可以将点数分配到任意核心属性。
- AI 不参与点数分配。
- 月度本地结算完成后，玩家可以在等待 AI 文本期间分配点数。

## 4. 卡片与牌组

统一使用：

- Card / 卡片
- Deck / 牌组
- Rarity / 稀有度

卡片本身没有等级。每张卡片严格归属一个主要核心属性，并拥有固定稀有度。

Demo 稀有度：

- Common
- Rare
- Special

### 4.1 卡片主要字段

```text
CardId
Name
Description
MeaningText
PrimaryCoreAttribute
CoreExpDelta
HpDelta
MpDelta
MoneyDelta
Rarity
CardType
UnlockRequirements
CardTags
HousingDelta
TuitionDelayMonths
RelatedCharacterId
PossibleEventIds
IsInitialCard
CannotRepeatConsecutively
```

### 4.2 卡片解锁条件

`UnlockRequirements` 支持：

- 多个核心属性等级
- RequiredFlags
- ForbiddenFlags
- RequiredFaculty
- RequiredSeminar
- RequiredCharacterId
- RequiredRelationshipStage

卡片解锁仅由核心属性等级、事件 flag、学部、研究会和关系状态决定。

### 4.3 月度牌组生成

每月生成最多 10 张当前可用卡片：

1. 加入部分初始 Common 卡。
2. 财务压力较高时，提高财务相关卡片出现率。
3. 从已解锁 Rare 与 Special 卡中抽取。
4. 从剩余已解锁卡片中随机补足。
5. 未满足解锁条件的卡片不会进入本月牌组。

玩家每月从牌组选择 1 到 4 张卡片执行。

### 4.4 保留与刷新

- 每月最多保留 1 张当前手牌到下个月。
- 保留卡可以在提交本月计划前取消。
- 每月可以消耗 MP 刷新一次当前手牌。
- 已选择、已保留或上月保留的卡片不能刷新。

## 5. 卡片结算

每张卡片执行时：

1. 根据对应核心属性等级、当前 MP 和随机波动计算结果。
2. 应用卡片固定的 HP、MP、Money 变化。
3. 根据结果倍率增加 `PrimaryCoreAttribute` EXP。
4. 记录该卡片的执行次数。
5. 给予人生经验点数。
6. 应用住房或学费相关效果。
7. 检查事件 flag、财务危机与角色条件。

结果等级：

- 失败：核心属性 EXP × 0.5，人生经验点数 +1
- 普通：核心属性 EXP × 1.0，人生经验点数 +1
- 大成功：核心属性 EXP × 1.5，人生经验点数 +2

## 6. 月循环

```text
MonthStart
→ MonthPlan
→ 本地月度结算
→ CoreAttributeAllocation
→ MonthResolution
→ OpportunitySelection
→ Relationship
→ EventScene
→ MonthlyReview
→ Archive
→ NextMonth
```

月初主页直接进入月计划，不存在额外的月初选择阶段。

## 7. 财务与住房

每月结算自动扣除：

- 房租
- 生活费
- 通信费
- 交通费

每 6 个月扣除一次学费。

Money 低于 0 时设置 `financial_crisis` flag 并触发经济危机事件。

住房卡可以修改：

- Rent
- HousingComfort
- CommuteBurden

财务压力较高或学费临近时，财务相关卡片更容易进入本月牌组。

## 8. 学部与研究会

学部与研究会相互独立：

- 学部决定初始核心属性倾向和部分事件时间。
- 研究会决定专属人物、事件、flag 和卡片条件。

角色、卡片和事件均可通过 ID 与学部、研究会及 flag 建立关联。

## 9. 人际关系

角色通过 JSON 定义背景、性格、喜好、边界、说话方式和互动倾向。

关系状态包含：

- Affection
- Trust
- Mood
- Stage
- Memories

角色关系可以解锁卡片或事件。互动结果由本地规则决定，AI 只负责根据角色设定、关系阶段和记忆生成演出文本。

## 10. AI 边界

AI 可以生成：

- 月度回顾
- 事件演出
- 关系互动演出
- 日志文本

AI 不得决定：

- HP、MP、Money
- 核心属性与人生经验点数
- 卡片结果与解锁
- 事件 flag
- 关系数值
- 财务、住房与学费数值
- 结局判定

## 11. 数据文件

```text
Content/zh-CN/cards.json
Content/zh-CN/events.json
Content/zh-CN/characters.json
Content/zh-CN/faculties.json
Content/zh-CN/seminars.json
Content/zh-CN/opportunities.json
Content/zh-CN/labels.json
Content/zh-CN/messages.json
```

所有可扩展卡片、角色、事件和文本内容均通过数据文件维护。

## 12. 验收原则

- 游戏只使用核心属性、卡片、牌组、稀有度和人生经验点数等统一术语。
- 卡片没有等级。
- 卡片解锁由核心属性等级、flag、学部、研究会和关系状态管理。
- 月初可以直接进入月计划。
- 月循环可以正常推进。
- AI 不参与本地规则与数值判定。
