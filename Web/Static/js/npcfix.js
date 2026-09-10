// npcfix — 新版"NPC修改"面板：盒子1 小人数值修改 / 盒子2 战斗单位
// 原"NPC"与"实体扫描"面板保持不动，本面板为增量功能。

// ===== 字段组（按职业定制；共同字段所有职业常驻） =====
const NPCFIX_COMMON_FIELDS = ['speed', 'hunger', 'heat', 'hp', 'hp_total'];
const NPCFIX_SOLDIER_FIELDS = ['atk_min', 'atk_max', 'magic_atk_min', 'magic_atk_max', 'phys_res', 'magic_res'];
const NPCFIX_WORKER_FIELDS = ['nature_work_effect', 'manpower'];
const NPCFIX_FIELD_SETS = {
  soldiers: NPCFIX_SOLDIER_FIELDS,   // 士兵：攻击血量等
  workers: NPCFIX_WORKER_FIELDS,     // 工作者：工作效率等
  laborers: [],                      // 杂工：仅共同字段
  children: [],
  prisoners: [],
  nobles: [],
  royals: [],                        // 王室成员：仅共同字段
  lords: NPCFIX_SOLDIER_FIELDS,
  misc: [],
  outsiders: [],
  others: [],
};

function toggleNpcfix() {
  npcfixOpen = !npcfixOpen;
  renderSidebar();
}

function selectNpcfixView(view) {
  const wasActive = (npcfixView === view);   // 点击前已展开该视图 => 这次是"收起"
  selectExclusiveView('npcfix', view);
  // 进入盒子1 时强制重扫我方 NPC：否则 npcListData 非空会直接复用上次缓存，
  // 表现为"切走再切回不刷新"（与 NPC 面板 openNpcPanel 的无条件扫描保持一致）。
  // 缓存为空时上面的渲染本身就会扫描，无需重复触发。
  if (!wasActive && view === 'box1' && npcfixView === 'box1' && npcListData.length > 0)
    renderNpcfixBox1(true);
  // 盒子2 同理：实体扫描结果也要刷一遍，否则船/怪物是新生成的就看不到
  if (!wasActive && view === 'box2' && npcfixView === 'box2' && entityEditorData.length > 0)
    renderNpcfixBox2(true);
}

// ===== 盒子1 小人数值修改 =====

// ===== 分组内筛选框（市民：工作者 / 杂工 / 儿童） =====
// 种族选项：race_id 0..9（顺序取自 race.json：0矮人…9蜥蜴人），
// 外加「石头人 / 小精灵」——它们不是种族，而是 npcType 23/30 的特殊单位。
const NPCFIX_RACES = [
  ['0', '矮人'], ['1', '蚁人'], ['2', '鼠人'], ['3', '猫人'], ['4', '羊人'], ['5', '狼人'],
  ['6', '猪人'], ['7', '精灵族'], ['8', '三眼人'], ['9', '蜥蜴人'],
  ['stone', '石头人'], ['sprite', '小精灵'],
];

// 卡片上的种族标识（data-race）
function npcfixRaceKey(npc) {
  const t = npc.npcType;
  if (t === 23) return 'sprite';   // 小精灵
  if (t === 30) return 'stone';    // 石头人
  return (npc.raceId === undefined || npc.raceId === null) ? '' : String(npc.raceId);
}

// 卡片上的职业标识（data-job）= npcType
function npcfixJobKey(npc) {
  return (npc.npcType === undefined || npc.npcType === null) ? '' : String(npc.npcType);
}

// 职业下拉的选项 = 本组里**实际出现**的 npcType（名称取自 npc_types.json），按人数降序
function npcfixJobOptions(list) {
  const cnt = new Map();
  for (const n of list) {
    const k = npcfixJobKey(n);
    cnt.set(k, (cnt.get(k) || 0) + 1);
  }
  return [...cnt.entries()]
    .sort((a, b) => b[1] - a[1] || (Number(a[0]) - Number(b[0])))
    .map(([k, c]) => [k, getNpcTypeName(Number(k))]);
}

// 过滤判定（纯函数，便于单测）：空值 = 不限
function npcfixMatchFilter(race, job, cardRace, cardJob) {
  return (!race || race === cardRace) && (!job || job === cardJob);
}

function npcfixFilterBar(list, withJob) {
  let h = '<div class="npcfix-filters"><span class="npcfix-fl">种族</span>';
  h += '<select class="npcfix-select" onchange="npcfixApplyFilter(this)"><option value="">不限</option>';
  for (const [v, label] of NPCFIX_RACES) h += '<option value="' + v + '">' + label + '</option>';
  h += '</select>';
  if (withJob) {
    h += '<span class="npcfix-fl">职业</span>';
    h += '<select class="npcfix-select" onchange="npcfixApplyFilter(this)"><option value="">不限</option>';
    for (const [v, label] of npcfixJobOptions(list))
      h += '<option value="' + v + '">' + esc(label) + '</option>';
    h += '</select>';
  }
  h += '</div>';
  return h;
}

