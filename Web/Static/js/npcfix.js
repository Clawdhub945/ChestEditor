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
  // 盒子3/4/5：切换时不再阻塞式重扫（用户实测每次要等十几秒）——
  // renderContent 已用缓存秒开列表，这里只触发后台静默刷新（60s 节流），完成后悄悄更新列表
  if (!wasActive && view === 'box3' && npcfixView === 'box3' && entityEditorData.length > 0)
    renderNpcfixBox3('auto');
  if (!wasActive && view === 'box4' && npcfixView === 'box4' && entityEditorData.length > 0)
    renderNpcfixBox4('auto');
  if (!wasActive && view === 'box5' && npcfixView === 'box5' && entityEditorData.length > 0)
    renderNpcfixBox5('auto');
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
    // 阵营 0 = kingdomId / hometownKingdomId / territoryKingdomId 三个字段全为 0，
    // 是未初始化或表现层残留的实体（实测「阵营0」里没有真正的战斗单位），一律不显示。
    if (kid === 0) continue;
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
  // 后缀：设施/船用带编号的设施名；其余只在真带兵种时显示兵种名
  // ⚠ soldierTypeId=0 时后端给的 soldierTypeName 是「市民」（DataTables.SoldierTypeName 的 0 值），
  //   直接拿来当后缀会让怪物/平民卡片都挂一个没意义的「(市民)」。
  const suffix = e.stuffNameWithIdIndex
    || ((e.soldierTypeId || 0) > 0 ? (e.soldierTypeName || '') : '');
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

// ===== 一键清除 / 战斗力缩放 / 定期清理 =====
// 目标范围统一用 spec 描述：'<kind>:<scope>[:g=<分组名>]'
//   kind  = monster(怪物) / humanoid(小人) / ship(战舰) / enemyAll(敌方单位) /
//           ourCombat(我方战斗单位) / oursAll(我方单位)
//   scope = all(不限阵营) / enemy(敌方：≠1 且 ≠0) / ours(我方：==1) / 具体阵营号
//   g=    = 可选，限定某个二级分组（龙 / 兵种名…），中文经 encodeURIComponent
// 只传字符串，因为要塞进 onclick 属性里。
function npcfixIsMonsterEntity(e) { return (e.className || '').indexOf('Monster') === 0; }

// 「是战舰」判据：必须与 npcfixCombatClassify 完全一致 ——
// 类名含 Ship **且** stuff_id 在战舰白名单内（货船 706001 / 其它船型不计）。
// 只判类名会让一键清除连商船一起清掉，和盒子里显示的东西对不上。
function npcfixIsWarship(e) {
  return (e.className || '').indexOf('Ship') >= 0 && NPCFIX_WAR_SHIPS.indexOf(e.stuffId) >= 0;
}

// 人形单位（与 npcfixCombatClassify 一致：排除怪物与所有船）
function npcfixIsHumanEntity(e) {
  const cn = e.className || '';
  return npcfixIsHumanUnit(cn) && cn.indexOf('Monster') !== 0 && cn.indexOf('Ship') < 0;
}

// 我方「战斗」单位：阵营1 且真带兵种（平民在盒子1，不算这里）
function npcfixIsOurCombatUnit(e) { return npcfixIsHumanEntity(e) && (e.soldierTypeId || 0) > 0; }

// 一切战斗单位（小人 + 怪物 + 战舰）——「毁灭吧！！！」用
function npcfixIsAnyUnit(e) {
  return npcfixIsHumanEntity(e) || npcfixIsMonsterEntity(e) || npcfixIsWarship(e);
}
// 我方一切战斗单位 —— 分区级「战斗力×10」用
function npcfixIsOurUnit(e) {
  return npcfixIsOurCombatUnit(e) || npcfixIsMonsterEntity(e) || npcfixIsWarship(e);
}

// ===== 设施/建筑（盒子3） =====
// ⚠ 用 StartsWith 而不是 Contains：实机全部设施类名都是 Facility* 开头
// （FacilityWall / FacilityBed / FacilityStorageBarn…），"包含"匹配没有任何收益，
// 反而可能吃进未知类名 —— 与 IsShipEntity 的教训同一性质。
function npcfixIsFacilityEntity(e) { return (e.className || '').indexOf('Facility') === 0; }

// ===== 掉落物（盒子5）：StuffOnMap*（StuffOnMapFaeces 粪便也在内），判据与渲染/清除三处共用 =====
function npcfixIsStuffOnMapEntity(e) { return (e.className || '').indexOf('StuffOnMap') === 0; }

// ===== 动物（盒子4）：活体 Animal + 尸体 AnimalDeadBody，判据与渲染/清除三处共用 =====
function npcfixIsAnimalEntity(e) {
  const cn = e.className || '';
  return cn === 'Animal' || cn === 'AnimalDeadBody';
}

const NPCFIX_CLEAR_KINDS = {
  monster: { name: '怪物', test: npcfixIsMonsterEntity },
  humanoid: { name: '小人', test: npcfixIsHumanEntity },
  ship: { name: '战舰', test: npcfixIsWarship },
  enemyAll: { name: '敌方单位', test: npcfixIsAnyUnit },
  ourCombat: { name: '我方战斗单位', test: npcfixIsOurCombatUnit },
  oursAll: { name: '我方单位', test: npcfixIsOurUnit },
  // 建筑：tail 定制确认框尾行（建筑不会"分裂"，写真实风险）；warn 追加高危提示行
  facility: {
    name: '建筑', test: npcfixIsFacilityEntity,
    tail: '（手动拆除流程：不返还材料与物品，删除后不可恢复）',
    warn: '箱子 / 仓库 / 床类建筑删除后，里面的物品与住宿功能一并消失！',
  },
  // 掉落物：route='stuff' —— 按 guid 走 /api/editor/stuff/batch（游戏自己的
  // MapStuffHelper.DestroyStuffOnMap），清注册表 + 取消 NPC 拾取任务；
  // 通用 destroy/batch 按 ptrHash 只销毁 GO，不清注册表，所以这里不能复用。
  stuff: {
    name: '掉落物', test: npcfixIsStuffOnMapEntity, route: 'stuff',
    keyField: 'guid', whereLabel: '地图上全部',
    tail: '（物品将直接消失，不会进背包 / 仓库 —— 游戏没有"强制拾取"语义）',
  },
  // 动物：route='animal' —— 活体走 AnimalHelper.DestroyAnimal、尸体走 DestroyElement
  // （通用 destroy/batch 不清 AnimalHelper 注册表，NPC 牧场任务会对着空气跑）。
  // 静默移除不掉肉：确认框 tail 写明。
  animal: {
    name: '动物', test: npcfixIsAnimalEntity, route: 'animal', whereLabel: '地图上全部',
    tail: '（静默移除：不掉肉、不生成尸体，删除后不可恢复）',
  },
};

// 二级分组的组名（必须与渲染分组时用的规则一致，否则 :g= 匹配不上）
function npcfixGroupKeyOf(kind, e) {
  if (kind === 'ourCombat') return e.soldierTypeName || '未知兵种';
  // 建筑：后端 name = DataTables.ItemName(stuff_id) 的中文名（铁墙 / 小床 / 大箱子…）
  if (kind === 'facility') return e.name || e.className || '未知建筑';
  // 掉落物：同走后端中文名（白银 / 银币 / 粪便…）
  if (kind === 'stuff') return e.name || e.className || '未知物品';
  // 动物：活体按种类（猪 / 鹿…），尸体统一一组
  if (kind === 'animal') return e.className === 'AnimalDeadBody' ? '动物尸体' : (e.name || '未知动物');
  const cn = e.className || '';
  if (cn.indexOf('Dragon') >= 0) return '龙';
  return e.name || cn || '未知怪物';
}

// 解析 spec → { def, where, pick }；spec 非法返回 null
function npcfixParseSpec(spec) {
  const parts = String(spec).split(':');
  const def = NPCFIX_CLEAR_KINDS[parts[0]];
  if (!def) return null;
  const kind = parts[0];
  const scope = parts[1];
  const group = (parts[2] && parts[2].indexOf('g=') === 0) ? decodeURIComponent(parts[2].slice(2)) : null;
  const test = e => {
    if (!def.test(e)) return false;
    if (group !== null && npcfixGroupKeyOf(kind, e) !== group) return false;
    // scope='enemy' 要同时排除阵营 0：阵营0 已被分类函数隐藏，
    // 操作范围必须跟着"显示范围"走，否则会动到盒子里没显示的东西（历史坑）。
    const kid = npcfixUnitKingdom(e);
    if (scope === 'all') return true;
    if (scope === 'enemy') return kid !== 1 && kid !== 0;
    if (scope === 'ours') return kid === 1;
    return kid === Number(scope);
  };
  // whereLabel：无阵营语义的种类（掉落物）在 scope='all' 时用"地图上全部"，别写"全部阵营"
  const where = def.whereLabel && scope === 'all' ? def.whereLabel
    : scope === 'all' ? '全部阵营'
    : scope === 'enemy' ? '敌方全部阵营'
      : scope === 'ours' ? '我方' : ('阵营' + scope);
  // keyField='guid' 的种类（掉落物）按 guid 收集，走专属接口；其余按 ptrHash。
  // test 一并返回：拾取进国库按 ptrHash 收集（后端要拿实体读 count），不复用 pick。
  const key = def.keyField || 'ptrHash';
  return { def: def, where: where, pick: () => entityEditorData.filter(test).map(e => e[key]), test: test };
}

