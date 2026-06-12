let data, state, cards, cardCatalog = [], labels, faculties = [], seminars = [], characters = [], finance, relationship, saveSlots = [], aiSettings;
let selectedFacultyId = "";
let planSelections = [];
let activeCategory = "all";
let refreshSelections = [];
let activeSaveSlot = 1;
let saveHudMode = "";
let lastUiState = "";
let transitionTimer;
let aiPollTimer;
let planSubmitting = false;
let coreAllocationDraft = {};
let deckFilter = "all";
let selectedCoreAttribute = "academic";
let visualNovelSceneId = "";
let visualNovelLineIndex = 0;

const uiNames={StartMenu:"开始",FacultySelection:"学部选择",SeminarSelection:"研究会选择",Opening:"开场",Continue:"读档",Achievements:"成就",MonthStart:"月初主页",Deck:"牌组查看",CoreAttributeDetail:"核心属性详情",MonthPlan:"月计划",CoreAttributeAllocation:"核心属性加点",MonthResolution:"月度结算",OpportunitySelection:"机会选择",Relationship:"人际关系",RelationshipScene:"互动演出",EventScene:"事件演出",MonthlyReview:"月度回顾",Archive:"日志",Ending:"结局"};
const flow=["MonthStart","Deck","MonthPlan","MonthResolution","OpportunitySelection","Relationship","EventScene","MonthlyReview","Archive"];
const categoryOrder=["all","academic","language","career","creation","life","relationship"];
const coreKeys=["academic","language","career","creation","life","relationship"];
const transitionEffects=["fx-snow","fx-scan","fx-tear"];

async function api(url,body){
  const r=await fetch(url,{method:body===undefined?"GET":"POST",headers:{"Content-Type":"application/json"},body:body===undefined?undefined:JSON.stringify(body)});
  const value=await r.json();
  if(value.error) toast(value.error);
  data=value; state=value.state; cards=value.cards||cards; cardCatalog=value.cardCatalog||cardCatalog; labels=value.labels||labels; faculties=value.faculties||faculties; seminars=value.seminars||seminars; characters=value.characters||characters; finance=value.finance||finance; relationship=value.relationship||relationship; saveSlots=value.saves||saveSlots; render();
  return value;
}
async function load(){await api("/api/state");await loadAiSettings()}
async function loadAiSettings(){
  const r=await fetch("/api/ai-settings");
  aiSettings=await r.json();
  render();
}
async function newGame(facultyId,seminarId){planSelections=[];refreshSelections=[]; await api("/api/game/new",{facultyId,seminarId})}
function setUi(name){api(`/api/ui/${name}`,{})}
function toast(msg){const t=document.querySelector("#toast");t.textContent=msg;t.classList.add("show");setTimeout(()=>t.classList.remove("show"),2200)}

function render(){
  const screen=document.querySelector("#screen");
  const uiChanged=lastUiState!==state.currentUiState;
  const visualNovelFocus=state.currentUiState==="RelationshipScene";
  const focusChanged=(lastUiState==="RelationshipScene")!==visualNovelFocus;
  const inShell=!["StartMenu","FacultySelection","SeminarSelection","Opening","Continue","Achievements","ApiSettings"].includes(state.currentUiState);
  document.querySelector("#month").textContent=`第 ${state.currentMonth} 月`;
  document.querySelector("#status").innerHTML=inShell
    ? `<span>HP <b class="data-readout">${state.stats.hp}</b></span><span>MP <b class="data-readout">${state.stats.mp}</b></span><span>持有金 <b class="data-readout">¥${state.stats.money.toLocaleString()}</b></span><span class="life-exp-hud">人生经验点数 <b class="data-readout">${state.unspentLifeExperiencePoints}</b></span>`
    : "";
  document.body.classList.toggle("is-start-shell",!inShell);
  document.body.classList.toggle("vn-focus",visualNovelFocus);
  document.querySelector("#nav").innerHTML=inShell
    ? flow.map(x=>`<button class="${state.currentUiState===x?"active":""}" onclick="setUi('${x}')">${uiNames[x]}</button>`).join("")
    : "";
  document.querySelector("#saveHud").innerHTML=inShell?saveHudView():"";
  const renderer=views[state.currentUiState]||views.StartMenu;
  screen.innerHTML=renderer();
  screen.dataset.ui=state.currentUiState;
  screen.classList.remove("screen-swap","screen-refresh");
  void screen.offsetWidth;
  screen.classList.add(uiChanged?"screen-swap":"screen-refresh");
  playTransmission(uiChanged,focusChanged);
  document.querySelectorAll("#month,.data-readout,.metric").forEach(el=>{
    el.classList.remove("data-glitch");
    void el.offsetWidth;
    el.classList.add("data-glitch");
  });
  lastUiState=state.currentUiState;
  scheduleAiPolling();
}

function playTransmission(uiChanged,focusChanged=false){
  const fx=document.querySelector("#transmission");
  if(!fx)return;
  const effect=focusChanged?"fx-noise":uiChanged?transitionEffects[Math.floor(Math.random()*transitionEffects.length)]:"fx-scan";
  clearTimeout(transitionTimer);
  fx.className=`transmission ${effect}`;
  void fx.offsetWidth;
  fx.classList.add("is-active");
  transitionTimer=setTimeout(()=>{fx.className="transmission"},760);
}

const label=x=>labels?.[x]||({all:"全部",free:"自由"}[x]||x);
const navButton=(target,text,kind="primary")=>`<button class="${kind}" onclick="setUi('${target}')">${text}</button>`;
const eventName=id=>({seminar_meeting:"ゼミ初见",first_report:"第一次レポート",part_time_hired:"便利店录用",hear_melody:"听出旋律结构",campus_festival:"校园祭准备",career_center:"Career Center 说明会",workshop_team:"Workshop 组队",hospitalized:"强制入院",burnout:"精神透支",financial_crisis:"经济危机"}[id]||id);
const stageName=x=>({stranger:"陌生",acquaintance:"相识",friend:"朋友",close:"亲近",special:"特别"}[x]||x);
const signedText=x=>`${x>0?"+":""}${x}`;
const signedClass=x=>x>=0?"good":"warn";

const coreCards=()=>coreKeys.map(key=>coreAttributeCard(key,0,"",true)).join("");