// 改选后本地过滤：隐藏不匹配的卡片，并把标题栏计数改成「命中 / 总数 个」。
// 只操作 display，不重建 DOM —— 已展开的字段、已填的输入框都保留。
function npcfixApplyFilter(sel) {
  const group = sel && sel.closest ? sel.closest('.npcfix-group') : null;
  if (!group) return;
  const selects = group.querySelectorAll('.npcfix-select');
  const race = selects[0] ? selects[0].value : '';
  const job = selects[1] ? selects[1].value : '';
  const cards = group.querySelectorAll('.npc-card');
  let shown = 0;
  for (const c of cards) {
    const show = npcfixMatchFilter(race, job, c.getAttribute('data-race'), c.getAttribute('data-job'));
    c.style.display = show ? '' : 'none';
    if (show) shown++;
  }
  const cnt = group.querySelector('.npcfix-count');
  if (cnt) cnt.textContent = (race || job) ? (shown + ' / ' + cards.length + ' 个') : (cards.length + ' 个');
}

// NPC 卡片网格（自适应列数；窄窗口自动降为 1 列，不会挤坏卡片）
// 卡片带 data-race / data-job，供上面的分组筛选框过滤
function npcfixGrid(list, groupKey) {
  let h = '<div class="npcfix-grid">';
  for (const npc of list)
    h += renderNpcCard(npc, {
      groupKey: groupKey,
      extraAttr: ' data-race="' + npcfixRaceKey(npc) + '" data-job="' + npcfixJobKey(npc) + '"'
    });
  h += '</div>';
  return h;
}

// 一级盒子标题栏右侧的「展开 / 收起」按钮：作用于**本盒子内**的二级分组卡片。
// ⚠ 按钮长在 <summary> 里：不阻止冒泡的话，点一下会连带把一级盒子收起/展开。
function npcfixBoxBtns() {
  const s = 'padding:2px 10px;background:var(--bg-input);color:var(--text-secondary)'
    + ';border:1px solid var(--border);border-radius:4px;cursor:pointer;font-size:11px;font-weight:400';
  const guard = 'event.preventDefault();event.stopPropagation();';
  return '<span style="display:flex;gap:4px;margin-left:8px">'
    + '<button onclick="' + guard + 'npcfixBoxExpandAll(this,true)" style="' + s + '">展开</button>'
    + '<button onclick="' + guard + 'npcfixBoxExpandAll(this,false)" style="' + s + '">收起</button>'
    + '</span>';
}

// el = 被点的按钮；就近取到所属的一级 <details>，只切换它内部的 .npcfix-group。
// 选择器严格限定 .npcfix-group：一级盒子与 NPC 卡片本身也是 details，不能用 'details'。
function npcfixBoxExpandAll(el, open) {
  const box = el && el.closest ? el.closest('details') : null;
  if (!box) return;
  for (const g of box.querySelectorAll('.npcfix-group')) g.open = !!open;
}

// 分组卡片：彩色左条标题栏 + 内部 NPC 网格。
// 取代原来的二级 <details> 折叠 —— 一级盒子展开后直接铺出若干张分组卡片，
// 少点一次（士兵→兵种 / 市民→职业 / 贵族→身份）。
// 卡片本身是 <details>：**默认收起**，点标题栏单独展开/收起；
// 想一次全摊开用所属一级盒子标题栏上的「展开 / 收起」。
// filterSpec 存在时在标题栏下插一行筛选框；{ job: true } 额外给一个「职业」下拉
function npcfixGroupCard(label, color, icon, list, groupKey, filterSpec) {
  if (!list || list.length === 0) return '';
  let h = '<details class="npcfix-group" style="--gc:' + color + '">';
  h += '<summary class="npcfix-group-head">';
  h += '<span class="npcfix-chev">&#x25B6;</span>';
  h += '<span>' + icon + '</span><span>' + esc(label) + '</span>';
  h += '<span class="npcfix-count">' + list.length + ' 个</span>';
  h += '</summary>';
  if (filterSpec) h += npcfixFilterBar(list, filterSpec.job === true);
  h += npcfixGrid(list, groupKey);
  h += '</details>';
  return h;
}