// 拼 spec（按钮工厂共用）
function npcfixSpecOf(kind, scope, group) {
  return kind + ':' + scope + (group ? ':g=' + encodeURIComponent(group) : '');
}

// ---- 按钮工厂：二级卡片标题栏（小） / 一级盒子标题栏（中） / 分区标题条（大） ----
function npcfixMiniBtn(text, color, action) {
  const s = 'padding:2px 8px;' + color + ';color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px';
  return '<span style="margin-left:6px"><button onclick="event.preventDefault();event.stopPropagation();'
    + action + '" style="' + s + '">' + text + '</button></span>';
}
function npcfixBoxBtn(text, color, action) {
  const s = 'padding:2px 10px;' + color + ';color:#fff;border:none;border-radius:4px'
    + ';cursor:pointer;font-size:11px;font-weight:400';
  return '<button onclick="event.preventDefault();event.stopPropagation();' + action + '" style="' + s + '">'
    + text + '</button>';
}
function npcfixSectionBtn(text, color, action, glow) {
  const s = 'padding:5px 14px;' + color + ';color:#fff;border:none;border-radius:6px;cursor:pointer'
    + ';font-size:13px;font-weight:700;letter-spacing:1px' + (glow ? ';box-shadow:0 0 10px rgba(231,76,60,.45)' : '');
  return '<button onclick="event.stopPropagation();' + action + '" style="' + s + '">' + text + '</button>';
}
const NPCFIX_BIFF_COLOR = 'background:var(--success-dark,#27ae60)';
const NPCFIX_NERF_COLOR = 'background:var(--info,#3498db)';
const NPCFIX_CLEAR_COLOR = 'background:var(--danger,#e74c3c)';

function npcfixClearBtn(kind, scope, group) {
  return npcfixMiniBtn('一键清除', NPCFIX_CLEAR_COLOR, 'npcfixClear(\'' + npcfixSpecOf(kind, scope, group) + '\')');
}
function npcfixScaleBtn(kind, scope, factor, group) {
  const pct = factor >= 1 ? ('×' + factor) : ('÷' + Math.round(1 / factor));
  return npcfixMiniBtn('战斗力' + pct, factor >= 1 ? NPCFIX_BIFF_COLOR : NPCFIX_NERF_COLOR,
    'npcfixScale(\'' + npcfixSpecOf(kind, scope, group) + '\',' + factor + ')');
}
function npcfixBoxClearBtn(kind, scope, group) {
  return npcfixBoxBtn('一键清除', NPCFIX_CLEAR_COLOR, 'npcfixClear(\'' + npcfixSpecOf(kind, scope, group) + '\')');
}
function npcfixBoxScaleBtn(kind, scope, factor, group) {
  const pct = factor >= 1 ? ('×' + factor) : ('÷' + Math.round(1 / factor));
  return npcfixBoxBtn('战斗力' + pct, factor >= 1 ? NPCFIX_BIFF_COLOR : NPCFIX_NERF_COLOR,
    'npcfixScale(\'' + npcfixSpecOf(kind, scope, group) + '\',' + factor + ')');
}

// 大分区标题条：「我方单位」绿 / 「敌方单位」红，右侧可挂按钮
function npcfixSectionTitle(label, color, trailingHtml) {
  return '<div style="display:flex;align-items:center;gap:8px;margin:14px 0 8px">'
    + '<span style="font-size:14px;font-weight:700;color:' + color + '">' + esc(label) + '</span>'
    + '<span style="flex:1;height:1px;background:' + color + ';opacity:.35"></span>'
    + (trailingHtml || '') + '</div>';
}

// 「毁灭吧！！！」：一键清除全部敌方战斗单位（小人 + 怪物 + 战舰）
function npcfixDestroyAllBtn() {
  return npcfixSectionBtn('毁灭吧！！！', NPCFIX_CLEAR_COLOR, 'npcfixClear(\'enemyAll:enemy\')', true);
}

// ===== 定期清理（独立一行盒子）：地图上出现敌方单位就慢慢清掉，不卡帧 =====
// 节奏固定为「每 1 秒一批」，每批数量用户可调（默认 20，最大 100）——时间不能改，数量能改。
let npcfixSafeOn = false;
let npcfixSafeTimer = null;
let npcfixSafeDone = new Set();
let npcfixSafeTicks = 0;
let npcfixSafeCount = 0;
let npcfixScanning = false;          // 正在全量重扫（扫描期间别再发销毁，省得大批 not found）
let npcfixSafeSaveLoads = 0;         // 开启那一刻的"读档成功次数"；变了 = 读过档 → 自动关闭
const NPCFIX_SAFE_INTERVAL = 1000;   // 拍间隔 ms（固定 1 秒，不可改）
const NPCFIX_SAFE_BATCH_DEFAULT = 20;
const NPCFIX_SAFE_BATCH_MAX = 100;
const NPCFIX_SAFE_RESCAN_EVERY = 12; // 每 N 拍重扫一次（拿新刷出来的敌人）

// 包一层重扫：标记"正在扫描"。分片扫描要好几秒，
// 这期间让定期清理先别发销毁请求（名单会整体替换，中途发只是白跑一趟）。
// ⚠ 故意不走 entityEditorScan()：那会 toast + renderContent()，
//   后台重扫会把整个面板重渲染，展开的字段卡片和滚动位置全丢。
let npcfixLastScanAt = 0;   // 上次实体扫描成功时间（切视图的后台静默刷新按 60s 节流）
async function npcfixScan() {
  npcfixScanning = true;
  try {
    await fetch('/api/editor/scan', { method: 'POST' });
    await fetchEntityEditorData();
    npcfixLastScanAt = Date.now();
  } finally { npcfixScanning = false; }
}

// 每秒清除数量：localStorage 记忆 + 夹在 1..100
function npcfixSafeGetBatch() {
  const v = parseInt(localStorage.getItem('chesteditor.cleanBatch') || '', 10);
  if (isNaN(v)) return NPCFIX_SAFE_BATCH_DEFAULT;
  return Math.min(NPCFIX_SAFE_BATCH_MAX, Math.max(1, v));
}
function npcfixSafeBatchChange(inp) {
  let v = parseInt(inp.value, 10);
  if (isNaN(v)) v = NPCFIX_SAFE_BATCH_DEFAULT;
  v = Math.min(NPCFIX_SAFE_BATCH_MAX, Math.max(1, v));
  inp.value = v;
  localStorage.setItem('chesteditor.cleanBatch', String(v));
  toast('定期清理：每秒清除 ' + v + ' 个');
}

function npcfixSafeStateText() {
  return npcfixSafeOn ? ('开（已清 ' + npcfixSafeCount + '）') : '关';
}
// 独立一行盒子：标题 + 状态 + 每秒数量输入 + 开关
function npcfixSafeBox() {
  const on = npcfixSafeOn;
  let h = '<div style="margin:0 0 12px;padding:10px 14px;border:1px solid var(--border);'
    + 'border-radius:var(--radius-sm);background:var(--bg-card);display:flex;align-items:center;gap:10px;flex-wrap:wrap">';
  h += '<span style="font-size:13px;font-weight:700;color:var(--text-primary)">&#x1F5D1; 定期清理</span>';
  h += '<span id="npcfixSafeState" style="font-size:12px;color:'
    + (on ? 'var(--success-dark,#27ae60)' : 'var(--text-muted)') + '">' + esc(npcfixSafeStateText()) + '</span>';
  h += '<span style="flex:1"></span>';
  h += '<span style="font-size:12px;color:var(--text-muted)">每秒清除</span>';
  h += '<input id="npcfixSafeBatch" type="number" min="1" max="' + NPCFIX_SAFE_BATCH_MAX + '" value="'
    + npcfixSafeGetBatch() + '" onchange="npcfixSafeBatchChange(this)" '
    + 'style="width:64px;padding:3px 6px;background:var(--bg-input);color:var(--text-primary)'
    + ';border:1px solid var(--border);border-radius:4px;font-size:12px">';
  h += '<span style="font-size:12px;color:var(--text-muted)">个敌方单位</span>';
  h += '<button onclick="event.stopPropagation();npcfixSafeToggle()" style="padding:5px 16px;'
    + (on ? 'background:var(--success-dark,#27ae60);color:#fff' : 'background:var(--bg-input);color:var(--text-secondary)')
    + ';border:1px solid var(--border);border-radius:6px;cursor:pointer;font-size:13px;font-weight:700">'
    + (on ? '关闭' : '开启') + '</button>';
  h += '<span style="font-size:11px;color:var(--text-muted)">只清敌方单位 · 读取存档时自动关闭</span>';
  h += '</div>';
  return h;
}
// 兼容旧调用：状态文本变了就就地更新（避免整盒重渲染）
function npcfixSafeUpdateBtn() {
  const st = document.getElementById('npcfixSafeState');
  if (st) {
    st.textContent = npcfixSafeStateText();
    st.style.color = npcfixSafeOn ? 'var(--success-dark,#27ae60)' : 'var(--text-muted)';
  }
}
// 读后端状态（主线程上判断"进没进存档 / 在不在读档 / 在不在扫描"）
async function npcfixFetchState() {
  try { return await fetch('/api/editor/state').then(r => r.json()); }
  catch (e) { return null; }
}
// 状态不对就自动关：返回 true 表示已自动关闭
function npcfixSafeCheckState(st) {
  if (!st) return false;                                  // 连不上就不动，别误关
  if (st.loading || (typeof st.saveLoads === 'number' && st.saveLoads !== npcfixSafeSaveLoads)) {
    npcfixSafeAutoOff('检测到读取存档，定期清理已自动关闭');
    return true;
  }
  if (st.inSave === false) {
    npcfixSafeAutoOff('已退出存档，定期清理已自动关闭');
    return true;
  }
  return false;
}
function npcfixSafeAutoOff(reason) {
  npcfixSafeOn = false;
  if (npcfixSafeTimer) { clearTimeout(npcfixSafeTimer); npcfixSafeTimer = null; }
  toast(reason + '（本次累计清除 ' + npcfixSafeCount + '）', true);
  npcfixSafeUpdateBtn();
}
async function npcfixSafeToggle() {
  if (npcfixSafeOn) {          // 关闭
    npcfixSafeOn = false;
    if (npcfixSafeTimer) { clearTimeout(npcfixSafeTimer); npcfixSafeTimer = null; }
    toast('定期清理已关闭（累计清除 ' + npcfixSafeCount + '）');
    npcfixSafeUpdateBtn();
    return;
  }
  // 开启前先问后端"现在能不能开"
  const st = await npcfixFetchState();
  if (st === null) { toast('开启失败：无法连接游戏接口', true); return; }
  if (st.inSave === false) { toast('开启失败：未进入存档', true); return; }
  if (st.loading) { toast('开启失败：正在读取存档', true); return; }
  if (st.scanning) { toast('开启失败：正在扫描，请稍后再试', true); return; }

  npcfixSafeOn = true;
  npcfixSafeCount = 0;
  npcfixSafeTicks = 0;
  npcfixSafeDone = new Set();
  npcfixSafeSaveLoads = (typeof st.saveLoads === 'number') ? st.saveLoads : 0;
  toast('定期清理已开启（每秒清除 ' + npcfixSafeGetBatch() + ' 个敌方单位）');
  npcfixSafeUpdateBtn();
  npcfixSafeTick();
}