const views={
StartMenu:()=>`<div class="start-menu"><span class="kicker">LOCAL RULES × SAVE TERMINAL</span><h2 class="glitch-title" data-text="日留学部模拟器">日留学部模拟器</h2><p class="subtle">选择一个入口。规则在本地结算，文本只是把结果照亮。</p><div class="start-actions"><button class="primary" onclick="setUi('FacultySelection')">New Start</button><button class="secondary" onclick="setUi('Continue')">Continue</button><button class="secondary" onclick="setUi('ApiSettings')">API Settings</button><button class="secondary" onclick="setUi('Achievements')">Achievements</button></div></div>`,
FacultySelection:()=>facultySelectionView(),
SeminarSelection:()=>seminarSelectionView(),
Opening:()=>openingView(),
Continue:()=>`<div class="start-menu load-menu"><span class="kicker">SAVE SLOTS</span><h2 class="section-title">读取存档</h2><div class="save-grid">${saveSlotsView(false)}</div><div class="actions">${navButton("StartMenu","返回","secondary")}</div></div>`,
ApiSettings:()=>apiSettingsView(),
Achievements:()=>`<div class="start-menu"><span class="kicker">ACHIEVEMENTS</span><h2 class="section-title">成就系统</h2><p class="quote">暂未开放。这里以后会记录路线、结局、隐藏事件和奇怪但闪光的小胜利。</p><div class="actions">${navButton("StartMenu","返回","secondary")}</div></div>`,
MonthStart:()=>`<div class="hero"><span class="kicker">MONTH ${state.currentMonth} · ${facultyName(state.facultyId)}</span><h2>四月以后，时间开始按月结算。</h2><p class="subtle">你是日本大学${facultyName(state.facultyId)}的一名留学生。本月从牌组抽取 ${monthlyDrawCount()} 张卡片，最多选择 ${monthlySelectLimit()} 张。核心属性等级、事件与关系会不断解锁新的卡片。</p><div class="actions">${navButton("MonthPlan","查看本月卡片")}${navButton("Deck","查看整个牌组","secondary")}</div></div>${financeOverview()}<h2 class="section-title home-core-title">6 个核心属性</h2><p class="subtle">点击核心属性可查看它已经解锁和仍未解锁的卡片。未分配人生经验点数：${state.unspentLifeExperiencePoints} · 累计获得：${state.totalLifeExperiencePointsEarned}</p><div class="grid">${coreCards()}</div>`,
Deck:()=>deckView(),
CoreAttributeDetail:()=>coreAttributeDetailView(),
MonthPlan:()=>monthPlanView(),
CoreAttributeAllocation:()=>coreAttributeAllocationView(),
MonthResolution:()=>{const r=state.lastResolution;if(!r)return empty("尚未完成本月计划。","MonthPlan");return `<span class="kicker">LOCAL RESOLUTION</span><h2 class="section-title">第 ${r.month} 月结算</h2><div class="grid"><div class="card"><h3>HP 变化</h3><span class="metric ${signedClass(r.hpDelta)}">${signedText(r.hpDelta)}</span></div><div class="card"><h3>MP 变化</h3><span class="metric ${signedClass(r.mpDelta)}">${signedText(r.mpDelta)}</span></div><div class="card"><h3>持有金变化</h3><span class="metric ${signedClass(r.moneyDelta)}">${signedText(r.moneyDelta)}</span></div></div><p class="quote small">本月获得人生经验点数 <b>+${r.lifeExperiencePointsEarned}</b>。${Object.keys(r.lifeExperienceAllocations||{}).length?`本次分配：${lifeAllocationText(r.lifeExperienceAllocations)}。`:"本次没有分配点数。"} 当前剩余 ${state.unspentLifeExperiencePoints} 点。</p><p class="quote small">行动后余额 ¥${r.moneyAfterActions.toLocaleString()}，固定支出 -¥${r.fixedExpenseTotal.toLocaleString()}${r.tuitionPaid?`，学费 -¥${r.tuitionPaid.toLocaleString()}`:""}，月底余额 ¥${r.moneyAfterExpenses.toLocaleString()}。财务风险：${riskText(r.financialRisk)}</p>${aiUsageView()}${r.events.length?`<h3>本月事件</h3><p>${r.events.map(x=>`<span class="tag">${eventName(x)}</span>`).join("")}</p>`:""}<h3>卡片执行记录</h3>${r.actions.map(x=>`<div class="result"><b>${x.actionName}</b><span class="${x.result==="大成功"?"good":x.result==="失败"?"warn":""}">${x.result}</span><span>判定 ${x.score||"-"}</span><span>${x.detail||""}</span></div>`).join("")}<div class="actions">${navButton("OpportunitySelection","查看本月机会")}</div>`},
OpportunitySelection:()=>`<span class="kicker">OPPORTUNITIES</span><h2 class="section-title">关键机会只会等待一个月</h2>${state.opportunities.length?state.opportunities.map(x=>`<div class="opportunity"><h3>${x.title}${x.selected?' <span class="tag">已选择</span>':""}</h3><p>${x.description}</p><p class="subtle">风险：${x.risk}<br>可能收益：${x.reward}</p>${state.selectedOpportunityId?`<span class="subtle">${x.selected?"本月已投入":"本月已选择其他去向"}</span>`:`<button onclick="selectOpportunity('${x.id}')">选择这个机会</button>`}</div>`).join(""):`<p class="quote">这个月没有额外关键机会。空白并不总是坏事。</p>`}<div class="actions">${!state.selectedOpportunityId&&state.opportunities.length?`<button class="secondary" onclick="skipOpportunity()">这次不投入</button>`:""}${navButton("Relationship","前往人际关系","secondary")}</div>`,
Relationship:()=>`<span class="kicker">RELATIONSHIPS</span><h2 class="section-title">与你建立联系的人</h2>${relationshipMeter()}${Object.values(state.relationships).filter(x=>x.stage!=="stranger").map(relationshipCard).join("")}<div class="actions">${navButton("EventScene","查看事件演出")}</div>`,
RelationshipScene:()=>relationshipSceneView(),
EventScene:()=>`<span class="kicker">EVENT SCENES</span><h2 class="section-title">本月发生的事</h2>${state.storedAiPayloads?.eventScenes?.length?state.storedAiPayloads.eventScenes.map(x=>`<p class="quote">${x}</p>`).join(""):`<p class="quote">本月没有触发需要单独演出的事件。这是正常情况，不代表 AI 没有生成；月度叙事会在“月度回顾”里继续显示。</p>`}<div class="actions">${navButton("MonthlyReview","阅读月度回顾")}</div>`,
MonthlyReview:()=>{const p=state.storedAiPayloads;return p?`<span class="kicker">MONTHLY REVIEW · AI PAYLOAD</span><h2 class="section-title">${p.title}</h2><p class="quote">${p.summary}</p>${p.paragraphs.map(x=>`<p class="subtle">${x}</p>`).join("")}${aiUsageView()}<div class="actions">${navButton("Archive","写入日志")}</div>`:empty("完成月度计划后才会生成回顾。","MonthPlan")},
Archive:()=>`<span class="kicker">ARCHIVE</span><h2 class="section-title">你的学部生活日志</h2>${state.monthlyLogs.map(x=>`<div class="log">${x}</div>`).join("")}<div class="actions"><button class="primary" onclick="nextMonth()">${state.currentMonth>=state.maxMonth?"查看结局":"进入下个月"}</button></div>`,
Ending:()=>`<div class="hero"><span class="kicker">ENDING</span><h2>${state.endingId}</h2><p class="subtle">这一年的所有数值、事件、关系和选择都由本地状态判定。文字只负责让结果被看见。</p><div class="actions"><button class="primary" onclick="setUi('FacultySelection')">开始新游戏</button></div></div>`
};

function monthPlanView(){
  const hand=state.currentMonthHand||[];
  const shown=hand.filter(card=>activeCategory==="all"||card.primaryCoreAttribute===activeCategory);
  return `<span class="kicker">MONTH PLAN · CARDS</span><h2 class="section-title">从本月牌组中选择 1 到 ${monthlySelectLimit()} 张卡片</h2><div class="month-plan-layout"><section class="plan-status"><h3>当前状态</h3>${statusSnapshot()}<h3>所属研究会</h3><p class="subtle">${seminarName(state.seminarId)}</p></section><section class="card-market"><div class="tabs">${categoryOrder.map(x=>`<button class="${activeCategory===x?"active":""}" onclick="setCategory('${x}')">${label(x)}</button>`).join("")}</div><h3>本月抽选（${hand.length} / ${monthlyDrawCount()} 张）</h3><p class="subtle">本月最多选择 ${monthlySelectLimit()} 张，可刷新 ${monthlySwitchLimit()} 张；新卡片由核心属性等级、事件与关系解锁。</p><div class="card-list">${shown.map(cardView).join("")}</div></section><aside class="plan-summary"><h3>本月计划 ${planSelections.length}/${monthlySelectLimit()}</h3>${selectedList()}${predictionPanel()}${refreshPanel()}${planSubmitStatus()}<button class="primary execute" ${planSelections.length===0||planSubmitting?"disabled":""} onclick="submitPlan()">${planSubmitting?"本地结算中...":"执行本月计划"}</button></aside></div>`;
}

