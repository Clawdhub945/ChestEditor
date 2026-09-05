// dragon — 由 HtmlUI 单体拆分（原 app.js）
function renderDragonItems() {
  const el = document.getElementById('dragonItems');
  if (!el) return;
  let html = '';
  for (const it of dragonItems) {
    html += '<div style="display:flex;align-items:center;gap:8px;padding:6px 0;border-bottom:1px solid var(--border)">';
    html += '<img src="/icon/' + it.stuffId + '" onerror="hideImg(this)" style="width:24px;height:24px;image-rendering:pixelated;border-radius:4px;background:var(--bg-input);padding:2px">';
    html += '<div style="flex:1;min-width:0">';
    html += '<div style="font-size:12px;font-weight:500;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(it.name) + '</div>';
    html += '<div style="font-size:10px;color:var(--text-muted)">ID:' + it.stuffId + '</div>';
    html += '</div>';
    html += '<input type="number" class="count-input" value="' + it.count + '" min="0" id="dragon_' + it.stuffId + '" style="width:60px">';
    html += '<button class="btn-adj" onclick="setDragonItem(' + it.stuffId + ')" style="font-size:11px;padding:3px 8px;width:auto;height:auto">设置</button>';
    html += '</div>';
  }
  el.innerHTML = html;
}


async function setDragonItem(stuffId) {
  const input = document.getElementById('dragon_' + stuffId);
  const newCount = parseInt(input.value) || 0;
  try {
    const r = await fetch('/api/dragon/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:stuffId, count:newCount})
    });
    dragonItems = await r.json();
    if (dragonView === 'materials') renderDragonMaterialsPanel();
    renderDragonItems();
    toast('龙素材已更新');
  } catch(e) { toast('操作失败', true); }
}


function renderDragonSummon() {
  const el = document.getElementById('dragonSummon');
  if (!el || dragonTypes.length === 0) return;
  let html = '<div style="font-size:11px;font-weight:600;color:var(--text-muted);margin-bottom:4px">召唤龙</div>';
  html += '<div style="display:flex;gap:6px;align-items:center;flex-wrap:wrap">';
  html += '<select id="dragonTypeSelect" style="flex:1;min-width:80px;font-size:11px;padding:2px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px">';
  for (let i = 0; i < dragonTypes.length; i++) {
    html += '<option value="' + i + '">' + esc(dragonTypes[i].cn) + '</option>';
  }
  html += '</select>';
  html += '<select id="dragonLevelSelect" style="width:50px;font-size:11px;padding:2px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px">';
  for (let lv = 1; lv <= 10; lv++) {
    html += '<option value="' + lv + '">' + lv + '级</option>';
  }
  html += '</select>';
  html += '<button class="btn-adj" onclick="doSummonDragon()" style="font-size:11px;padding:3px 8px;background:#2a4a2a;color:#6f6">召唤</button>';
  html += '</div>';
  el.innerHTML = html;
}


async function doSummonDragon() {
  const typeIdx = parseInt(document.getElementById('dragonTypeSelect').value);
  const level = parseInt(document.getElementById('dragonLevelSelect').value);
  const typeName = dragonTypes[typeIdx] ? dragonTypes[typeIdx].cn : '';
  try {
    const r = await fetch('/api/dragon/summon', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({typeIndex:typeIdx, level:level})
    });
    const res = await r.json();
    if (res.result && (res.result.includes('True') || res.result === 'ok')) {
      toast(typeName + ' Lv' + level + ' 召唤成功!');
    } else {
      toast('召唤结果: ' + (res.result || '未知'), true);
    }
  } catch(e) { toast('召唤失败', true); }
}


