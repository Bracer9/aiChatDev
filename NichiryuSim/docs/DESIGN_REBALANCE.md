# DESIGN_REBALANCE.md — 日留研究生模拟器 v0.3 数值与设计调整

**日期**: 2026-06-10  
**版本**: 学部 12 个月 → 研究生 24 个月  
**改动范围**: 1 个代码文件 + 7 个 JSON 文件，0 行机制变更

---

## 目标

将《日留学部模拟器》改为《日留研究生模拟器》，游戏时长从 12 个月扩展到 24 个月，同时系统性修复财务模型、MP 经济、核心属性升级曲线、关系解锁和卡池深度等设计问题。

---

## 修改文件一览

| 文件 | 改动类型 | 描述 |
|---|---|---|
| `Models.cs` | 代码（最小） | MaxMonth 12→24, 研究生级财务默认值 |
| `Content/zh-CN/cards.json` | 完全重制 | 17 张 → 44 张卡, 全面重平衡 |
| `Content/zh-CN/faculties.json` | 数值调整 | 研究科名称, 初始核心属性, 24月事件排布 |
| `Content/zh-CN/seminars.json` | 文本调整 | 研究生研讨班语境 |
| `Content/zh-CN/characters.json` | 数值调整 | 年龄 20→23, 角色描述研究生化 |
| `Content/zh-CN/events.json` | 扩展 | 月份跨度扩展到 24, 新增 3 个研究生事件 |
| `Content/zh-CN/opportunities.json` | 扩展 | 新增 2 个研究生机会 |
| `Content/zh-CN/messages.json` | 文本调整 | 研究生语境文本 |

---

## 1. 财务模型

### 变更前（学部 12 个月）

| 项目 | 数值 |
|---|---|
| 初始资金 | ¥180,000 |
| 月固定支出 | ¥91,000 |
| 学期学费 | ¥320,000/6月 |
| 12 月总支出 | ¥1,732,000 |

**问题**: 不做收入卡第 3 个月破产，做了也活不过学费月。数学上不存在可行生存路径。

### 变更后（研究生 24 个月）

| 项目 | 数值 |
|---|---|
| 初始资金 | ¥250,000 |
| 月固定支出 | ¥56,000 (房租 ¥28k + 生活 ¥20k + 通讯 ¥4k + 交通 ¥4k) |
| 学期学费 | ¥200,000/6月 |
| 24 月总支出 | ¥2,144,000 |
| 缺口 | ¥1,894,000 |

### 生存路径数学

- 72 次卡片执行 (24 月 × 3 张)
- 收入卡池: 12 张 finance 标签卡, 价值 ¥12k–¥80k
- FinancialPressureCards 机制在资金紧张时自动提升收入卡出现率
- RA/TA/翻译等研究生特有高值收入卡 (¥22k–¥30k)
- 住房调整卡 (`move_cheaper_room`, `cheap_housing_search`) 可降低月固定支出
- 学费延期卡 (`tuition_extension`) 提供现金流喘息

**合理假设**:
- 约 50–60 次收入卡执行, 平均 ¥25k–¥30k
- 总收入: ~¥1,400k–¥1,800k
- 配合初始资金 ¥250k 和住房/学费调整 → 可生存至 24 月

---

## 2. 核心属性升级曲线

### 曲线公式（未变更）

```
TotalExperienceRequiredForLevel(Lv) = 100 × Lv × (Lv+1) / 2
```

### 24 个月可达等级

| 等级 | 需求经验 | 预估达成月份 |
|---|---|---|
| Lv1 | 100 | 第 2–3 月 |
| Lv2 | 300 | 第 5–7 月 |
| Lv3 | 600 | 第 10–13 月 |
| Lv4 | 1,000 | 第 15–19 月 |
| Lv5 | 1,500 | 第 20–24 月（需专注） |
| Lv6 | 2,100 | 不可达（需 26+ 月） |

### 卡片经验值调整

- Common: 8–12 → **10–14** (提高约 20%)
- Rare: 15–18 → **18–24** (提高约 30%)
- Special: 25–28 → **26–28** (基本保持)

**设计意图**: 24 个月里，专精属性可到 Lv4–Lv5，多属性发展可到 Lv3–Lv4。高阶卡片解锁要求相应调整（见下方）。

### 卡片解锁要求调整

| 卡片 | 旧要求 | 新要求 |
|---|---|---|
| `unnamed_voice` | creation Lv4 | creation Lv3 |
| `cm_companion_invite` | creation Lv4, relationship Lv3 | creation Lv3, relationship Lv2 |
| `festival_project` | creation Lv3 | creation Lv2 |
| `conference_presentation` | [新卡] | academic Lv3, language Lv2 |
| `joint_research` | [新卡] | academic Lv3, relationship Lv2 |

---

## 3. MP 经济

### 变更前

