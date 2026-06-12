# 日留学部模拟器 — 完整机制文档

> **生成日期**: 2026-06-10  
> **版本**: Demo v0.2 (学部 12 个月)  
> **范围**: 本文档完全忠实于当前源代码，不包含任何未实现的机制或讨论中的修改。

---

## 1. 项目概况

《日留学部模拟器》是一个以日本学部生活为题材的养成 SLG 原型。本地代码负责所有规则、数值、状态管理、事件触发和结局判定，AI（当前为 Mock 模式）只负责文本演出。

**技术栈**: C# / .NET 8, ASP.NET Core Minimal API, 纯 HTML/CSS/JavaScript

**游戏时长**: 12 个月

---

## 2. 游戏状态 (GameState)

### 2.1 基础状态

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `CurrentMonth` | int | 1 | 当前月份 |
| `MaxMonth` | int | **12** | 最大月份（到达时触发结局） |
| `CurrentUiState` | string | "StartMenu" | 当前 UI 状态 |
| `FacultyId` | string | "media_culture" | 所选学部 ID |
| `SeminarId` | string | "magical_girl" | 所选研究会 ID |

### 2.2 玩家属性 (PlayerStats)

| 字段 | 默认值 | 上限 | 说明 |
|---|---|---|---|
| `HP` | 100 | 100 | 体力 |
| `MP` | 100 | 100 | 精神力 |
| `Money` | **180,000** | 无上限 | 持有金（日元） |

### 2.3 住房 (HousingState)

| 字段 | 默认值 | 说明 |
|---|---|---|
| `HousingId` | "standard_apartment" | 住房标识 |
| `Name` | "standard_apartment" | 住房名称 |
| `Rent` | **42,000** | 月租金 |
| `HousingComfort` | 55 | 舒适度 (0-100) |
| `CommuteBurden` | 35 | 通勤负担 (0-100) |

### 2.4 月度固定支出 (MonthlyExpenseState)

| 字段 | 默认值 |
|---|---|
| `LivingCost` | **36,000** |
| `CommunicationCost` | **5,000** |
| `TransportationCost` | **8,000** |

**月固定支出总额 = Rent + LivingCost + CommunicationCost + TransportationCost = 91,000 日元**

### 2.5 学费 (TuitionState)

| 字段 | 默认值 | 说明 |
|---|---|---|
| `Amount` | **320,000** | 学费金额 |
| `IntervalMonths` | 6 | 缴纳间隔 |
| `NextDueMonth` | 6 | 下次缴纳月份（当 CurrentMonth >= NextDueMonth 时触发扣款） |

### 2.6 核心属性 (CoreAttributes)

六项核心属性，通过累积经验值晋级。

| 属性 | ID | 说明 |
|---|---|---|
| 学业 | `academic` | |
| 语言 | `language` | |
| 职业 | `career` | |
| 创作 | `creation` | |
| 生活 | `life` | |
| 人际关系 | `relationship` | |

**等级公式**:
```
TotalExperienceRequiredForLevel(Lv) = 100 × Lv × (Lv + 1) / 2
ExperiencePerLifeExperiencePoint = 10
```

**各等级所需经验**:

| 等级 | 累计经验需求 |
|---|---|
| Lv1 | 100 |
| Lv2 | 300 |
| Lv3 | 600 |
| Lv4 | 1,000 |
| Lv5 | 1,500 |
| Lv6 | 2,100 |

- `Level(category)` 返回当前等级
- `ExperienceIntoCurrentLevel(category)` 返回当前等级内已累积的经验
- `ExperienceRequiredForNextLevel(category)` 返回到下一级还需要多少经验