// 我方单类别盒子（俘虏 / 外来者 / 其他）：展开后直接铺卡片网格；
// 只有一类，不再套一层分组卡片。
function npcfixSimpleGroup(label, color, icon, list, groupKey) {
  if (!list || list.length === 0) return '';
  return htmlDetailsGroup(label + ' (' + list.length + ')', color, icon, list.length + ' 个',
    '<div class="npcfix-box-body">' + npcfixGrid(list, groupKey) + '</div>');
}

async function renderNpcfixBox1(forceScan) {
  const el = document.getElementById('content');
  // 首次进入（无缓存）或显式刷新时才真正扫描；其余情况复用已扫描数据直接渲染
  const needScan = forceScan === true || npcListData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F9F0; 小人数值修改</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">我方NPC · 字段按职业定制（敌方单位在「战斗单位」）</span>';
  html += '<button onclick="renderNpcfixBox1(true)" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div id="npcfixBox1Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">' + (needScan ? '扫描中...' : '') + '</div>';
  html += '</div>';
  el.innerHTML = html;

  if (needScan) {
    try {
      const r = await fetch('/api/npc/scan', { method: 'POST' });
      npcListData = await r.json();
      renderSidebar();
    } catch (e) {
      document.getElementById('npcfixBox1Body').innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(e.message) + '</div>';
      return;
    }
  }

  const body = document.getElementById('npcfixBox1Body');
  if (npcListData.length === 0) {
    body.innerHTML = '<div style="padding:40px;text-align:center;color:var(--text-muted)">未发现我方 NPC</div>';
    return;
  }

  // 分敌我：本面板只列我方（阵营1）。敌方战斗单位已迁到「盒子2 战斗单位」。
  const ours = npcListData.filter(n => (n.hometownKingdomId || 0) === 1);

  // 贵族线先摘出来（三类互斥，优先级 领主 > 王室成员 > 贵族），其余再按职业分类：
  //   领主     = npcType 70（Npc.is_lord_class 派生的职业）
  //   王室成员 = is_royal 标记（后端读 Npc.is_royal），且非领主
  //   贵族     = classifyNpcByType 的 nobles 桶（npcType 61），此时已不含王室成员
  // 王室成员优先归贵族盒，不再落进 儿童/工作者 等桶，避免同一人出现在两个盒子。
  const lords = ours.filter(n => (n.npcType || 0) === 70);
  const royals = ours.filter(n => n.isRoyal === true && (n.npcType || 0) !== 70);
  const noblePtrs = new Set(lords.concat(royals).map(n => n.ptrHash));
  const oursCommon = ours.filter(n => !noblePtrs.has(n.ptrHash));

  // 我方士兵再按职业（兵种名）细分
  const oursByType = classifyNpcByType(oursCommon);
  const professions = {};
  for (const n of oursByType.soldiers) {
    const prof = n.soldierTypeName || '未知兵种';
    if (!professions[prof]) professions[prof] = [];
    professions[prof].push(n);
  }

  let html2 = '';

  // ===== 我方 =====
  html2 += '<div style="font-size:13px;font-weight:600;color:var(--success-dark,#27ae60);margin:4px 0 6px">我方 (阵营1) · ' + ours.length + ' 个</div>';

  // 士兵：整组套一个盒子（绿色主题），展开后再按兵种细分（二级菜单）
  const profEntries = Object.entries(professions).sort((a, b) => b[1].length - a[1].length);
  if (profEntries.length > 0) {
    let inner = '<div class="npcfix-box-body">';
    for (const [prof, list] of profEntries)
      inner += npcfixGroupCard('士兵 · ' + prof, 'var(--success-dark, #27ae60)', '&#x2694;', list, 'soldiers');
    inner += '</div>';
    html2 += htmlDetailsGroup('士兵 (' + oursByType.soldiers.length + ')', 'var(--success-dark, #27ae60)', '&#x2694;', oursByType.soldiers.length + ' 个', inner, null, npcfixBoxBtns());
  }

  // 市民：工作者 / 杂工 / 儿童 归入同一个盒子（二级菜单）
  // 杂工组额外并入「小精灵 / 石头人」（npcType 23/30，classifyNpcByType 归入 misc）：
  // 它们同为我方平民单位，单独成组会从市民计数里漏掉（我方总数 > 各盒子之和）。
  // 三组都带「种族」筛选框；「工作者」额外带「职业」筛选框（职业选项按实际出现的人生成）
  const citizenGroups = [
    { key: 'workers', label: '工作者', icon: '&#x1F527;', color: '#f39c12', list: oursByType.workers, filter: { job: true } },
    { key: 'laborers', label: '杂工', icon: '&#x1F6E0;', color: '#95a5a6', list: (oursByType.laborers || []).concat(oursByType.misc || []), filter: {} },
    { key: 'children', label: '儿童', icon: '&#x1F476;', color: '#e91e63', list: oursByType.children, filter: {} },
  ];
  let citizenInner = '<div class="npcfix-box-body">';
  let citizenCount = 0;
  for (const g of citizenGroups) {
    const list = g.list;
    if (!list || list.length === 0) continue;
    citizenCount += list.length;
    citizenInner += npcfixGroupCard(g.label, g.color, g.icon, list, g.key, g.filter || null);
  }
  citizenInner += '</div>';
  if (citizenCount > 0)
    html2 += htmlDetailsGroup('市民 (' + citizenCount + ')', '#3498db', '&#x1F3E0;', citizenCount + ' 个', citizenInner, null, npcfixBoxBtns());

  // 俘虏
  html2 += npcfixSimpleGroup('俘虏', '#7f8c8d', '&#x1F512;', oursByType.prisoners, 'prisoners');

  // 贵族：领主 / 贵族 / 王室成员 三类归入同一个盒子（二级菜单）
  const nobleGroups = [
    { key: 'lords', label: '领主', icon: '&#x1F3F0;', color: '#e67e22', list: lords },
    { key: 'nobles', label: '贵族', icon: '&#x1F451;', color: '#f1c40f', list: oursByType.nobles },
    { key: 'royals', label: '王室成员', icon: '&#x1F934;', color: '#8e44ad', list: royals },
  ];
  let nobleInner = '<div class="npcfix-box-body">';
  let nobleCount = 0;
  for (const g of nobleGroups) {
    const list = g.list;
    if (!list || list.length === 0) continue;
    nobleCount += list.length;
    nobleInner += npcfixGroupCard(g.label, g.color, g.icon, list, g.key);
  }
  nobleInner += '</div>';
  if (nobleCount > 0)
    html2 += htmlDetailsGroup('贵族 (' + nobleCount + ')', '#f1c40f', '&#x1F451;', nobleCount + ' 个', nobleInner, null, npcfixBoxBtns());

  // 外来者：旅客 / 商人 / 外乡人 等（npcType -15 ~ -5）
  html2 += npcfixSimpleGroup('外来者', '#9b59b6', '&#x1F464;', oursByType.outsiders, 'outsiders');

  // 其他（未归类的我方 NPC，保持独立盒子）
  html2 += npcfixSimpleGroup('其他', '#34495e', '&#x2753;', oursByType.others, 'others');

  body.innerHTML = html2 || '<div style="padding:40px;text-align:center;color:var(--text-muted)">暂无数据</div>';
}