// 一拍：核对游戏状态 → 重扫（偶尔）→ 挑最多「每秒数量」个敌方单位 → 批量销毁（后端按帧摊开）
async function npcfixSafeTick() {
  if (!npcfixSafeOn) return;
  try {
    // 每拍先核对状态：读档了 / 退出存档了 → 自动关闭。
    // ⚠ 放在"有没有敌人"之前 —— 地图上没敌人时也得能感知到读档。
    if (npcfixSafeCheckState(await npcfixFetchState())) return;
    if (npcfixSafeTicks % NPCFIX_SAFE_RESCAN_EVERY === 0) {
      await npcfixScan();
      npcfixSafeDone = new Set();   // 重扫后数据是新的一份，旧的"已清"记录作废
    }
    if (!npcfixScanning) {
      const targets = entityEditorData
        .filter(e => !npcfixSafeDone.has(e.ptrHash) && npcfixIsAnyUnit(e))
        .filter(e => { const k = npcfixUnitKingdom(e); return k !== 1 && k !== 0; })
        .slice(0, npcfixSafeGetBatch())
        .map(e => e.ptrHash);
      if (targets.length > 0) {
        const r = await destroyBatch(targets);
        for (const h of targets) npcfixSafeDone.add(h);
        npcfixSafeCount += (r && r.destroyed) || 0;
      }
    }
  } catch (e) { /* 单拍失败不中断整个模式 */ }
  npcfixSafeTicks++;
  npcfixSafeUpdateBtn();
  if (npcfixSafeOn) npcfixSafeTimer = setTimeout(npcfixSafeTick, NPCFIX_SAFE_INTERVAL);
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
  html += '<span style="color:var(--text-muted);font-size:13px">我方单位 / 敌方单位 · 战斗单位、怪物、舰船（区分敌我，商船与阵营0不计）</span>';
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
  const GREEN = 'var(--success-dark, #27ae60)';
  let oursHtml = '';
  let enemyHtml = '';

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
      inner += npcfixEntityGroupCard('我方 · ' + name, GREEN, '&#x2694;', prof[name], { combat: true },
        npcfixScaleBtn('ourCombat', 1, 10, name));
    inner += '</div>';
    oursHtml += htmlDetailsGroup('我方战斗单位 (' + c.ours.length + ')', GREEN, '&#x2694;',
      c.ours.length + ' 个', inner, null, npcfixBoxBtns() + npcfixBoxScaleBtn('ourCombat', 1, 10));
  }

  // ===== 我方-怪物（阵营1 地界刷出的怪物：蚁巢工蚁 / 龙…，按种类二级分组） =====
  // 主题色与「我方战斗单位」一致用绿：它同属"我方单位"。
  if (c.monstersOurs.length > 0) {
    // 分组名用中文：后端 name = DataTables.ItemName(stuff_id)，怪物就是「5级工蚁 / 5级骷髅战士」；
    // 类名（MonsterAntWorker / Monster3 / Monster23…）只是预制体名，用户看不懂，仅作兜底。
    // 龙单独并成一张卡：每种龙都是个位数、又按「N级X龙」拆开，不合并会铺出十几张卡片，太碎。
    // 二级按钮带上 :g=组名 —— 否则点「龙」这张卡会的按钮会清/强化全部我方怪物。
    const kinds = {};
    for (const e of c.monstersOurs) {
      const cn = e.className || '';
      const k = cn.indexOf('Dragon') >= 0 ? '龙' : (e.name || cn || '未知怪物');
      (kinds[k] = kinds[k] || []).push(e);
    }
    const entries = Object.keys(kinds).sort((a, b) => kinds[b].length - kinds[a].length);
    let inner = '<div class="npcfix-box-body">';
    for (const name of entries) {
      const isDragon = (name === '龙');
      inner += npcfixEntityGroupCard(name, GREEN, isDragon ? '&#x1F409;' : '&#x1F47E;',
        kinds[name], {},
        npcfixScaleBtn('monster', 1, 10, name) + npcfixClearBtn('monster', 1, name));
    }
    inner += '</div>';
    oursHtml += htmlDetailsGroup('我方-怪物 (' + c.monstersOurs.length + ')', GREEN, '&#x1F47E;',
      c.monstersOurs.length + ' 个', inner, null, npcfixBoxBtns() + npcfixBoxScaleBtn('monster', 1, 10));
  }

  // ===== 敌方-小人（按阵营；一级/二级都有一键清除 + 战斗力÷10） =====
  if (Object.keys(c.humanoids).length > 0) {
    const n = npcfixSumKinds(c.humanoids);
    enemyHtml += htmlDetailsGroup('敌方-小人 (' + n + ')', '#c0392b', '&#x1F464;', n + ' 个',
      '<div class="npcfix-box-body">' + npcfixKingdomGroups(c.humanoids, 'var(--text-muted)', '&#x1F464;',
        kid => npcfixClearBtn('humanoid', kid) + npcfixScaleBtn('humanoid', kid, 0.1)) + '</div>',
      null, npcfixBoxBtns() + npcfixBoxClearBtn('humanoid', 'enemy') + npcfixBoxScaleBtn('humanoid', 'enemy', 0.1));
  }

  // ===== 敌方-怪物（按阵营；一级/二级都有一键清除 + 战斗力÷10） =====
  if (Object.keys(c.monstersEnemy).length > 0) {
    const n = npcfixSumKinds(c.monstersEnemy);
    enemyHtml += htmlDetailsGroup('敌方-怪物 (' + n + ')', 'var(--danger, #e74c3c)', '&#x1F47E;', n + ' 个',
      '<div class="npcfix-box-body">' + npcfixKingdomGroups(c.monstersEnemy, 'var(--danger, #e74c3c)', '&#x1F47E;',
        kid => npcfixClearBtn('monster', kid) + npcfixScaleBtn('monster', kid, 0.1)) + '</div>',
      null, npcfixBoxBtns() + npcfixBoxClearBtn('monster', 'enemy') + npcfixBoxScaleBtn('monster', 'enemy', 0.1));
  }

  // ===== 舰船：**我方舰队独立成一个盒子**，放进「我方单位」区（与敌方舰队彻底分开） =====
  if (c.shipsOurs.length > 0)
    oursHtml += htmlDetailsGroup('我方舰队 (' + c.shipsOurs.length + ')', GREEN, '&#x1F6A2;',
      c.shipsOurs.length + ' 个',
      '<div class="npcfix-box-body">' + npcfixEntityGrid(c.shipsOurs, {}) + '</div>',
      null, npcfixBoxBtns() + npcfixBoxScaleBtn('ship', 1, 10));

  // ===== 敌方舰队：留在「敌方单位」区，按阵营二级分组；一级/二级都有一键清除 + 战斗力÷10 =====
  // ⚠ 一级按钮的 scope 用 'enemy'（不再用 'all'）：盒子现在只含敌方舰队，
  //   操作范围必须等于显示范围，不然会连我方舰队一起清掉。
  if (Object.keys(c.shipsEnemy).length > 0) {
    const n = npcfixSumKinds(c.shipsEnemy);
    enemyHtml += htmlDetailsGroup('敌方舰队 (' + n + ')', '#3498db', '&#x1F6A2;', n + ' 个',
      '<div class="npcfix-box-body">' + npcfixKingdomGroups(c.shipsEnemy, '#3498db', '&#x1F6A2;',
        kid => npcfixClearBtn('ship', kid) + npcfixScaleBtn('ship', kid, 0.1)) + '</div>',
      null, npcfixBoxBtns() + npcfixBoxClearBtn('ship', 'enemy') + npcfixBoxScaleBtn('ship', 'enemy', 0.1));
  }

  // ===== 组装：两条分区线**一直显示**（没有单位也要在），右侧挂分区级按钮 =====
  //   我方单位 ── [战斗力×10]
  //   敌方单位 ── [战斗力÷10] [毁灭吧！！！]
  //   （定期清理独立成一行盒子，放在「敌方单位」横线下）
  const nOurs = c.ours.length + c.monstersOurs.length + c.shipsOurs.length;
  const nEnemy = npcfixSumKinds(c.humanoids) + npcfixSumKinds(c.monstersEnemy) + npcfixSumKinds(c.shipsEnemy);
  let h = '';
  h += npcfixSectionTitle('我方单位', GREEN,
    npcfixSectionBtn('战斗力×10', NPCFIX_BIFF_COLOR, 'npcfixScale(\'oursAll:ours\',10)'))
    + (oursHtml || npcfixEmptyHint('暂无我方单位（' + nOurs + ' 个）'));
  h += npcfixSectionTitle('敌方单位', 'var(--danger, #e74c3c)',
    npcfixSectionBtn('战斗力÷10', NPCFIX_NERF_COLOR, 'npcfixScale(\'enemyAll:enemy\',0.1)')
    + npcfixDestroyAllBtn())
    + npcfixSafeBox()                                    // ← 定期清理：紧跟「敌方单位」横线之下
    + (enemyHtml || npcfixEmptyHint('暂无敌方单位（' + nEnemy + ' 个）'));

  body.innerHTML = h;
}

