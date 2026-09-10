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
];
const DATA = LIVE.concat(SYNTH);

// ---- 最小 DOM / 环境 mock ----
function mkEl() {
  return {
    _h: '', value: '', checked: false, open: false, dataset: {}, style: {},
    classList: { add() {}, remove() {}, toggle() {} },
    get innerHTML() { return this._h; }, set innerHTML(v) { this._h = v; },
    querySelector: () => null, querySelectorAll: () => [],
    closest: () => null, getAttribute: () => null, setAttribute() {},
    appendChild() {}, addEventListener() {},
    set textContent(v) { this._t = v; }, get textContent() { return this._t; },
  };
}
const els = {};
const ctx = {
  console, window: {},
  document: { body: mkEl(), getElementById: id => (els[id] = els[id] || mkEl()),
    querySelector: () => null, querySelectorAll: () => [], createElement: mkEl, addEventListener() {} },
  localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
  // 只用于让"空数据时 renderNpcfixBox2 自动扫描"这条路走通（扫描结果就是空）
  fetch: () => Promise.resolve({ json: () => Promise.resolve([]) }),
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
  ok(clearSrc.indexOf('const before = new Set') < clearSrc.indexOf('npcfixKillInChunks(hashes)'),
    'before 快照在第一轮销毁之前');

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

  // ---------- 9) 安全发展模式 ----------
  console.log('\n== 9) 安全发展模式 ==');
  ok(/npcfixSafeToggle\(\)/.test(H), '「敌方单位」有安全发展模式按钮');
  ok(ctx.npcfixSafeBtnText() === '安全发展模式: 关', '初始状态为「关」');
  ctx.npcfixSafeToggle();
  ok(ctx.npcfixSafeBtnText().indexOf('安全发展模式: 开') === 0, '点一下变「开」');
  ctx.npcfixSafeToggle();
  ok(ctx.npcfixSafeBtnText() === '安全发展模式: 关', '再点一下变回「关」');
  const safeSrc = src.slice(src.indexOf('async function npcfixSafeTick'));
  ok(/NPCFIX_SAFE_BATCH/.test(safeSrc) && /slice\(0, NPCFIX_SAFE_BATCH\)/.test(safeSrc),
    '每拍按 NPCFIX_SAFE_BATCH 取一批');
  const safeBatch = ev('NPCFIX_SAFE_BATCH');
  console.log('  NPCFIX_SAFE_BATCH =', safeBatch, '/ 间隔', ev('NPCFIX_SAFE_INTERVAL') + 'ms',
    '/ 每', ev('NPCFIX_SAFE_RESCAN_EVERY'), '拍重扫');
  ok(safeBatch >= 20, '每拍清除数量已调大（≥20）');
  ok(/k !== 1 && k !== 0/.test(safeSrc), '只清敌方（不含我方 1 / 阵营0）');
  ok(/npcfixScanning/.test(safeSrc), '重扫进行中时跳过这一拍（避免大批 not found）');
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

  console.log('\n' + (fails === 0 ? 'ALL PASS' : (fails + ' FAILED')));
  process.exit(fails === 0 ? 0 : 1);
})();