// NPC 卡片统一由 npc.js 的 renderNpcCard(npc, {groupKey}) 渲染（见 npc.js）

// 卡片字段加载：一次请求，渲染 职业快速字段 + 全字段表格
async function loadNpcfixCard(details, ptrHash, groupKey) {
  // 展开字段时让这张卡片占满整行（CSS .npc-card-open）：
  // 字段表最小宽度约 330px，留在 260px 的网格格里会横向溢出卡片。
  // 收起时同步摘掉 class —— 必须在下面的提前 return 之前处理。
  const card = details.closest ? details.closest('.npc-card') : null;
  if (card) card.classList.toggle('npc-card-open', details.open);
  if (!details.open) return;
  const body = document.getElementById('npcfix-body-' + ptrHash);
  if (!body || body.dataset.loaded) return;
  body.dataset.loaded = '1';
  body.innerHTML = '加载中...';
  try {
    const [fields, translations] = await Promise.all([
      fetch('/api/npc/fields/' + ptrHash + '?t=' + Date.now()).then(r => r.json()),
      loadFieldTranslations()
    ]);
    if (fields.error) { body.innerHTML = '<span style="color:var(--danger)">实体已失效，请重新扫描</span>'; return; }

    const keys = [...NPCFIX_COMMON_FIELDS, ...(NPCFIX_FIELD_SETS[groupKey] || [])];
    const seen = new Set();
    const groupLabel = { soldiers: '士兵', workers: '工作者', laborers: '杂工', children: '儿童', prisoners: '俘虏', nobles: '贵族', royals: '王室成员', lords: '领主', outsiders: '外来者', others: '其他' }[groupKey] || groupKey;
    let quick = '<div style="font-size:11px;color:var(--accent-light);margin-bottom:4px">常用字段（' + groupLabel + '）</div>' + npcTableHeader(ptrHash, false);
    for (const key of keys) {
      if (seen.has(key)) continue;
      seen.add(key);
      const f = fields[key];
      if (!f || f.isString) continue;
      const displayVal = (typeof f.value === 'number') ? (f.isFloat ? f.value.toFixed(2) : f.value) : (f.value || 0);
      quick += npcNumRowHtml(ptrHash, key, f.isFloat, displayVal, translations[key] || '', {
        inpId: 'npc_inp_' + ptrHash + '_' + key
      });
    }

    const allKeys = Object.keys(fields).filter(k => k !== 'error' && !seen.has(k));
    const numKeys = allKeys.filter(k => !fields[k].isString);
    const strKeys = allKeys.filter(k => fields[k].isString);
    let full = npcTableHeader(ptrHash, true);
    full += npcFieldRowsHtml(ptrHash, fields, translations, numKeys, strKeys, 'npcfix_all_');
    full += '</tbody></table>';

    quick += '</tbody></table>';

    body.innerHTML = quick +
      '<details style="margin-top:4px"><summary style="cursor:pointer;font-size:11px;color:var(--text-muted);user-select:none">所有字段 (' + (numKeys.length + strKeys.length) + ')</summary>' +
      '<div style="margin-top:4px">' + full + '</div></details>';

    updateCheckAllState(ptrHash);
  } catch (e) {
    body.innerHTML = '<span style="color:var(--danger)">加载失败: ' + esc(String(e)) + '</span>';
  }
}