// 空分区占位（分区线要一直显示，没内容时给一句灰字）
function npcfixEmptyHint(text) {
  return '<div style="padding:12px 14px;color:var(--text-muted);font-size:12px;'
    + 'border:1px dashed var(--border);border-radius:var(--radius-sm)">' + esc(text) + '</div>';
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

// 一键清除的单批数量：分批提交，避免一帧内回收成百上千个单位（卡帧 + 资源峰值）
const NPCFIX_KILL_CHUNK = 120;

// 分批销毁：每批之间让出一帧；返回 { destroyed, failed }
async function npcfixKillInChunks(hashes) {
  let destroyed = 0, failed = 0;
  for (let i = 0; i < hashes.length; i += NPCFIX_KILL_CHUNK) {
    const part = hashes.slice(i, i + NPCFIX_KILL_CHUNK);
    try {
      const r = await destroyBatch(part);
      destroyed += (r && r.destroyed) || 0;
      failed += (r && r.failed) || 0;
    } catch (e) { failed += part.length; }
    await new Promise(r => setTimeout(r, 60));
  }
  return { destroyed: destroyed, failed: failed };
}

// 动物专用：按 ptrHash 走 /api/editor/animal/batch（活体 OnBeDestroy 完整流程 /
// 尸体 MapStuffHelper.DestroyElement，见 AnimalService）。后端已按帧摊开。
async function npcfixKillAnimalInChunks(ptrHashes) {
  let destroyed = 0, failed = 0;
  for (let i = 0; i < ptrHashes.length; i += NPCFIX_KILL_CHUNK) {
    const part = ptrHashes.slice(i, i + NPCFIX_KILL_CHUNK);
    try {
      const r = await fetch('/api/editor/animal/batch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ptrHashes: part, mode: 'onBeDestroy' })
      }).then(x => x.json());
      destroyed += (r && r.destroyed) || 0;
      failed += (r && r.failed) || 0;
    } catch (e) { failed += part.length; }
    await new Promise(r => setTimeout(r, 60));
  }
  return { destroyed: destroyed, failed: failed };
}
// 掉落物专用：按 guid 走 /api/editor/stuff/batch（游戏自己的 MapStuffHelper.DestroyStuffOnMap，
// 清注册表 + 取消 NPC 拾取任务）。后端已按帧摊开，这里照旧分批提交（少几次往返）。
// 返回同构 {destroyed, failed}（failed = guid 已不在注册表，多半被 NPC 捡走了）。
async function npcfixKillStuffInChunks(guids) {
  let destroyed = 0, missing = 0;
  for (let i = 0; i < guids.length; i += NPCFIX_KILL_CHUNK) {
    const part = guids.slice(i, i + NPCFIX_KILL_CHUNK);
    try {
      const r = await fetch('/api/editor/stuff/batch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ guids: part })
      }).then(x => x.json());
      destroyed += (r && r.destroyed) || 0;
      missing += (r && r.missing) || 0;
    } catch (e) { missing += part.length; }
    await new Promise(r => setTimeout(r, 60));
  }
  return { destroyed: destroyed, failed: missing };
}

// 清完/改完把面板刷新一下：否则界面上还挂着已经销毁的卡片，用户得再点一次
// 「重新扫描」才能看到结果 —— 那又多扫一遍全量。
function npcfixRefreshView() {
  if (npcfixView === 'box2') renderNpcfixBox2(false);
  if (npcfixView === 'box3') renderNpcfixBox3(false);
  if (npcfixView === 'box4') renderNpcfixBox4(false);
  if (npcfixView === 'box5') renderNpcfixBox5(false);
}

// 一键清除（spec 见 npcfixParseSpec）：
// 按范围收集 → 分批销毁 → 等1秒 → 重扫 → 再清一遍（覆盖分裂怪）→ 舰船落岸船员收尾。
// 后端对 Monster* 走"静默移除"（skip_show_dead_anim + DeadOnBattle(null,false)），不播死亡动画、不掉东西。
async function npcfixClear(spec) {
  const p = npcfixParseSpec(spec);
  if (!p) return;
  // 先算数量：确认框要按数量决定是否追加"可能卡顿"提示
  const hashes = p.pick();
  if (hashes.length === 0) { toast('没有可清除的' + p.def.name, true); return; }
  // tail 定制尾行（默认文案对"建筑"不适用——建筑不会分裂，写的是拆除的真实风险）
  let msg = '确定清除' + p.where + '的 ' + hashes.length + ' 个' + p.def.name + '？\n'
    + (p.def.tail || '（将分批执行2遍，覆盖分裂怪）');
  if (p.def.warn) msg += '\n\n⚠ ' + p.def.warn;
  if (!confirm(msg)) return;

  // 记住"动刀前"都有谁：舰船被摧毁时游戏会把船员丢上岸变成士兵，
  // 靠这个集合才能认出哪些敌方小人是刚刷出来的。
  const before = new Set(entityEditorData.map(e => e.ptrHash));

  toast('清除中...', false);
  // 按种类路由销毁接口：
  //   route='stuff'  → /api/editor/stuff/batch（guid，游戏注册表删除）
  //   route='animal' → /api/editor/animal/batch（AnimalHelper/MapStuffHelper）
  //   默认           → destroy/batch(ptrHash)
  const kill = p.def.route === 'stuff' ? npcfixKillStuffInChunks
    : p.def.route === 'animal' ? npcfixKillAnimalInChunks
      : npcfixKillInChunks;
  const d1 = await kill(hashes);
  await new Promise(r => setTimeout(r, 1000));
  // 重扫拿到分裂/新刷的目标，再清一遍（掉落物：清掉NPC来不及捡而刚掉的）
  await npcfixScan();
  const hashes2 = p.pick();
  let d2 = { destroyed: 0 };
  if (hashes2.length > 0) d2 = await kill(hashes2);
  await npcfixScan();

  // 舰船专属收尾：ShipHelper.DestroyShip(船, false) 会把船员按 sailor_count 生成士兵丢在船的位置
  // （反编译 ShipHelper.DestroyShip 最后那段 do/while + SoldierHelper.CreateSoldier）。
  // 这些士兵不在"动刀前"的名单里，是清完才冒出来的 —— 就是要二次清除的对象。
  let sailor = { destroyed: 0 };
  const kindName = String(spec).split(':')[0];
  if (kindName === 'ship' || kindName === 'enemyAll') {
    for (let round = 0; round < 2; round++) {
      const fresh = entityEditorData
        .filter(e => !before.has(e.ptrHash) && npcfixIsHumanEntity(e))
        .filter(e => { const k = npcfixUnitKingdom(e); return k !== 1 && k !== 0; })
        .map(e => e.ptrHash);
      if (fresh.length === 0) break;
      const r = await npcfixKillInChunks(fresh);
      sailor.destroyed += (r.destroyed || 0);
      await new Promise(rs => setTimeout(rs, 600));
      await npcfixScan();
    }
  }

  // 掉落物：missing = 请求时 guid 已不在注册表（多半被 NPC 抢先捡走了），如实告知
  const gone = (d1.failed || 0) + (d2.failed || 0);
  toast('清除完成: 首轮' + (d1.destroyed || 0) + ' + 二轮' + (d2.destroyed || 0)
    + (gone > 0 ? ' + 已消失' + gone : '')
    + (sailor.destroyed > 0 ? ' + 落岸船员' + sailor.destroyed : ''));
  npcfixRefreshView();
}