function statusSnapshot(){
  return `<div class="mini-stats"><span>HP <b>${state.stats.hp}</b></span><span>MP <b>${state.stats.mp}</b></span><span>¥ <b>${state.stats.money.toLocaleString()}</b></span></div><div class="mini-core">${coreKeys.map(k=>`<span>${label(k)} Lv ${coreAttributeLevel(state.core[k])}</span>`).join("")}</div><p class="subtle">人生经验点数：${state.unspentLifeExperiencePoints}<br>本月固定支出：¥${monthlyFixedExpense(state.housing).toLocaleString()} / 学费：${tuitionDueThisMonth(state.tuition)?"本月扣款":"还有 "+monthsUntilTuition(state.tuition)+" 个月"}</p>`;
}

function saveHudView(){
  return `<div class="hud-buttons">${saveHudMode==="save"?"":`<button class="edge-note note-save" onclick="pluckHud(this,'save')">SAVE</button>`}${saveHudMode==="load"?"":`<button class="edge-note note-load" onclick="pluckHud(this,'load')">LOAD</button>`}<button class="edge-note note-menu" onclick="pluckHud(this,'menu')">MENU</button></div>${saveHudMode?`<div class="save-hud-panel"><div class="hud-panel-head"><b>${saveHudMode==="save"?"保存到 Slot":"读取 Slot"}</b><button onclick="toggleSaveHud('')">×</button></div><div class="save-grid compact">${saveSlotsView(saveHudMode==="save")}</div></div>`:""}`;
}

function pluckHud(button,mode){
  button.classList.remove("note-pluck");
  void button.offsetWidth;
  button.classList.add("note-pluck");
  setTimeout(()=>{
    if(mode==="menu")setUi("StartMenu");
    else toggleSaveHud(mode);
  },260);
}

function toggleSaveHud(mode){
  saveHudMode=saveHudMode===mode?"":mode;
  render();
}

function saveSlotsView(allowSave){
  const slots=saveSlots.length?saveSlots:Array.from({length:20},(_,i)=>({slot:i+1,exists:false}));
  return slots.map(s=>`<article class="save-slot ${s.exists?"filled":"empty"}"><h3>Slot ${String(s.slot).padStart(2,"0")}</h3>${s.exists?`<p class="subtle">第 ${s.currentMonth} 月 / ¥${Number(s.money||0).toLocaleString()}<br>${formatSaveTime(s.savedAt)}</p><button class="primary" onclick="loadGame(${s.slot})">读取</button>`:`<p class="subtle">EMPTY</p><button disabled>读取</button>`}${allowSave?`<button class="secondary" onclick="saveToSlot(${s.slot})">保存</button>`:""}</article>`).join("");
}

function aiUsageView(){
  const u=state.storedAiPayloads?.usage;
  const source=state.storedAiPayloads?.source||"unknown";
  const reason=state.storedAiPayloads?.fallbackReason;
  const sourceText=source==="fallback"?"Fallback":source==="mock"?"Mock":source;
  if(source==="pending")return `<div class="ai-pending"><b>AI 演出传输中</b><span></span><p>本地结算已经完成。你可以先继续看结果，文本演出稍后会自动回来。</p></div>`;
  if(!u)return `<p class="quote small">AI 来源：${sourceText}${reason?`（${escapeHtml(reason)}）`:""}。未记录真实 API usage。</p>`;
  return `<p class="quote small">AI 来源：${sourceText} · ${u.model||"model"} · 输入 ${Number(u.promptTokens||0).toLocaleString()} tokens / 输出 ${Number(u.completionTokens||0).toLocaleString()} tokens / 合计 ${Number(u.totalTokens||0).toLocaleString()} tokens${u.promptCacheHitTokens?` · cache hit ${Number(u.promptCacheHitTokens).toLocaleString()}`:""}</p>`;
}

function scheduleAiPolling(){
  clearTimeout(aiPollTimer);
  const monthlyPending=state?.storedAiPayloads?.source==="pending";
  const openingPending=state?.openingNarration?.source==="pending";
  const relationshipPending=state?.relationshipInteraction?.source==="pending";
  if(!monthlyPending&&!openingPending&&!relationshipPending)return;
  aiPollTimer=setTimeout(async()=>{
    const r=await fetch("/api/state");
    const value=await r.json();
    data=value; state=value.state; cards=value.cards||cards; cardCatalog=value.cardCatalog||cardCatalog; labels=value.labels||labels; faculties=value.faculties||faculties; seminars=value.seminars||seminars; characters=value.characters||characters; finance=value.finance||finance; relationship=value.relationship||relationship; saveSlots=value.saves||saveSlots;
    render();
  },2000);
}

function facultySelectionView(){
  return `<div class="faculty-select"><span class="kicker">NEW GAME · FACULTY ROUTE</span><h2 class="section-title">选择你将进入的学部</h2><p class="quote small">学部决定大盘节奏与事件时间；下一步还会独立选择研究会。</p><div class="faculty-grid">${faculties.map(f=>`<article class="faculty-card"><span class="tag">${f.theme}</span><h3>${f.name}</h3><p>${f.description}</p><p class="subtle">初始倾向：${coreDeltaText(f.initialCoreDelta)}<br>第一次报告：第 ${f.eventMonths?.first_report||2} 月<br>就业中心窗口：第 ${f.eventMonths?.career_center||3} 月<br>校园祭窗口：第 ${f.eventMonths?.campus_festival||8} 月</p><button class="primary" onclick="chooseFaculty('${f.id}')">选择 ${f.name}</button></article>`).join("")}</div><div class="actions">${navButton("StartMenu","返回主菜单","secondary")}</div></div>`;
}

function seminarSelectionView(){
  const faculty=faculties.find(x=>x.id===selectedFacultyId);
  const available=seminars.filter(x=>x.facultyIds?.includes(selectedFacultyId));
  return `<div class="faculty-select"><span class="kicker">NEW GAME · SEMINAR ROUTE</span><h2 class="section-title">${faculty?.name||""}：选择研究会</h2><p class="quote small">研究会独立于学部，决定更具体的人物、事件、flag 和机会倾向。魔法少女研究与雨宫栞只存在于对应研究会。</p><div class="faculty-grid seminar-grid">${available.map(s=>`<article class="faculty-card"><span class="tag">${s.theme}</span><h3>${s.name}</h3><p>${s.description}</p><p class="subtle">初始人物：${s.initialCharacterIds?.length?s.initialCharacterIds.map(characterName).join(" / "):"无专属人物"}<br>机会倾向：${(s.opportunityBias||[]).map(label).join(" / ")}</p><button class="primary" onclick="newGame('${selectedFacultyId}','${s.id}')">加入 ${s.name}</button></article>`).join("")}</div><div class="actions">${navButton("FacultySelection","重新选择学部","secondary")}</div></div>`;
}

function chooseFaculty(id){selectedFacultyId=id;setUi("SeminarSelection")}

function openingView(){
  const p=state.openingNarration;
  if(!p)return `<div class="opening-screen"><h2 class="section-title">开场文本尚未准备完成</h2></div>`;
  const usage=p.source==="local"
    ? `<p class="quote small">开场来源：本地学部与研究会模板，不调用 AI。</p>`
    : p.usage?`<p class="quote small">AI 来源：${p.source} · 输入 ${Number(p.usage.promptTokens||0).toLocaleString()} / 输出 ${Number(p.usage.completionTokens||0).toLocaleString()} tokens</p>`:`<p class="quote small">来源：${p.source}${p.fallbackReason?`（${escapeHtml(p.fallbackReason)}）`:""}</p>`;
  return `<div class="opening-screen"><span class="kicker">PROLOGUE · ${facultyName(p.facultyId)}</span><h2 class="section-title">${p.title}</h2><div class="opening-prose">${p.paragraphs.map(x=>`<p>${x}</p>`).join("")}</div>${usage}<div class="actions"><button class="primary" onclick="beginFirstMonth()">开始第一个月</button></div></div>`;
}