// ===== 盒子2 战斗单位 =====
// 布局照搬盒子1 的「士兵」盒子：一级盒子 → 二级分组卡片 → 单位卡片网格。
// 数据源是实体扫描（/api/editor/scan）—— 船和怪物都不是 NPC，只有它扫得到。
//
// 船的两件事都得靠实体字段判，不能拿名字猜（原实现用 /war|战/ 正则猜，className 又
// 以 Ship 开头，结果所有船都被算成战舰，商船也混进来了）：
//   · 敌我 —— 实体自身 kingdom_id（后端输出 kingdomId；游戏里 Ship.SetInfo 里
//             kingdom_id = territory.kingdom_id，Territory.IsMyTerritory 也用它判敌我）
//   · 商船 —— stuff_id 706001 是「货船」，706002「战舰」/706003「巨型战舰」才是战舰
//             （出处：反编译 Ship.IsTradingShip / Ship.get_IsBattleShip）
const NPCFIX_TRADE_SHIP = 706001;          // 货船（商船）——不计
const NPCFIX_WAR_SHIPS = [706002, 706003]; // 战舰 / 巨型战舰

// 单位所属阵营：船读自身 kingdom_id，NPC 读 hometown_kingdom_id
function npcfixUnitKingdom(e) {
  return e.kingdomId || e.hometownKingdomId || e.territoryKingdomId || 0;
}

// 真·人形单位：类名白名单 + 表现层/UI 黑名单。
// ⚠ 绝不能用 /Npc|Soldier|BattleUnit/ 这种"包含"匹配 —— 实体扫描会把 NPC 的**躯体模型**
// （NpcBody，goName 就是 body / npc_body）、UI 预制件（NpcListView / NpcListItem /
// NpcAttrItem / NpcBodyUI…）、子弹（BulletSoldier）、尸体（NpcDeadBody）一并扫出来。
// 它们是表现层/界面物件，自身不持有 kingdom_id，三个阵营字段全为 0，于是一股脑落进
// 「阵营0」。实机 11208 条实体里「阵营0」2548 条，其中 2240 条是 NpcBody —— 全是垃圾。
const NPCFIX_UNIT_RE = /^(Npc|Monster|Soldier|BattleUnit)/;
const NPCFIX_NOT_UNIT_RE = /(Body|Footprint|View|UI|Item|Dialog|Panel|Bar|Group|Text|Button|Bullet|Faeces|Orderly|AdjustLimit|Recruit|Attr|Nature|Helper|Task|Finder|Limit)/;

// 人形单位（NPC / Soldier / BattleUnit；Monster 与 Ship 另有分支，不经过这里）
function npcfixIsHumanUnit(cn) {
  return NPCFIX_UNIT_RE.test(cn) && !NPCFIX_NOT_UNIT_RE.test(cn);
}

// 我方「战斗」单位：只有带兵种（soldier_type_id > 0）才算。
// ⚠ 不能写成 `|| !!e.soldierTypeName`：后端对 id=0 输出的是「市民」（非空字符串，
// 见 DataTables.SoldierTypeName），那样判据恒真 —— 实机「我方战斗单位」1143 条里
// 只有 206 条真有兵种，其余是 875 个平民 NPC + 62 具尸体。
function npcfixIsCombatUnit(e) {
  return (e.soldierTypeId || 0) > 0;
}