// 战斗力缩放（spec 见 npcfixParseSpec；factor=10 → ×10，factor=0.1 → ÷10）
// 只动 攻击/血量/魔法攻击 共 6 个字段，由后端 /api/editor/scale/batch 在一次主线程任务里写完，
// 所以这里也能分批（每批 40 个实体）而不会把主线程卡死。
async function npcfixScale(spec, factor) {
  const p = npcfixParseSpec(spec);
  if (!p) return;
  const hashes = p.pick();
  if (hashes.length === 0) { toast('没有可改战斗力的' + p.def.name, true); return; }
  const pct = factor >= 1 ? ('×' + factor) : ('÷' + Math.round(1 / factor));
  let msg = '确定把' + p.where + '的 ' + hashes.length + ' 个' + p.def.name + '战斗力 ' + pct + '？'
    + '\n（攻击 / 血量 / 魔法攻击，共 6 个字段）';
  if (!confirm(msg)) return;
  toast('战斗力 ' + pct + ' 中...', false);
  let entities = 0, fields = 0;
  for (let i = 0; i < hashes.length; i += NPCFIX_KILL_CHUNK) {
    const part = hashes.slice(i, i + NPCFIX_KILL_CHUNK);
    try {
      const r = await fetch('/api/editor/scale/batch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ptrHashes: part, factor: factor })
      }).then(x => x.json());
      entities += (r && r.entities) || 0;
      fields += (r && r.fields) || 0;
    } catch (e) { /* 单批失败不中断 */ }
    await new Promise(rs => setTimeout(rs, 40));
  }
  toast('战斗力 ' + pct + ' 完成: ' + entities + ' 个单位 / ' + fields + ' 个字段');
}
// ===== 盒子3 建筑物 =====
// 数据源：实体扫描（/api/editor/entities，零后端改动）。实机分布：设施 40+ 种，
// 我方 ~2323 / 敌方 ~58（敌方设施 kingdomId=0 但 hometownKingdomId=102，兜底函数直接可用）。
// ⚠ 设施量大（铁墙 1574 个）—— 每组限量渲染卡片 + 顶部搜索框，别把 DOM 铺爆。
const NPCFIX_BOX3_CARD_LIMIT = 24;   // 每组最多渲染的卡片数，超出显示"还有 N 个"
let npcfixBox3Query = '';            // 搜索词（仅前端过滤已扫描数据，不触发扫描）

// 设施分组卡：与 npcfixEntityGroupCard 的区别 —— 网格限量 + "还有 N 个"提示 + 组级按钮。
// clearSpec/pickupSpec 可选（盒子3 建筑只给清除；盒子5 掉落物清除+拾取进国库都给）。
// 展开状态由 data-g 记录（搜索重绘后恢复，见 npcfixBox3RenderBody）。
function npcfixFacilityGroupCard(label, color, icon, list, clearSpec, pickupSpec) {
  if (!list || list.length === 0) return '';
  const shown = list.slice(0, NPCFIX_BOX3_CARD_LIMIT);
  let h = '<details class="npcfix-group" style="--gc:' + color + '" data-g="' + esc(label) + '">';
  h += '<summary class="npcfix-group-head">';
  h += '<span class="npcfix-chev">&#x25B6;</span>';
  h += '<span>' + icon + '</span><span>' + esc(label) + '</span>';
  h += '<span class="npcfix-count">' + list.length + ' 个</span>';
  if (pickupSpec) h += npcfixMiniBtn('拾取', NPCFIX_BIFF_COLOR, "npcfixPickup('" + pickupSpec + "')");
  if (clearSpec) h += npcfixMiniBtn('一键清除', NPCFIX_CLEAR_COLOR, "npcfixClear('" + clearSpec + "')");
  h += '</summary>';
  h += npcfixEntityGrid(shown, {});
  if (list.length > shown.length)
    h += '<div style="padding:0 12px 10px;font-size:12px;color:var(--text-muted)">…还有 '
      + (list.length - shown.length) + ' 个未显示（用上方搜索框过滤后查看）</div>';
  h += '</details>';
  return h;
}

async function renderNpcfixBox3(forceScan) {
  const el = document.getElementById('content');
  // ⚡ 性能：'auto'（切视图触发的静默刷新）也必须先保证列表渲染出来——
  // renderContent 现在虽然会分发到本函数，但节流命中时仍要靠这里兜底显示缓存。
  // forceScan === 'auto'  → 切视图触发的后台静默刷新（60s 节流，不重建页面）
  // forceScan === true    → 重新扫描按钮：同样后台执行，不再阻塞等待
  // 无缓存                → 必须先扫描才有内容（每次会话仅第一次）
  if (forceScan === 'auto') {
    if (entityEditorData.length === 0) return renderNpcfixBox3(true);   // 无缓存走完整流程
    npcfixBox3RenderBody();                                            // 先保证列表在（秒开）
    if (Date.now() - npcfixLastScanAt < 60000 || npcfixScanning) return;
    npcfixScan().then(() => { if (npcfixView === 'box3') npcfixBox3RenderBody(); });
    return;
  }
  const needScan = forceScan === true || entityEditorData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F3D7; 建筑物</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">我方建筑 / 敌方建筑 · 按建筑名分组（大分组限量显示，用搜索框过滤）</span>';
  html += '<button onclick="renderNpcfixBox3(true)" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<input id="npcfixBox3Search" value="' + esc(npcfixBox3Query) + '"'
    + ' placeholder="搜索建筑名，如：墙 / 床 / 箱（留空显示全部）"'
    + ' oninput="npcfixBox3Query=this.value;npcfixBox3RenderBody()"'
    + ' style="width:300px;padding:6px 10px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  html += '<span id="npcfixBox3Summary" style="color:var(--text-muted);font-size:12px"></span>';
  html += '</div>';
  html += '<div id="npcfixBox3Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">' + (needScan && entityEditorData.length === 0 ? '扫描中...' : '') + '</div>';
  html += '</div>';
  el.innerHTML = html;

  // 有缓存：先把列表画出来（秒开），再视需要后台扫描
  if (entityEditorData.length > 0) npcfixBox3RenderBody();

  if (needScan) {
    // 可见的"扫描中"提示（有缓存时列表先显示旧数据 + 顶部横幅；无缓存时 body 整页扫描中）
    if (entityEditorData.length > 0) {
      const b = document.getElementById('npcfixBox3Body');
      if (b && b.insertAdjacentHTML) b.insertAdjacentHTML('afterbegin',
        '<div style="padding:6px 12px;margin-bottom:8px;background:var(--bg-input);border:1px dashed var(--border);border-radius:6px;font-size:12px;color:var(--text-muted)">⏳ 扫描中，完成后列表自动更新…</div>');
    }
    const sum = document.getElementById('npcfixBox3Summary');
    if (sum) sum.textContent = '扫描中，完成后列表自动刷新…';
    try {
      await fetch('/api/editor/scan', { method: 'POST' });
      await fetchEntityEditorData();
      npcfixLastScanAt = Date.now();
    } catch (e) {
      const b = document.getElementById('npcfixBox3Body');
      if (b && entityEditorData.length === 0) b.innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(String((e && e.message) || e)) + '</div>';
      return;
    }
    if (npcfixView !== 'box3') return;   // 扫描期间用户切走了，别覆盖新视图
    npcfixBox3RenderBody();
  }
}

// 分类 + 分组 + 渲染（搜索框输入时只重绘 body，不重新扫描；重绘保持滚动位置）
function npcfixBox3RenderBody() {
  const body = document.getElementById('npcfixBox3Body');
  if (!body) return;
  const keepScroll = body.scrollTop;
  const GREEN = 'var(--success-dark, #27ae60)';
  const RED = 'var(--danger, #e74c3c)';

  // 分类：判据 StartsWith('Facility')（与清除范围共用同一个判据，历史坑）；
  // 阵营0 隐藏（与盒子2 同规则）
  const ours = {}, enemy = {};
  let nOurs = 0, nEnemy = 0;
  for (const e of entityEditorData) {
    if (!npcfixIsFacilityEntity(e)) continue;
    const kid = npcfixUnitKingdom(e);
    if (kid === 0) continue;
    const k = npcfixGroupKeyOf('facility', e);
    if (kid === 1) { (ours[k] = ours[k] || []).push(e); nOurs++; }
    else { (enemy[k] = enemy[k] || []).push(e); nEnemy++; }
  }

  const sum = document.getElementById('npcfixBox3Summary');
  if (sum) sum.textContent = '共 ' + (nOurs + nEnemy) + ' 座（我方 ' + nOurs + ' · 敌方 ' + nEnemy + '）';

  // 搜索：组名命中 → 显示整组（仍限量）；组名不中 → 卡片名（带编号的设施名）命中才显示
  const q = (npcfixBox3Query || '').trim().toLowerCase();
  const renderGroups = (map, color, icon, scope) => {
    const names = Object.keys(map).sort((a, b) => map[b].length - map[a].length);
    let h = '';
    for (const name of names) {
      let list = map[name];
      const groupHit = !q || name.toLowerCase().indexOf(q) >= 0;
      if (!groupHit) {
        list = list.filter(e => String(e.stuffNameWithIdIndex || e.name || '').toLowerCase().indexOf(q) >= 0);
        if (list.length === 0) continue;
      }
      h += npcfixFacilityGroupCard(name, color, icon, list, npcfixSpecOf('facility', scope, name));
    }
    return h;
  };

  // 记住展开的组（搜索重绘后恢复，不然每敲一个字全收起）
  const wasOpen = [];
  body.querySelectorAll('details.npcfix-group[open]').forEach(d => { if (d.dataset && d.dataset.g) wasOpen.push(d.dataset.g); });

  let h = '';
  // 我方建筑：不挂分区级一键清除 —— 1574 堵墙误触一次就没了，组级清除足够
  h += npcfixSectionTitle('我方建筑', GREEN)
    + (renderGroups(ours, GREEN, '&#x1F3E0;', 'ours') || npcfixEmptyHint('暂无我方建筑（' + nOurs + ' 个）'));
  // 敌方建筑：分区级一键清除（清打城残墙 / 大炮）；每组也带
  h += npcfixSectionTitle('敌方建筑', RED, npcfixBoxClearBtn('facility', 'enemy'))
    + (renderGroups(enemy, RED, '&#x1F3F0;', 'enemy') || npcfixEmptyHint('暂无敌方建筑（' + nEnemy + ' 个）'));

  body.innerHTML = h;
  if (wasOpen.length > 0)
    body.querySelectorAll('details.npcfix-group').forEach(d => {
      if (d.dataset && wasOpen.indexOf(d.dataset.g) >= 0) d.open = true;
    });
  body.scrollTop = keepScroll;   // 重绘后保持滚动位置（后台扫描刷新不惊动阅读）
}