function renderDragonSoulsList() {
  const el = document.getElementById('dragonSoulsList');
  if (!el) return;
  if (dragonSouls.length === 0) {
    el.innerHTML = '<div style="padding:8px;color:var(--text-muted);font-size:11px;text-align:center">暂无龙魂</div>';
    return;
  }
  let html = '';
  // 搜索地图龙按钮
  html += '<div style="margin-bottom:6px"><button class="btn-adj" onclick="searchMapDragons()" style="font-size:11px;padding:3px 8px;width:auto;height:auto">搜索地图龙</button></div>';
  for (let i = 0; i < dragonSouls.length; i++) {
    const s = dragonSouls[i];
    const active = s.is_active ? '已召唤' : '待命';
    const activeColor = s.is_active ? '#4caf50' : '#888';
    const stuffId = s.StuffId || s.stuff_id || 0;
    const typeName = findDragonTypeName(stuffId);
    const typeIdx = findDragonTypeIndex(stuffId);
    html += '<div style="padding:6px 0;border-bottom:1px solid var(--border)">';
    html += '<div style="display:flex;align-items:center;gap:8px">';
    html += '<div style="width:32px;height:32px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden">';
    if (typeIdx >= 0) html += '<img src="/api/dragon/icon/' + typeIdx + '" style="width:28px;height:28px;object-fit:contain" onerror="hideImg(this)">';
    else html += '<span style="font-size:14px">&#x1F409;</span>';
    html += '</div>';
    html += '<div style="flex:1;min-width:0">';
    html += '<div style="font-size:12px;font-weight:500">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    html += '<div style="font-size:10px;color:' + activeColor + '">' + active + '</div>';
    html += '</div></div>';
    // 强化编辑
    const parts = [
      {key:'Head', label:'头', enhanceRange:[1001,1050]},
      {key:'Claw', label:'爪', enhanceRange:[3001,3050]},
      {key:'Shield', label:'甲', enhanceRange:[2001,2050]},
      {key:'Cloud', label:'魂', enhanceRange:[4001,4050]},
    ];
    html += '<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:4px;margin-left:36px;align-items:center">';
    for (const p of parts) {
      const v = s[p.key] ?? s[p.key.toLowerCase()] ?? 0;
      html += '<span style="font-size:10px;color:var(--text-muted)">' + p.label + ':</span>';
      html += '<input type="number" id="soul_' + i + '_' + p.key + '" value="' + v + '" min="0" max="50" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px">';
      html += '<button class="btn-adj" onclick="setSoulProp(' + i + ',\'' + p.key + '\',' + i + ')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
    }
    // potentiality
    const pot = s.Potentiality ?? s.potentiality ?? 0;
    html += '<span style="font-size:10px;color:var(--text-muted)">潜力:</span>';
    html += '<input type="number" id="soul_' + i + '_Potentiality" value="' + pot + '" min="0" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px">';
    html += '<button class="btn-adj" onclick="setSoulProp(' + i + ',\'Potentiality\',' + i + ')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
    html += '</div>';
    // nature
    const natures = s.NatureList || s.nature_list;
    if (natures && natures.length > 0) {
      html += '<div style="display:flex;flex-wrap:wrap;gap:3px;margin-top:3px;margin-left:36px">';
      for (const nid of natures) {
        const n = dragonNatures.find(x => x.id === nid);
        html += '<span style="font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf">' + (n ? n.name : '#' + nid) + '</span>';
      }
      html += '</div>';
    }
    html += '</div>';
  }
  el.innerHTML = html;
}


async function setSoulProp(idx, prop) {
  const input = document.getElementById('soul_' + idx + '_' + prop);
  const val = parseInt(input.value) || 0;
  try {
    const r = await fetch('/api/dragon/soul/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({index:idx, property:prop, value:val})
    });
    const res = await r.json();
    if (res.ok) {
      toast(prop + ' 已设置为 ' + val);
      fetchDragonSouls().then(() => { renderSidebar(); if (showDragonSouls) renderContent(); });
    } else {
      toast(res.error || '设置失败', true);
    }
  } catch(e) { toast('操作失败', true); }
}


async function searchMapDragons() {
  try {
    await fetch('/api/dragon/searchmap', {method:'POST'});
    toast('已搜索，查看BepInEx日志');
  } catch(e) { toast('搜索失败', true); }
}


async function searchMapDragonEntities() {
  try {
    await fetch('/api/dragon/searchmap2', {method:'POST'});
    toast('已搜索龙实体，查看BepInEx日志');
  } catch(e) { toast('搜索失败', true); }
}