// 分类（纯函数，便于单测）：
//   我方战斗单位 / 我方-怪物 / 敌方-小人(按阵营) / 敌方-怪物(按阵营) /
//   船(我方 & 敌方，排除商船)
function npcfixCombatClassify(list) {
  const ours = [];
  const humanoids = {}, monstersEnemy = {}, monstersOurs = [];
  const shipsOurs = [], shipsEnemy = {};
  for (const e of list) {
    const cn = e.className || '';
    const kid = npcfixUnitKingdom(e);
    if (cn.indexOf('Ship') >= 0) {
      if (e.stuffId === NPCFIX_TRADE_SHIP) continue;             // 商船/货船：不计
      if (NPCFIX_WAR_SHIPS.indexOf(e.stuffId) < 0) continue;      // 其它船型也不列
      if (kid === 1) shipsOurs.push(e);
      else (shipsEnemy[kid] = shipsEnemy[kid] || []).push(e);
      continue;
    }
    if (cn.indexOf('Monster') === 0) {
      // 怪物也分敌我：阵营1 的是我方地界刷出来的（蚁巢工蚁、龙…），
      // 不能混进「敌方-怪物」，否则组头会显示成「敌方-怪物 → 我方」，语义反了。
      if (kid === 1) monstersOurs.push(e);
      else (monstersEnemy[kid] = monstersEnemy[kid] || []).push(e);
      continue;
    }
    if (!npcfixIsHumanUnit(cn)) continue;
    if (kid === 1) { if (npcfixIsCombatUnit(e)) ours.push(e); }
    else (humanoids[kid] = humanoids[kid] || []).push(e);
  }
  return { ours: ours, humanoids: humanoids, monstersOurs: monstersOurs,
           monstersEnemy: monstersEnemy, shipsOurs: shipsOurs, shipsEnemy: shipsEnemy };
}

function npcfixSumKinds(o) {
  let n = 0;
  for (const k in o) n += o[k].length;
  return n;
}

// 单位卡：头部（名称/阵营/GUID/按钮）+ 懒加载字段表。
// 复用 .npc-card / .npc-card-open：展开字段时占满整行，否则字段表（最小宽约 330px）
// 会在 260px 的网格格里横向溢出（与盒子1 同一个坑）。
function npcfixEntityCard(e, opts) {
  opts = opts || {};
  const ph = e.ptrHash || 0;
  const displayName = e.npcName || e.name || e.goName || 'unknown';
  const suffix = e.stuffNameWithIdIndex || e.soldierTypeName || '';
  const kInfo = getKingdomInfo(npcfixUnitKingdom(e));
  const btn = 'padding:3px 8px;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap;color:#fff';
  let h = '<div class="npc-card" style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius)">';
  h += '<div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap;padding:8px 12px">';
  h += '<span style="font-weight:600;color:var(--text-primary);font-size:13px">' + esc(displayName) + '</span>';
  if (suffix) h += '<span style="font-size:11px;color:var(--text-muted)">(' + esc(suffix) + ')</span>';
  if (kInfo) h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + kInfo.bg + ';color:' + kInfo.fg + '">' + esc(kInfo.name) + '</span>';
  h += '<span style="font-size:11px;color:var(--text-muted);margin-left:auto" title="GUID:' + (e.guid || 0) + '">#' + (e.guid || 0) + '</span>';
  if (opts.combat) {
    h += '<button onclick="event.stopPropagation();npcfixMultiply(' + ph + ',\'atk\')" style="' + btn + ';background:var(--warning,#e67e22)">攻击×10</button>';
    h += '<button onclick="event.stopPropagation();npcfixMultiply(' + ph + ',\'hp\')" style="' + btn + ';background:var(--success-dark,#27ae60)">血量×10</button>';
  }
  h += '<button onclick="event.stopPropagation();npcfixToggleUnit(' + ph + ')" style="' + btn + ';background:var(--accent)">字段</button>';
  h += '<button onclick="event.stopPropagation();locateEditorEntity(' + ph + ')" style="' + btn + ';background:var(--info,#3498db)">定位</button>';
  h += '<button onclick="event.stopPropagation();destroyEditorEntity(' + ph + ')" style="' + btn + ';background:var(--danger,#e74c3c)">消除</button>';
  h += '</div>';
  h += '<div id="editor_fields_' + ph + '" style="display:none;padding:6px 12px 10px;overflow-x:auto"></div>';
  h += '</div>';
  return h;
}

// 展开/收起单位卡的字段表（展开时整卡占满一行，与盒子1 一致）
function npcfixToggleUnit(ptrHash) {
  const c = document.getElementById('editor_fields_' + ptrHash);
  if (!c) return;
  const card = c.closest ? c.closest('.npc-card') : null;
  const open = (c.style.display === 'none' || !c.style.display);
  c.style.display = open ? 'block' : 'none';
  if (card) card.classList.toggle('npc-card-open', open);
  if (open) loadEntityEditorFields(ptrHash);
}

