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
  selectedChest = -1;
  dragonView = '';
  npcView = '';
  npcfixView = (npcfixView === view) ? '' : view;
  renderSidebar();
  renderContent();
}

// ===== 盒子1 小人数值修改 =====

async function renderNpcfixBox1() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F9F0; 小人数值修改</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">我方NPC · 字段按职业定制</span>';
  html += '<button onclick="renderNpcfixBox1()" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';
  html += '<div id="npcfixBox1Body" style="flex:1;overflow-y:auto;min-height:0;color:var(--text-muted)">扫描中...</div>';
  html += '</div>';
  el.innerHTML = html;

  if (npcListData.length === 0) {
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

  // 按职业分组（复用 NPC 面板的分类逻辑）
  const groups = [
    { key: 'soldiers', label: '士兵', icon: '&#x2694;', color: '#e74c3c', items: classifyNpcByType(npcListData).soldiers },
    { key: 'workers', label: '工作者', icon: '&#x1F527;', color: '#f39c12', items: classifyNpcByType(npcListData).workers },
    { key: 'laborers', label: '杂工', icon: '&#x1F6E0;', color: '#95a5a6', items: classifyNpcByType(npcListData).laborers },
    { key: 'children', label: '儿童', icon: '&#x1F476;', color: '#e91e63', items: classifyNpcByType(npcListData).children },
    { key: 'others', label: '其他', icon: '&#x2753;', color: '#34495e', items: classifyNpcByType(npcListData).others },
  ];

  let html2 = '';
  for (const g of groups) {
    if (g.items.length === 0) continue;
    let cards = '<div style="padding:4px 0">';
    for (const npc of g.items)
      cards += '<div style="margin-bottom:6px">' + renderNpcfixNpcCard(npc, g.key) + '</div>';
    cards += '</div>';
    html2 += htmlDetailsGroup(g.label + ' (' + g.items.length + ')', g.color, g.icon, g.items.length + ' 个', cards);
  }
  body.innerHTML = html2 || '<div style="padding:40px;text-align:center;color:var(--text-muted)">暂无数据</div>';
}

// 单个 NPC 卡：字段编辑懒加载（展开 details 才请求一次字段，职业字段置顶）
function renderNpcfixNpcCard(npc, groupKey) {
  const ptrHash = npc.ptrHash || 0;
  const displayName = npc.npcName || npc.name || ('NPC#' + npc.guid);
  const soldierType = npc.soldierTypeName || '';
  const npcTypeName = getNpcTypeName(npc.npcType || 0);
  let h = '';
  h += '<div style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius)">';

  h += '<div style="display:flex;align-items:center;gap:8px;padding:8px 14px;border-bottom:1px solid var(--border)">';
  h += '<span style="font-weight:600;color:var(--text-primary);font-size:13px">' + esc(displayName) + '</span>';
  if (npcTypeName) h += '<span style="font-size:11px;padding:1px 8px;border-radius:8px;background:var(--warning,#e67e22);color:#fff">' + esc(npcTypeName) + '</span>';
  if (soldierType) h += '<span style="font-size:11px;padding:1px 8px;border-radius:8px;background:var(--accent);color:#fff">' + esc(soldierType) + '</span>';
  h += '<span style="font-size:11px;color:var(--text-muted);margin-left:auto">GUID:' + npc.guid + '</span>';
  h += '<button onclick="event.stopPropagation();locateEditorEntity(' + ptrHash + ')" style="padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">定位</button>';
  h += '</div>';

  h += '<details style="border-top:1px solid var(--border)" ontoggle="loadNpcfixCard(this,' + ptrHash + ',\'' + groupKey + '\')">';
  h += '<summary style="cursor:pointer;padding:6px 14px;font-size:12px;color:var(--text-muted);user-select:none">字段编辑（职业字段置顶）</summary>';
  h += '<div id="npcfix-body-' + ptrHash + '" style="padding:6px 14px 10px;color:var(--text-muted);font-size:12px">展开加载...</div>';
  h += '</details>';

  h += '</div>';
  return h;
}

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
    let quick = npcTableHeader(ptrHash, false).replace('class="npc-fields-table"', 'class="npc-fields-table"') ;
    quick = '<div style="font-size:11px;color:var(--accent-light);margin-bottom:4px">常用字段（' + groupLabel + '）</div>' + npcTableHeader(ptrHash, false);
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
    const checked = new Set(getCheckedFields(ptrHash));
    let full = npcTableHeader(ptrHash, true);
    for (const key of numKeys) {
      const f = fields[key];
      const displayVal = (typeof f.value === 'number') ? (f.isFloat ? f.value.toFixed(2) : f.value) : (f.value || 0);
      full += npcNumRowHtml(ptrHash, key, f.isFloat, displayVal, translations[key] || '', {
        inpId: 'npcfix_all_' + ptrHash + '_' + key,
        checkbox: checked.has(key),
        rowCls: 'npc-field-row',
        dataKey: esc(key).toLowerCase()
      });
    }
    for (const key of strKeys) {
      full += npcStrRowHtml(key, fields[key].value, translations[key] || '', {
        ptrHash: ptrHash,
        checkbox: checked.has(key),
        rowCls: 'npc-field-row',
        dataKey: esc(key).toLowerCase()
      });
    }
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
