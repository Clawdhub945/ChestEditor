// 盒子2 回归：实机数据 + 合成样本，跑 npcfixCombatClassify / 渲染 / 一键清除范围
// 用法: node _tools/test_box2_clear.js <entities.json>
//
// 为什么要掺合成样本：实机世界是会变的（怪物会被打光、船会开走），
// 只靠 live 数据会让「敌方-怪物」「船」这类盒子时有时无，断言没法稳定跑。
// 所以 live 负责"跟真实数据对得上"，合成样本负责"结构/按钮/文案一定被覆盖"。
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const LIVE = JSON.parse(fs.readFileSync(process.argv[2] || 'live_tmp.json', 'utf8'));

// ---- 合成样本：每种盒子各塞几条 ----
const mk = (o) => Object.assign(
  { goName: '', className: '', npcName: '', stuffNameWithIdIndex: '', soldierTypeId: 0,
    soldierTypeName: '', hometownKingdomId: 0, territoryKingdomId: 0, kingdomId: 0,
    ptrHash: 0, guid: 0, stuffId: 0, name: '', fieldCount: 100 }, o);
let ph = -900000, gd = 900000;
const nx = o => mk(Object.assign({ ptrHash: --ph, guid: ++gd }, o));
const SYNTH = [
  nx({ className: 'Npc', soldierTypeId: 202, soldierTypeName: '剑士', hometownKingdomId: 1, territoryKingdomId: 1 }),     // 我方战斗单位
  nx({ className: 'MonsterAntWorker', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 201385, name: '5级工蚁' }),   // 我方怪物
  nx({ className: 'MonsterDragon5Fire', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 201441, name: '1级火龙' }),  // 我方龙
  nx({ className: 'Npc', soldierTypeId: 202, soldierTypeName: '剑士', hometownKingdomId: 89, territoryKingdomId: 89 }),    // 敌方小人
  nx({ className: 'MonsterAntSoldier', hometownKingdomId: 89, territoryKingdomId: 89, stuffId: 201395, name: '5级兵蚁' }), // 敌方怪物
  nx({ className: 'Ship', kingdomId: 100, hometownKingdomId: 100, territoryKingdomId: 100, stuffId: 706002, name: '战舰' }),// 敌方战舰
  nx({ className: 'Ship', kingdomId: 1, hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 706002, name: '战舰' }),      // 我方战舰
  nx({ className: 'Ship', kingdomId: 100, hometownKingdomId: 100, territoryKingdomId: 100, stuffId: 706001, name: '货船' }),// 商船：不该出现
  nx({ className: 'Npc', soldierTypeId: 202, soldierTypeName: '剑士', stuffId: 0 }),                                       // 阵营0：不该出现
  nx({ className: 'FacilityWall', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 102006, name: '铁墙', stuffNameWithIdIndex: '铁墙1' }),            // 我方建筑
  nx({ className: 'FacilityStorageBarn', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 103002, name: '大箱子', stuffNameWithIdIndex: '大箱子1' }), // 我方建筑·箱子（高危）
  nx({ className: 'FacilityCityWall', hometownKingdomId: 102, territoryKingdomId: 102, stuffId: 102008, name: '城墙', stuffNameWithIdIndex: '城墙1' }),    // 敌方建筑
  nx({ className: 'StuffOnMap', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 304001, name: '白银', stuffNameWithIdIndex: '白银1' }),             // 掉落物（结构断言的稳定锚点，实机会被拾光）
  nx({ className: 'Animal', hometownKingdomId: 1, territoryKingdomId: 1, stuffId: 501005, name: '猪', goName: '猪13201139' }),                            // 动物（领地牲畜）
  nx({ className: 'AnimalDeadBody', stuffId: 501006, name: '猪' }),                                                                                        // 动物尸体
];
const DATA = LIVE.concat(SYNTH);

// ---- 最小 DOM / 环境 mock ----
function mkEl() {
  return {
    _h: '', value: '', checked: false, open: false, dataset: {}, style: {}, options: [], selectedIndex: -1,
    classList: { add() {}, remove() {}, toggle() {} },
    get innerHTML() { return this._h; }, set innerHTML(v) { this._h = v; },
    querySelector: () => null, querySelectorAll: () => [],
    closest: () => null, getAttribute: () => null, setAttribute() {},
    appendChild() {}, addEventListener() {}, remove() {},
    set textContent(v) { this._t = v; }, get textContent() { return this._t; },
  };
}
const els = {};
// fetch 桩：按 URL 分发（/state 由测试用例随时改写）
let stateResp = { inSave: true, loading: false, saveLoads: 5 };
const lsStore = {};
const ctx = {
  console, window: {},
  document: { body: mkEl(), getElementById: id => (els[id] = els[id] || mkEl()),
    querySelector: () => null, querySelectorAll: () => [], createElement: mkEl, addEventListener() {} },
  localStorage: {
    getItem: k => (k in lsStore ? lsStore[k] : null),
    setItem: (k, v) => { lsStore[k] = String(v); }, removeItem: k => { delete lsStore[k]; },
  },
  // 分发：state / destroy 带游戏状态，entities 返回空数组，animals 返回动物列表
  fetch: url => {
    const u = String(url);
    let body = {};
    if (u.indexOf('/api/editor/state') >= 0) body = stateResp;
    else if (u.indexOf('/api/editor/destroy') >= 0)
      body = { ok: true, destroyed: 0, failed: 0, inSave: stateResp.inSave, loading: stateResp.loading, saveLoads: stateResp.saveLoads };
    else if (u.indexOf('/api/editor/entities') >= 0) body = [];
    else if (u.indexOf('/api/editor/animals') >= 0)
      body = { animals: [{ id: 501001, name: '鸡' }, { id: 501002, name: '羊' }, { id: 501003, name: '牛' },
        { id: 501004, name: '马' }, { id: 501005, name: '猪' }, { id: 501006, name: '驯狼' }] };
    return Promise.resolve({ json: () => Promise.resolve(body) });
  },
  setTimeout, clearTimeout, Date, JSON, Math, Number, String, Object, Array, Promise, Set, Map,
};
ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);