// 单位网格（与盒子1 的 npcfix-grid 同一套样式）
function npcfixEntityGrid(list, opts) {
  let h = '<div class="npcfix-grid">';
  for (const e of list) h += npcfixEntityCard(e, opts);
  h += '</div>';
  return h;
}

// 二级分组卡片（同盒子1 的 npcfixGroupCard，只是内部换成单位网格）
// trailingHtml：挂在标题栏计数右侧（怪物组的「一键清除」）
function npcfixEntityGroupCard(label, color, icon, list, opts, trailingHtml) {
  if (!list || list.length === 0) return '';
  let h = '<details class="npcfix-group" style="--gc:' + color + '">';
  h += '<summary class="npcfix-group-head">';
  h += '<span class="npcfix-chev">&#x25B6;</span>';
  h += '<span>' + icon + '</span><span>' + esc(label) + '</span>';
  h += '<span class="npcfix-count">' + list.length + ' 个</span>';
  h += (trailingHtml || '');
  h += '</summary>';
  h += npcfixEntityGrid(list, opts);
  h += '</details>';
  return h;
}

// 怪物组的「一键清除」按钮：长在 <summary> 里，必须阻止冒泡，否则点它会把分组一起开合
function npcfixKillBtn(kid) {
  const s = 'padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px';
  return '<span style="margin-left:8px"><button onclick="event.preventDefault();event.stopPropagation();npcfixKillMonsters(' + kid + ')" style="' + s + '">一键清除(2遍)</button></span>';
}

// 按阵营铺二级分组（敌方-小人 / 敌方-怪物 / 敌方舰队 共用）
function npcfixKingdomGroups(map, fallbackColor, icon, extraTrailing) {
  let h = '';
  const kinds = Object.keys(map).map(Number).sort((a, b) => a - b);
  for (const kid of kinds) {
    const kInfo = getKingdomInfo(kid);
    h += npcfixEntityGroupCard(kInfo ? kInfo.name : ('阵营' + kid), kInfo ? kInfo.bg : fallbackColor,
      icon, map[kid], {}, extraTrailing ? extraTrailing(kid) : null);
  }
  return h;
}

