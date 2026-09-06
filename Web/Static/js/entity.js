// entity — 由 HtmlUI 单体拆分（原 app.js）
async function entityEditorScan() {
  const btn = document.getElementById('entityEditorScanBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    await fetch('/api/editor/scan', {method:'POST'});
    await fetchEntityEditorData();
    toast('统一扫描完成 (' + entityEditorData.length + ' 个实体)');
  } catch(e) {
    toast('扫描失败: ' + e.message, true);
  } finally {
    if (btn) { btn.textContent = '开始扫描'; btn.disabled = false; }
    renderContent();
  }
}


function toggleEditorEntity(ptrHash) {
  const container = document.getElementById('editor_fields_' + ptrHash);
  if (!container) return;
  if (container.style.display === 'none') {
    container.style.display = 'block';
    loadEntityEditorFields(ptrHash);
  } else {
    container.style.display = 'none';
  }
}


async function loadEntityEditorFields(ptrHash) {
  const container = document.getElementById('editor_fields_' + ptrHash);
  if (!container) return;
  if (container.dataset.loaded) return;
  container.innerHTML = '<div style="padding:8px;color:var(--text-muted);font-size:12px">加载中...</div>';
  try {
    const r = await fetch('/api/editor/fields/' + ptrHash + '?t=' + Date.now());
    const fields = await r.json();
    if (fields.error) { container.innerHTML = '<div style="padding:8px;color:var(--danger);font-size:12px">' + esc(fields.error) + '</div>'; return; }
    container.innerHTML = renderEditorFieldsTable(fields, ptrHash);
    container.dataset.loaded = '1';
  } catch(e) {
    container.innerHTML = '<div style="padding:8px;color:var(--danger);font-size:12px">加载失败</div>';
  }
}


function renderEditorFieldsTable(fields, ptrHash) {
  let html = '<table style="width:100%;border-collapse:collapse;font-size:12px">';
  html += '<tr style="background:var(--bg-card)"><th style="text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)">字段</th><th style="text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)">类型</th><th style="text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)">值</th><th style="padding:4px 8px;border-bottom:1px solid var(--border)">操作</th></tr>';
  for (const [name, f] of Object.entries(fields)) {
    const typeLabel = f.isFloat ? 'float' : (f.isString ? 'string' : 'int');
    const val = f.value !== undefined ? f.value : '';
    html += '<tr style="border-bottom:1px solid var(--border-light,rgba(255,255,255,0.05))">';
    html += '<td style="padding:4px 8px;color:var(--text-primary)">' + esc(name) + '</td>';
    html += '<td style="padding:4px 8px;color:var(--text-muted)">' + typeLabel + '</td>';
    html += '<td style="padding:4px 8px">';
    if (f.isString) {
      html += '<span style="color:var(--accent-light)">' + esc(String(val)) + '</span>';
    } else {
      html += '<input id="editor_' + name + '_' + ptrHash + '" type="text" value="' + esc(String(val)) + '" style="width:120px;padding:2px 6px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px">';
    }
    html += '</td>';
    html += '<td style="padding:4px 8px;text-align:center">';
    if (!f.isString) {
      html += '<button onclick="setEntityEditorField(' + ptrHash + ', \'' + esc(name) + '\')" style="padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">OK</button>';
    }
    html += '</td></tr>';
  }
  html += '</table>';
  return html;
}


async function listEntityMethods(ptrHash) {
  try {
    const r = await fetch('/api/editor/listmethods', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    alert(d.methods || 'no methods');
  } catch(e) { toast('查询失败', true); }
}


async function destroyEditorEntities(ptrHashes) {
  if (!confirm('确定一键消除 ' + ptrHashes.length + ' 个实体？此操作不可撤销。')) return;
  let ok = 0, fail = 0;
  for (const ph of ptrHashes) {
    try {
      const r = await fetch('/api/editor/destroy', {
        method: 'POST', headers: {'Content-Type':'application/json'},
        body: JSON.stringify({ptrHash: ph})
      });
      const d = await r.json();
      if (d.error) fail++; else ok++;
    } catch(e) { fail++; }
  }
  toast('消除完成: ' + ok + ' 成功' + (fail > 0 ? ', ' + fail + ' 失败' : ''));
  await fetchEntityEditorData();
  renderContent();
}


async function locateEditorEntity(ptrHash) {
  try {
    const r = await fetch('/api/editor/locate', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash})
    });
    const j = await r.json();
    if (j.error) { showToast('定位失败: ' + j.error, 'error'); return; }
    showToast('已定位到 (' + (j.x||0).toFixed(1) + ', ' + (j.y||0).toFixed(1) + ')', 'success');
  } catch(e) { showToast('定位请求失败', 'error'); }
}