### 2.7 卡片系统状态

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MonthlyDrawCount` | int | 5 | 每月基础抽卡数 |
| `MonthlySelectLimit` | int | **3** | 每月最多执行卡片数 |
| `MonthlySwitchLimit` | int | 2 | 每月最多刷新卡片数 |
| `EffectiveMonthlyDrawCount` | int | 5+ | 实际抽卡数，life Lv ≥ 2 时 +1 |
| `CurrentMonthHand` | List | [] | 本月手牌 |
| `SelectedMonthCards` | List | [] | 本月已选择的卡片 |
| `ReservedCardForNextMonth` | Card? | null | 保留至下月的卡片（最多 1 张） |
| `HasRefreshedCardsThisMonth` | bool | false | 本月是否已刷新过卡片 |
| `CardExecutionCounters` | Dict | {} | 每张卡的历史执行次数 |

### 2.8 关系系统状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `Relationships` | Dict | key=角色ID, value=CharacterRelationship |
| `RelationshipActionsUsedThisMonth` | int | 本月已使用的关系行动次数 |
| `RelationshipInteraction` | Payload? | 当前关系互动载荷（视觉小说场景） |

**CharacterRelationship 结构**:
- `CharacterId`, `Name`
- `Stage`: "stranger" → "acquaintance" → "friend" → "close" → "special"
- `Affection`, `Trust`: 好感/信任数值
- `Mood`, `MoodValue`: 情绪/情绪值
- `InteractionCount`, `LastInteractionMonth`, `LastActionId`
- `Memories`: List of CharacterMemory（包含 id, month, type, title, summary, importance, tags）

### 2.9 系统状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `Flags` | Dict<string, bool> | 全局 flag 字典 |
| `TriggeredEventIds` | List | 已触发过的事件 ID |
| `CurrentMonthEventIds` | List | 本月触发的事件 ID |
| `Opportunities` | List | 本月机会列表 |
| `SelectedOpportunityId` | string? | 已选择的机会 ID |
| `LastResolution` | MonthResolution? | 上月结算数据 |
| `StoredAiPayloads` | AiPayloadBundle? | AI 生成的文本载荷 |
| `MonthlyLogs` | List | 月度日志 |
| `HospitalizedCount` | int | 入院次数 |
| `BurnoutCount` | int | 精神透支次数 |
| `EndingId` | string? | 结局 ID |
| `UnspentLifeExperiencePoints` | int | 未分配的人生经验点数 |
| `TotalLifeExperiencePointsEarned` | int | 累计获得的人生经验点数 |

---

## 3. 卡片系统

### 3.1 卡片定义 (CardDefinition)

每张卡片的静态数据从 `Content/zh-CN/cards.json` 加载，共 32 张（原版 30 张含可能重复）。

| 字段 | 类型 | 说明 |
|---|---|---|
| `CardId` | string | 唯一标识 |
| `Name` | string | 卡片名称 |
| `Description` | string | 卡片描述 |
| `MeaningText` | string | 意义文本（UI 展示用） |
| `PrimaryCoreAttribute` | string | 主核心属性 |
| `CoreExpDelta` | int | 基础经验值增益 |
| `HpDelta` | int | HP 变化 |
| `MpDelta` | int | MP 变化 |
| `MoneyDelta` | int | 金钱变化 |
| `Rarity` | string | "Common" / "Rare" / "Special" |
| `CardType` | string | "Action" / "Finance" / "Relationship" / "Event" / "Housing" |
| `CardTags` | string[] | 标签 |
| `IsInitialCard` | bool | 是否为初始可用卡 |
| `InitialFacultyIds` | string[] | 哪些学部初始可用 |
| `UnlockRequirements` | object | 解锁条件 |
| `HousingDelta` | object | 住房变化（可选） |
| `TuitionDelayMonths` | int | 学费延期月数（可选） |
| `RelatedCharacterId` | string? | 关联角色（可选） |
| `PossibleEventIds` | string[] | 可能触发的事件（可选） |
| `IsHiddenUntilUnlocked` | bool | 解锁前是否隐藏 |
| `CannotRepeatConsecutively` | bool | 同月不可连续执行 |

### 3.2 卡片解锁规则 (CardService)

解锁检查 `GetUnlockReason()` 按以下顺序:

1. **初始卡**: 若 `IsInitialCard == true` 或 `InitialFacultyIds` 包含当前学部 → 直接解锁
2. **核心属性等级**: 检查 `RequiredCoreAttributeLevels` 中的所有要求
3. **Flag**: 检查 `RequiredFlags`（所有必须为 true）和 `ForbiddenFlags`（所有必须为 false）
4. **学部/研究会**: 检查 `RequiredFaculty` 和 `RequiredSeminar`
5. **角色**: 检查 `RequiredCharacterId`（关系不能为 "stranger"）和 `RequiredRelationshipStage`（关系阶段必须 >= 要求）

**关系阶段排序**: stranger(0) < acquaintance(1) < friend(2) < close(3) < special(4)

### 3.3 月度牌组生成 (MonthlyDeckService)

每月初调用 `GenerateForMonth()`：

1. 如有上月保留的卡且未过期，加入手牌并标记 `IsPinnedFromLastMonth`
2. 从已解锁的卡片池中随机抽取，优先级：
   - 1 张 **财务压力卡**（当 Money < 60,000 或 扣除固定支出后 < 30,000 或 距学费到期 ≤ 2 月时触发，从 finance 标签卡中随机选）
   - 1 张 **Special** 稀有度卡
   - 1 张 **Rare** 稀有度卡
   - 随机填充到 `EffectiveMonthlyDrawCount` 张
3. 5 张手牌（life Lv ≥ 2 时 6 张）

### 3.4 卡片执行与判定

#### 成功率计算 (Score)

```
mpBonus = MP ≥ 70 ? +5 : MP ≥ 40 ? 0 : MP ≥ 20 ? -8 : -15
Score = 50 + CoreLevel(PrimaryCoreAttribute) × 3 + mpBonus + Random(-15, +15)
```

**结果分级**:
| 分数 | 结果 | 倍率 | 额外 |
|---|---|---|---|
| < 40 | 失败 | 0.5x 经验 | MP 额外 -3 |
| 40-74 | 普通 | 1.0x 经验 | — |
| ≥ 75 | 大成功 | 1.5x 经验 | +2 人生经验点（普通为 +1） |

#### 卡片执行流程 (ResolveMonth)

1. 验证在 "MonthPlan" UI 状态
2. 解析选中卡片（验证无重复、数量 ≥ 1 且 ≤ 3）
3. 逐张执行：
   - **burnout 检查**: 若本月 burnout flag 为 true，所有后续卡片跳过
   - **连续不可执行**: 若 `CannotRepeatConsecutively` 且与上一张相同 ID，跳过
   - **成功率判定**: 计算 Score，确定结果等级和倍率
   - **应用效果**: HP/MP/Money/CoreExp 变化
   - **housingDelta**: 若有住房变化，应用（Rent ≥ 20000 下限，Comfort/Commute 0-100 范围）
   - **tuitionDelayMonths**: 若学费距到期 ≤ 2 月，延期学费
   - **危机检测**: HP ≤ 0 → 强制入院（hospitalized flag, Money -10,000, HP=50, MP-10）；MP ≤ 0 → 精神透支（burnout flag, MP=50, HP+10）
4. 扣除月度固定支出和学费
5. 检查月度事件
6. 生成机会
7. 触发后台 AI 生成

---

## 4. 经济系统

### 4.1 月度结算

卡片执行后，扣除：
```
月固定支出 = Rent(42,000) + LivingCost(36,000) + CommunicationCost(5,000) + TransportationCost(8,000) = 91,000
学费 = 若 CurrentMonth ≥ NextDueMonth 则扣除 320,000，并将 NextDueMonth += 6
```

### 4.2 财务风险评估

```
projectedBalance = Money - (月固定支出 + 到期学费)
riskLevel:
  projectedBalance < 0      → "crisis"
  projectedBalance < 30,000 → "pressure"
  projectedBalance < 80,000 → "watch"
  否则                      → "stable"