async function renderNpcfixBox2(forceScan) {
  const el = document.getElementById('content');
  // 首次进入（无缓存）或显式刷新时才真正扫描；其余情况复用已扫描数据直接渲染
  const needScan = forceScan === true || entityEditorData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x2694; 战斗单位</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">我方战斗单位/怪物 · 敌方小人/怪物 · 舰船（区分敌我，商船不计）</span>';
  html += '<button onclick="renderNpcfixBox2(true)" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div id="npcfixBox2Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">' + (needScan ? '扫描中...' : '') + '</div>';
  html += '</div>';
  el.innerHTML = html;

  if (needScan) {
    try {
      await fetch('/api/editor/scan', { method: 'POST' });
      await fetchEntityEditorData();
    } catch (e) {
      const b = document.getElementById('npcfixBox2Body');
      if (b) b.innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(String((e && e.message) || e)) + '</div>';
      return;
    }
  }

  const body = document.getElementById('npcfixBox2Body');
  if (!body) return;
  const c = npcfixCombatClassify(entityEditorData);
  let h = '';

  // ===== 我方战斗单位（按兵种二级分组，与盒子1「士兵」同款） =====
  if (c.ours.length > 0) {
    const prof = {};
    for (const e of c.ours) {
      const k = e.soldierTypeName || '未知兵种';
      (prof[k] = prof[k] || []).push(e);
    }
    const entries = Object.keys(prof).sort((a, b) => prof[b].length - prof[a].length);
    let inner = '<div class="npcfix-box-body">';
    for (const name of entries)
      inner += npcfixEntityGroupCard('我方 · ' + name, 'var(--success-dark, #27ae60)', '&#x2694;', prof[name], { combat: true });
    inner += '</div>';
    h += htmlDetailsGroup('我方战斗单位 (' + c.ours.length + ')', 'var(--success-dark, #27ae60)', '&#x2694;',
      c.ours.length + ' 个', inner, null, npcfixBoxBtns());
  }

  // ===== 敌方-小人（按阵营） =====
  if (Object.keys(c.humanoids).length > 0) {
    const n = npcfixSumKinds(c.humanoids);
    h += htmlDetailsGroup('敌方-小人 (' + n + ')', '#c0392b', '&#x1F464;', n + ' 个',
      '<div class="npcfix-box-body">' + npcfixKingdomGroups(c.humanoids, 'var(--text-muted)', '&#x1F464;') + '</div>',
      null, npcfixBoxBtns());
  }

  // ===== 我方-怪物（阵营1 地界刷出的怪物：蚁巢工蚁 / 龙…，按种类二级分组） =====
  if (c.monstersOurs.length > 0) {
    const kinds = {};
    for (const e of c.monstersOurs) {
      const k = e.className || '未知怪物';
      (kinds[k] = kinds[k] || []).push(e);
    }
    const entries = Object.keys(kinds).sort((a, b) => kinds[b].length - kinds[a].length);
    let inner = '<div class="npcfix-box-body">';
    for (const name of entries)
      inner += npcfixEntityGroupCard(name, 'var(--warning, #e67e22)', '&#x1F47E;', kinds[name], {}, npcfixKillBtn(1));
    inner += '</div>';
    h += htmlDetailsGroup('我方-怪物 (' + c.monstersOurs.length + ')', 'var(--warning, #e67e22)', '&#x1F47E;',
      c.monstersOurs.length + ' 个', inner, null, npcfixBoxBtns());
  }

  // ===== 敌方-怪物（按阵营，每组带一键清除） =====
  if (Object.keys(c.monstersEnemy).length > 0) {
    const n = npcfixSumKinds(c.monstersEnemy);
    h += htmlDetailsGroup('敌方-怪物 (' + n + ')', 'var(--danger, #e74c3c)', '&#x1F47E;', n + ' 个',
      '<div class="npcfix-box-body">' + npcfixKingdomGroups(c.monstersEnemy, 'var(--danger, #e74c3c)', '&#x1F47E;', npcfixKillBtn) + '</div>',
      null, npcfixBoxBtns());
  }

  // ===== 船：只列战舰，区分敌我（货船/商船不计） =====
  if (c.shipsOurs.length > 0 || Object.keys(c.shipsEnemy).length > 0) {
    let inner = '<div class="npcfix-box-body">';
    if (c.shipsOurs.length > 0)
      inner += npcfixEntityGroupCard('我方舰队', 'var(--success-dark, #27ae60)', '&#x1F6A2;', c.shipsOurs, {});
    inner += npcfixKingdomGroups(c.shipsEnemy, '#3498db', '&#x1F6A2;');
    inner += '</div>';
    const n = c.shipsOurs.length + npcfixSumKinds(c.shipsEnemy);
    h += htmlDetailsGroup('船 · 战舰 (' + n + ')', '#3498db', '&#x1F6A2;', n + ' 个', inner, null, npcfixBoxBtns());
  }

  body.innerHTML = h || '<div style="padding:40px;text-align:center;color:var(--text-muted)">没有匹配的战斗单位</div>';
}

// ×10：读取当前字段值并写回 10 倍
async function npcfixMultiply(ptrHash, kind) {
  try {
    const r = await fetch('/api/editor/fields/' + ptrHash + '?t=' + Date.now());
    const fields = await r.json();
    if (fields.error) { toast('实体已失效，请重新扫描', true); return; }
    const keys = kind === 'atk' ? ['atk_min', 'atk_max'] : ['hp', 'hp_total'];
    for (const k of keys) {
      const f = fields[k];
      if (!f || f.isString) continue;
      const v = (typeof f.value === 'number' ? f.value : 0) * 10;
      await fetch('/api/editor/set', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ptrHash, field: k, value: v })
      });
    }
    toast((kind === 'atk' ? '攻击' : '血量') + '已×10');
  } catch (e) { toast('操作失败', true); }
}

// 批量销毁（一次主线程任务）
async function destroyBatch(hashes) {
  const r = await fetch('/api/editor/destroy/batch', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ptrHashes: hashes })
  });
  return r.json();
}

// 怪物一键清除：销毁 → 等1秒 → 重扫 → 再销毁一遍（覆盖分裂怪）→ 刷新
async function npcfixKillMonsters(kid) {
  if (!confirm('确定清除该阵营全部怪物？（将执行2遍，覆盖分裂怪）')) return;
  toast('清除中...', false);
  const pick = () => entityEditorData
    .filter(e => (e.className || '').indexOf('Monster') === 0 && npcfixUnitKingdom(e) === kid)
    .map(e => e.ptrHash);
  const hashes = pick();
  if (hashes.length === 0) { toast('没有怪物', true); return; }
  const d1 = await destroyBatch(hashes);
  await new Promise(r => setTimeout(r, 1000));
  // 重扫拿到分裂新生成的怪物，再清一遍
  await entityEditorScan();
  const hashes2 = pick();
  let d2 = { destroyed: 0 };
  if (hashes2.length > 0) d2 = await destroyBatch(hashes2);
  await entityEditorScan();
  toast('清除完成: 首轮' + (d1.destroyed || 0) + ' + 二轮' + (d2.destroyed || 0));
}