function beginFirstMonth(){api("/api/game/begin",{})}
function facultyName(id){return faculties.find(x=>x.id===id)?.name||id}
function seminarName(id){return seminars.find(x=>x.id===id)?.name||id}

function planSubmitStatus(){
  return planSubmitting?`<div class="ai-pending plan-pending"><b>本地结算中</b><span></span><p>正在掷骰、扣支出、检查事件。AI 文本不会阻塞这一步。</p></div>`:"";
}

function coreAttributeLevel(experience){
  let level=0;
  while(experience>=coreAttributeTotalForLevel(level+1))level++;
  return level;
}

function coreAttributeTotalForLevel(level){return 100*level*(level+1)/2}

function coreAttributeProgress(experience){
  const level=coreAttributeLevel(experience);
  const floor=coreAttributeTotalForLevel(level);
  const ceiling=coreAttributeTotalForLevel(level+1);
  return {level,current:experience-floor,required:ceiling-floor,remaining:ceiling-experience};
}

function coreAttributeCard(key,extraExperience=0,controls="",clickable=false){
  const experience=(state.core[key]||0)+extraExperience;
  const p=coreAttributeProgress(experience);
  const width=Math.min(100,Math.round(p.current/p.required*100));
  return `<div class="card core-attribute-card ${clickable?"clickable":""}" ${clickable?`role="button" tabindex="0" onclick="openCoreAttributeDetail('${key}')" onkeydown="if(event.key==='Enter'||event.key===' ')openCoreAttributeDetail('${key}')"`:""}><h3>${label(key)}</h3><span class="metric">Lv ${p.level}</span><div class="bar"><i style="width:${width}%"></i></div><small class="subtle">${experience} EXP · 距离 Lv ${p.level+1} 还需 ${p.remaining} EXP</small>${clickable?`<span class="core-card-link">查看相关卡片 →</span>`:""}${controls}</div>`;
}

function deckView(){
  const filters=[
    ["all","全部"],["unlocked","已解锁"],["locked","未解锁"],
    ...coreKeys.map(x=>[x,label(x)]),
    ["Common","Common"],["Rare","Rare"],["Special","Special"]
  ];
  const shown=cardCatalog.filter(item=>catalogMatchesFilter(item,deckFilter));
  const unlockedCount=cardCatalog.filter(x=>x.unlocked).length;
  return `<span class="kicker">DECK · CARD CATALOG</span><h2 class="section-title">当前整个牌组</h2><p class="quote small">共 ${cardCatalog.length} 张卡片，已解锁 ${unlockedCount} 张，未解锁 ${cardCatalog.length-unlockedCount} 张。未解锁卡片会显示当前还差哪些条件。</p><div class="tabs deck-filters">${filters.map(([id,name])=>`<button class="${deckFilter===id?"active":""}" onclick="setDeckFilter('${id}')">${name}</button>`).join("")}</div><div class="deck-card-grid">${shown.map(catalogCardView).join("")||`<p class="quote">当前过滤条件下没有卡片。</p>`}</div><div class="actions">${navButton("MonthStart","返回月初主页","secondary")}${navButton("MonthPlan","前往本月计划")}</div>`;
}

function coreAttributeDetailView(){
  const key=selectedCoreAttribute||"academic";
  const p=coreAttributeProgress(state.core[key]||0);
  const relevant=cardCatalog.filter(x=>x.card.primaryCoreAttribute===key&&(!x.card.isHiddenUntilUnlocked||x.unlocked));
  const unlocked=relevant.filter(x=>x.unlocked);
  const locked=relevant.filter(x=>!x.unlocked);
  return `<span class="kicker">CORE ATTRIBUTE · ${label(key)}</span><h2 class="section-title">${label(key)}属性详情</h2><div class="core-detail-summary"><div><span>当前等级</span><b>Lv ${p.level}</b></div><div><span>累计经验</span><b>${state.core[key]||0} EXP</b></div><div><span>距离下一级</span><b>${p.remaining} EXP</b></div><div><span>相关卡片</span><b>${unlocked.length} / ${relevant.length}</b></div></div><h3 class="deck-section-title">已解锁${label(key)}卡片</h3><div class="deck-card-grid">${unlocked.map(catalogCardView).join("")||`<p class="quote">尚未解锁该属性的卡片。</p>`}</div><h3 class="deck-section-title">未解锁${label(key)}卡片</h3><div class="deck-card-grid">${locked.map(catalogCardView).join("")||`<p class="quote">该属性的卡片已经全部解锁。</p>`}</div><div class="actions">${navButton("MonthStart","返回月初主页","secondary")}${navButton("Deck","查看整个牌组")}</div>`;
}

function catalogMatchesFilter(item,filter){
  if(filter==="all")return true;
  if(filter==="unlocked")return item.unlocked;
  if(filter==="locked")return !item.unlocked;
  if(filter==="Common"||filter==="Rare"||filter==="Special")return item.card.rarity===filter;
  if(item.card.isHiddenUntilUnlocked&&!item.unlocked)return false;
  if(coreKeys.includes(filter))return item.card.primaryCoreAttribute===filter;
  return false;
}

function catalogCardView(item){
  const card=item.card;
  if(card.isHiddenUntilUnlocked&&!item.unlocked)return hiddenCatalogCardView(item);
  return `<article class="action-card catalog-card cat-${card.primaryCoreAttribute} rarity-${card.rarity} ${item.unlocked?"unlocked":"locked"}"><div class="card-head"><span class="tag">${item.unlocked?"已解锁":"未解锁"}</span><span class="tag">${rarityName(card.rarity)}</span><span class="tag">${label(card.primaryCoreAttribute)}</span></div><h3>${escapeHtml(card.name)}</h3><p>${escapeHtml(card.description||"")}</p><p class="quote small">${escapeHtml(card.meaningText||"")}</p><div class="delta-row"><span class="${signedClass(card.hpDelta)}">HP ${signedText(card.hpDelta)}</span><span class="${signedClass(card.mpDelta)}">MP ${signedText(card.mpDelta)}</span><span class="${signedClass(card.moneyDelta)}">¥ ${signedText(card.moneyDelta)}</span></div><p class="subtle">核心属性收益：${label(card.primaryCoreAttribute)} +${card.coreExpDelta} EXP<br>卡片类型：${typeName(card.cardType)}</p>${unlockConditionsView(item)}</article>`;
}

function hiddenCatalogCardView(item){
  return `<article class="action-card catalog-card hidden-card locked condition-only"><div class="card-head"><span class="tag">未解锁</span><span class="tag">???</span><span class="tag">???</span></div><h3>？？？？？？</h3><p>卡片信息：？？？？？？</p>${unlockConditionsView(item)}</article>`;
}

function unlockConditionsView(item){
  if(item.unlocked)return "";
  const conditions=item.unlockConditions||[];
  if(!conditions.length)return "";
  return `<div class="unlock-conditions ${item.unlocked?"all-met":""}"><b>解锁条件</b>${conditions.map(x=>`<div class="unlock-condition ${x.satisfied?"met":"unmet"}"><span>${x.satisfied?"✓":"×"} ${escapeHtml(x.label)}</span><small>要求：${escapeHtml(x.required)} · 当前：${escapeHtml(x.current)}</small></div>`).join("")}</div>`;
}

