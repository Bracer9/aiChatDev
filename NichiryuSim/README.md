# 日留学部模拟器 Demo

基于 `prototype/DemoHld.md` 与 `docs/DemoHld_v0.2_OpportunityCards.md` 实现的首版可玩 Demo。

## 启动

```powershell
cd D:\ai\aiChatDev\NichiryuSim
dotnet run
```

启动后打开终端输出的本地地址。

## 当前包含

- 中文系统与游戏文本
- 12 个月本地状态
- 卡片式月计划，每月从牌组选择 1-4 张卡片
- 每月生成最多 10 张卡片的牌组，并提供实时资源预测
- 每月可保留 1 张当前手牌到下月；每月可刷新最多 2 张当前手牌，消耗 MP 5
- HP、MP、持有金与六项核心成长
- 本地成功判定、核心属性等级与 flag 解锁、事件、机会和关系推进
- Phase 2：固定稀有度卡片牌组、数据化 Demo 事件、单次机会选择规则
- Mock AI 月度 payload，不修改 `GameState`
- 月度结算、机会、关系、事件、回顾、日志和结局 UI

当前为 Mock AI 阶段，后续可在不改变本地规则主权的前提下接入真实 LLM API。
