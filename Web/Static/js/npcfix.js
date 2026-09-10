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
}

// ===== 盒子1 小人数值修改 =====

async function renderNpcfixBox1(forceScan) {
  const el = document.getElementById('content');
  // 首次进入（无缓存）或显式刷新时才真正扫描；其余情况复用已扫描数据直接渲染
  const needScan = forceScan === true || npcListData.length === 0;
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F9F0; 小人数值修改</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">我方NPC · 字段按职业定制</span>';
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

  // 先分敌我：阵营1=我方；其余阵营里带兵种ID的是敌方士兵
  const ours = npcListData.filter(n => (n.hometownKingdomId || 0) === 1);
  const enemySoldiers = npcListData.filter(n => (n.hometownKingdomId || 0) !== 1 && (n.soldierTypeId || 0) > 0);

  // 我方士兵再按职业（兵种名）细分
  const oursByType = classifyNpcByType(ours);
  const professions = {};
  for (const n of oursByType.soldiers) {
    const prof = n.soldierTypeName || '未知兵种';
    if (!professions[prof]) professions[prof] = [];
    professions[prof].push(n);
  }

  // 敌方士兵按阵营细分
  const enemyByKingdom = {};
  for (const n of enemySoldiers) {
    const kid = n.hometownKingdomId || 0;
    if (!enemyByKingdom[kid]) enemyByKingdom[kid] = [];
    enemyByKingdom[kid].push(n);
  }

  let html2 = '';

  // ===== 我方 =====
  html2 += '<div style="font-size:13px;font-weight:600;color:var(--success-dark,#27ae60);margin:4px 0 6px">我方 (阵营1) · ' + ours.length + ' 个</div>';

  // 士兵：整组套一个盒子（绿色主题），展开后再按兵种细分（二级菜单）
  const profEntries = Object.entries(professions).sort((a, b) => b[1].length - a[1].length);
  if (profEntries.length > 0) {
    let inner = '<div style="padding:2px 0 2px 10px">';
    for (const [prof, list] of profEntries) {
      let cards = '<div style="padding:4px 0">';
      for (const npc of list)
        cards += '<div style="margin-bottom:6px">' + renderNpcCard(npc, {groupKey: 'soldiers'}) + '</div>';
      cards += '</div>';
      inner += htmlDetailsGroup('士兵·' + prof + ' (' + list.length + ')', 'var(--success-dark, #27ae60)', '&#x2694;', list.length + ' 个', cards);
    }
    inner += '</div>';
    html2 += htmlDetailsGroup('士兵 (' + oursByType.soldiers.length + ')', 'var(--success-dark, #27ae60)', '&#x2694;', oursByType.soldiers.length + ' 个', inner);
  }

  // 市民：工作者 / 杂工 / 儿童 归入同一个盒子（二级菜单）
  // 杂工组额外并入「小精灵 / 石头人」（npcType 23/30，classifyNpcByType 归入 misc）：
  // 它们同为我方平民单位，单独成组会从市民计数里漏掉（我方总数 > 各盒子之和）。
  const citizenGroups = [
    { key: 'workers', label: '工作者', icon: '&#x1F527;', color: '#f39c12', list: oursByType.workers },
    { key: 'laborers', label: '杂工', icon: '&#x1F6E0;', color: '#95a5a6', list: (oursByType.laborers || []).concat(oursByType.misc || []) },
    { key: 'children', label: '儿童', icon: '&#x1F476;', color: '#e91e63', list: oursByType.children },
  ];
  let citizenInner = '<div style="padding:2px 0 2px 10px">';
  let citizenCount = 0;
  for (const g of citizenGroups) {
    const list = g.list;
    if (!list || list.length === 0) continue;
    citizenCount += list.length;
    let cards = '<div style="padding:4px 0">';
    for (const npc of list)
      cards += '<div style="margin-bottom:6px">' + renderNpcCard(npc, {groupKey: g.key}) + '</div>';
    cards += '</div>';
    citizenInner += htmlDetailsGroup(g.label + ' (' + list.length + ')', g.color, g.icon, list.length + ' 个', cards);
  }
  citizenInner += '</div>';
  if (citizenCount > 0)
    html2 += htmlDetailsGroup('市民 (' + citizenCount + ')', '#3498db', '&#x1F3E0;', citizenCount + ' 个', citizenInner);

  // 其他（未归类的我方 NPC，保持独立盒子）
  const othersList = oursByType.others;
  if (othersList && othersList.length > 0) {
    let cards = '<div style="padding:4px 0">';
    for (const npc of othersList)
      cards += '<div style="margin-bottom:6px">' + renderNpcCard(npc, {groupKey: 'others'}) + '</div>';
    cards += '</div>';
    html2 += htmlDetailsGroup('其他 (' + othersList.length + ')', '#34495e', '&#x2753;', othersList.length + ' 个', cards);
  }

  // ===== 敌方士兵（按阵营） =====
  const enemyKinds = Object.keys(enemyByKingdom).map(Number).sort((a, b) => b - a);
  if (enemyKinds.length > 0) {
    html2 += '<div style="font-size:13px;font-weight:600;color:var(--danger,#e74c3c);margin:10px 0 6px">敌方士兵 · ' + enemySoldiers.length + ' 个</div>';
    for (const kid of enemyKinds) {
      const kInfo = getKingdomInfo(kid);
      const label = kInfo ? kInfo.name : ('阵营' + kid);
      const list = enemyByKingdom[kid];
      // 敌方职业分布小统计
      const profDist = {};
      for (const n of list) profDist[n.soldierTypeName || '?'] = (profDist[n.soldierTypeName || '?'] || 0) + 1;
      const profText = Object.entries(profDist).sort((a, b) => b[1] - a[1]).map(([n, c]) => n + '×' + c).join('、');
      let cards = '<div style="padding:2px 0 4px;font-size:11px;color:var(--text-muted)">' + esc(profText) + '</div>';
      for (const npc of list)
        cards += '<div style="margin-bottom:6px">' + renderNpcCard(npc, {groupKey: 'soldiers'}) + '</div>';
      html2 += htmlDetailsGroup(label + ' (' + list.length + ')', kInfo ? kInfo.bg : '#7f8c8d', '&#x2694;', list.length + ' 个', cards);
    }
  }

  body.innerHTML = html2 || '<div style="padding:40px;text-align:center;color:var(--text-muted)">暂无数据</div>';
}