function coreAttributeAllocationView(){
  const drafted=Object.values(coreAllocationDraft).reduce((sum,x)=>sum+x,0);
  const remaining=Math.max(0,state.unspentLifeExperiencePoints-drafted);
  const cards=coreKeys.map(key=>{
    const points=coreAllocationDraft[key]||0;
    const controls=`<div class="allocation-controls"><button class="secondary" ${points<=0?"disabled":""} onclick="changeCoreAllocation('${key}',-1)">−</button><b>${points} 点</b><button class="primary" ${remaining<=0?"disabled":""} onclick="changeCoreAllocation('${key}',1)">＋</button></div><small class="subtle">确认后增加 ${points*10} 核心属性 EXP</small>`;
    return coreAttributeCard(key,points*10,controls);
  }).join("");
  return `<span class="kicker">CORE ATTRIBUTES · LIFE EXPERIENCE</span><h2 class="section-title">把经历变成你想成为的方向</h2><div class="life-exp-summary"><div><span>未分配人生经验点数</span><b>${state.unspentLifeExperiencePoints}</b></div><div><span>本次准备分配</span><b>${drafted}</b></div><div><span>分配后剩余</span><b>${remaining}</b></div></div><p class="quote small">每 1 点人生经验点数可增加指定核心属性 10 EXP。核心属性等级越高，升到下一级需要的 EXP 越多。这里的分配完全由你决定，AI 不参与。</p>${aiUsageView()}<div class="grid core-allocation-grid">${cards}</div><div class="actions"><button class="primary" onclick="confirmCoreAllocation()">${drafted>0?"确认分配":"跳过并继续"}</button><button class="secondary" ${drafted<=0?"disabled":""} onclick="resetCoreAllocation()">撤回本次全部分配</button></div>`;
}

function changeCoreAllocation(key,delta){
  const drafted=Object.values(coreAllocationDraft).reduce((sum,x)=>sum+x,0);
  if(delta>0&&drafted>=state.unspentLifeExperiencePoints)return;
  coreAllocationDraft[key]=Math.max(0,(coreAllocationDraft[key]||0)+delta);
  render();
}

function resetCoreAllocation(){coreAllocationDraft={};render()}

async function confirmCoreAllocation(){
  const allocations={...coreAllocationDraft};
  coreAllocationDraft={};
  await api("/api/core-attributes/allocate",{allocations});
}

function lifeAllocationText(allocations){
  return Object.entries(allocations||{}).filter(([,v])=>v>0).map(([k,v])=>`${label(k)} +${v} 点`).join(" / ")||"无";
}

function relationshipMeter(){
  const used=relationship?.actionsUsed||0;
  const limit=relationship?.actionLimit||0;
  const remaining=Math.max(0,limit-used);
  return `<div class="relationship-meter"><b>本月可互动 ${used}/${limit}</b><span class="${remaining>0?"good":"warn"}">${remaining>0?`还可行动 ${remaining} 次`:"本月互动次数已用完"}</span><p class="subtle">关系阶段、好感/信赖和关系能力提升后，每月可互动次数会提高。</p></div>`;
}

function relationshipRemaining(){return Math.max(0,(relationship?.actionLimit||0)-(relationship?.actionsUsed||0))}

function relationshipSceneView(){
  const p=state.relationshipInteraction;
  if(!p||p.source==="pending")return `<span class="kicker">RELATIONSHIP SCENE · AI PERFORMANCE</span><h2 class="section-title">正在生成这次相处</h2><div class="opening-loader"><div class="opening-loader-bar"><i></i></div><p>本地关系数值已经结算。AI 正在根据当前好感、信赖、关系阶段和人物性格生成即时场景……</p></div>`;
  if(visualNovelSceneId!==p.interactionId){
    visualNovelSceneId=p.interactionId;
    visualNovelLineIndex=0;
  }
  const lines=p.lines||[];
  if(visualNovelLineIndex>=lines.length)return visualNovelResultView(p);
  const line=lines[visualNovelLineIndex];
  const backgroundPath=line.backgroundPath||p.backgroundPath||"/assets/backgrounds/default_campus.png";
  const defaultNpcPortrait=p.characters?.find(x=>x.characterId===p.characterId)?.portraitPath||"/assets/portraits/default_npc.png";
  const portraitFallback=line.lineType==="npc"?defaultNpcPortrait:"/assets/portraits/default_player.png";
  const portraitPath=line.portraitPath||portraitFallback;
  const progress=`${visualNovelLineIndex+1} / ${lines.length}`;
  const portrait=line.lineType==="narration"?"":`<div class="vn-portrait ${line.lineType}">${vnImage(portraitPath,line.speakerName,portraitFallback)}</div>`;
  return `<div class="visual-novel-scene line-${line.lineType}" onclick="nextVisualNovelLine()"><button class="vn-skip" onclick="event.stopPropagation();skipVisualNovel()">SKIP</button><div class="vn-stage"><div class="vn-background">${vnImage(backgroundPath,p.backgroundId||"场景")}</div><div class="vn-stage-shade"></div>${portrait}<span class="vn-scene-title">${escapeHtml(p.title||"互动场景")}</span><span class="vn-line-progress">${progress}</span></div><div class="vn-dialogue"><div class="vn-speaker"><b>${escapeHtml(line.speakerName||"旁白")}</b>${line.lineType==="npc"?`<span>${expressionName(line.expression)}</span>`:""}</div><p>${escapeHtml(line.text||"")}</p><button class="primary vn-next" onclick="event.stopPropagation();nextVisualNovelLine()">下一句</button></div></div>`;
}

function visualNovelResultView(p){
  const options=p.interactionOptions||[];
  const finalLine=(p.lines||[]).at(-1)||{};
  const backgroundPath=finalLine.backgroundPath||p.backgroundPath||"/assets/backgrounds/default_campus.png";
  const defaultNpcPortrait=p.characters?.find(x=>x.characterId===p.characterId)?.portraitPath||"/assets/portraits/default_npc.png";
  const portraitPath=finalLine.lineType==="npc"?(finalLine.portraitPath||defaultNpcPortrait):defaultNpcPortrait;
  const stage=`<div class="vn-stage"><div class="vn-background">${vnImage(backgroundPath,p.backgroundId||"场景")}</div><div class="vn-stage-shade"></div><div class="vn-portrait npc">${vnImage(portraitPath,characterName(p.characterId),defaultNpcPortrait)}</div><span class="vn-scene-title">${escapeHtml(p.title||"互动场景")}</span></div>`;
  if(!p.selectedOptionId&&options.length){
    return `<div class="visual-novel-scene vn-choice-scene">${stage}<div class="vn-choice-overlay"><span>YOUR RESPONSE</span><div class="vn-choice-list">${options.map((option,index)=>`<button class="vn-choice" onclick="chooseRelationshipOption('${escapeAttr(p.interactionId)}','${escapeAttr(option.optionId)}')"><span>${String(index+1).padStart(2,"0")}</span><b>${escapeHtml(option.text)}</b></button>`).join("")}</div></div><div class="vn-dialogue line-player"><div class="vn-speaker"><b>你</b></div><p>短暂的停顿里，对方正在等待你的回应。</p></div></div>`;
  }
  const selected=options.find(x=>x.optionId===p.selectedOptionId);
  return `<div class="visual-novel-scene vn-result-scene">${stage}<div class="vn-dialogue"><div class="vn-speaker"><b>${escapeHtml(characterName(p.characterId))}</b></div><p>${escapeHtml(p.choiceResultText||p.resultText||"这次互动结束了。")}</p><small class="vn-result-meta">${selected?`你的回应：${escapeHtml(selected.text)} · `:""}好感 ${signedText(p.affectionDelta)} / 信赖 ${signedText(p.trustDelta)}</small><button class="primary vn-next" onclick="setUi('EventScene')">继续</button></div></div>`;
}