function findDragonTypeIndex(stuffId) {
  if (!stuffId) return -1;
  for (let i = 0; i < dragonTypes.length; i++) {
    if (stuffId >= dragonTypes[i].baseId && stuffId <= dragonTypes[i].baseId + 9) return i;
  }
  return -1;
}


function findDragonTypeName(stuffId) {
  if (!stuffId) return '';
  for (const dt of dragonTypes) {
    if (stuffId >= dt.baseId && stuffId <= dt.baseId + 9) {
      return dt.cn + ' Lv' + (stuffId - dt.baseId + 1);
    }
  }
  return 'ID:' + stuffId;
}


function renderDragonSummonList() {
  const el = document.getElementById('dragonSummonList');
  if (!el || dragonTypes.length === 0) return;
  let html = '';
  for (let i = 0; i < dragonTypes.length; i++) {
    const dt = dragonTypes[i];
    html += '<div style="padding:8px 0;border-bottom:1px solid var(--border)">';
    html += '<div style="display:flex;align-items:center;gap:8px">';
    html += '<div style="width:28px;height:28px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;font-size:14px">&#x1F409;</div>';
    html += '<div style="flex:1;min-width:0">';
    html += '<div style="font-size:12px;font-weight:500">' + esc(dt.cn) + '</div>';
    html += '<div style="font-size:10px;color:var(--text-muted)">' + esc(dt.name) + ' (ID:' + dt.baseId + '~' + (dt.baseId+9) + ')</div>';
    html += '</div>';
    html += '<select id="summonLv_' + i + '" style="width:48px;font-size:11px;padding:2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px">';
    for (let lv = 1; lv <= 10; lv++) {
      html += '<option value="' + lv + '">' + lv + '级</option>';
    }
    html += '</select>';
    html += '<button class="btn-adj" onclick="doSummonDragonAt(' + i + ')" style="font-size:11px;padding:3px 8px;background:#2a4a2a;color:#6f6;width:auto;height:auto">召唤</button>';
    html += '</div>';
    // nature 选择标签
    html += '<div style="display:flex;flex-wrap:wrap;gap:3px;margin-top:4px;margin-left:36px">';
    for (const n of dragonNatures) {
      const checked = natureSelected(i, n.id);
      html += '<span class="filter-tag' + (checked ? ' on' : '') + '" onclick="toggleSummonNature(' + i + ',' + n.id + ',this)" style="font-size:10px;padding:1px 5px">' + n.name + '</span>';
    }
    html += '</div>';
    html += '</div>';
  }
  el.innerHTML = html;
}

// 每条龙选中的 nature 存在全局对象里

let _summonNatures = {};


function natureSelected(typeIdx, natureId) {
  const key = 't' + typeIdx;
  return _summonNatures[key] && _summonNatures[key].includes(natureId);
}


function toggleSummonNature(typeIdx, natureId, el) {
  const key = 't' + typeIdx;
  if (!_summonNatures[key]) _summonNatures[key] = [];
  const arr = _summonNatures[key];
  const idx = arr.indexOf(natureId);
  if (idx >= 0) { arr.splice(idx, 1); el.classList.remove('on'); }
  else { arr.push(natureId); el.classList.add('on'); }
}


async function doSummonDragonAt(typeIdx) {
  const level = parseInt(document.getElementById('summonLv_' + typeIdx).value);
  const typeName = dragonTypes[typeIdx] ? dragonTypes[typeIdx].cn : '';
  const natures = _summonNatures['t' + typeIdx] || [];
  try {
    const r = await fetch('/api/dragon/summon', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({typeIndex:typeIdx, level:level, natures:natures})
    });
    const res = await r.json();
    if (res.result && (res.result.includes('True') || res.result === 'ok')) {
      toast(typeName + ' Lv' + level + ' 召唤成功!');
    } else {
      toast('召唤结果: ' + (res.result || '未知'), true);
    }
  } catch(e) { toast('召唤失败', true); }
}


function selectDragonView(view) {
  selectedChest = -1;
  dragonView = (dragonView === view) ? '' : view;
  showDragonSouls = (dragonView === 'souls');
  renderSidebar();
  renderContent();
}


function renderDragonSoulsPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el) return;
  const activeSouls = dragonSouls.filter(s => s.is_active);
  if (activeSouls.length === 0) {
    el.innerHTML = '<div style="padding:20px;text-align:center;color:var(--text-muted)">暂无活跃龙魂</div>';
    return;
  }
  let html = '';
  html += '<div class="plan-section" style="margin-top:12px">';
  html += '<div class="plan-header"><span>地图龙 (' + activeSouls.length + ')</span>';
  html += '<button class="btn-adj" onclick="refreshDragonEntities()" style="font-size:11px;padding:3px 8px;width:auto;height:auto;margin-left:auto">刷新属性</button></div>';
  html += '<div class="items" style="max-height:500px;overflow-y:auto">';
  for (let i = 0; i < dragonSouls.length; i++) {
    const s = dragonSouls[i];
    if (!s.is_active) continue;
    const stuffId = s.StuffId || s.stuff_id || 0;
    const typeName = findDragonTypeName(stuffId);
    const typeIdx = findDragonTypeIndex(stuffId);
    const active = s.is_active ? '已召唤' : '待命';
    const activeColor = s.is_active ? '#4caf50' : '#888';

    html += '<div class="item" style="flex-direction:column;align-items:flex-start;gap:4px">';
    html += '<div style="display:flex;align-items:center;gap:8px;width:100%">';
    html += '<div style="width:48px;height:48px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0">';
    if (typeIdx >= 0) html += '<img src="/api/dragon/icon/' + typeIdx + '" style="width:44px;height:44px;object-fit:contain" onerror="hideImg(this)">';
    else html += '<span style="font-size:24px">&#x1F409;</span>';
    html += '</div>';
    html += '<div style="flex:1;min-width:0">';
    html += '<div class="iname">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    html += '<div style="font-size:10px;color:' + activeColor + '">' + active + '</div>';
    html += '</div></div>';

    // 强化编辑
    const parts = [
      {key:'head', label:'龙头'}, {key:'claw', label:'龙爪'},
      {key:'shield', label:'龙甲'}, {key:'cloud', label:'龙魂'},
      {key:'potentiality', label:'潜力'},
    ];
    html += '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%">';
    for (const p of parts) {
      const v = s[p.key] ?? 0;
      html += '<div style="background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px">';
      html += '<span style="font-size:9px;color:var(--text-muted)">' + p.label + '</span>';
      html += '<div style="display:flex;align-items:center;gap:2px">';
      html += '<input type="number" id="soul_' + i + '_' + p.key + '" value="' + v + '" min="0" max="50" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-secondary);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
      html += '<button class="btn-adj" onclick="setSoulProp(' + i + ',\'' + p.key + '\')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
      html += '</div></div>';
    }
    html += '</div>';

    // nature
    const natures = s.nature_list;
    if (natures && natures.length > 0) {
      html += '<div style="display:flex;flex-wrap:wrap;gap:3px;width:100%">';
      for (const nid of natures) {
        const n = dragonNatures.find(x => x.id === nid);
        html += '<span style="font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf">' + (n ? n.name : '#' + nid) + '</span>';
      }
      html += '</div>';
    }

    // 战斗属性（匹配实体）
    let ents = [];
    try { ents = Array.isArray(dragonEntities) ? dragonEntities.filter(e => Number(e.stuff_id) === Number(stuffId) && Number(e.guid) > 0) : []; } catch(ex) {}
    for (const e of ents) {
      const hp = e.hp || 0;
      const hpTotal = e.hp_total || 0;
      const hpPct = hpTotal > 0 ? Math.round(hp / hpTotal * 100) : 0;
      html += '<div style="width:100%;background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:6px">';
      html += '<div style="display:flex;justify-content:space-between;font-size:9px;color:var(--text-muted);margin-bottom:3px"><span>HP (GUID:' + e.guid + ')</span><span>' + Math.round(hp) + ' / ' + Math.round(hpTotal) + ' (' + hpPct + '%)</span></div>';
      html += '<div style="background:var(--bg-secondary);border-radius:3px;height:6px;overflow:hidden;margin-bottom:4px">';
      html += '<div style="background:' + (hpPct > 50 ? '#4caf50' : hpPct > 20 ? '#ff9800' : '#f44336') + ';height:100%;width:' + hpPct + '%"></div>';
      html += '</div>';
      const efields = [
        {key:'hp', label:'当前HP', step:1}, {key:'hp_total', label:'HP上限', step:1},
        {key:'atk_max', label:'物攻', step:1}, {key:'magic_atk_max', label:'魔攻', step:1},
        {key:'speed', label:'速度', step:0.01}, {key:'power', label:'力量', step:1},
      ];
      html += '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%">';
      for (const f of efields) {
        const v = (e[f.key] || 0);
        const disp = f.step < 1 ? v.toFixed(2) : Math.round(v);
        html += '<div style="background:var(--bg-secondary);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px">';
        html += '<span style="font-size:9px;color:var(--text-muted)">' + f.label + '</span>';
        html += '<div style="display:flex;align-items:center;gap:2px;width:100%">';
        html += '<input type="number" id="de_' + e.guid + '_' + f.key + '" value="' + disp + '" step="' + f.step + '" style="flex:1;min-width:0;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
        html += '<button class="btn-adj" onclick="setDE(' + e.guid + ',\'' + f.key + '\')" style="font-size:9px;padding:1px 4px;width:auto;height:auto;flex-shrink:0">设</button>';
        html += '</div></div>';
      }
      html += '</div></div>';
    }

    html += '</div>';
  }
  html += '</div></div>';
  el.innerHTML = html;
}


function renderDragonMaterialsPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el) return;
  let html = '';
  html += '<div class="plan-section">';
  html += '<div class="plan-header"><span>龙素材 (' + dragonItems.length + ')</span></div>';
  html += '<div class="items">';
  for (const it of dragonItems) {
    html += '<div class="item">';
    html += '<img src="/icon/' + it.stuffId + '" onerror="hideImg(this)">';
    html += '<div class="iname" title="' + esc(it.name) + '">' + esc(it.name) + '</div>';
    html += '<div class="iid">ID:' + it.stuffId + '</div>';
    html += '<div class="icount">';
    html += '<input type="number" class="count-input" value="' + it.count + '" min="0" id="dragon_' + it.stuffId + '">';
    html += '</div>';
    html += '<div class="btns">';
    html += '<button class="btn-rm" onclick="setDragonItem(' + it.stuffId + ')">设置</button>';
    html += '</div></div>';
  }
  if (dragonItems.length === 0) html += '<div style="padding:20px;text-align:center;color:var(--text-muted)">暂无龙素材</div>';
  html += '</div></div>';
  el.innerHTML = html;
}


function renderDragonSummonPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el || dragonTypes.length === 0) return;
  const idleSouls = dragonSouls.filter(s => !s.is_active);
  let html = '';

  // 召唤新龙
  html += '<div class="plan-section">';
  html += '<div class="plan-header"><span>召唤新龙 (' + dragonTypes.length + ')</span></div>';
  html += '<div class="items">';
  for (let i = 0; i < dragonTypes.length; i++) {
    const dt = dragonTypes[i];
    html += '<div class="item" style="flex-direction:column;align-items:stretch;gap:6px;padding:10px">';
    // 第一行: 图标 + 名字 + ID
    html += '<div style="display:flex;align-items:center;gap:6px">';
    html += '<div style="width:40px;height:40px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0">';
    html += '<img src="/api/dragon/icon/' + i + '" style="width:36px;height:36px;object-fit:contain" onerror="hideImg(this)">';
    html += '</div>';
    html += '<div style="flex:1;min-width:0">';
    html += '<div class="iname" style="font-size:11px">' + esc(dt.cn) + '</div>';
    html += '<div style="font-size:9px;color:var(--text-muted)">' + esc(dt.name) + ' ID:' + dt.baseId + '</div>';
    html += '</div></div>';
    // 第二行: 等级 + 召唤按钮
    html += '<div style="display:flex;align-items:center;gap:6px">';
    html += '<select id="summonLv_' + i + '" style="flex:1;font-size:10px;padding:3px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px">';
    for (let lv = 1; lv <= 10; lv++) {
      html += '<option value="' + lv + '">' + lv + '级</option>';
    }
    html += '</select>';
    html += '<button class="btn-adj" onclick="doSummonDragonAt(' + i + ')" style="font-size:10px;padding:4px 10px;background:#2a4a2a;color:#6f6;width:auto;height:auto;white-space:nowrap">召唤</button>';
    html += '</div>';
    // nature 选择
    html += '<div style="display:flex;flex-wrap:wrap;gap:3px">';
    for (const n of dragonNatures) {
      const checked = natureSelected(i, n.id);
      html += '<span class="filter-tag' + (checked ? ' on' : '') + '" onclick="toggleSummonNature(' + i + ',' + n.id + ',this)" style="font-size:9px;padding:1px 4px">' + n.name + '</span>';
    }
    html += '</div>';
    html += '</div>';
  }
  html += '</div></div>';

  // 待命龙魂
  if (idleSouls.length > 0) {
    html += '<div class="plan-section">';
    html += '<div class="plan-header"><span>待命龙魂 (' + idleSouls.length + ')</span></div>';
    html += '<div class="items">';
    for (let si = 0; si < dragonSouls.length; si++) {
      const s = dragonSouls[si];
      if (s.is_active) continue;
      const stuffId = s.StuffId || s.stuff_id || 0;
      const typeName = findDragonTypeName(stuffId);
      const typeIdx = findDragonTypeIndex(stuffId);
      html += '<div class="item" style="flex-direction:column;align-items:stretch;gap:6px;padding:10px">';
      html += '<div style="display:flex;align-items:center;gap:6px">';
      html += '<div style="width:40px;height:40px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0">';
      if (typeIdx >= 0) html += '<img src="/api/dragon/icon/' + typeIdx + '" style="width:36px;height:36px;object-fit:contain" onerror="hideImg(this)">';
      else html += '<span style="font-size:20px">&#x1F409;</span>';
      html += '</div>';
      html += '<div style="flex:1;min-width:0;overflow:hidden">';
      html += '<div class="iname" style="font-size:11px">' + esc(typeName || ('龙魂#' + (si+1))) + '</div>';
      html += '<div style="font-size:9px;color:var(--text-muted)">待命</div>';
      html += '</div></div>';
      // 属性
      const parts = [
        {key:'head', label:'龙头'}, {key:'claw', label:'龙爪'},
        {key:'shield', label:'龙甲'}, {key:'cloud', label:'龙魂'},
        {key:'potentiality', label:'潜力'},
      ];
      html += '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%">';
      for (const p of parts) {
        const v = s[p.key] ?? 0;
        html += '<div style="background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px">';
        html += '<span style="font-size:9px;color:var(--text-muted)">' + p.label + '</span>';
        html += '<div style="display:flex;align-items:center;gap:2px">';
        html += '<input type="number" id="idle_soul_' + si + '_' + p.key + '" value="' + v + '" min="0" max="50" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-secondary);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
        html += '<button class="btn-adj" onclick="setSoulProp(' + si + ',\'' + p.key + '\')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
        html += '</div></div>';
      }
      html += '</div>';
      // nature
      const natures = s.nature_list;
      if (natures && natures.length > 0) {
        html += '<div style="display:flex;flex-wrap:wrap;gap:3px;width:100%">';
        for (const nid of natures) {
          const n = dragonNatures.find(x => x.id === nid);
          html += '<span style="font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf">' + (n ? n.name : '#' + nid) + '</span>';
        }
        html += '</div>';
      }
      html += '</div>';
    }
    html += '</div></div>';
  }

  el.innerHTML = html;
}


async function setDE(guid, field) {
  const inp = document.getElementById('de_' + guid + '_' + field);
  if (!inp) return;
  const val = parseFloat(inp.value);
  if (isNaN(val)) { toast('数值无效', true); return; }
  try {
    const r = await fetch('/api/dragon/entity/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({guid, field, value: val})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('已设置');
  } catch(e) { toast('设置失败', true); }
}


async function refreshDragonEntities() {
  await fetchDragonEntities();
  // 不重新渲染，避免覆盖用户正在输入的值和滚动位置
  // 数据已更新，下次交互时自动生效
  toast('龙属性已刷新 (' + dragonEntities.length + '条)');
}

// 设置物品数量（先删除全部再添加指定数量）
