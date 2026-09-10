// 盒子2 回归：用实机数据跑 npcfixCombatClassify + 渲染 + 一键清除范围，断言守恒关系
// 用法: node _tools/test_box2_clear.js <entities.json>
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const live = JSON.parse(fs.readFileSync(process.argv[2] || 'live_tmp.json', 'utf8'));

// ---- 最小 DOM / 环境 mock ----
function mkEl() {
  const el = {
    _h: '', value: '', checked: false, open: false, dataset: {}, style: {},
    classList: { add(){}, remove(){}, toggle(){} },
    get innerHTML() { return this._h; }, set innerHTML(v) { this._h = v; },
    querySelector: () => null, querySelectorAll: () => [],
    closest: () => null, getAttribute: () => null, setAttribute(){},
    appendChild(){}, addEventListener(){}, set textContent(v) { this._t = v; }, get textContent(){ return this._t; }
  };
  return el;
}
const els = {};
const ctx = {
  console,
  window: {},
  document: {
    body: mkEl(),
    getElementById: id => (els[id] = els[id] || mkEl()),
    querySelector: () => null,
    querySelectorAll: () => [],
    createElement: () => mkEl(),
    addEventListener() {},
  },
  localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
  setTimeout, clearTimeout, Date, JSON, Math, Number, String, Object, Array, Promise, Set, Map,
};
ctx.window = ctx;
ctx.globalThis = ctx;
vm.createContext(ctx);

// ---- 载入除 main.js 外的全部前端模块 ----
const dir = path.join('Web', 'Static', 'js');
const files = fs.readdirSync(dir).filter(f => f.endsWith('.js') && f !== 'main.js');
for (const f of files) {
  const src = fs.readFileSync(path.join(dir, f), 'utf8');
  try { vm.runInContext(src, ctx, { filename: f }); }
  catch (e) { console.log('  [skip]', f, e.message.slice(0, 80)); }
}

// ---- 注入实机数据（let 全局必须走 runInContext）----
vm.runInContext('entityEditorData = ' + JSON.stringify(live) + ';', ctx);

const classify = ctx.npcfixCombatClassify(live);
const sumKinds = ctx.npcfixSumKinds;
// ⚠ 顶层 const/let 不会挂到 context 上（词法绑定），必须回 vm 里求值
const ev = expr => vm.runInContext(expr, ctx);

let fails = 0;
function ok(cond, msg) { console.log((cond ? '  ok  ' : '  FAIL') + '  ' + msg); if (!cond) fails++; }

console.log('== 数据规模 ==');
console.log('  实体总数', live.length, '| 怪物', live.filter(e => (e.className||'').startsWith('Monster')).length);

// ---------- 1) 我方-怪物：龙合并 ----------
console.log('\n== 1) 我方-怪物 分组 ==');
const ours = classify.monstersOurs;
const kinds = {};
for (const e of ours) {
  const cn = e.className || '';
  const k = cn.indexOf('Dragon') >= 0 ? '龙' : (e.name || cn || '未知怪物');
  (kinds[k] = kinds[k] || []).push(e);
}
const dragonNames = new Set(ours.filter(e => (e.className||'').indexOf('Dragon') >= 0).map(e => e.name || e.className));
console.log('  我方怪物', ours.length, '| 分组数', Object.keys(kinds).length);
console.log('  分组:', Object.keys(kinds).sort((a,b)=>kinds[b].length-kinds[a].length)
  .map(k => k + '(' + kinds[k].length + ')').join(' '));
if (dragonNames.size > 0) {
  console.log('  合并前会是独立卡片的龙名:', [...dragonNames].join(' / '));
  ok(!!kinds['龙'], '龙只占 1 个分组');
  ok(kinds['龙'].length === ours.filter(e => (e.className||'').indexOf('Dragon') >= 0).length, '「龙」分组的人数 = 全部龙');
  ok(dragonNames.size > 1, '合并确实减少了卡片数（原 ' + dragonNames.size + ' 张 → 1 张）');
} else {
  console.log('  (本次实机数据里我方没有龙)');
}
ok(Object.values(kinds).reduce((a, l) => a + l.length, 0) === ours.length, '分组守恒 == 我方怪物总数');
ok(!Object.keys(kinds).some(k => /^Monster/.test(k)), '分组名里没有英文类名残留（除兜底）');

// ---------- 2) 一键清除范围：与分类桶一致 ----------
console.log('\n== 2) 一键清除范围 vs 分类桶 ==');
const KINDS = ev('NPCFIX_CLEAR_KINDS');
ok(!!KINDS && !!KINDS.monster && !!KINDS.humanoid && !!KINDS.ship, 'NPCFIX_CLEAR_KINDS 已定义');

function scopeFilter(kind, scope) {
  const def = KINDS[kind];
  return live.filter(e => {
    if (!def.test(e)) return false;
    const kid = ctx.npcfixUnitKingdom(e);
    if (scope === 'all') return true;
    if (scope === 'enemy') return kid !== 1;
    return kid === Number(scope);
  });
}

// 敌方-小人 一级 = humanoid:enemy
const hAll = scopeFilter('humanoid', 'enemy');
ok(hAll.length === sumKinds(classify.humanoids),
  'humanoid:enemy (' + hAll.length + ') == 敌方-小人盒 (' + sumKinds(classify.humanoids) + ')');

// 敌方-小人 二级 = humanoid:<kid>
let hBad = 0;
for (const kid of Object.keys(classify.humanoids)) {
  const n = scopeFilter('humanoid', Number(kid)).length;
  if (n !== classify.humanoids[kid].length) { hBad++; console.log('   kid', kid, n, '!=', classify.humanoids[kid].length); }
}
ok(hBad === 0, 'humanoid:<kid> 与各阵营分组逐一吻合');