function nextVisualNovelLine(){visualNovelLineIndex++;render()}
function skipVisualNovel(){visualNovelLineIndex=(state.relationshipInteraction?.lines||[]).length;render()}
async function chooseRelationshipOption(interactionId,optionId){
  await api("/api/relationship/choice",{interactionId,optionId});
}
function vnImage(path,alt,fallbackPath=""){return path?`<img src="${escapeAttr(path)}" alt="${escapeAttr(alt||"")}" data-fallback-src="${escapeAttr(fallbackPath||"")}" onload="markVisualNovelImageLoaded(this)" onerror="fallbackVisualNovelImage(this)">`:""}
function markVisualNovelImageLoaded(image){
  image.hidden=false;
  image.parentElement?.classList.remove("portrait-failed");
}
function fallbackVisualNovelImage(image){
  const fallback=image.dataset.fallbackSrc;
  if(fallback){
    image.dataset.fallbackSrc="";
    image.src=fallback;
    return;
  }
  image.hidden=true;
  image.parentElement?.classList.add("portrait-failed");
}
function expressionName(expression){return {neutral:"平静",calm:"平静",happy:"喜悦",sad:"悲伤",angry:"生气",surprised:"惊讶"}[expression]||"平静"}

function apiSettingsView(){
  const s=aiSettings||{mode:"Mock",endpoint:"https://api.deepseek.com/chat/completions",model:"deepseek-v4-flash",hasApiKey:false,maskedApiKey:"",timeoutSeconds:45,modelOptions:[]};
  const models=s.modelOptions?.length?s.modelOptions:[
    {label:"V4 Flash",model:"deepseek-v4-flash",description:"更省 token，适合日常月度演出。"},
    {label:"V4 Pro",model:"deepseek-v4-pro",description:"更强，适合重要剧情月或复杂事件。"}
  ];
  return `<div class="start-menu load-menu api-settings"><span class="kicker">AI NARRATION SETTINGS</span><h2 class="section-title">API 设置</h2><p class="subtle">这里保存的是本机运行时配置，不会写进 repo。主菜单和读档页仍使用本地规则；AI 只负责月度文本演出。</p><div class="api-form"><label>模式<select id="aiMode" onchange="syncAiEndpoint()"><option value="Mock" ${s.mode==="Mock"?"selected":""}>Mock（不调用 API）</option><option value="DeepSeek" ${s.mode==="DeepSeek"||s.mode==="OpenAICompatible"?"selected":""}>DeepSeek / OpenAI-Compatible</option></select></label><label>模型<select id="aiModel">${models.map(m=>`<option value="${escapeAttr(m.model)}" ${s.model===m.model?"selected":""}>${m.label} · ${m.model}</option>`).join("")}</select></label><label>Endpoint<input id="aiEndpoint" value="${escapeAttr(s.endpoint||"https://api.deepseek.com/chat/completions")}"></label><label>API Key<input id="aiApiKey" type="password" autocomplete="off" placeholder="${s.hasApiKey?`已保存：${escapeAttr(s.maskedApiKey)}`:"粘贴 DeepSeek API Key"}"></label><label>Timeout 秒<input id="aiTimeout" type="number" min="5" max="120" value="${s.timeoutSeconds||45}"></label><label class="clear-key"><input id="aiClearKey" type="checkbox"> 清除已保存 API Key</label></div><div class="api-model-notes">${models.map(m=>`<p><b>${m.label}</b> <span>${m.description}</span></p>`).join("")}</div><div class="actions"><button class="primary" onclick="saveAiSettings()">保存设置</button><button class="secondary" onclick="testAiSettings()">低 token 测试连接</button>${navButton("StartMenu","返回","secondary")}</div></div>`;
}

function formatSaveTime(value){
  if(!value)return "时间未知";
  const d=new Date(value);
  return Number.isNaN(d.getTime())?"时间未知":d.toLocaleString();
}

function financeOverview(){
  const f=finance||buildFinanceSnapshot(state);
  return `<h2 class="section-title">生活费与住房压力</h2><div class="grid finance-grid"><div class="card"><h3>当前 Money</h3><span class="metric">¥${f.currentMoney.toLocaleString()}</span><small class="subtle">风险：${riskText(f.riskLevel)}</small></div><div class="card"><h3>本月固定支出</h3><span class="metric warn">¥${f.monthlyFixedExpense.toLocaleString()}</span><small class="subtle">房租 ${f.rent.toLocaleString()} / 生活 ${f.livingCost.toLocaleString()} / 通信交通 ${(f.communicationCost+f.transportationCost).toLocaleString()}</small></div><div class="card"><h3>扣除后预计余额</h3><span class="metric ${signedClass(f.projectedBalanceAfterFixed)}">¥${f.projectedBalanceAfterFixed.toLocaleString()}</span><small class="subtle">${f.tuitionDueThisMonth?"本月另扣学费 ¥"+f.tuitionAmount.toLocaleString():"距离下次学费 "+f.monthsUntilTuition+" 个月 / ¥"+f.tuitionAmount.toLocaleString()}</small></div><div class="card"><h3>住房状态</h3><span class="metric">¥${f.rent.toLocaleString()}</span><small class="subtle">舒适度 ${f.housingComfort} / 通勤负担 ${f.commuteBurden}</small></div></div>`;
}

function cardView(card){
  const selected=planSelections.some(x=>x.cardId===card.cardId);
  const locked=!!card.lockedReason;
  const canReserve=!locked&&!selected&&!card.isReservedForNextMonth;
  const canRefresh=!locked&&!selected&&!card.isReservedForNextMonth&&!card.isPinnedFromLastMonth&&!state.hasRefreshedCardsThisMonth;
  const reserveReason=reserveDisabledReason(card,{locked,selected});
  return `<article class="action-card cat-${card.primaryCoreAttribute} type-${card.cardType} rarity-${card.rarity} ${locked?"locked":""} ${selected?"selected":""}"><div class="card-head"><span class="tag">${rarityName(card.rarity)}</span><span class="tag">${typeName(card.cardType)}</span><span class="tag">${label(card.primaryCoreAttribute)}</span>${card.isInitialCard?`<span class="tag">基础初始</span>`:card.initialFacultyIds?.includes(state.facultyId)?`<span class="tag">学部初始</span>`:""}${card.isPinnedFromLastMonth?`<span class="tag">上月保留</span>`:""}${card.isReservedForNextMonth?`<span class="tag">已保留</span>`:""}</div><h3>${card.name}</h3><p>${card.description||""}</p><p class="quote small">${card.meaningText||""}</p><div class="delta-row"><span class="${signedClass(card.hpDelta)}">HP ${signedText(card.hpDelta)}</span><span class="${signedClass(card.mpDelta)}">MP ${signedText(card.mpDelta)}</span><span class="${signedClass(card.moneyDelta)}">¥ ${signedText(card.moneyDelta)}</span></div><p class="subtle">主要核心属性：${label(card.primaryCoreAttribute)}<br>核心属性经验：+${card.coreExpDelta}<br>${housingDeltaText(card.housingDelta)}${card.tuitionDelayMonths?`<br>学费延纳：+${card.tuitionDelayMonths} 个月`:""}卡片标签：${card.cardTags?.length?card.cardTags.join(" / "):"无"}${card.possibleEventIds?.length?`<br>可能事件：${card.possibleEventIds.map(eventName).join(" / ")}`:""}</p>${locked?`<p class="warn">未解锁：${card.lockedReason}</p>`:`<div class="card-actions"><button class="secondary" ${selected||card.isReservedForNextMonth?"disabled":""} onclick="addCard('${card.cardId}')">${selected?"已加入":card.isReservedForNextMonth?"已保留":"加入本月计划"}</button>${reserveControl(card,canReserve,reserveReason)}<label class="refresh-check"><input type="checkbox" ${refreshSelections.includes(card.cardId)?"checked":""} ${canRefresh?"":"disabled"} onchange="toggleRefresh('${card.cardId}',this.checked)"> 刷新</label>${reserveReason?`<small class="subtle">${reserveReason}</small>`:""}</div>`}</article>`;
}