// ===== 盒子5 掉落物 =====
// 数据源：实体扫描（/api/editor/entities）。实机掉落物 = StuffOnMap*（打怪/采集后掉在地上的物品堆），
// 实测 2495 堆：白银 637 / 银币 607 / 蓝宝石碎片 507 / 粪便 85 ……
// ⚠ 物品无阵营语义（73 个粪便三个阵营字段全 0 是常态，不是脏数据）——
//   所以这里【不做】盒子2/3 的"阵营0 隐藏"，也不分敌我，单列表按物品名分组。
// 删除走 /api/editor/stuff/batch（游戏自己的 MapStuffHelper.DestroyStuffOnMap，
// 清注册表 + 取消 NPC 拾取任务）；物品直接消失、不进背包（源码核实，游戏没有强制拾取语义）。

async function renderNpcfixBox5(forceScan) {
  const el = document.getElementById('content');
  // ⚡ 性能：'auto'（切视图触发的静默刷新）也必须先保证列表渲染出来（兜底显示缓存）
  if (forceScan === 'auto') {
    if (entityEditorData.length === 0) return renderNpcfixBox5(true);
    npcfixBox5RenderBody();
    if (Date.now() - npcfixLastScanAt < 60000 || npcfixScanning) return;
    npcfixScan().then(() => { if (npcfixView === 'box5') npcfixBox5RenderBody(); });
    return;
  }
  const needScan = forceScan === true || entityEditorData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F4B0; 掉落物</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">掉在地图上的物品堆 · 按物品名分组（大分组限量显示，用搜索框过滤）</span>';
  html += npcfixBoxBtn('拾取', NPCFIX_BIFF_COLOR, "npcfixPickup('stuff:all')")
    + npcfixBoxClearBtn('stuff', 'all');
  html += '<button onclick="renderNpcfixBox5(true)" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div style="display:flex;align-items:center;gap:10px;margin-bottom:12px;flex-shrink:0;flex-wrap:wrap">';
  html += '<button id="npcfixAutoPickBtn" onclick="npcfixAutoPickToggle()"'
    + ' style="padding:5px 14px;background:var(--bg-input);color:var(--text-secondary);border:1px solid var(--border);border-radius:6px;cursor:pointer;font-size:13px;font-weight:700">'
    + npcfixAutoPickBtnText() + '</button>';
  html += '<span style="font-size:13px;color:var(--text-muted)">每隔</span>';
  html += '<input type="number" min="' + NPCFIX_AUTO_PICK_MIN + '" max="' + NPCFIX_AUTO_PICK_MAX + '"'
    + ' value="' + npcfixAutoPickGetInterval() + '" onchange="npcfixAutoPickIntervalChange(this)"'
    + ' style="width:64px;padding:5px 8px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  html += '<span style="font-size:13px;color:var(--text-muted)">秒拾取到</span>';
  html += npcfixAutoPickTargetSelectHtml();
  html += '<span style="font-size:11px;color:var(--text-muted)">间隔 5~60 秒 · 读取存档时自动关闭</span>';
  html += '</div>';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<input id="npcfixBox5Search" value="' + esc(npcfixBox5Query) + '"'
    + ' placeholder="搜索物品名，如：银 / 宝石 / 肉（留空显示全部）"'
    + ' oninput="npcfixBox5Query=this.value;npcfixBox5RenderBody()"'
    + ' style="width:300px;padding:6px 10px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  html += '<span id="npcfixBox5Summary" style="color:var(--text-muted);font-size:12px"></span>';
  html += '</div>';
  html += '<div id="npcfixBox5Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">' + (needScan && entityEditorData.length === 0 ? '扫描中...' : '') + '</div>';
  html += '</div>';
  el.innerHTML = html;

  // 有缓存：先把列表画出来（秒开），再视需要后台扫描
  if (entityEditorData.length > 0) npcfixBox5RenderBody();

  if (needScan) {
    if (entityEditorData.length > 0) {
      const b = document.getElementById('npcfixBox5Body');
      if (b && b.insertAdjacentHTML) b.insertAdjacentHTML('afterbegin',
        '<div style="padding:6px 12px;margin-bottom:8px;background:var(--bg-input);border:1px dashed var(--border);border-radius:6px;font-size:12px;color:var(--text-muted)">⏳ 扫描中，完成后列表自动更新…</div>');
    }
    const sum = document.getElementById('npcfixBox5Summary');
    if (sum) sum.textContent = '扫描中，完成后列表自动刷新…';
    try {
      await fetch('/api/editor/scan', { method: 'POST' });
      await fetchEntityEditorData();
      npcfixLastScanAt = Date.now();
    } catch (e) {
      const b = document.getElementById('npcfixBox5Body');
      if (b && entityEditorData.length === 0) b.innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(String((e && e.message) || e)) + '</div>';
      return;
    }
    if (npcfixView !== 'box5') return;
    npcfixBox5RenderBody();
  }
}

let npcfixBox5Query = '';   // 搜索词（纯前端过滤已扫描数据，不触发扫描）

function npcfixBox5RenderBody() {
  const body = document.getElementById('npcfixBox5Body');
  if (!body) return;
  const keepScroll = body.scrollTop;

  // 分组：组名 = 物品中文名（与 :g= 定位共用 npcfixGroupKeyOf，否则清除范围对不上显示）
  const kinds = {};
  let total = 0;
  for (const e of entityEditorData) {
    if (npcfixIsStuffOnMapEntity(e)) {
      const k = npcfixGroupKeyOf('stuff', e);
      (kinds[k] = kinds[k] || []).push(e);
      total++;
    }
  }

  const sum = document.getElementById('npcfixBox5Summary');
  if (sum) sum.textContent = '共 ' + total + ' 堆（' + Object.keys(kinds).length + ' 种）';

  const q = (npcfixBox5Query || '').trim().toLowerCase();
  const names = Object.keys(kinds).sort((a, b) => kinds[b].length - kinds[a].length);
  let h = '';
  for (const name of names) {
    let list = kinds[name];
    const groupHit = !q || name.toLowerCase().indexOf(q) >= 0;
    if (!groupHit) {
      // 组名不中 → 按带编号的显示名（stuffNameWithIdIndex）匹配
      list = list.filter(e => String(e.stuffNameWithIdIndex || e.name || '').toLowerCase().indexOf(q) >= 0);
      if (list.length === 0) continue;
    }
    h += npcfixFacilityGroupCard(name, 'var(--warning, #e67e22)', '&#x1F4B0;', list,
      npcfixSpecOf('stuff', 'all', name), npcfixSpecOf('stuff', 'all', name));
  }
  body.innerHTML = h || npcfixEmptyHint('没有匹配的掉落物（共 ' + total + ' 堆）');
  body.scrollTop = keepScroll;   // 重绘后保持滚动位置
}

// 拾取进容器（spec = 'stuff:all[:g=组名]'）：按范围收集 → 分批 入库+移除 → 重扫刷新。
// 目标用自动拾取下拉的当前选择（手动/自动共用一个目标，避免两处状态不一致）。
async function npcfixPickup(spec) {
  const p = npcfixParseSpec(spec);
  if (!p) return;
  const hashes = entityEditorData.filter(e => p.test(e)).map(e => e.ptrHash);
  if (hashes.length === 0) { toast('没有可拾取的掉落物', true); return; }
  const targetName = npcfixAutoPickTargetLabel();
  const g = spec.indexOf(':g=') >= 0 ? '该物品' : '全部';
  if (!confirm('确定把' + g + ' ' + hashes.length + ' 堆掉落物拾取进「' + targetName + '」？')) return;
  toast('拾取中...', false);
  let picked = 0, skipped = 0;
  for (let i = 0; i < hashes.length; i += NPCFIX_KILL_CHUNK) {
    const part = hashes.slice(i, i + NPCFIX_KILL_CHUNK);
    try {
      const r = await fetch('/api/editor/stuff/pickup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ptrHashes: part, target: npcfixAutoPickGetTarget() })
      }).then(x => x.json());
      picked += (r && r.picked) || 0;
      skipped += (r && r.skipped) || 0;
    } catch (e) { skipped += part.length; }
    await new Promise(r => setTimeout(r, 60));
  }
  toast('拾取完成: ' + picked + ' 堆入「' + targetName + '」' + (skipped > 0 ? '（跳过 ' + skipped + ' 堆空堆/失效）' : ''));
  await npcfixScan();
  npcfixRefreshView();
}