// 敌方-怪物 一级 / 二级
ok(scopeFilter('monster', 'enemy').length === sumKinds(classify.monstersEnemy),
  'monster:enemy (' + scopeFilter('monster','enemy').length + ') == 敌方-怪物盒 (' + sumKinds(classify.monstersEnemy) + ')');
let mBad = 0;
for (const kid of Object.keys(classify.monstersEnemy)) {
  if (scopeFilter('monster', Number(kid)).length !== classify.monstersEnemy[kid].length) mBad++;
}
ok(mBad === 0, 'monster:<kid> 与各阵营分组逐一吻合');

// 我方-怪物 二级 = monster:1
ok(scopeFilter('monster', 1).length === classify.monstersOurs.length, 'monster:1 == 我方-怪物盒');

// 船：一级 ship:all / 二级 ship:1 与各阵营
const shipTotal = classify.shipsOurs.length + sumKinds(classify.shipsEnemy);
ok(scopeFilter('ship', 'all').length === shipTotal, 'ship:all (' + scopeFilter('ship','all').length + ') == 船盒 (' + shipTotal + ')');
ok(scopeFilter('ship', 1).length === classify.shipsOurs.length, 'ship:1 == 我方舰队');
let sBad = 0;
for (const kid of Object.keys(classify.shipsEnemy)) {
  if (scopeFilter('ship', Number(kid)).length !== classify.shipsEnemy[kid].length) sBad++;
}
ok(sBad === 0, 'ship:<kid> 与各阵营舰队吻合');

// ---------- 3) 危险边界：绝不能误伤我方 NPC ----------
console.log('\n== 3) 边界：不一键清除到自己人 ==');
const oursNpc = live.filter(e => (e.className||'').indexOf('Npc') === 0 && e.className !== 'NpcBody'
  && (e.hometownKingdomId || 0) === 1);
console.log('  我方 Npc 实体', oursNpc.length);
ok(!hAll.some(e => (e.hometownKingdomId || 0) === 1), 'humanoid:enemy 里没有阵营1的人形');
ok(!scopeFilter('monster', 'enemy').some(e => (e.hometownKingdomId||0) === 1), 'monster:enemy 里没有阵营1的怪物');

// ---------- 4) 渲染产物：一级/二级清除按钮 ----------
console.log('\n== 4) 渲染 HTML 按钮 ==');
const html = ctx.npcfixEntityGroupCard('测试组', '#c0392b', 'x', classify.monstersOurs.slice(0, 2), {}, ctx.npcfixClearBtn('monster', 89));
ok(/npcfixClear\('monster:89'\)/.test(html), '二级按钮 spec = monster:89');

// 版本号确认（防"改了没生效"）
// ---------- 5) 确认框文案：数量 > 100 追加"可能卡顿" ----------
console.log('\n== 5) 确认框文案 ==');
let captured = null;
ctx.confirm = msg => { captured = msg; return false; };   // 一律取消，避免真的发起销毁
(async () => {
  await ctx.npcfixClear('monster:enemy');          // 实机 ~1700 只 > 100
  console.log('  [>100] ' + String(captured).replace(/\n/g, '⏎'));
  ok(/数量过多可能卡顿2-5s/.test(captured), '数量 > 100 时追加了「数量过多可能卡顿2-5s」');
  ok(/确定清除敌方全部阵营的 \d+ 个怪物/.test(captured), '文案里有范围/种类/数量');

  await ctx.npcfixClear('ship:100');              // 敌方舰队 8 条 < 100
  console.log('  [<100] ' + String(captured).replace(/\n/g, '⏎'));
  ok(!/数量过多可能卡顿/.test(captured), '数量 ≤ 100 时不追加提示');
  ok(/确定清除阵营100的 8 个战舰/.test(captured), '二级分组文案按阵营号描述');

  // ---------- 6) 整盒渲染：按钮落位 ----------
  console.log('\n== 6) renderNpcfixBox2 产出 ==');
  await ctx.renderNpcfixBox2();
  const boxHtml = els['npcfixBox2Body'].innerHTML;
  const boxCount = (label) => {
    // 取该一级盒子的 summary 段（到第一个 </summary> 为止）
    const i = boxHtml.indexOf(label);
    if (i < 0) return null;
    return boxHtml.slice(i, boxHtml.indexOf('</summary>', i));
  };
  for (const [label, need] of [['敌方-小人', true], ['敌方-怪物', true], ['船 · 战舰', true], ['我方-怪物', false]]) {
    const seg = boxCount(label);
    if (seg === null) { console.log('  (实机数据里没有 ' + label + ')'); continue; }
    const hasBoxBtn = /npcfixClear\('/.test(seg);
    ok(hasBoxBtn === need, label + ' 一级清除按钮: ' + (hasBoxBtn ? '有' : '无') + (need ? '' : '（本次未要求）'));
  }
  const totalBtns = (boxHtml.match(/一键清除/g) || []).length;
  console.log('  全盒「一键清除」按钮数:', totalBtns);
  ok(totalBtns >= 8, '按钮数量合理（≥8）');
  ok(!/npcfixKillMonsters/.test(boxHtml), '没有旧函数残留');
  ok(!/MonsterDragon/.test(boxHtml), '我方怪物分组里没有出现英文类名（龙已合并）');

  console.log('\n' + (fails === 0 ? 'ALL PASS' : (fails + ' FAILED')));
  process.exit(fails === 0 ? 0 : 1);
})();