```

Money < 0 时触发 `financial_crisis` flag 和事件。

### 4.3 收入卡片

| CardId | MoneyDelta | 条件 |
|---|---|---|
| `light_work` | +5,000 | life Lv ≥ 1 |
| `short_intensive_work` | +18,000 | life Lv ≥ 1 |
| `frugal_living` | +9,000 | life Lv ≥ 1 |
| `part_time_shift` | +10,000 | 需 `convenience_store_hired` flag |
| `family_support` | +60,000 | relationship Lv ≥ 1 (Rare) |

### 4.4 住房调整

`move_cheaper_room` 卡: Money -40,000, Rent -12,000, Comfort -12, Commute +18

### 4.5 学费延期

`tuition_extension` 卡: 若距学费到期 ≤ 2 月，NextDueMonth += 2

---

## 5. 关系系统

### 5.1 角色

当前有 2 个角色（定义在 `characters.json`）：

| ID | 名称 | 初始关系 | 所属研究会 |
|---|---|---|---|
| `classmate` | 林澄 | acquaintance (好感8, 信赖5) | 全部 |
| `seminar_girl` | 雨宫 | stranger (好感0, 信赖0) | magical_girl 专属 |

### 5.2 关系行动限制

每月的最大关系行动次数（`RelationshipActionLimit`）：

| 条件 | 次数 |
|---|---|
| 没有非 stranger 角色 | 0 |
| 有 close 阶段角色 或 好感 ≥ 55 且信赖 ≥ 40 或 relationship Lv ≥ 3 | 3 |
| 有 friend 阶段角色 或 好感 ≥ 25 且信赖 ≥ 15 或 relationship Lv ≥ 1 | 2 |
| 其他 | 1 |

### 5.3 互动类型

每个角色定义 `interactionPreferences`，包含如 `chat`、`support` 等互动类型，各有好感/信赖倍率、心情变化、记忆权重和核心属性增益。

### 5.4 视觉小说场景

`RelationshipAction` API 调用后：
- 生成 `RelationshipInteractionPayload`
- UI 切换到 "RelationshipScene"
- 显示视觉小说式对话和选项
- 玩家选择后，应用好感/信赖变化、更新角色阶段和 flag

### 5.5 角色记忆

每个角色维护 `CharacterMemory` 列表，记录互动历史（月、类型、内容、重要性、标签）。

---

## 6. 事件系统

### 6.1 事件定义

事件定义在 `events.json`，共 11 个：

| ID | 名称 | 月份范围 | 触发条件 |
|---|---|---|---|
| `first_report` | 第一次报告 | 2-2 | 自动 |
| `part_time_hired` | 便利店录用 | 1-3 | 自动 |
| `hear_melody` | 听出旋律结构 | 1-12 | 自动 |
| `seminar_meeting` | 研究会初见 | 1-12 | magical_girl 研究会 |
| `workshop_team` | Workshop 组队 | 1-12 | `met.seminar_girl` flag |
| `campus_festival` | 校园祭准备 | 8-10 | 自动 |
| `ai_miku_seed` | 未命名的歌声 | 1-12 | 自动 |
| `career_center` | Career Center 说明会 | 3-6 | 自动 |
| `hospitalized` | 强制入院 | 1-12 | HP ≤ 0 时触发 |
| `burnout` | 精神透支 | 1-12 | MP ≤ 0 时触发 |
| `financial_crisis` | 经济危机 | 1-12 | Money < 0 时触发 |

### 6.2 事件触发

- 月度事件: 每月结算后，`CheckMonthlyEvents` 检查所有在当前月份范围内的事件
- 紧急事件: HP ≤ 0 / MP ≤ 0 / Money < 0 时立即触发
- 每个事件只触发一次（记录在 `TriggeredEventIds`）

---

## 7. 机会系统

### 7.1 机会生成

每月结算后 `GenerateOpportunities` 根据触发过的事件生成机会：

| 机会 ID | 条件 |
|---|---|
| `workshop` | 已触发 `seminar_meeting` + seminar_girl 在 acquaintance + (creation Lv ≥ 2 或 academic Lv ≥ 2) |
| `career_briefing` | 已触发 `career_center` |
| `festival_creation` | 已触发 `campus_festival` |

### 7.2 机会选择

- 选择 `workshop`: seminar_girl 好感 +8, 信赖 +6；触发 `workshop_team` 事件
- 可跳过机会（`SelectedOpportunityId = "skipped"`）
- 选择或跳过后 UI 进入 "Relationship" 状态

---

## 8. 结局系统

第 12 个月结束时判定 `DetermineEnding()`：

1. **崩溃结局** (`ending.collapse`): 
   - `HospitalizedCount ≥ 2` 或 `BurnoutCount ≥ 2` 或 `Money < -30,000`

2. **创作路线种子** (`ending.creation_seed`):
   - `creation Lv ≥ 5` 且 (`festival_creation` flag 或 `burnout` 事件已触发)

3. **普通结局** (`ending.normal`):
   - 以上条件都不满足

---

## 9. AI 叙事系统

### 9.1 模式

两种模式（在 `AiNarrationOptions` 中配置）:
- **Mock**: 使用预置数据，不调用 API
- **Live**: 调用 LLM API（当前默认指向 DeepSeek）

### 9.2 AI 载荷类型

- `MonthlyReviewPayload`: 月度回顾（标题、摘要、段落）
- `EventScenePayload`: 事件演出文本
- `RelationshipPayload`: 角色关系状态文本
- `OpportunityPayload`: 机会风味文本
- `ArchiveMemoryPayload`: 日志文本

### 9.3 生成流程

- 月度结算后：后台 `Task.Run` 异步生成月度载荷
- 关系互动后：后台异步生成视觉小说场景
- 生成前先存 "pending" 占位载荷，UI 轮询自动刷新
- 失败时 fallback 到本地文本

---

## 10. 游戏流程

### 10.1 UI 状态机

```
StartMenu → FacultySelection → SeminarSelection → Opening
  → MonthStart → Deck → MonthPlan → CoreAttributeAllocation
  → MonthResolution → OpportunitySelection → Relationship
  → RelationshipScene → EventScene → MonthlyReview → Archive
  → MonthStart (循环) → Ending