function reserveControl(card,canReserve,reserveReason){
  if(card.isReservedForNextMonth)return `<button class="secondary" onclick="cancelReserveCard('${card.cardId}')">取消保留</button>`;
  return `<button ${canReserve?"":"disabled"} title="${escapeAttr(reserveReason)}" onclick="reserveCard('${card.cardId}')">保留</button>`;
}

function reserveDisabledReason(card,context){
  if(card.isReservedForNextMonth)return "这张卡已保留到下个月。";
  if(context.selected)return "已加入本月计划的卡不能保留。";
  if(context.locked)return card.lockedReason||"未解锁卡不能保留。";
  return "";
}

function selectedList(){
  if(planSelections.length===0)return `<p class="quote small">还没有选择行动。先挑一张这个月想抓住的机会。</p>`;
  return `<div class="selected-list">${planSelections.map((item,i)=>{const card=findCard(item.cardId);return `<div class="selected-item"><b>${card?.name||item.cardId}</b><button onclick="removeCard(${i})">删除</button>${noteEditor(item,i)}</div>`}).join("")}</div>`;
}

function noteEditor(item,i){
  return `<label>备注<input value="${escapeAttr(item.customNote||"")}" oninput="updateSelection(${i},'customNote',this.value)" placeholder="可选，影响月度文本演出"></label>`;
}

function predictionPanel(){
  const p=predict();
  return `<div class="prediction"><h3>实时预测</h3><p>HP：${state.stats.hp} → <b class="${signedClass(p.hpAfter-state.stats.hp)}">${p.hpAfter}</b><br>MP：${state.stats.mp} → <b class="${signedClass(p.mpAfter-state.stats.mp)}">${p.mpAfter}</b><br>行动后余额：${state.stats.money.toLocaleString()} → <b class="${signedClass(p.moneyAfterActions-state.stats.money)}">${p.moneyAfterActions.toLocaleString()}</b><br>扣除本月固定支出后：<b class="${signedClass(p.moneyAfterExpenses)}">${p.moneyAfterExpenses.toLocaleString()}</b></p><p class="subtle">核心属性收益：${coreDeltaText(p.coreDelta)}<br>卡片标签：${p.cardTags.length?p.cardTags.join(" / "):"无"}<br>住房变化：${housingDeltaText(p.housingDelta)||"无"}财务风险：${riskText(p.financialRisk)}${p.crisis?`<br><span class="warn">会触发财务危机</span>`:""}<br>风险提示：${p.risks.length?p.risks.join("；"):"当前风险可控"}</p></div>`;
}

function refreshPanel(){
  const limit=monthlySwitchLimit();
  const disabled=limit<1||state.hasRefreshedCardsThisMonth||refreshSelections.length===0||refreshSelections.length>limit||state.stats.mp<5;
  const reason=limit<1?"当前不能刷新":state.hasRefreshedCardsThisMonth?"本月已刷新":state.stats.mp<5?"MP 不足 5":refreshSelections.length>limit?`最多选择 ${limit} 张刷新`:refreshSelections.length===0?"勾选要刷新的卡片":"消耗 MP 5";
  return `<div class="prediction"><h3>刷新卡片</h3><p class="subtle">已勾选 ${refreshSelections.length}/${limit}。${reason}</p><button class="secondary execute" ${disabled?"disabled":""} onclick="refreshCards()">刷新所选卡</button></div>`;
}

function predict(){
  let hp=state.stats.hp, mp=state.stats.mp, money=state.stats.money;
  let housing={...state.housing};
  let tuition={...state.tuition};
  const core={}, tags=new Set();
  const housingDelta={rentDelta:0,housingComfortDelta:0,commuteBurdenDelta:0};
  for(const item of planSelections){
    const card=resolveClientCard(item);
    hp=Math.min(100,hp+card.hpDelta);
    mp=Math.min(100,mp+card.mpDelta);
    money+=card.moneyDelta;
    applyHousingPreview(housing,card.housingDelta);
    mergeHousingDelta(housingDelta,card.housingDelta);
    if(card.tuitionDelayMonths&&monthsUntilTuition(tuition)<=2)tuition.nextDueMonth+=card.tuitionDelayMonths;
    core[card.primaryCoreAttribute]=(core[card.primaryCoreAttribute]||0)+(card.coreExpDelta||0);
    (card.cardTags||[]).forEach(x=>tags.add(x));
  }
  const moneyAfterActions=money;
  const totalDue=monthlyFixedExpense(housing)+(tuitionDueThisMonth(tuition)?tuition.amount:0);
  const moneyAfterExpenses=moneyAfterActions-totalDue;
  const risks=[];
  if(hp<=0)risks.push("严重风险：可能强制入院");
  else if(hp<30)risks.push("身体状态危险");
  if(mp<=0)risks.push("严重风险：可能强制摆烂一个月");
  else if(mp<30)risks.push("精神状态危险");
  if(moneyAfterExpenses<0)risks.push("经济危机：月底余额会跌破 0");
  else if(moneyAfterExpenses<30000)risks.push("经济压力上升");
  if(tuitionDueThisMonth(tuition))risks.push("本月会扣除学费");
  return {hpAfter:hp,mpAfter:mp,moneyAfterActions,moneyAfterExpenses,coreDelta:core,cardTags:[...tags],housingDelta,financialRisk:riskLevel(moneyAfterExpenses),crisis:moneyAfterExpenses<0,risks};
}

function resolveClientCard(item){
  const original=findCard(item.cardId);
  return original||{hpDelta:0,mpDelta:0,moneyDelta:0,primaryCoreAttribute:"life",coreExpDelta:0,cardTags:[],housingDelta:{}};
}

function buildFinanceSnapshot(s){
  const fixed=monthlyFixedExpense(s.housing);
  const due=tuitionDueThisMonth(s.tuition);
  const tuitionDueAmount=due?s.tuition.amount:0;
  const projected=s.stats.money-fixed-tuitionDueAmount;
  return {
    currentMoney:s.stats.money,
    rent:s.housing.rent,
    housingComfort:s.housing.housingComfort,
    commuteBurden:s.housing.commuteBurden,
    livingCost:s.monthlyExpense.livingCost,
    communicationCost:s.monthlyExpense.communicationCost,
    transportationCost:s.monthlyExpense.transportationCost,
    monthlyFixedExpense:fixed,
    tuitionAmount:s.tuition.amount,
    tuitionDueThisMonth:due,
    monthsUntilTuition:monthsUntilTuition(s.tuition),
    projectedBalanceAfterFixed:projected,
    riskLevel:riskLevel(projected)
  };
}

function monthlyFixedExpense(housing){
  return (housing?.rent||0)+(state.monthlyExpense?.livingCost||0)+(state.monthlyExpense?.communicationCost||0)+(state.monthlyExpense?.transportationCost||0);
}

function tuitionDueThisMonth(tuition){return state.currentMonth>=(tuition?.nextDueMonth||999)}
function monthsUntilTuition(tuition){return Math.max(0,(tuition?.nextDueMonth||0)-state.currentMonth)}
function riskLevel(money){return money<0?"crisis":money<30000?"pressure":money<80000?"watch":"stable"}
function riskText(level){return {stable:"稳定",watch:"注意",pressure:"紧张",crisis:"危机"}[level]||level}