// ---- 载入除 main.js 外的全部前端模块 ----
const dir = path.join('Web', 'Static', 'js');
for (const f of fs.readdirSync(dir).filter(f => f.endsWith('.js') && f !== 'main.js')) {
  try { vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), ctx, { filename: f }); }
  catch (e) { console.log('  [skip]', f, e.message.slice(0, 80)); }
}

// ⚠ 顶层 const/let 不会挂到 context 上（词法绑定），必须回 vm 里求值
const ev = expr => vm.runInContext(expr, ctx);
const setData = d => vm.runInContext('entityEditorData = ' + JSON.stringify(d) + ';', ctx);
setData(DATA);

const KINDS = ev('NPCFIX_CLEAR_KINDS');
const sumKinds = ctx.npcfixSumKinds;
const kidOf = e => ctx.npcfixUnitKingdom(e);
const classify = ctx.npcfixCombatClassify(DATA);

let fails = 0;
function ok(cond, msg) { console.log((cond ? '  ok  ' : '  FAIL') + '  ' + msg); if (!cond) fails++; }

// 与 npcfixClear 里的 inScope 保持同一语义（enemy 同时排除 1 和 0）
function scopeFilter(kind, scope, data) {
  const def = KINDS[kind];
  return (data || DATA).filter(e => {
    if (!def.test(e)) return false;
    const kid = kidOf(e);
    if (scope === 'all') return true;
    if (scope === 'enemy') return kid !== 1 && kid !== 0;
    if (scope === 'ours') return kid === 1;
    return kid === Number(scope);
  });
}

console.log('== 数据规模 ==');
console.log('  实机', LIVE.length, '条 | 合成', SYNTH.length, '条 | 合计', DATA.length);

// ---------- 1) 阵营0 必须隐藏 ----------
console.log('\n== 1) 阵营0 隐藏 ==');
const zeroUnits = LIVE.filter(e => kidOf(e) === 0 && ctx.npcfixIsAnyUnit(e));
console.log('  实机阵营0 战斗单位:', zeroUnits.length);
const boxed = [
  ...classify.ours, ...classify.monstersOurs, ...classify.shipsOurs,
  ...Object.values(classify.humanoids).flat(),
  ...Object.values(classify.monstersEnemy).flat(),
  ...Object.values(classify.shipsEnemy).flat(),
];
ok(boxed.every(e => e && typeof e === 'object' && e.className !== undefined), '每个盒子元素都是实体（无嵌套数组）');
ok(!boxed.some(e => kidOf(e) === 0), '任何盒子里都没有阵营0 单位');
ok(!('0' in classify.humanoids) && !(0 in classify.monstersEnemy) && !(0 in classify.shipsEnemy),
  '阵营0 分组键没有生成');

// ---------- 2) 我方-怪物：龙合并 + 绿色 ----------
console.log('\n== 2) 我方-怪物 分组 ==');
const ours = classify.monstersOurs;
const kinds = {};
for (const e of ours) {
  const cn = e.className || '';
  const k = cn.indexOf('Dragon') >= 0 ? '龙' : (e.name || cn || '未知怪物');
  (kinds[k] = kinds[k] || []).push(e);
}
const dragons = ours.filter(e => (e.className || '').indexOf('Dragon') >= 0);
console.log('  我方怪物', ours.length, '| 分组数', Object.keys(kinds).length);
console.log('  分组:', Object.keys(kinds).sort((a, b) => kinds[b].length - kinds[a].length)
  .map(k => k + '(' + kinds[k].length + ')').join(' '));
console.log('  龙（合并前会各占一张卡）:', dragons.map(e => e.name || e.className).join(' / ') || '(无)');
ok(Object.values(kinds).reduce((a, l) => a + l.length, 0) === ours.length, '分组守恒 == 我方怪物总数');
if (dragons.length > 0) {
  ok(!!kinds['龙'] && kinds['龙'].length === dragons.length, '所有龙只占「龙」这 1 张卡');
  ok(new Set(dragons.map(e => e.name)).size >= 1, '龙原本有 ' + new Set(dragons.map(e => e.name)).size + ' 个不同名字');
}
ok(!Object.keys(kinds).some(k => /^Monster/.test(k)), '分组名里没有英文类名残留');

// ---------- 3) 一键清除范围 vs 分类桶 ----------
console.log('\n== 3) 一键清除范围 vs 分类桶 ==');
ok(!!KINDS.enemyAll, 'enemyAll（毁灭吧）已定义');
ok(scopeFilter('humanoid', 'enemy').length === sumKinds(classify.humanoids),
  'humanoid:enemy (' + scopeFilter('humanoid', 'enemy').length + ') == 敌方-小人盒 (' + sumKinds(classify.humanoids) + ')');
ok(scopeFilter('monster', 'enemy').length === sumKinds(classify.monstersEnemy),
  'monster:enemy (' + scopeFilter('monster', 'enemy').length + ') == 敌方-怪物盒 (' + sumKinds(classify.monstersEnemy) + ')');
ok(scopeFilter('monster', 1).length === classify.monstersOurs.length, 'monster:1 == 我方-怪物盒');
ok(scopeFilter('ship', 1).length === classify.shipsOurs.length, 'ship:1 == 我方舰队');
ok(scopeFilter('ship', 'enemy').length === sumKinds(classify.shipsEnemy), 'ship:enemy == 敌方舰队盒');
ok(scopeFilter('ship', 'all').length === classify.shipsOurs.length + sumKinds(classify.shipsEnemy),
  'ship:all == 我方 + 敌方全部舰队（货船不计）');

let bad = 0;
for (const key of ['humanoids', 'monstersEnemy', 'shipsEnemy']) {
  const kind = key === 'humanoids' ? 'humanoid' : (key === 'monstersEnemy' ? 'monster' : 'ship');
  for (const kid of Object.keys(classify[key]))
    if (scopeFilter(kind, Number(kid)).length !== classify[key][kid].length) bad++;
}
ok(bad === 0, '各阵营二级分组逐一吻合');

const enemySum = sumKinds(classify.humanoids) + sumKinds(classify.monstersEnemy) + sumKinds(classify.shipsEnemy);
ok(scopeFilter('enemyAll', 'enemy').length === enemySum,
  'enemyAll:enemy (' + scopeFilter('enemyAll', 'enemy').length + ') == 敌方单位总数 (' + enemySum + ')');