// NPC 卡片统一由 npc.js 的 renderNpcCard(npc, {groupKey}) 渲染（见 npc.js）

// 卡片字段加载：一次请求，渲染 职业快速字段 + 全字段表格
async function loadNpcfixCard(details, ptrHash, groupKey) {
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
    const groupLabel = { soldiers: '士兵', workers: '工作者', laborers: '杂工', children: '儿童', others: '其他' }[groupKey] || groupKey;
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

function renderNpcfixBox2() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x2694; 战斗单位</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">' + entityEditorData.length + ' 个实体</span>';
  html += '<button onclick="entityEditorScan()" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div id="npcfixBox2Body" style="flex:1;overflow-y:auto;min-height:0">';
  if (entityEditorData.length === 0)
    html += '<div style="padding:40px;text-align:center;color:var(--text-muted)">点击右上"重新扫描"</div>';
  else
    html += renderNpcfixBox2Groups();
  html += '</div></div>';
  el.innerHTML = html;
}

function renderNpcfixBox2Groups() {
  const isBattle = (cn) => /Npc|Soldier|BattleUnit/.test(cn) && !/NpcHelper|NpcTask/.test(cn);
  const ours = [], humanoids = {}, monsters = {}, ships = [];
  for (const e of entityEditorData) {
    const cn = e.className || '';
    const kid = e.hometownKingdomId || e.territoryKingdomId || 0;
    if (cn.includes('Ship')) { ships.push(e); continue; }
    if (cn.startsWith('Monster')) {
      if (!monsters[kid]) monsters[kid] = [];
      monsters[kid].push(e);
      continue;
    }
    if (isBattle(cn)) {
      if (kid === 1) ours.push(e);
      else {
        if (!humanoids[kid]) humanoids[kid] = [];
        humanoids[kid].push(e);
      }
    }
  }

  let html = '';
  // 我方单位（可修改 + ×10）
  if (ours.length > 0)
    html += editorGroupHtml('我方单位', 'var(--success-dark, #27ae60)', '#fff', ours.length + ' 个',
      '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">' + ours.map(npcfixUnitItemHtml).join('') + '</div>');

  // 敌方-小人（按阵营）
  const humanKinds = Object.keys(humanoids).map(Number).sort((a, b) => a - b);
  if (humanKinds.length > 0) {
    let inner = '';
    for (const kid of humanKinds) {
      const kInfo = getKingdomInfo(kid);
      inner += editorGroupHtml(kInfo ? esc(kInfo.name) : ('阵营' + kid),
        kInfo ? kInfo.bg : 'var(--text-muted)', kInfo ? kInfo.fg : '#fff',
        humanoids[kid].length + ' 个', editorItemsHtml(humanoids[kid]));
    }
    html += editorGroupHtml('敌方-小人', '#c0392b', '#fff', humanKinds.reduce((s, k) => s + humanoids[k].length, 0) + ' 个', inner);
  }

  // 敌方-怪物（按阵营，一键清除执行两遍间隔1秒）
  const monsterKinds = Object.keys(monsters).map(Number).sort((a, b) => a - b);
  if (monsterKinds.length > 0) {
    let inner = '';
    for (const kid of monsterKinds) {
      const kInfo = getKingdomInfo(kid);
      const label = (kInfo ? esc(kInfo.name) : ('阵营' + kid)) + '（清除执行2遍，间隔1秒）';
      const hashes = monsters[kid].map(e => e.ptrHash);
      inner += editorGroupHtml(label, 'var(--danger, #e74c3c)', '#fff', monsters[kid].length + ' 个', editorItemsHtml(monsters[kid]));
      // 一键清除按钮插到组 summary 后：用带按钮的分组头
      inner += '';
    }
    html += editorGroupHtml('敌方-怪物', 'var(--danger, #e74c3c)', '#fff',
      monsterKinds.reduce((s, k) => s + monsters[k].length, 0) + ' 个',
      '<div style="padding:6px 0">' +
      monsterKinds.map(kid => {
        const kInfo = getKingdomInfo(kid);
        const label = (kInfo ? esc(kInfo.name) : ('阵营' + kid));
        return '<div style="display:flex;align-items:center;gap:8px;margin-bottom:6px">' +
          '<span style="font-size:12px;color:var(--text-primary)">' + label + ' (' + monsters[kid].length + ')</span>' +
          '<button onclick="npcfixKillMonsters(' + kid + ')" style="padding:3px 10px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键清除(2遍)</button>' +
          '</div><div style="display:flex;flex-direction:column;gap:4px;padding:0 0 6px 12px">' +
          monsters[kid].map(e => renderEditorEntityItem(e)).join('') + '</div>';
      }).join('') +
      '</div>');
  }

  // 船（仅战舰）
  if (ships.length > 0) {
    const warships = ships.filter(e => /war|battle|attack|战/i.test((e.goName || '') + (e.className || '')) || (e.className || '').indexOf('Ship') === 0);
    if (warships.length > 0)
      html += editorGroupHtml('船-战舰', '#3498db', '#fff', warships.length + ' 个', editorItemsHtml(warships));
    const traders = ships.length - warships.length;
    if (traders > 0)
      html += '<div style="margin:4px 0 8px 12px;font-size:11px;color:var(--text-muted)">另有 ' + traders + ' 艘商船未列入</div>';
  }

  if (html === '') html = '<div style="padding:20px;text-align:center;color:var(--text-muted)">没有匹配的战斗单位</div>';
  return html;
}