// ===== 自动拾取（盒子5）：每 N 秒把地上掉落物拾取进选定容器，读档/退出自动关 =====
// 目标下拉：常用 4 项（国库/王座/最小箱子/货架）+「更多容器」分组（从实体扫描动态生成）。
// 后端 target：'treasury' 走游戏原生 GetKingdomTreasureBox；数字 stuff_id 走实体表找在场实例。
const NPCFIX_AUTO_PICK_MIN = 5;      // 间隔下限（秒）
const NPCFIX_AUTO_PICK_MAX = 60;     // 间隔上限（秒）
const NPCFIX_AUTO_PICK_DEFAULT = 10; // 默认间隔
const NPCFIX_PICK_TARGETS = [        // 常用目标（stuff_id 实测：国库109005 / 王座106005 / 大箱子103001 / 货架103003）
  { v: 'treasury', label: '🏛 国库' },
  { v: '106005', label: '👑 王座' },
  { v: '103001', label: '📦 大箱子（最小数值的箱子）' },
  { v: '103003', label: '🗂 货架' },
];
let npcfixAutoPickOn = false;
let npcfixAutoPickTimer = null;
let npcfixAutoPickCount = 0;
let npcfixAutoPickBusy = false;
let npcfixAutoPickSaveLoads = -1;
let npcfixAutoPickPicked = new Set(); // 本轮已拾 ptrHash（重扫前防重复入账——不然同一堆会被反复 AddStuff 刷资源）

function npcfixAutoPickGetInterval() {
  const v = parseInt(localStorage.getItem('chesteditor.autoPickInterval'), 10);
  if (isNaN(v)) return NPCFIX_AUTO_PICK_DEFAULT;
  return Math.min(NPCFIX_AUTO_PICK_MAX, Math.max(NPCFIX_AUTO_PICK_MIN, v));
}
function npcfixAutoPickIntervalChange(el) {
  let v = parseInt(el.value, 10);
  if (isNaN(v)) v = NPCFIX_AUTO_PICK_DEFAULT;
  v = Math.min(NPCFIX_AUTO_PICK_MAX, Math.max(NPCFIX_AUTO_PICK_MIN, v));
  el.value = v;
  localStorage.setItem('chesteditor.autoPickInterval', String(v));
  if (npcfixAutoPickOn) toast('自动拾取间隔已改为每 ' + v + ' 秒');
}
function npcfixAutoPickTargetChange(el) {
  localStorage.setItem('chesteditor.autoPickTarget', el.value);
  toast('拾取目标：' + (el.options[el.selectedIndex] ? el.options[el.selectedIndex].text : el.value));
}
function npcfixAutoPickGetTarget() { return localStorage.getItem('chesteditor.autoPickTarget') || 'treasury'; }
function npcfixAutoPickTargetLabel() {
  const v = npcfixAutoPickGetTarget();
  const hit = NPCFIX_PICK_TARGETS.find(t => t.v === v);
  if (hit) return hit.label;
  // 动态"更多"目标：从当前下拉取显示名
  const sel = document.getElementById('npcfixAutoPickTarget');
  if (sel && sel.selectedIndex >= 0 && sel.options[sel.selectedIndex]) return sel.options[sel.selectedIndex].text;
  return v;
}

// 目标下拉：常用 optgroup + 更多容器 optgroup（Facility 按 stuffId 去重，排除常用）
function npcfixAutoPickTargetSelectHtml() {
  const cur = npcfixAutoPickGetTarget();
  const commonIds = new Set(NPCFIX_PICK_TARGETS.map(t => t.v).filter(v => v !== 'treasury').map(Number));
  const seen = new Map();
  for (const e of entityEditorData) {
    if ((e.className || '').indexOf('Facility') !== 0) continue;
    if (!e.stuffId || commonIds.has(e.stuffId) || seen.has(e.stuffId)) continue;
    seen.set(e.stuffId, e.name || e.className);
  }
  let found = NPCFIX_PICK_TARGETS.some(t => t.v === cur) || seen.has(Number(cur));
  let h = '<select id="npcfixAutoPickTarget" onchange="npcfixAutoPickTargetChange(this)"'
    + ' style="padding:5px 8px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  h += '<optgroup label="常用">';
  for (const t of NPCFIX_PICK_TARGETS)
    h += '<option value="' + t.v + '"' + (t.v === cur ? ' selected' : '') + '>' + t.label + '</option>';
  h += '</optgroup>';
  if (seen.size > 0) {
    h += '<optgroup label="更多容器">';
    for (const [sid, name] of [...seen.entries()].sort((a, b) => a[0] - b[0]))
      h += '<option value="' + sid + '"' + (String(sid) === cur ? ' selected' : '') + '>' + esc(name) + '</option>';
    h += '</optgroup>';
  }
  h += '</select>';
  // 记忆的目标不在选项里（容器被拆/换存档）→ 存一份修正
  if (!found) localStorage.setItem('chesteditor.autoPickTarget', 'treasury');
  return h;
}

function npcfixAutoPickBtnText() {
  return npcfixAutoPickOn ? ('自动拾取: 开（已拾 ' + npcfixAutoPickCount + '）') : '自动拾取: 关';
}
function npcfixAutoPickUpdateBtn() {
  const b = document.getElementById('npcfixAutoPickBtn');
  if (b) b.textContent = npcfixAutoPickBtnText();
}
function npcfixAutoPickForceOff(reason) {
  npcfixAutoPickOn = false;
  if (npcfixAutoPickTimer) { clearTimeout(npcfixAutoPickTimer); npcfixAutoPickTimer = null; }
  toast(reason + '（本次累计拾取 ' + npcfixAutoPickCount + ' 堆）', true);
  npcfixAutoPickUpdateBtn();
}
// 读档/退出自动关（与定期清理同规则，各自记 saveLoads 基线）
function npcfixAutoPickCheckState(st) {
  if (!st) return false;
  if (st.loading || (typeof st.saveLoads === 'number' && st.saveLoads !== npcfixAutoPickSaveLoads)) {
    npcfixAutoPickForceOff('检测到读取存档，自动拾取已自动关闭');
    return true;
  }
  if (st.inSave === false) { npcfixAutoPickForceOff('已退出存档，自动拾取已自动关闭'); return true; }
  return false;
}
async function npcfixAutoPickToggle() {
  if (npcfixAutoPickOn) {
    npcfixAutoPickOn = false;
    if (npcfixAutoPickTimer) { clearTimeout(npcfixAutoPickTimer); npcfixAutoPickTimer = null; }
    toast('自动拾取已关闭（累计拾取 ' + npcfixAutoPickCount + ' 堆）');
    npcfixAutoPickUpdateBtn();
    return;
  }
  // 开启前核对状态（与定期清理同款）
  const st = await npcfixFetchState();
  if (!st) { toast('开启失败：无法连接游戏接口', true); return; }
  if (st.inSave === false) { toast('开启失败：未进入存档', true); return; }
  if (st.loading) { toast('开启失败：正在读取存档', true); return; }
  if (st.scanning) { toast('开启失败：正在扫描，请稍后再试', true); return; }
  npcfixAutoPickSaveLoads = typeof st.saveLoads === 'number' ? st.saveLoads : -1;
  npcfixAutoPickOn = true;
  npcfixAutoPickCount = 0;
  npcfixAutoPickPicked = new Set();
  npcfixAutoPickBusy = false;
  toast('自动拾取已开启：每 ' + npcfixAutoPickGetInterval() + ' 秒 → ' + npcfixAutoPickTargetLabel());
  npcfixAutoPickUpdateBtn();
  npcfixAutoPickTick();
}
// 一拍：核对状态 → 收集未拾的掉落物 → 分批入容器 → 记录成功名单（防重扫前重复入账）
async function npcfixAutoPickTick() {
  if (!npcfixAutoPickOn) return;
  try {
    if (npcfixAutoPickCheckState(await npcfixFetchState())) return;
    if (!npcfixScanning && !npcfixAutoPickBusy) {
      npcfixAutoPickBusy = true;
      try {
        const hashes = entityEditorData
          .filter(e => npcfixIsStuffOnMapEntity(e) && !npcfixAutoPickPicked.has(e.ptrHash))
          .map(e => e.ptrHash);
        if (hashes.length > 0) {
          const r = await fetch('/api/editor/stuff/pickup', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ptrHashes: hashes, target: npcfixAutoPickGetTarget() })
          }).then(x => x.json());
          for (const h of (r && r.pickedHashes) || []) npcfixAutoPickPicked.add(h);
          npcfixAutoPickCount += (r && r.picked) || 0;
        }
      } finally { npcfixAutoPickBusy = false; }
    }
  } catch (e) { /* 单拍失败不中断整个模式 */ }
  npcfixAutoPickUpdateBtn();
  if (npcfixAutoPickOn) npcfixAutoPickTimer = setTimeout(npcfixAutoPickTick, npcfixAutoPickGetInterval() * 1000);
}

// ===== 盒子4 动物 =====
// 数据源：实体扫描（零后端扫描改动）。实机：领地牲畜（猪，kingdom=1）与野生动物同类 Animal，
// 动物被杀后留 AnimalDeadBody（尸体，内含肉，走 MapStuffHelper.DestroyElement 删除）。
// 删除走 /api/editor/animal/batch：活体 AnimalHelper.DestroyAnimal（静默移除不掉肉）、
// 尸体 DestroyElement —— 都是游戏自己的注销路径。
// ⚠ Animal 有 is_dead 字段，扫描已过滤池化尸体（与 StuffOnMap 同坑同修）。