ok(!scopeFilter('enemyAll', 'enemy').some(e => kidOf(e) === 1 || kidOf(e) === 0),
  '毁灭吧 只含有效敌方阵营（不含我方 1 / 阵营0）');
ok(scopeFilter('ship', 'all').every(e => e.stuffId !== 706001), '货船不在清除范围内');

// ---------- 4) 边界：不一键清除到自己人 ----------
console.log('\n== 4) 边界 ==');
const oursNpc = LIVE.filter(e => (e.className || '').indexOf('Npc') === 0 && e.className !== 'NpcBody'
  && (e.hometownKingdomId || 0) === 1);
console.log('  实例我方 Npc 实体:', oursNpc.length);
ok(!scopeFilter('humanoid', 'enemy').some(e => kidOf(e) === 1), 'humanoid:enemy 里没有阵营1');
ok(!scopeFilter('monster', 'enemy').some(e => kidOf(e) === 1), 'monster:enemy 里没有阵营1');

// ---------- 5) renderNpcfixBox2 结构 ----------
console.log('\n== 5) renderNpcfixBox2 产出 ==');
const src = fs.readFileSync(path.join(dir, 'npcfix.js'), 'utf8');
(async () => {
  await ctx.renderNpcfixBox2();
  const H = els['npcfixBox2Body'].innerHTML;
  const segment = label => {
    const i = H.indexOf(label);
    return i < 0 ? null : H.slice(i, H.indexOf('</summary>', i));
  };
  for (const label of ['我方战斗单位', '我方-怪物', '我方舰队', '敌方-小人', '敌方-怪物', '敌方舰队']) {
    const seg = segment(label);
    if (seg === null) { ok(false, '缺少盒子 ' + label); continue; }
    console.log('  ' + label + ' 标题栏: ' + seg.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 95));
  }

  const iOurs = H.indexOf('>我方单位<');
  const iEnemy = H.indexOf('>敌方单位<');
  const iOursMon = H.indexOf('我方-怪物 (');
  const iOursFleet = H.indexOf('我方舰队 (');
  const iEnemyMan = H.indexOf('敌方-小人 (');
  console.log('  下标: 我方单位=' + iOurs + ' 我方-怪物=' + iOursMon + ' 我方舰队=' + iOursFleet
    + ' 敌方单位=' + iEnemy + ' 敌方-小人=' + iEnemyMan);
  ok(iOurs >= 0 && iEnemy >= 0, '「我方单位」「敌方单位」两条分区标题都在');
  ok(iOurs < iOursMon && iOursMon < iEnemy, '我方-怪物 已上移到「我方单位」区');
  ok(iOursFleet > iOurs && iOursFleet < iEnemy, '我方舰队 在「我方单位」区（已与敌方舰队分开）');
  ok(iEnemy < iEnemyMan, '「敌方单位」在敌方小人之前（已隔开）');
  ok(/毁灭吧！！！/.test(H), '「敌方单位」后面有「毁灭吧！！！」');
  ok(/npcfixClear\('enemyAll:enemy'\)/.test(H), '毁灭吧 的 spec = enemyAll:enemy');

  const monSeg = H.slice(iOursMon - 200, H.indexOf('</summary>', iOursMon));
  ok(/#27ae60/.test(monSeg), '我方-怪物 盒子是绿色');
  ok(!/e67e22|--warning/.test(monSeg), '我方-怪物 盒子里没有残留橙色');

  // 一级菜单按钮
  for (const [label, spec] of [['敌方-小人', "npcfixClear('humanoid:enemy')"],
    ['敌方-怪物', "npcfixClear('monster:enemy')"], ['敌方舰队', "npcfixClear('ship:enemy')"]]) {
    const seg = segment(label);
    ok(seg && seg.indexOf(spec) >= 0, label + ' 一级菜单有一键清除');
  }
  ok(segment('敌方-怪物').indexOf("npcfixClear('monster:") >= 0, '敌方-怪物 二级菜单有一键清除');
  ok(/npcfixKillMonsters/.test(H) === false, '没有旧函数残留');
  ok(!/MonsterDragon/.test(H), '我方怪物分组里没有英文类名（龙已合并）');

  // ---------- 6) 确认框文案 ----------
  console.log('\n== 6) 确认框文案（用合成大数据保证确定性） ==');
  let captured = null;
  ctx.confirm = m => { captured = m; return false; };   // 一律取消，不真的销毁
  const many = n => Array.from({ length: n }, (_, i) => mk({
    className: 'MonsterAntWorker', hometownKingdomId: 89, territoryKingdomId: 89,
    stuffId: 201385, name: '5级工蚁', ptrHash: -920000 - i, guid: 920000 + i }));

  setData(many(150));
  await ctx.npcfixClear('monster:enemy');
  console.log('  [150] ' + String(captured).replace(/\n/g, ' ⏎ '));
  ok(/确定清除敌方全部阵营的 150 个怪物/.test(captured), '文案含范围/数量/种类');
  ok(!/卡顿/.test(captured), '文案里已不再出现"卡顿"提示');

  setData(many(80));
  await ctx.npcfixClear('monster:89');
  console.log('  [80 ] ' + String(captured).replace(/\n/g, ' ⏎ '));
  ok(/确定清除阵营89的 80 个怪物/.test(captured), '二级分组文案按阵营号描述');
  ok(!/卡顿/.test(captured), '少量时同样没有"卡顿"提示');
  ok(!/数量过多/.test(src), '源码里已彻底删掉"数量过多"文案');

  // 战斗力缩放同样不该带"卡顿"提示
  await ctx.npcfixScale('monster:89', 0.1);
  console.log('  [缩放] ' + String(captured).replace(/\n/g, ' ⏎ '));
  ok(/战斗力 ÷10/.test(captured) && !/卡顿/.test(captured), '缩放确认框也无"卡顿"提示');

  // ---------- 7) 舰船落岸船员：二次清除 ----------
  console.log('\n== 7) 舰船落岸船员的二次清除 ==');
  const clearSrc = src.slice(src.indexOf('async function npcfixClear'));
  ok(/kindName === 'ship' \|\| kindName === 'enemyAll'/.test(clearSrc), 'npcfixClear 里有"清过船"的判断');
  ok(/!before\.has\(e\.ptrHash\)/.test(clearSrc), '靠"动刀前名单"识别新刷出来的士兵');
  ok(/npcfixIsHumanEntity\(e\)/.test(clearSrc), '只是对敌方小人做二次清除（不误伤别的）');
  ok(/before\.has/.test(clearSrc) && /const before = new Set/.test(clearSrc), '动刀前就记住了 before 集合（顺序正确）');
  ok(clearSrc.indexOf('const before = new Set') < clearSrc.indexOf('const d1 = await kill(hashes)'),
    'before 快照在第一轮销毁之前（kill 按 keyField 路由）');

  // ---------- 8) 战斗力 ×10 / ÷10 按钮 ----------
  console.log('\n== 8) 战斗力缩放按钮 ==');
  ok(/npcfixScale\('oursAll:ours',10\)/.test(H), '「我方单位」分区线: 战斗力×10');
  ok(/npcfixScale\('enemyAll:enemy',0\.1\)/.test(H), '「敌方单位」分区线: 战斗力÷10');
  ok(/npcfixScale\('ourCombat:1',10\)/.test(H), '我方战斗单位 一级: 战斗力×10');
  ok(/npcfixScale\('monster:1',10\)/.test(H), '我方-怪物 一级: 战斗力×10');
  ok(/npcfixScale\('humanoid:enemy',0\.1\)/.test(H), '敌方-小人 一级: 战斗力÷10');
  ok(/npcfixScale\('monster:enemy',0\.1\)/.test(H), '敌方-怪物 一级: 战斗力÷10');
  ok(/npcfixScale\('ship:1',10\)/.test(H), '我方舰队 一级: 战斗力×10');
  ok(/npcfixScale\('ship:enemy',0\.1\)/.test(H), '敌方舰队 一级: 战斗力÷10');
  ok(/npcfixScale\('ship:100',0\.1\)/.test(H), '敌方舰队 二级: 战斗力÷10');
  ok(/npcfixScale\('humanoid:89',0\.1\)/.test(H), '敌方-小人 二级: 战斗力÷10');
  ok(/npcfixScale\('ourCombat:1:g=/.test(H), '我方战斗单位 二级带 :g=兵种');
  ok(/npcfixScale\('monster:1:g=/.test(H), '我方-怪物 二级带 :g=种类');
  ok(/npcfixClear\('monster:1:g=/.test(H), '我方-怪物 二级清除也带 :g=（只清本组，不再清全部）');

  // 战斗力缩放用的是新后端接口
  ok(/\/api\/editor\/scale\/batch/.test(src), '前端调 /api/editor/scale/batch');

  // ---------- 9) 定期清理（独立盒子 + 每秒数量可调 + 各种判断） ----------
  console.log('\n== 9) 定期清理 ==');
  ok(/npcfixSafeBox\(\)/.test(src), '定期清理渲染成独立一行盒子（npcfixSafeBox）');
  ok(/安全发展模式/.test(H) === false && /定期清理/.test(H), '已改名为「定期清理」');
  const iSafeBox = H.indexOf('定期清理');
  console.log('  下标: 敌方单位=' + iEnemy + ' 定期清理=' + iSafeBox + ' 敌方-小人=' + iEnemyMan);
  ok(iSafeBox > iEnemy && iSafeBox < iEnemyMan, '定期清理盒在「敌方单位」横线之下、敌方-小人之上');
  ok(H.indexOf('npcfixSafeBatchChange(this)') >= 0, '有"每秒清除 N 个"的数字输入框');
  ok(H.indexOf('npcfixSafeToggle()') >= 0, '有开/关按钮');
  ok(ev('NPCFIX_SAFE_INTERVAL') === 1000, '节奏固定 1 秒一拍（时间不可改）');
  console.log('  默认每秒', ev('NPCFIX_SAFE_BATCH_DEFAULT'), '个 / 上限', ev('NPCFIX_SAFE_BATCH_MAX'),
    '个 / 间隔', ev('NPCFIX_SAFE_INTERVAL') + 'ms / 每', ev('NPCFIX_SAFE_RESCAN_EVERY'), '拍重扫');
  ok(ev('NPCFIX_SAFE_BATCH_DEFAULT') === 20 && ev('NPCFIX_SAFE_BATCH_MAX') === 100,
    '默认 20 / 最大 100');

  // 数量输入的夹取
  ctx.npcfixSafeBatchChange({ value: '55' });
  ok(ctx.npcfixSafeGetBatch() === 55, '输入 55 → 生效 55（localStorage 记忆）');
  ctx.npcfixSafeBatchChange({ value: '500' });
  ok(ctx.npcfixSafeGetBatch() === 100, '输入 500 → 夹到上限 100');
  ctx.npcfixSafeBatchChange({ value: '0' });
  ok(ctx.npcfixSafeGetBatch() === 1, '输入 0 → 夹到下限 1');
  ctx.npcfixSafeBatchChange({ value: 'abc' });
  ok(ctx.npcfixSafeGetBatch() === 20, '输入非法 → 回到默认 20');

  // 开关 + 各种判断
  ok(ctx.npcfixSafeStateText() === '关', '初始状态为「关」');
  let toasts = [];
  ctx.toast = (m, isErr) => { toasts.push(m); };
  stateResp = { inSave: false, loading: false, saveLoads: 0 };      // 没进存档
  await ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === false, '未进存档 → 开启失败（保持关闭）');
  ok(toasts.some(t => t.indexOf('开启失败') === 0), '弹出「开启失败」');

  stateResp = { inSave: true, loading: true, saveLoads: 0 };        // 正在读档
  await ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === false, '正在读档 → 开启失败');

  stateResp = { inSave: true, loading: false, saveLoads: 3, scanning: false };
  await ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === true, '进存档后可正常开启');
  ok(toasts.some(t => t.indexOf('每秒清除 20') >= 0), '开启提示带每秒数量');
  ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === false, '再点一次关闭');

  // 读档后自动关闭
  stateResp = { inSave: true, loading: false, saveLoads: 3, scanning: false };
  await ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === true, '再次开启（saveLoads=3）');
  stateResp = { inSave: true, loading: false, saveLoads: 4, scanning: false };  // 读了一次档
  await ctx.npcfixSafeTick();
  ok(ev('npcfixSafeOn') === false, '读档后自动关闭');
  ok(toasts.some(t => t.indexOf('读取存档') >= 0), '提示「读取存档，已自动关闭」');

  // 退出存档后自动关闭
  stateResp = { inSave: true, loading: false, saveLoads: 4, scanning: false };
  await ctx.npcfixSafeToggle();
  ok(ev('npcfixSafeOn') === true, '再次开启');
  stateResp = { inSave: false, loading: false, saveLoads: 4, scanning: false }; // 退出了
  await ctx.npcfixSafeTick();
  ok(ev('npcfixSafeOn') === false, '退出存档 → 自动关闭');

  const safeSrc = src.slice(src.indexOf('async function npcfixSafeTick'));
  ok(/slice\(0, npcfixSafeGetBatch\(\)\)/.test(safeSrc), '每批数量来自可调输入');
  ok(/k !== 1 && k !== 0/.test(safeSrc), '只清敌方（不含我方 1 / 阵营0）');
  ok(/npcfixScanning/.test(safeSrc), '重扫进行中时跳过这一拍（避免大批 not found）');
  ok(/npcfixSafeCheckState\(/.test(safeSrc), '每一拍都会核对游戏状态（读档/退出自动关闭）');
  ok(/async function npcfixScan\(\)/.test(src) && /npcfixScanning = true/.test(src),
    'npcfixScan() 会给重扫打标记');
  console.log('  NPCFIX_KILL_CHUNK =', ev('NPCFIX_KILL_CHUNK'), '（后端按帧摊开，批次可以更大）');

  // ---------- 10) 分区线常驻（没有单位也要在） ----------
  console.log('\n== 10) 分区线常驻 ==');
  setData([]);
  await ctx.renderNpcfixBox2();
  const H2 = els['npcfixBox2Body'].innerHTML;
  ok(H2.indexOf('>我方单位<') >= 0, '空数据时「我方单位」线仍在');
  ok(H2.indexOf('>敌方单位<') >= 0, '空数据时「敌方单位」线仍在');
  ok(H2.indexOf('暂无我方单位') >= 0 && H2.indexOf('暂无敌方单位') >= 0, '空数据时有占位提示');
  ok(/npcfixScale\('oursAll:ours',10\)/.test(H2) && /npcfixScale\('enemyAll:enemy',0\.1\)/.test(H2),
    '空数据时分区按钮仍在');

  // ---------- 11) 侧边栏：旧面板已隐藏 ----------
  console.log('\n== 11) 侧边栏 ==');
  await ctx.renderSidebar();
  const sb = els['chestList'].innerHTML;
  ok(sb.indexOf('>NPC<') < 0, '侧边栏已隐藏「NPC」分类');
  ok(sb.indexOf('实体扫描') < 0, '侧边栏已隐藏「实体扫描」分类');
  ok(sb.indexOf('NPC修改') >= 0 && sb.indexOf('盒子1 小人数值修改') >= 0, '「NPC修改」分类仍在（盒子1/盒子2）');
  ok(sb.indexOf('>容器<') >= 0 && sb.indexOf('>驯龙<') >= 0 && sb.indexOf('>科技树<') >= 0,
    '容器 / 驯龙 / 科技树 不受影响');
  ok(ev('HIDE_LEGACY_PANELS') === true, 'HIDE_LEGACY_PANELS = true（想恢复改 false）');

  // ---------- 12) 盒子3 建筑物 ----------
  console.log('\n== 12) 盒子3 建筑物 ==');
  // 判据：StartsWith('Facility')，不是 Contains（实体分类的历史坑）
  ok(ev('npcfixIsFacilityEntity({className:"FacilityWall"})') === true, 'FacilityWall 命中');
  ok(ev('npcfixIsFacilityEntity({className:"MyFacility"})') === false, '类名 Facility 不在开头 → 不命中（StartsWith 而非 Contains）');
  ok(ev('npcfixIsFacilityEntity({className:"Npc"})') === false, 'Npc 不命中');

  // 实机守恒：全部设施都能被分类（我方 / 敌方 / 阵营0）
  const liveFac = LIVE.filter(e => ctx.npcfixIsFacilityEntity(e));
  const liveFacOurs = liveFac.filter(e => kidOf(e) === 1).length;
  const liveFacEnemy = liveFac.filter(e => kidOf(e) !== 1 && kidOf(e) !== 0).length;
  const liveFacZero = liveFac.filter(e => kidOf(e) === 0).length;
  console.log('  实机设施', liveFac.length, '= 我方', liveFacOurs, '+ 敌方', liveFacEnemy, '(阵营0:', liveFacZero, ')');
  ok(liveFacOurs + liveFacEnemy + liveFacZero === liveFac.length, '实机设施守恒（我方+敌方+阵营0=全部）');

  // 渲染：分区线 / 中文分组 / 按钮 spec / 防误触
  setData(DATA);
  await ctx.renderNpcfixBox3(false);
  const H3 = els['npcfixBox3Body'].innerHTML;
  ok(H3.indexOf('>我方建筑<') >= 0 && H3.indexOf('>敌方建筑<') >= 0, '两条分区线都在');
  ok(H3.indexOf('>敌方建筑<') < H3.indexOf('城墙'), '敌方建筑分区线在敌方分组之前');
  ok(H3.indexOf('铁墙') >= 0 && H3.indexOf('大箱子') >= 0, '我方建筑按中文名分组（铁墙 / 大箱子）');
  ok(H3.indexOf('城墙') >= 0, '敌方建筑有「城墙」组');
  ok(H3.indexOf("npcfixClear('facility:ours:g=%E9%93%81%E5%A2%99')") >= 0, '铁墙组按钮 spec = facility:ours:g=铁墙');
  ok(H3.indexOf("npcfixClear('facility:enemy')") >= 0, '敌方建筑分区有「一键清除」（facility:enemy）');
  ok(!/npcfixClear\('facility:ours'\)/.test(H3), '我方建筑分区没有"一键全删"按钮（防误触）');
  ok(/data-g="铁墙"/.test(H3), '组卡带 data-g（搜索重绘后恢复展开状态用）');

  // 确认框：tail（拆除真实风险）+ warn（箱子/床高危）
  let capC = null;
  ctx.confirm = m => { capC = m; return false; };
  await ctx.npcfixClear('facility:ours:g=铁墙');
  console.log('  [清除铁墙] ' + String(capC).replace(/\n/g, ' ⏎ '));
  ok(/不返还材料与物品/.test(capC), '确认框提示不返还材料（手动拆除流程）');
  ok(/箱子/.test(capC) && /床/.test(capC), '确认框有箱子/床高危提示');
  ok(!/覆盖分裂怪/.test(capC), '建筑确认框不再写"覆盖分裂怪"');

  // 限量渲染：独立小数据集（31 面铁墙）→ 只渲染 24 张卡 + "还有 7 个"提示
  // ⚠ 不能用 DATA.concat：实机里本来就有 1574 面铁墙，数量会跟着实机变
  const manyWalls = Array.from({ length: 30 }, (_, i) => mk({
    className: 'FacilityWall', hometownKingdomId: 1, territoryKingdomId: 1,
    stuffId: 102006, name: '铁墙', stuffNameWithIdIndex: '铁墙X' + (i + 1), ptrHash: -930000 - i, guid: 930000 + i }));
  setData(manyWalls.concat(SYNTH));
  await ctx.npcfixBox3RenderBody();
  const H3b = els['npcfixBox3Body'].innerHTML;
  ok(/还有 7 个未显示/.test(H3b), '31 面铁墙只渲染 24 张卡（提示还有 7 个）');
  ok(ev('NPCFIX_BOX3_CARD_LIMIT') === 24, '每组限量 = 24');

  // 搜索过滤：搜「箱」→ 只留大箱子组（回到完整数据集）
  setData(DATA);

  // 搜索过滤：搜「箱」→ 只留大箱子组，铁墙/城墙组不显示
  ev('npcfixBox3Query = "箱"');
  await ctx.npcfixBox3RenderBody();
  const H3c = els['npcfixBox3Body'].innerHTML;
  ok(H3c.indexOf('大箱子') >= 0, '搜「箱」时大箱子组显示');
  ok(H3c.indexOf('铁墙') < 0, '搜「箱」时铁墙组隐藏');
  ok(H3c.indexOf('城墙') < 0, '搜「箱」时城墙组隐藏');
  ev('npcfixBox3Query = ""');
  await ctx.npcfixBox3RenderBody();
  ok(els['npcfixBox3Body'].innerHTML.indexOf('铁墙') >= 0, '清空搜索词后恢复全部分组');

  // ---------- 13) 盒子5 掉落物 ----------
  console.log('\n== 13) 盒子5 掉落物 ==');
  // 判据：StuffOnMap*（含 StuffOnMapFaeces 粪便），StartsWith 不是 Contains
  ok(ev('npcfixIsStuffOnMapEntity({className:"StuffOnMap"})') === true, 'StuffOnMap 命中');
  ok(ev('npcfixIsStuffOnMapEntity({className:"StuffOnMapFaeces"})') === true, 'StuffOnMapFaeces（粪便）命中');
  ok(ev('npcfixIsStuffOnMapEntity({className:"MyStuffOnMap"})') === false, '前缀不符 → 不命中');

  // 实机守恒：全部 StuffOnMap* 都能分组（物品无阵营语义，全 0 的粪便也照常显示）
  const liveStuff = LIVE.filter(e => ctx.npcfixIsStuffOnMapEntity(e));
  const liveZeroStuff = liveStuff.filter(e => kidOf(e) === 0).length;
  console.log('  实机掉落物', liveStuff.length, '堆（其中阵营字段全0', liveZeroStuff, '堆；实机会被拾光，结构断言靠 SYNTH 的白银，不作实机状态断言）');

  setData(DATA);
  await ctx.renderNpcfixBox5(false);
  const H5 = els['npcfixBox5Body'].innerHTML;
  ok(H5.indexOf('白银') >= 0 || H5.indexOf('银币') >= 0 || liveStuff.length === 0,
    '按物品中文名分组（白银/银币…，实机没有时跳过）');
  ok(H5.indexOf("npcfixClear('stuff:all:g=") >= 0, '组级按钮 spec = stuff:all:g=<物品名>');
  ok(els['content'].innerHTML.indexOf("npcfixClear('stuff:all')") >= 0, '标题栏有「全部清除」（stuff:all）');
  ok(/还有 \d+ 个未显示/.test(H5) === (liveStuff.length + 1 > 24) || liveStuff.length === 0,
    '大组限量渲染（超出 24 显示"还有 N 个"）');
  ok(!/阵营0/.test(H5), '没有「阵营0」分组键（掉落物不做阵营0隐藏）');

  // keyField='guid'：pick 收集的是 guid 不是 ptrHash
  const guidPick = ev(`npcfixParseSpec('stuff:all:g=白银') ? 1 : 0`) === 1 || !DATA.some(e => e.name === '白银')
    ? true : true;   // spec 合法性
  ok(ev('NPCFIX_CLEAR_KINDS.stuff.keyField') === 'guid', 'stuff 的 keyField = guid');
  const guidsPicked = vm.runInContext(
    'npcfixParseSpec("stuff:all").pick().slice(0,3)', ctx);
  const allAreGuids = guidsPicked.every(v => DATA.some(e => e.guid === v));
  ok(allAreGuids, 'pick 返回的是 guid（与实体表的 guid 字段对得上）');

  // 确认框：where='地图上全部' + tail（不进背包）
  let cap5 = null;
  ctx.confirm = m => { cap5 = m; return false; };
  await ctx.npcfixClear('stuff:all');
  console.log('  [清除掉落物] ' + String(cap5).replace(/\n/g, ' ⏎ '));
  ok(/确定清除地图上全部的 \d+ 个掉落物/.test(cap5), 'where 用「地图上全部」（不是"全部阵营"）');
  ok(/不会进背包/.test(cap5), '确认框写明物品直接消失、不进背包');

  // kill 路由：走 /api/editor/stuff/batch（不走 destroy/batch）
  const calls = [];
  const origFetch = ctx.fetch;
  ctx.fetch = (u, o) => { calls.push(String(u)); return origFetch(u, o); };
  ctx.confirm = () => true;
  setData(DATA.filter(e => e.className === 'StuffOnMap').slice(0, 5));
  await ctx.npcfixClear('stuff:all');
  ctx.fetch = origFetch;
  ok(calls.some(u => u.indexOf('/api/editor/stuff/batch') >= 0), '销毁请求走 /api/editor/stuff/batch');
  ok(!calls.some(u => u.indexOf('/api/editor/destroy/batch') >= 0), '不走 destroy/batch（ptrHash 只销 GO 不清注册表）');

  // 拾取进国库：按钮落位 + 接口路由
  setData(DATA);
  await ctx.renderNpcfixBox5(false);
  const H5p = els['npcfixBox5Body'].innerHTML;
  ok(H5p.indexOf("npcfixPickup('stuff:all:g=") >= 0, '组级有「拾取」按钮（stuff:all:g=<物品名>）');
  ok(els['content'].innerHTML.indexOf("npcfixPickup('stuff:all')") >= 0, '标题栏有「拾取进国库」（stuff:all）');
  let cap6 = null;
  ctx.confirm = m => { cap6 = m; return true; };   // 同意执行
  const calls2 = [];
  ctx.fetch = (u, o) => { calls2.push(String(u)); return origFetch(u, o); };
  await ctx.npcfixPickup('stuff:all');
  ctx.fetch = origFetch;
  console.log('  [拾取确认] ' + String(cap6).replace(/\n/g, ' ⏎ '));
  ok(/拾取进「/.test(cap6) && /\d+ 堆/.test(cap6), '拾取确认框含数量与目标容器');
  ok(calls2.some(u => u.indexOf('/api/editor/stuff/pickup') >= 0), '拾取请求走 /api/editor/stuff/pickup');
  ok(!calls2.some(u => u.indexOf('/api/editor/destroy/batch') >= 0) && !calls2.some(u => u.indexOf('/api/editor/stuff/batch') >= 0),
    '拾取不走销毁接口');

  // ---------- 14) 自动拾取 ----------
  console.log('\n== 14) 自动拾取 ==');
  stateResp = { inSave: true, loading: false, saveLoads: 5 };   // 9) 组测过"读档自动关"，把状态还原
  ok(ev('NPCFIX_AUTO_PICK_MIN') === 5 && ev('NPCFIX_AUTO_PICK_MAX') === 60, '间隔范围 5~60 秒');
  // 间隔夹取：4→5 / 61→60 / 非法→10 / 正常保留
  const clamp = v => vm.runInContext(`(function(){ var el={value:'${v}'}; npcfixAutoPickIntervalChange(el); return el.value; })()`, ctx);
  console.log('  夹取: 4→' + clamp(4), '61→' + clamp(61), 'abc→' + clamp('abc'), '30→' + clamp(30));
  ok(clamp(4) === 5 && clamp(61) === 60 && clamp('abc') === 10 && clamp(30) === 30, '间隔夹取 4→5 / 61→60 / 非法→10 / 30 不变');
  // 目标下拉：常用 optgroup + 更多容器 optgroup（动态）
  setData(DATA);
  const selHtml = ev('npcfixAutoPickTargetSelectHtml()');
  ok(selHtml.indexOf('optgroup label="常用"') >= 0, '下拉有「常用」分组');
  ok(selHtml.indexOf('optgroup label="更多容器"') >= 0, '下拉有「更多容器」分组（动态生成）');
  ok(selHtml.indexOf('value="treasury"') >= 0 && selHtml.indexOf('value="106005"') >= 0
    && selHtml.indexOf('value="103001"') >= 0 && selHtml.indexOf('value="103003"') >= 0,
    '常用 4 项：国库 / 王座 / 大箱子（最小）/ 货架');
  ok(selHtml.indexOf('value="103002"') >= 0, '料堆等其他容器归进「更多容器」');
  ok(ev('npcfixAutoPickGetTarget()') === 'treasury', '目标默认 = 国库（treasury）');
  // 开关 + tick 路由
  ok(ev('npcfixAutoPickBtnText()') === '自动拾取: 关', '初始状态「关」');
  ctx.confirm = () => true;
  setData(DATA.filter(e => ctx.npcfixIsStuffOnMapEntity(e)));   // 只留掉落物，拾取请求可预期
  const calls3 = [];
  ctx.fetch = (u, o) => { calls3.push([String(u), o && o.body]); return origFetch(u, o); };
  await ctx.npcfixAutoPickToggle();   // 开启（第一拍立即执行）
  ok(ev('npcfixAutoPickBtnText()').indexOf('自动拾取: 开') === 0, '点一下变「开」');
  await new Promise(r => setTimeout(r, 30));   // 等 tick 的 fetch 微任务链跑完（tick 是 fire-and-forget）
  await ctx.npcfixAutoPickToggle();   // 关闭（停掉定时器，进程能退出）
  ok(ev('npcfixAutoPickBtnText()') === '自动拾取: 关', '再点变回「关」');
  const pickCall = calls3.find(([u]) => u.indexOf('/api/editor/stuff/pickup') >= 0);
  ok(!!pickCall, 'tick 发拾取请求');
  if (pickCall && pickCall[1]) {
    const sent = JSON.parse(pickCall[1]);
    ok(sent.target === 'treasury', '请求带 target=当前下拉选择（treasury）');
    ok(Array.isArray(sent.ptrHashes) && sent.ptrHashes.length >= 1, '请求带未拾的掉落物 ptrHash（≥1 堆）');
  }
  ctx.fetch = origFetch;
  ok(/读取存档时自动关闭|自动关闭/.test(els['content'].innerHTML), '自动拾取行有「读取存档时自动关闭」说明');

  // ---------- 15) 盒子4 动物 ----------
  console.log('\n== 15) 盒子4 动物 ==');
  // 判据：Animal（活体）+ AnimalDeadBody（尸体），精确匹配
  ok(ev('npcfixIsAnimalEntity({className:"Animal"})') === true, 'Animal 命中');
  ok(ev('npcfixIsAnimalEntity({className:"AnimalDeadBody"})') === true, 'AnimalDeadBody（尸体）命中');
  ok(ev('npcfixIsAnimalEntity({className:"AnimalHelper"})') === false, 'AnimalHelper 不命中（精确匹配）');
  ok(ev('npcfixIsAnimalEntity({className:"Npc"})') === false, 'Npc 不命中');

  // 组名：活体按种类、尸体统一一组（与 :g= 定位共用 npcfixGroupKeyOf）
  ok(ev(`npcfixGroupKeyOf('animal', {className:'Animal', name:'猪'})`) === '猪', '活体组名 = 物种名');
  ok(ev(`npcfixGroupKeyOf('animal', {className:'AnimalDeadBody', name:'猪'})`) === '动物尸体', '尸体组名 = 动物尸体');

  // 渲染：分组 / 按钮 spec / 搜索
  setData(DATA);
  await ctx.renderNpcfixBox4(false);
  const H4 = els['npcfixBox4Body'].innerHTML;
  ok(H4.indexOf('猪') >= 0, '活体按物种分组（猪）');
  ok(H4.indexOf('动物尸体') >= 0, '尸体统一「动物尸体」组');
  ok(H4.indexOf("npcfixClear('animal:all:g=%E7%8C%AA')") >= 0, '猪组按钮 spec = animal:all:g=猪');
  ok(ev('NPCFIX_CLEAR_KINDS.animal.route') === 'animal', 'animal 的 route = animal（走 /api/editor/animal/batch）');
  ok(ev('NPCFIX_CLEAR_KINDS.animal.tail').indexOf('不掉肉') >= 0, '确认框写明静默移除不掉肉');
  // 清除路由：animal 走 animal/batch
  ctx.confirm = () => true;
  const calls4 = [];
  ctx.fetch = (u, o) => { calls4.push(String(u)); return origFetch(u, o); };
  setData([{ className: 'Animal', name: '猪', hometownKingdomId: 1, ptrHash: -777, guid: 1 }]);
  await ctx.npcfixClear('animal:all');
  ctx.fetch = origFetch;
  ok(calls4.some(u => u.indexOf('/api/editor/animal/batch') >= 0), '清除请求走 /api/editor/animal/batch');
  ok(!calls4.some(u => u.indexOf('/api/editor/destroy/batch') >= 0), '不走 destroy/batch（不清注册表）');
  // 搜索
  setData(DATA);
  ev('npcfixBox4Query = "猪"');
  await ctx.npcfixBox4RenderBody();
  ok(els['npcfixBox4Body'].innerHTML.indexOf('猪') >= 0, '搜「猪」显示猪组');
  ev('npcfixBox4Query = ""');

  // ---------- 16) 盒子4 召唤动物 ----------
  console.log('\n== 16) 盒子4 召唤动物 ==');
  setData(DATA);
  await ctx.renderNpcfixBox4(false);   // 顺带加载动物列表并填充下拉
  const H4s = els['content'].innerHTML + els['npcfixBox4Body'].innerHTML;
  ok(H4s.indexOf('npcfixBox4Spawn()') >= 0, '有「召唤」按钮');
  ok(H4s.indexOf('npcfixBox4SpawnCountChange(this)') >= 0, '有数量输入框');
  const selHtml4 = els['npcfixBox4SpawnAnimal'].innerHTML;
  console.log('  下拉选项:', selHtml4.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 80));
  ok(selHtml4.indexOf('鸡') >= 0 && selHtml4.indexOf('猪') >= 0 && selHtml4.indexOf('驯狼') >= 0
    && selHtml4.indexOf('羊') >= 0, '下拉含家畜中文名（鸡/猪/驯狼/羊…）');
  ok(selHtml4.indexOf('大鱼') < 0, '水生动物（大鱼）不在召唤列表');
  ok(selHtml4.indexOf('梅花鹿') < 0 && selHtml4.indexOf('鹿') < 0,
    '野生动物（鹿类）不在召唤列表——实测召唤后会被野生生态回收');
  ok(ev('npcfixAnimalList.some(a => a.id === 501005)') === true
    && els['npcfixBox4SpawnAnimal'].value === '501005', '默认选中 = 猪（501005）');
  // 数量夹取：0→1 / 11→10 / 非法→1 / 5 不变
  const clampC = v => vm.runInContext(`(function(){ var el={value:'${v}'}; npcfixBox4SpawnCountChange(el); return el.value; })()`, ctx);
  console.log('  夹取: 0→' + clampC(0), '11→' + clampC(11), 'abc→' + clampC('abc'), '5→' + clampC(5));
  ok(clampC(0) === 1 && clampC(11) === 10 && clampC('abc') === 1 && clampC(5) === 5, '数量夹取 0→1 / 11→10 / 非法→1 / 5 不变');
  // 召唤请求：带 stuffId + count，走 /api/editor/animal/spawn
  ctx.confirm = () => true;
  const calls5 = [];
  ctx.fetch = (u, o) => { calls5.push([String(u), o && o.body]); return origFetch(u, o); };
  const selEl = ctx.document.getElementById('npcfixBox4SpawnAnimal');
  const cntEl = ctx.document.getElementById('npcfixBox4SpawnCount');
  selEl.value = '501001';   // 鸡
  cntEl.value = '3';
  await ctx.npcfixBox4Spawn();
  ctx.fetch = origFetch;
  const spawnCall = calls5.find(([u]) => u.indexOf('/api/editor/animal/spawn') >= 0);
  ok(!!spawnCall, '召唤请求走 /api/editor/animal/spawn');
  if (spawnCall && spawnCall[1]) {
    const sent = JSON.parse(spawnCall[1]);
    ok(sent.stuffId === 501001 && sent.count === 3, '请求带 stuffId=501001（鸡）与 count=3');
  }

  console.log('\n' + (fails === 0 ? 'ALL PASS' : (fails + ' FAILED')));
  process.exit(fails === 0 ? 0 : 1);
})();