```

### 10.2 新游戏流程

1. 选择学部（3 个: media_culture / business / international_studies）
2. 选择研究会（每个学部 2 个可选）
3. 开场叙事（本地生成，含 4 段文本）
4. **UnspentLifeExperiencePoints 初始 = 6**（可在 CoreAttributeAllocation 中分配）
5. 学部初始核心属性加算（见表）

**学部初始属性**:
| 学部 | creation | academic | relationship | career | language | life |
|---|---|---|---|---|---|---|
| media_culture | +10 | +4 | +3 | — | — | — |
| business | — | +6 | — | +10 | — | +3 |
| international_studies | — | +3 | +6 | — | +10 | — |

### 10.3 月度循环

1. **MonthStart**: 显示当前状态
2. **Deck**: 查看牌组详情
3. **MonthPlan**: 选择 1-3 张卡执行（可保留 1 张、可刷新最多 2 张，刷新消耗 5 MP）
4. **CoreAttributeAllocation**: 分配人生经验点到六项属性（每点=10 经验）
5. **MonthResolution**: 查看结算结果
6. **OpportunitySelection**: 选择或跳过机会
7. **Relationship**: 进行关系行动
8. **RelationshipScene**: 视觉小说互动场景
9. **EventScene**: 事件演出
10. **MonthlyReview**: 月度回顾
11. **Archive**: 查看日志
12. 调用 `NextMonth()` → 回到 MonthStart 或进入 Ending

---

## 11. API 端点

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/state` | 获取完整游戏状态 |
| GET | `/api/cards` | 获取卡片列表 |
| GET | `/api/cards/catalog` | 获取卡片目录（含解锁状态） |
| GET | `/api/saves` | 获取存档列表 |
| GET | `/api/ai-settings` | 获取 AI 设置 |
| POST | `/api/ai-settings` | 保存 AI 设置 |
| POST | `/api/ai-settings/test` | 测试 AI 连接 |
| POST | `/api/game/new` | 新游戏 {facultyId?, seminarId?} |
| POST | `/api/game/begin` | 开始第一个月 |
| POST | `/api/save` | 存档 {slot} |
| POST | `/api/load` | 读档 {slot} |
| POST | `/api/month/plan` | 提交月计划 {selectedCardIds[], selectedCards[{cardId, customNote}]} |
| POST | `/api/core-attributes/allocate` | 分配核心属性 {allocations: {category: points}} |
| POST | `/api/month/card/reserve` | 保留卡片 {cardId} |
| POST | `/api/month/card/unreserve` | 取消保留 {cardId} |
| POST | `/api/month/card/refresh` | 刷新卡片 {cardIds[]} |
| POST | `/api/opportunity/select` | 选择机会 {opportunityId} |
| POST | `/api/opportunity/skip` | 跳过机会 |
| POST | `/api/relationship/action` | 关系行动 {characterId, relationshipActionId, sceneId?} |
| POST | `/api/relationship/choice` | 关系选择 {interactionId, optionId} |
| POST | `/api/month/next` | 进入下月 |
| POST | `/api/ui/{uiState}` | 切换 UI 状态 |