async function renderNpcfixBox4(forceScan) {
  const el = document.getElementById('content');
  // ⚡ 性能：'auto'（切视图触发的静默刷新）也必须先保证列表渲染出来（兜底显示缓存）
  if (forceScan === 'auto') {
    if (entityEditorData.length === 0) return renderNpcfixBox4(true);
    npcfixBox4RenderBody();
    if (Date.now() - npcfixLastScanAt < 60000 || npcfixScanning) return;
    npcfixScan().then(() => { if (npcfixView === 'box4') npcfixBox4RenderBody(); });
    return;
  }
  const needScan = forceScan === true || entityEditorData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F43E; 动物</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">牲畜与野生动物 · 按种类分组（尸体单独一组；大分组限量显示）</span>';
  html += '<button onclick="renderNpcfixBox4(true)" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  // 召唤行：下拉选动物（默认猪）× 数量输入框（默认1，1~10）→ 随机陆地格生成
  html += '<div style="display:flex;align-items:center;gap:10px;margin-bottom:12px;flex-shrink:0;flex-wrap:wrap">';
  html += '<span style="font-size:13px;color:var(--text-secondary);font-weight:700">✨ 召唤动物</span>';
  html += '<select id="npcfixBox4SpawnAnimal" style="padding:5px 8px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px"></select>';
  html += '<span style="font-size:13px;color:var(--text-muted)">× </span>';
  html += '<input type="number" id="npcfixBox4SpawnCount" min="1" max="10" value="1"'
    + ' onchange="npcfixBox4SpawnCountChange(this)"'
    + ' style="width:60px;padding:5px 8px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  html += '<button onclick="npcfixBox4Spawn()"'
    + ' style="padding:5px 14px;background:var(--accent);color:#fff;border:none;border-radius:6px;cursor:pointer;font-size:13px;font-weight:700">召唤</button>';
  html += '<span style="font-size:11px;color:var(--text-muted)">数量 1~10 · 家畜由游戏安置到族群区域 · 视角自动跟随</span>';
  html += '</div>';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-shrink:0">';
  html += '<input id="npcfixBox4Search" value="' + esc(npcfixBox4Query) + '"'
    + ' placeholder="搜索动物名，如：猪 / 鹿（留空显示全部）"'
    + ' oninput="npcfixBox4Query=this.value;npcfixBox4RenderBody()"'
    + ' style="width:300px;padding:6px 10px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px">';
  html += '<span id="npcfixBox4Summary" style="color:var(--text-muted);font-size:12px"></span>';
  html += '</div>';
  html += '<div id="npcfixBox4Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">' + (needScan && entityEditorData.length === 0 ? '扫描中...' : '') + '</div>';
  html += '</div>';
  el.innerHTML = html;

  // 有缓存：先把列表画出来（秒开），再视需要后台扫描
  if (entityEditorData.length > 0) npcfixBox4RenderBody();

  if (needScan) {
    if (entityEditorData.length > 0) {
      const b = document.getElementById('npcfixBox4Body');
      if (b && b.insertAdjacentHTML) b.insertAdjacentHTML('afterbegin',
        '<div style="padding:6px 12px;margin-bottom:8px;background:var(--bg-input);border:1px dashed var(--border);border-radius:6px;font-size:12px;color:var(--text-muted)">⏳ 扫描中，完成后列表自动更新…</div>');
    }
    const sum = document.getElementById('npcfixBox4Summary');
    if (sum) sum.textContent = '扫描中，完成后列表自动刷新…';
    try {
      await fetch('/api/editor/scan', { method: 'POST' });
      await fetchEntityEditorData();
      npcfixLastScanAt = Date.now();
    } catch (e) {
      const b = document.getElementById('npcfixBox4Body');
      if (b && entityEditorData.length === 0) b.innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(String((e && e.message) || e)) + '</div>';
      return;
    }
    if (npcfixView !== 'box4') return;
    npcfixBox4RenderBody();
  }
  // 召唤下拉：种类列表只拉一次（后端 animal.json + 官方中文名），每次渲染重填并恢复默认选中
  await npcfixLoadAnimals();
  npcfixBox4FillSpawnSelect();
}

let npcfixBox4Query = '';   // 搜索词（纯前端过滤已扫描数据，不触发扫描）

function npcfixBox4RenderBody() {
  const body = document.getElementById('npcfixBox4Body');
  if (!body) return;
  const keepScroll = body.scrollTop;

  // 分组：组名走 npcfixGroupKeyOf('animal')（活体按种类、尸体统一一组），与 :g= 定位共用
  const kinds = {};
  let total = 0, corpse = 0;
  for (const e of entityEditorData) {
    if (!npcfixIsAnimalEntity(e)) continue;
    const k = npcfixGroupKeyOf('animal', e);
    (kinds[k] = kinds[k] || []).push(e);
    total++;
    if (e.className === 'AnimalDeadBody') corpse++;
  }

  const sum = document.getElementById('npcfixBox4Summary');
  if (sum) sum.textContent = '共 ' + total + ' 只/具（' + Object.keys(kinds).length + ' 组）'
    + (corpse > 0 ? '，其中尸体 ' + corpse + ' 具' : '');

  const q = (npcfixBox4Query || '').trim().toLowerCase();
  const names = Object.keys(kinds).sort((a, b) => kinds[b].length - kinds[a].length);
  let h = '';
  for (const name of names) {
    let list = kinds[name];
    const groupHit = !q || name.toLowerCase().indexOf(q) >= 0;
    if (!groupHit) {
      list = list.filter(e => String(e.stuffNameWithIdIndex || e.name || '').toLowerCase().indexOf(q) >= 0);
      if (list.length === 0) continue;
    }
    const icon = name === '动物尸体' ? '&#x1F480;' : '&#x1F416;';
    const color = name === '动物尸体' ? 'var(--text-muted)' : 'var(--success-dark, #27ae60)';
    h += npcfixFacilityGroupCard(name, color, icon, list, npcfixSpecOf('animal', 'all', name));
  }
  body.innerHTML = h || npcfixEmptyHint('没有匹配的动物（共 ' + total + ' 只/具）');
  body.scrollTop = keepScroll;   // 重绘后保持滚动位置
}

// ===== 盒子4 召唤动物 =====
// 种类来自后端 /api/editor/animals（animal.json 陆地动物 + 官方中文名，默认选中猪 501005）。
// 创建走游戏自己的 AnimalHelper.CreateAnimal(随机陆地格, stuffId, 1)，位置与野生动物刷新同源。
let npcfixAnimalList = null;   // [{id, name}] 缓存

async function npcfixLoadAnimals() {
  if (npcfixAnimalList) return npcfixAnimalList;
  try {
    const r = await fetch('/api/editor/animals').then(x => x.json());
    npcfixAnimalList = (r && r.animals) || [];
  } catch (e) { npcfixAnimalList = []; }
  return npcfixAnimalList;
}

function npcfixBox4FillSpawnSelect() {
  const sel = document.getElementById('npcfixBox4SpawnAnimal');
  if (!sel || npcfixAnimalList.length === 0) return;
  sel.innerHTML = npcfixAnimalList
    .map(a => '<option value="' + a.id + '">' + esc(a.name) + '</option>').join('');
  // 默认 = 猪（501005）
  const pig = npcfixAnimalList.find(a => a.id === 501005);
  if (pig) sel.value = String(pig.id);
}

function npcfixBox4SpawnCountChange(el) {
  let v = parseInt(el.value, 10);
  if (isNaN(v)) v = 1;
  v = Math.min(10, Math.max(1, v));
  el.value = v;
}

async function npcfixBox4Spawn() {
  const sel = document.getElementById('npcfixBox4SpawnAnimal');
  const cnt = document.getElementById('npcfixBox4SpawnCount');
  if (!sel || !cnt) return;
  const stuffId = parseInt(sel.value, 10);
  let count = parseInt(cnt.value, 10);
  if (isNaN(stuffId) || stuffId <= 0) { toast('请选择动物种类', true); return; }
  if (isNaN(count)) count = 1;
  count = Math.min(10, Math.max(1, count));
  const name = sel.options[sel.selectedIndex] ? sel.options[sel.selectedIndex].text : ('#' + stuffId);
  if (!confirm('确定召唤 ' + count + ' 只「' + name + '」？\n家畜出生后由游戏安置到其族群/牧场区域（位置以实际为准，视角会自动跟过去）')) return;
  toast('召唤中...', false);
  // ⚠ 定位不用 guid：CreateAnimal 刚返回时游戏还没分配 guid（读到 0，会匹配到
  // 2266 个 guid=0 实体里的任意一个——实测相机飞到了不相干的 NPC 头上）。
  // 改用 ptrHash 差集：召唤前记快照，重扫后取新出现的动物。
  const beforeHashes = new Set(entityEditorData.map(e => e.ptrHash));
  let r = null;
  try {
    r = await fetch('/api/editor/animal/spawn', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ stuffId: stuffId, count: count })
    }).then(x => x.json());
  } catch (e) { toast('召唤失败: ' + esc(String((e && e.message) || e)), true); return; }
  const spawned = (r && r.spawned) || 0;
  toast('召唤完成: ' + spawned + ' 只「' + name + '」（游戏安置到族群区域，视角跟随中）');
  await npcfixScan();
  const newborn = entityEditorData
    .filter(e => !beforeHashes.has(e.ptrHash) && (e.className || '') === 'Animal')[0];
  if (newborn) {
    try {
      await fetch('/api/editor/locate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ptrHash: newborn.ptrHash })
      });
    } catch (e) { /* 定位失败不影响结果 */ }
  }
  npcfixRefreshView();
}