function applyHousingPreview(housing,delta){
  if(!delta)return;
  housing.rent=Math.max(20000,(housing.rent||0)+(delta.rentDelta||0));
  housing.housingComfort=Math.max(0,Math.min(100,(housing.housingComfort||0)+(delta.housingComfortDelta||0)));
  housing.commuteBurden=Math.max(0,Math.min(100,(housing.commuteBurden||0)+(delta.commuteBurdenDelta||0)));
}

function mergeHousingDelta(target,delta){
  if(!delta)return;
  target.rentDelta+=(delta.rentDelta||0);
  target.housingComfortDelta+=(delta.housingComfortDelta||0);
  target.commuteBurdenDelta+=(delta.commuteBurdenDelta||0);
}

function housingDeltaText(delta){
  if(!delta)return "";
  const parts=[];
  if(delta.rentDelta)parts.push(`房租 ${signedText(delta.rentDelta)}`);
  if(delta.housingComfortDelta)parts.push(`舒适度 ${signedText(delta.housingComfortDelta)}`);
  if(delta.commuteBurdenDelta)parts.push(`通勤负担 ${signedText(delta.commuteBurdenDelta)}`);
  return parts.length?`${parts.join(" / ")}<br>`:"";
}

function addCard(cardId){
  if(planSelections.length>=monthlySelectLimit()){toast(`每月最多选择 ${monthlySelectLimit()} 张卡。`);return}
  if(planSelections.some(x=>x.cardId===cardId))return;
  const card=findCard(cardId);
  if(!card||card.lockedReason){toast(card?.lockedReason||"该卡不可选。");return}
  planSelections.push({cardId,freeCategory:"academic",customNote:""});
  refreshSelections=refreshSelections.filter(x=>x!==cardId);
  render();
}
function removeCard(index){planSelections.splice(index,1);render()}
function updateSelection(index,key,value){planSelections[index][key]=value;render()}
function setCategory(category){activeCategory=category;render()}
function setDeckFilter(filter){deckFilter=filter;render()}
function openCoreAttributeDetail(key){selectedCoreAttribute=key;setUi("CoreAttributeDetail")}
function toggleRefresh(cardId,checked){
  if(checked){
    if(refreshSelections.length>=monthlySwitchLimit()){toast(`每次最多刷新 ${monthlySwitchLimit()} 张卡。`);render();return}
    if(!refreshSelections.includes(cardId))refreshSelections.push(cardId);
  }else{
    refreshSelections=refreshSelections.filter(x=>x!==cardId);
  }
  render();
}
async function reserveCard(cardId){await api("/api/month/card/reserve",{cardId})}
async function cancelReserveCard(cardId){await api("/api/month/card/unreserve",{cardId})}
async function refreshCards(){const ids=[...refreshSelections];refreshSelections=[];await api("/api/month/card/refresh",{cardIds:ids})}
async function saveAiSettings(){
  const body={
    mode:aiMode.value,
    endpoint:aiEndpoint.value,
    model:aiModel.value,
    apiKey:aiApiKey.value,
    clearApiKey:aiClearKey.checked,
    timeoutSeconds:Number(aiTimeout.value||45)
  };
  const r=await fetch("/api/ai-settings",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});
  const value=await r.json();
  if(value.error)toast(value.error);
  else{aiSettings=value.aiSettings;toast("AI 设置已保存");render();}
}
function monthlyDrawCount(){return state.effectiveMonthlyDrawCount}
function monthlySelectLimit(){return state.monthlySelectLimit}
function monthlySwitchLimit(){return state.monthlySwitchLimit}
function syncAiEndpoint(){
  if(aiMode.value==="DeepSeek"&&(!aiEndpoint.value||aiEndpoint.value.includes("api.openai.com")))
    aiEndpoint.value="https://api.deepseek.com/chat/completions";
}
async function testAiSettings(){
  toast("正在测试 AI 连接...");
  const r=await fetch("/api/ai-settings/test",{method:"POST"});
  const value=await r.json();
  toast(value.error||value.message||"测试完成");
}
async function saveToSlot(slot){
  const info=saveSlots.find(x=>x.slot===slot);
  if(info?.exists&&!confirm(`Slot ${String(slot).padStart(2,"0")} 已有存档。确认覆盖？`))return;
  activeSaveSlot=slot;
  const value=await api("/api/save",{slot});
  if(!value.error)toast(`已保存到 Slot ${String(slot).padStart(2,"0")}`);
}
async function loadGame(slot){planSelections=[];refreshSelections=[];saveHudMode="";await api("/api/load",{slot});}
function findCard(cardId){return (state.currentMonthHand||[]).find(x=>x.cardId===cardId)}
function coreDeltaText(core){const entries=Object.entries(core||{});return entries.length?entries.map(([k,v])=>`${label(k)} ${signedText(v)}`).join(" / "):"无"}
function typeName(type){return {Action:"行动",Event:"事件",Relationship:"关系",Housing:"住房",Finance:"财务"}[type]||type}
function rarityName(rarity){return rarity||"Common"}
function characterName(id){return characters.find(x=>x.id===id)?.name||id||"未指定"}
function relationshipCard(rel){
  const profile=characters.find(x=>x.id===rel.characterId);
  const memories=(rel.memories||[]).slice().sort((a,b)=>b.month-a.month||b.importance-a.importance).slice(0,3);
  const memoryView=memories.length
    ? `<div class="relationship-memories"><span class="kicker">共同记忆</span>${memories.map(x=>`<p class="subtle">M${x.month} · ${escapeHtml(x.title)}</p>`).join("")}</div>`
    : `<p class="subtle">还没有形成明确的共同记忆。</p>`;
  return `<div class="relationship"><h3>${escapeHtml(rel.name)} <span class="tag">${stageName(rel.stage)}</span> <span class="tag">${moodName(rel.mood)}</span></h3><p class="subtle">${escapeHtml(profile?.role||"")} ${profile?.personalityTags?.length?`· ${profile.personalityTags.map(escapeHtml).join(" / ")}`:""}</p><p>${state.storedAiPayloads?.relationshipTexts?.[rel.characterId]||"你们之间的关系还没有形成清晰的语言。"}</p><p class="subtle">好感 ${rel.affection} · 信赖 ${rel.trust} · 已互动 ${rel.interactionCount||0} 次${rel.lastInteractionMonth?` · 上次互动 M${rel.lastInteractionMonth}`:""}</p>${memoryView}<button class="secondary" ${relationshipRemaining()<=0?"disabled":""} onclick="relAction('${rel.characterId}','chat')">主动闲聊</button> <button class="secondary" ${relationshipRemaining()<=0?"disabled":""} onclick="relAction('${rel.characterId}','support')">倾听近况</button></div>`;
}
function moodName(id){return {warm:"温暖",open:"放松",calm:"平静",neutral:"平常",guarded:"有所保留",upset:"低落"}[id]||id||"平常"}
function escapeAttr(text){return String(text).replaceAll("&","&amp;").replaceAll('"',"&quot;").replaceAll("<","&lt;")}
function escapeHtml(text){return String(text).replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;")}
function empty(text,target){return `<p class="quote">${text}</p><div class="actions">${navButton(target,"继续")}</div>`}
async function submitPlan(){
  if(planSubmitting)return;
  planSubmitting=true;
  render();
  await api("/api/month/plan",{selectedCards:planSelections});
  planSubmitting=false;
  render();
}
function selectOpportunity(id){api("/api/opportunity/select",{opportunityId:id})}
function skipOpportunity(){api("/api/opportunity/skip",{})}
function relAction(id,action){api("/api/relationship/action",{characterId:id,relationshipActionId:action})}
function nextMonth(){planSelections=[];refreshSelections=[];api("/api/month/next",{})}
load();