所有 POST 端点返回完整 state + cards + labels 等数据。

---

## 12. 内容数据文件

| 文件 | 条目数 | 说明 |
|---|---|---|
| `Content/zh-CN/cards.json` | 32 | 卡片定义 |
| `Content/zh-CN/faculties.json` | 3 | 学部定义 |
| `Content/zh-CN/seminars.json` | 4 | 研究会定义 |
| `Content/zh-CN/characters.json` | 2 | 角色定义 |
| `Content/zh-CN/events.json` | 11 | 事件定义 |
| `Content/zh-CN/opportunities.json` | 3 | 机会定义 |
| `Content/zh-CN/messages.json` | ~50 | 系统消息/文本模板 |
| `Content/zh-CN/labels.json` | 8 | 属性标签 |
| `Content/zh-CN/scenes.json` | 6 | 场景/背景定义 |

---

## 13. 存档系统

- 格式: JSON 文件，保存在 `Saves/` 目录
- 结构: `SaveFile { Version: 1, Slot, SavedAt, State }`
- 存档只能在非菜单 UI 状态下执行
- 读档后自动修正 UI 状态到合法值

---

## 附录: 关键数值速查

| 参数 | 值 |
|---|---|
| 初始 HP | 100 |
| 初始 MP | 100 |
| 初始资金 | ¥180,000 |
| 月固定支出 | ¥91,000 |
| 学期学费 | ¥320,000（每 6 月） |
| 每月抽卡数 | 5 (life Lv≥2 时 6) |
| 每月可选卡数 | ≤ 3 |
| 每月可刷新卡数 | ≤ 2 (消耗 5 MP) |
| 每月可保留卡数 | 1 |
| 初始人生经验点 | 6 |
| 每人生经验点经验值 | 10 |
| 等级公式 | 100 × Lv × (Lv+1) / 2 |
| 成功率基底 | 50 + 等级×3 + MP加成 + 随机(-15,+15) |
| 大成功阈值 | ≥ 75 |
| 失败阈值 | < 40 |
| 关系阶段排序 | stranger(0) < acquaintance(1) < friend(2) < close(3) < special(4) |