async function destroyEditorEntity(ptrHash) {
  const e = entityEditorData.find(x => x.ptrHash === ptrHash);
  const name = e ? (e.npcName || e.name || e.goName) : ('ptrHash=' + ptrHash);
  if (!confirm('确定消除 ' + name + '？此操作不可撤销。')) return;
  try {
    const r = await fetch('/api/editor/destroy', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('已消除 ' + name);
    await fetchEntityEditorData();
    renderContent();
  } catch(e) { toast('消除失败', true); }
}


async function setEntityEditorField(ptrHash, field) {
  const input = document.getElementById('editor_' + field + '_' + ptrHash);
  const val = parseFloat(input.value) || 0;
  try {
    const r = await fetch('/api/editor/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash, field, value: val})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('设置成功');
    const container = document.getElementById('editor_fields_' + ptrHash);
    if (container) container.dataset.loaded = '';
    loadEntityEditorFields(ptrHash);
  } catch(e) { toast('设置失败', true); }
}


// ===== 实体编辑器：分组渲染（模块级，四类分组共用折叠模板） =====

// 实体分组折叠块
// countHtml: 右侧计数文本；contentHtml: 展开内容；opts: { hashes(一键消除列表), noDestroy }
function editorGroupHtml(label, bg, fg, countHtml, contentHtml, opts) {
  opts = opts || {};
  let h = '<details style="margin-bottom:8px;margin-left:12px">';
  h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
  h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + bg + ';color:' + fg + '">' + label + '</span>';
  h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + countHtml + '</span>';
  if (!opts.noDestroy)
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(opts.hashes || []) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
  h += '</summary>';
  h += contentHtml;
  h += '</details>';
  return h;
}

function editorItemsHtml(items) {
  let h = '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
  for (const e of items) h += renderEditorEntityItem(e);
  return h + '</div>';
}

// NPC 子分类渲染函数
function renderEditorEntityItem(e) {
  let h = '';
  const goName = e.goName || 'unknown';
  const npcName = e.npcName || '';
  const hometownKingdomId = e.hometownKingdomId || 0;
  const territoryKingdomId = e.territoryKingdomId || 0;
  const kingdomId = hometownKingdomId || territoryKingdomId;
  const guid = e.guid || 0;
  const ptrHash = e.ptrHash || 0;
  const entityName = e.name || '';
  const fieldCount = e.fieldCount || 0;
  const displayName = npcName || entityName || goName;
  const stuffNameWithIdIndex = e.stuffNameWithIdIndex || '';
  const soldierTypeName = e.soldierTypeName || '';
  const nameSuffix = stuffNameWithIdIndex || soldierTypeName;
  const kInfo = getKingdomInfo(kingdomId);
  h += '<div style="margin-bottom:4px">';
  h += '<div style="display:flex;align-items:center;gap:8px;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);cursor:pointer" onclick="toggleEditorEntity(' + ptrHash + ')">';
  h += '<span style="font-weight:600;color:var(--text-primary)">' + esc(displayName) + (nameSuffix ? ' <span style="font-weight:400;color:var(--text-muted)">(' + esc(nameSuffix) + ')</span>' : '') + '</span>';
  if (kInfo) h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + kInfo.bg + ';color:' + kInfo.fg + '">' + esc(kInfo.name) + '</span>';
  if (npcName && entityName) h += '<span style="font-size:10px;color:var(--text-muted)">' + esc(entityName) + '</span>';
  h += '<span style="font-size:11px;color:var(--text-muted);margin-left:auto">' + esc(e.className || '') + ' GUID:' + guid + '</span>';
  h += '<button onclick="event.stopPropagation();listEntityMethods(' + ptrHash + ')" style="padding:3px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap">方法</button>';
  h += '<button onclick="event.stopPropagation();locateEditorEntity(' + ptrHash + ')" style="padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap">定位</button>';
  h += '<button onclick="event.stopPropagation();destroyEditorEntity(' + ptrHash + ')" style="padding:3px 10px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap">消除</button>';
  h += '</div>';
  h += '<div id="editor_fields_' + ptrHash + '" style="display:none;padding:4px 0 4px 12px"></div>';
  h += '</div>';
  return h;
}

function renderNpcGroup(items) {
  const mine = [];
  const others = {};
  for (const e of items) {
    const kid = e.hometownKingdomId || 0;
    if (kid === 1) { mine.push(e); continue; }
    if (!others[kid]) others[kid] = [];
    others[kid].push(e);
  }
  let h = '';
  // 我方
  if (mine.length > 0) {
    const workers = [];
    const soldiers = [];
    for (const e of mine) {
      const stn = e.soldierTypeName || '';
      if (stn === '民兵' || stn === '市民') workers.push(e); else soldiers.push(e);
    }
    const mineHashes = mine.map(e => e.ptrHash);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:var(--success-dark, #27ae60);color:#fff">我方</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + mine.length + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(mineHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    // 工作者
    if (workers.length > 0) {
      const wHashes = workers.map(e => e.ptrHash);
      h += '<details style="margin-bottom:8px;margin-left:12px">';
      h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
      h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:#f39c12;color:#fff">工作者</span>';
      h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + workers.length + ' 个</span>';
      h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(wHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
      h += '</summary>';
      h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
      for (const e of workers) h += renderEditorEntityItem(e);
      h += '</div></details>';
    }
    // 士兵
    if (soldiers.length > 0) {
      const sHashes = soldiers.map(e => e.ptrHash);
      h += '<details style="margin-bottom:8px;margin-left:12px">';
      h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
      h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:#3498db;color:#fff">士兵</span>';
      h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + soldiers.length + ' 个</span>';
      h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(sHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
      h += '</summary>';
      h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
      for (const e of soldiers) h += renderEditorEntityItem(e);
      h += '</div></details>';
    }
    h += '</details>';
  }
  // 其他阵营按王国分组
  const sortedKids = Object.keys(others).map(Number).sort((a,b) => a - b);
  for (const kid of sortedKids) {
    const group = others[kid];
    const kInfo = getKingdomInfo(kid);
    const label = kInfo ? kInfo.name : ('阵营' + kid);
    const bg = kInfo ? kInfo.bg : 'var(--text-muted)';
    const fg = kInfo ? kInfo.fg : '#fff';
    const groupHashes = group.map(e => e.ptrHash);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + bg + ';color:' + fg + '">' + esc(label) + '</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + group.length + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
    for (const e of group) h += renderEditorEntityItem(e);
    h += '</div></details>';
  }
  return h;
}

function renderTerritoryGroup(items) {
  const mine = [];
  const others = {};
  for (const e of items) {
    const kid = e.territoryKingdomId || e.hometownKingdomId || 0;
    if (kid === 1) { mine.push(e); continue; }
    if (!others[kid]) others[kid] = [];
    others[kid].push(e);
  }
  let h = '';
  // 我方（默认展开）
  if (mine.length > 0) {
    const mineHashes = mine.map(e => e.ptrHash);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:var(--success-dark, #27ae60);color:#fff">我方</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + mine.length + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(mineHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
    for (const e of mine) h += renderEditorEntityItem(e);
    h += '</div></details>';
  }
  // 其他阵营按 territory 分组（默认收起）
  const sortedKids = Object.keys(others).map(Number).sort((a,b) => a - b);
  for (const kid of sortedKids) {
    const group = others[kid];
    const kInfo = getKingdomInfo(kid);
    const label = kInfo ? kInfo.name : ('阵营' + kid);
    const bg = kInfo ? kInfo.bg : 'var(--text-muted)';
    const fg = kInfo ? kInfo.fg : '#fff';
    const groupHashes = group.map(e => e.ptrHash);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + bg + ';color:' + fg + '">' + esc(label) + '</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + group.length + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
    for (const e of group) h += renderEditorEntityItem(e);
    h += '</div></details>';
  }
  return h;
}

function renderBuildingGroup(items) {
  const monsterBuildNames = ['巨树', '巨型蘑菇', '巨型蜂巢'];
  const monsterBuilds = {};
  const normalBuilds = [];
  for (const e of items) {
    const dn = e.npcName || e.name || e.goName || '';
    let matched = false;
    for (const mn of monsterBuildNames) {
      if (dn.includes(mn)) {
        if (!monsterBuilds[mn]) monsterBuilds[mn] = [];
        monsterBuilds[mn].push(e);
        matched = true;
        break;
      }
    }
    if (!matched) normalBuilds.push(e);
  }
  const monsterKeys = Object.keys(monsterBuilds);
  let h = '';
  if (monsterKeys.length > 0) {
    const totalMonster = monsterKeys.reduce((s, k) => s + monsterBuilds[k].length, 0);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:var(--danger, #e74c3c);color:#fff">怪物建筑 备注：这里显示我方是因为可交互的</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + totalMonster + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify([].concat(...monsterKeys.map(k => monsterBuilds[k].map(e => e.ptrHash)))) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    for (const mn of monsterBuildNames) {
      const group = monsterBuilds[mn];
      if (!group || group.length === 0) continue;
      const groupHashes = group.map(e => e.ptrHash);
      h += '<details style="margin-bottom:8px;margin-left:12px">';
      h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
      h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:var(--warning, #f39c12);color:#fff">' + esc(mn) + '</span>';
      h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + group.length + ' 个</span>';
      h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
      h += '</summary>';
      h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
      for (const e of group) h += renderEditorEntityItem(e);
      h += '</div></details>';
    }
    h += '</details>';
  }
  if (normalBuilds.length > 0) {
    if (monsterKeys.length > 0) h += '<div style="margin:8px 0 4px 12px;font-size:12px;font-weight:600;color:var(--text-muted)">普通建筑 (' + normalBuilds.length + ')</div>';
    h += renderTerritoryGroup(normalBuilds);
  }
  return h;
}

function renderAnimalGroup(items) {
  const hasTerritory = [];
  const noTerritory = {};
  for (const e of items) {
    const kid = e.territoryKingdomId || e.hometownKingdomId || 0;
    if (kid > 0) { hasTerritory.push(e); continue; }
    const label = e.npcName || e.name || e.goName || '未知';
    if (!noTerritory[label]) noTerritory[label] = [];
    noTerritory[label].push(e);
  }
  let h = '';
  if (hasTerritory.length > 0) h += renderTerritoryGroup(hasTerritory);
  const sortedLabels = Object.keys(noTerritory).sort();
  for (const label of sortedLabels) {
    const group = noTerritory[label];
    const groupHashes = group.map(e => e.ptrHash);
    h += '<details style="margin-bottom:8px;margin-left:12px">';
    h += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
    h += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:var(--warning, #f39c12);color:#fff">' + esc(label) + '</span>';
    h += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + group.length + ' 个</span>';
    h += '<button onclick="event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')" style="padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">一键消除</button>';
    h += '</summary>';
    h += '<div style="display:flex;flex-direction:column;gap:4px;padding:6px 0">';
    for (const e of group) h += renderEditorEntityItem(e);
    h += '</div></details>';
  }
  return h;
}

function renderNpcGroup(items) {
  const mine = [];
  const others = {};
  for (const e of items) {
    const kid = e.hometownKingdomId || 0;
    if (kid === 1) { mine.push(e); continue; }
    if (!others[kid]) others[kid] = [];
    others[kid].push(e);
  }
  let h = '';
  // 我方（按兵种分：工作者/士兵）
  if (mine.length > 0) {
    const workers = [];
    const soldiers = [];
    for (const e of mine) {
      const stn = e.soldierTypeName || '';
      if (stn === '民兵' || stn === '市民') workers.push(e); else soldiers.push(e);
    }
    let inner = '';
    if (workers.length > 0) inner += editorGroupHtml('工作者', '#f39c12', '#fff', workers.length + ' 个', editorItemsHtml(workers));
    if (soldiers.length > 0) inner += editorGroupHtml('士兵', '#3498db', '#fff', soldiers.length + ' 个', editorItemsHtml(soldiers));
    h += editorGroupHtml('我方', 'var(--success-dark, #27ae60)', '#fff', mine.length + ' 个', inner, {hashes: mine.map(e => e.ptrHash)});
  }
  // 其他阵营按王国分组
  const sortedKids = Object.keys(others).map(Number).sort((a,b) => a - b);
  for (const kid of sortedKids) {
    const group = others[kid];
    const kInfo = getKingdomInfo(kid);
    const label = kInfo ? esc(kInfo.name) : ('阵营' + kid);
    h += editorGroupHtml(label, kInfo ? kInfo.bg : 'var(--text-muted)', kInfo ? kInfo.fg : '#fff', group.length + ' 个', editorItemsHtml(group));
  }
  return h;
}

function renderTerritoryGroup(items) {
  const mine = [];
  const others = {};
  for (const e of items) {
    const kid = e.territoryKingdomId || e.hometownKingdomId || 0;
    if (kid === 1) { mine.push(e); continue; }
    if (!others[kid]) others[kid] = [];
    others[kid].push(e);
  }
  let h = '';
  // 我方（默认展开）
  if (mine.length > 0)
    h += editorGroupHtml('我方', 'var(--success-dark, #27ae60)', '#fff', mine.length + ' 个', editorItemsHtml(mine));
  // 其他阵营按 territory 分组（默认收起）
  const sortedKids = Object.keys(others).map(Number).sort((a,b) => a - b);
  for (const kid of sortedKids) {
    const group = others[kid];
    const kInfo = getKingdomInfo(kid);
    const label = kInfo ? esc(kInfo.name) : ('阵营' + kid);
    h += editorGroupHtml(label, kInfo ? kInfo.bg : 'var(--text-muted)', kInfo ? kInfo.fg : '#fff', group.length + ' 个', editorItemsHtml(group));
  }
  return h;
}

function renderBuildingGroup(items) {
  const monsterBuildNames = ['巨树', '巨型蘑菇', '巨型蜂巢'];
  const monsterBuilds = {};
  const normalBuilds = [];
  for (const e of items) {
    const dn = e.npcName || e.name || e.goName || '';
    let matched = false;
    for (const mn of monsterBuildNames) {
      if (dn.includes(mn)) {
        if (!monsterBuilds[mn]) monsterBuilds[mn] = [];
        monsterBuilds[mn].push(e);
        matched = true;
        break;
      }
    }
    if (!matched) normalBuilds.push(e);
  }
  const monsterKeys = Object.keys(monsterBuilds);
  let h = '';
  if (monsterKeys.length > 0) {
    const totalMonster = monsterKeys.reduce((s, k) => s + monsterBuilds[k].length, 0);
    let inner = '';
    for (const mn of monsterBuildNames) {
      const group = monsterBuilds[mn];
      if (!group || group.length === 0) continue;
      inner += editorGroupHtml(esc(mn), 'var(--warning, #f39c12)', '#fff', group.length + ' 个', editorItemsHtml(group));
    }
    h += editorGroupHtml('怪物建筑 备注：这里显示我方是因为可交互的', 'var(--danger, #e74c3c)', '#fff', totalMonster + ' 个', inner,
      {hashes: [].concat(...monsterKeys.map(k => monsterBuilds[k].map(e => e.ptrHash)))});
  }
  if (normalBuilds.length > 0) {
    if (monsterKeys.length > 0) h += '<div style="margin:8px 0 4px 12px;font-size:12px;font-weight:600;color:var(--text-muted)">普通建筑 (' + normalBuilds.length + ')</div>';
    h += renderTerritoryGroup(normalBuilds);
  }
  return h;
}

function renderAnimalGroup(items) {
  const hasTerritory = [];
  const noTerritory = {};
  for (const e of items) {
    const kid = e.territoryKingdomId || e.hometownKingdomId || 0;
    if (kid > 0) { hasTerritory.push(e); continue; }
    const label = e.npcName || e.name || e.goName || '未知';
    if (!noTerritory[label]) noTerritory[label] = [];
    noTerritory[label].push(e);
  }
  let h = '';
  if (hasTerritory.length > 0) h += renderTerritoryGroup(hasTerritory);
  const sortedLabels = Object.keys(noTerritory).sort();
  for (const label of sortedLabels) {
    const group = noTerritory[label];
    h += editorGroupHtml(esc(label), 'var(--warning, #f39c12)', '#fff', group.length + ' 个', editorItemsHtml(group));
  }
  return h;
}


function renderEntityEditorPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style="padding:20px;height:100%;display:flex;flex-direction:column;overflow:hidden">';
  html += '<h2 style="color:var(--accent-light);margin-bottom:16px;font-size:18px">&#x270F; 修改</h2>';

  html += '<div style="display:flex;gap:12px;margin-bottom:20px;align-items:center">';
  html += '<button id="entityEditorScanBtn" onclick="entityEditorScan()" style="padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500">开始扫描</button>';
  html += '<span style="color:var(--text-muted);font-size:13px">' + entityEditorData.length + ' 个实体</span>';
  html += '</div>';

  if (entityEditorData.length === 0) {
    html += '<div style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center">';
    html += '<div style="font-size:48px;margin-bottom:16px">&#x270F;</div>';
    html += '<div style="color:var(--text-secondary);font-size:16px;margin-bottom:8px">暂无数据</div>';
    html += '<div style="color:var(--text-muted);font-size:13px">点击上方按钮扫描实体</div>';
    html += '</div>';
  } else {
    // 按类别分组
    function editorClassify(cn) {
      if (cn === 'Npc') return 'npc';
      if (cn.startsWith('Facility')) return 'building';
      if (cn.includes('Animal')) return 'animal';
      if (cn.startsWith('Monster')) return 'monster';
      if (cn.includes('Ship')) return 'ship';
      if (cn.startsWith('Stuff')) return 'drop';
      return 'other';
    }
    const catDefs = [
      {key:'npc', label:'NPC', icon:'&#x1F464;', color:'#3498db'},
      {key:'monster', label:'怪物', icon:'&#x1F47E;', color:'var(--danger, #e74c3c)'},
      {key:'building', label:'建筑物', icon:'&#x1F3D7;', color:'var(--success)'},
      {key:'animal', label:'动物', icon:'&#x1F43E;', color:'var(--warning, #f39c12)'},
      {key:'ship', label:'船', icon:'&#x26F5;', color:'#3498db'},
      {key:'drop', label:'掉落物', icon:'&#x1F4E6;', color:'var(--accent)'},
      {key:'other', label:'其他（别乱删除容易游戏奔溃）', icon:'&#x2753;', color:'var(--text-muted)'}
    ];
    const groups = {};
    for (const cd of catDefs) groups[cd.key] = [];
    for (const e of entityEditorData) {
      const cat = editorClassify(e.className || '');
      groups[cat].push(e);
    }


    html += '<div style="flex:1;overflow-y:auto;padding-right:8px">';
    for (const cd of catDefs) {
      const items = groups[cd.key];
      if (items.length === 0) continue;
      const catHashes = items.map(e => e.ptrHash);
      html += '<details style="margin-bottom:12px">';
      html += '<summary style="cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px">';
      html += '<span>' + cd.icon + '</span>';
      html += '<span style="color:' + cd.color + '">' + cd.label + '</span>';
      html += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + items.length + ' 个</span>';
      html += '</summary>';
      if (cd.key === 'npc') {
        html += renderNpcGroup(items);
      } else if (cd.key === 'monster' || cd.key === 'ship') {
        html += renderTerritoryGroup(items);
      } else if (cd.key === 'building') {
        html += renderBuildingGroup(items);
      } else if (cd.key === 'animal') {
        html += renderAnimalGroup(items);
      } else {
        html += '<div style="display:flex;flex-direction:column;gap:6px;padding:8px 0">';
        for (const e of items) html += renderEditorEntityItem(e);
        html += '</div>';
      }
      html += '</details>';
    }
    html += '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
}