// 我方战斗单位条目（带 攻击×10 / 血量×10）
function npcfixUnitItemHtml(e) {
  const ph = e.ptrHash || 0;
  const displayName = e.npcName || e.name || e.goName || 'unknown';
  let h = '<div style="margin-bottom:4px">';
  h += '<div style="display:flex;align-items:center;gap:8px;padding:6px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm)">';
  h += '<span style="font-weight:600;color:var(--text-primary)">' + esc(displayName) + '</span>';
  h += '<span style="font-size:11px;color:var(--text-muted);margin-left:auto">GUID:' + (e.guid || 0) + '</span>';
  h += '<button onclick="event.stopPropagation();npcfixMultiply(' + ph + ',\'atk\')" style="padding:3px 8px;background:var(--warning,#e67e22);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">攻击×10</button>';
  h += '<button onclick="event.stopPropagation();npcfixMultiply(' + ph + ',\'hp\')" style="padding:3px 8px;background:var(--success-dark,#27ae60);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">血量×10</button>';
  h += '<button onclick="event.stopPropagation();locateEditorEntity(' + ph + ')" style="padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">定位</button>';
  h += '</div></div>';
  return h;
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
  const hashes = entityEditorData
    .filter(e => (e.className || '').startsWith('Monster') && (e.hometownKingdomId || e.territoryKingdomId || 0) === kid)
    .map(e => e.ptrHash);
  if (hashes.length === 0) { toast('没有怪物', true); return; }
  const d1 = await destroyBatch(hashes);
  await new Promise(r => setTimeout(r, 1000));
  // 重扫拿到分裂新生成的怪物，再清一遍
  await entityEditorScan();
  const hashes2 = entityEditorData
    .filter(e => (e.className || '').startsWith('Monster') && (e.hometownKingdomId || e.territoryKingdomId || 0) === kid)
    .map(e => e.ptrHash);
  let d2 = { destroyed: 0 };
  if (hashes2.length > 0) d2 = await destroyBatch(hashes2);
  await entityEditorScan();
  toast('清除完成: 首轮' + (d1.destroyed || 0) + ' + 二轮' + (d2.destroyed || 0));
}