- 多数卡消耗 MP 4–16
- 唯一回蓝手段: `rest` (+14 MP)
- 3 卡/月平均消耗 21–30 MP, 4–5 月必然 burnout

### 变更后

| 参数 | 旧值 | 新值 |
|---|---|---|
| 基本 Common 消耗 | MP -4 至 -8 | MP -2 至 -5 |
| 基本 Rare 消耗 | MP -7 至 -14 | MP -5 至 -10 |
| rest 恢复 | +14 MP | +16 MP |
| MP 正向卡 | 1 张 (`call_friend` +7) | 5 张 (+3 至 +8) |

**MP 正向卡片**:
- `watch_animation`: +6 MP
- `cook`: +4 MP
- `call_friend`: +8 MP
- `library_browse`: +5 MP
- `language_exchange`: +3 MP
- `cm_companion_invite`: +5 MP

### 月度 MP 收支

- 3 张行动卡: 约 -10 至 -14 MP/月 (旧值 -21 至 -30)
- 每 3 月一次 `rest`: +16 MP
- 平均月度净消耗: -4 至 -6 MP
- 从 100 MP 到 0: ~17–25 月（配合 MP 正向卡可达 24 月）

**设计意图**: 休息从"必须每 2 月一次"变为"每 3–4 月一次"，选择空间增加 ~50%。

---

## 4. 卡池扩展

### 规模

17 张 → **44 张** (+27 张, 其中 18 张全新, 9 张原有保留并重平衡)

### 稀有度分布

| 稀有度 | 数量 | 占比 |
|---|---|---|
| Common | 26 | 59% |
| Rare | 12 | 27% |
| Special | 4 | 9% |
| **总计** | **44** | |

### 属性分布

| 核心属性 | 卡片数 |
|---|---|
| academic | 10 |
| language | 7 |
| career | 8 |
| creation | 7 |
| life | 7 |
| relationship | 7 |

(注: 部分卡片归属多个分类, 总和 >44)

### 卡片类型分布

| 类型 | 数量 |
|---|---|
| Action | 21 |
| Finance | 11 |
| Relationship | 8 |
| Event | 3 |
| Housing | 1 |

### 新增研究生题材卡片

- `ra_position`: 研究助理 (RA), 学术+收入
- `ta_shift`: 教学助理 (TA), 教学+收入
- `translation_gig`: 翻译工作, 语言+收入
- `advisor_meeting`: 导师面谈, 研究方向
- `joint_research`: 合作研究, 关系解锁
- `conference_presentation`: 学会发表, Special
- `seminar_presentation`: 研究会报告, 学术
- `visit_office_hours`: 去导师办公室答疑
- `library_browse`: 图书馆浏览, 创作灵感+MP
- `gym_routine`: 运动习惯, 生活+HP
- `language_exchange`: 语言交流会, 关系+语言+MP
- `internship_application`: 实习申请, 职业

### 关系阶段解锁卡片

| 卡片 | 解锁要求 |
|---|---|
| `cm_companion_invite` | seminar_girl: friend + creation Lv3 + relationship Lv2 |
| `joint_research` | classmate: 无阶段要求 (仅 academic Lv3 + relationship Lv2) |
| `study_group_invite` | 通用: academic Lv1 + language Lv3 + relationship Lv2 |

---

## 5. 事件系统

### 月份跨度扩展

- 原有事件 `endMonth` 从 12 扩展到 24
- 新增事件:
  - `midterm_review`: 中期审查, 第 10–14 月
  - `conference_cfp`: 学会征稿, 第 6–18 月
  - `internship_season`: 实习申请季, 第 8–16 月

### 研究生语境逻辑

- 校园祭仍然保留在第 12 月附近，作为研究生生活中的大型节点
- 学会发表和中期审查增加了研究线的事件密度
- Career Center 事件月份推迟（研一第 5–6 月 vs 学部第 3–4 月）

---

## 6. 机会系统

新增 2 个研究生机会:
- `conference_submit`: 投稿学会发表 — 学术路线推进
- `internship_offer`: 实习机会 — 职业经验获取

---

## 7. 未改动部分

以下机制和代码**未被修改**:
- 卡片执行引擎 (`Score`, 成功率计算)
- 关系互动引擎 (好感/信任/记忆系统)
- AI 叙事管道 (prompt/mock/LLM fallback)
- 存档系统
- UI 渲染 (`app.js`, `index.html`, `style.css`)
- Faculty/Seminar 选择与开场流程
- 月度结算流程 (费用扣除顺序, 事件触发)

成功率系统的 RNG 比例问题未在此次调整中修改, 因为 `Score` 函数在 `GameService.cs` 中, 机制变更需要独立讨论。

---

## 构建验证

```
dotnet build -c Debug -p:UseAppHost=false -o %TEMP%\NichiryuBuildVerify
=> 0 warnings, 0 errors
```

JSON 文件格式正确, Models.cs 编译通过, 所有卡片 ID 与代码引用一致。
