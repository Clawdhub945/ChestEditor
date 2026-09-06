// dragon — 龙系统面板：素材背包、龙魂强化、召唤、龙实体属性
// ===== 共用片段 =====

// 龙图标（typeIdx 无效时回退为龙 emoji）
function dragonIconHtml(typeIdx, boxSize, imgSize) {
  const inner = (typeIdx >= 0)
    ? '<img src="/api/dragon/icon/' + typeIdx + '" style="width:' + imgSize + 'px;height:' + imgSize + 'px;object-fit:contain" onerror="hideImg(this)">'
    : '<span style="font-size:' + Math.round(imgSize * 0.55) + 'px">&#x1F409;</span>';
  return '<div style="width:' + boxSize + 'px;height:' + boxSize + 'px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0">' + inner + '</div>';
}

// 等级下拉（召唤 UI 三处共用）
function summonLevelSelectHtml(id, style) {
  let h = '<select id="' + id + '" style="' + style + '">';
  for (let lv = 1; lv <= 10; lv++) h += '<option value="' + lv + '">' + lv + '级</option>';
  return h + '</select>';
}

// 龙魂天性标签（列表/面板/待命三处共用）
function natureTagsHtml(natures, containerStyle) {
  if (!natures || natures.length === 0) return '';
  let h = '<div style="display:flex;flex-wrap:wrap;gap:3px;' + containerStyle + '">';
  for (const nid of natures) {
    const n = dragonNatures.find(x => x.id === nid);
    h += '<span style="font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf">' + (n ? n.name : '#' + nid) + '</span>';
  }
  return h + '</div>';
}

// 强化等级上限（超过自动跳回）
const SOUL_PART_MAX = 50;

function clampSoulInput(input) {
  let v = parseInt(input.value);
  if (isNaN(v) || v < 0) v = 0;
  if (v > SOUL_PART_MAX) v = SOUL_PART_MAX;
  input.value = v;
}

// 龙魂强化编辑器（侧栏列表 / 待命列表 共用）
// 后端字段为小写 head/claw/shield/cloud/potentiality（经 /api/dragon/souls 实测确认）
const SOUL_PARTS_GRID = [
  {key:'head', label:'龙头', max:50}, {key:'claw', label:'龙爪', max:50},
  {key:'shield', label:'龙甲', max:50}, {key:'cloud', label:'龙魂', max:50},
  {key:'potentiality', label:'潜力', max:50},
];
const SOUL_PARTS_COMPACT = [
  {key:'head', label:'头', max:50}, {key:'claw', label:'爪', max:50},
  {key:'shield', label:'甲', max:50}, {key:'cloud', label:'魂', max:50},
  {key:'potentiality', label:'潜力'},
];

function soulPartsEditorHtml(soul, soulIdx, idPrefix, parts, useGrid) {
  const get = (k) => soul[k] ?? soul[k.toLowerCase()] ?? 0;
  let h = '';
  if (useGrid) h += '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%">';
  else h += '<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:4px;margin-left:36px;align-items:center">';
  for (const p of parts) {
    const v = get(p.key);
    const maxAttr = p.max ? ' max="' + p.max + '"' : '';
    if (useGrid) {
      h += '<div style="background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px">';
      h += '<span style="font-size:9px;color:var(--text-muted)">' + p.label + '</span>';
      h += '<div style="display:flex;align-items:center;gap:2px">';
      h += '<input type="number" id="' + idPrefix + soulIdx + '_' + p.key + '" value="' + v + '" min="0"' + maxAttr + ' onchange="clampSoulInput(this)" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-secondary);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
      h += '<button class="btn-adj" onclick="setSoulProp(' + soulIdx + ',\'' + p.key + '\')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
      h += '</div></div>';
    } else {
      h += '<span style="font-size:10px;color:var(--text-muted)">' + p.label + ':</span>';
      h += '<input type="number" id="' + idPrefix + soulIdx + '_' + p.key + '" value="' + v + '" min="0"' + maxAttr + ' onchange="clampSoulInput(this)" style="width:36px;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px">';
      h += '<button class="btn-adj" onclick="setSoulProp(' + soulIdx + ',\'' + p.key + '\')" style="font-size:9px;padding:1px 4px;width:auto;height:auto">设</button>';
    }
  }
  h += '</div>';
  return h;
}

// ===== 侧栏：龙素材 + 召唤快捷栏 =====

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
  html += summonLevelSelectHtml('dragonLevelSelect', 'width:50px;font-size:11px;padding:2px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px');
  html += '<button class="btn-adj" onclick="doSummonDragon()" style="font-size:11px;padding:3px 8px;background:#2a4a2a;color:#6f6">召唤</button>';
  html += '</div>';
  el.innerHTML = html;
}

// 召唤请求（快捷栏与召唤面板共用）
async function summonDragonRequest(typeIndex, level, natures) {
  const typeName = dragonTypes[typeIndex] ? dragonTypes[typeIndex].cn : '';
  try {
    const body = {typeIndex:typeIndex, level:level};
    if (natures && natures.length > 0) body.natures = natures;
    const r = await fetch('/api/dragon/summon', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify(body)
    });
    const res = await r.json();
    if (res.result && (res.result.includes('True') || res.result === 'ok')) {
      toast(typeName + ' Lv' + level + ' 召唤成功!');
      // 立即扫描地图实体 + 拉取龙魂列表（不等 3 秒轮询），
      // 实体先扫、龙魂后拉（龙魂变化触发重渲染时实体数据已是最新）
      await fetchDragonEntities();
      await fetchDragonSouls();
      if (dragonView === 'souls') renderDragonSoulsPanel();
    } else {
      toast('召唤结果: ' + (res.result || '未知'), true);
    }
  } catch(e) { toast('召唤失败', true); }
}


async function doSummonDragon() {
  const typeIdx = parseInt(document.getElementById('dragonTypeSelect').value);
  const level = parseInt(document.getElementById('dragonLevelSelect').value);
  return summonDragonRequest(typeIdx, level);
}


async function doSummonDragonAt(typeIdx) {
  const level = parseInt(document.getElementById('summonLv_' + typeIdx).value);
  const natures = _summonNatures['t' + typeIdx] || [];
  return summonDragonRequest(typeIdx, level, natures);
}


// ===== 侧栏：龙魂列表 =====

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
    html += dragonIconHtml(typeIdx, 32, 28);
    html += '<div style="flex:1;min-width:0">';
    html += '<div style="font-size:12px;font-weight:500">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    html += '<div style="font-size:10px;color:' + activeColor + '">' + active + '</div>';
    html += '</div></div>';
    html += soulPartsEditorHtml(s, i, 'soul_', SOUL_PARTS_COMPACT, false);
    html += natureTagsHtml(s.NatureList || s.nature_list, 'margin-top:3px;margin-left:36px');
    html += '</div>';
  }
  el.innerHTML = html;
}


async function setSoulProp(idx, prop) {
  const input = document.getElementById('soul_' + idx + '_' + prop);
  const val = parseInt(input.value) || 0;
  await postSoulProp(idx, prop, val, true);
}

// 提交单个强化值；refresh=true 时成功后刷新数据与界面（批量设置时只在最后一次刷新）
async function postSoulProp(idx, prop, val, refresh) {
  try {
    const r = await fetch('/api/dragon/soul/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({index:idx, property:prop, value:val})
    });
    const res = await r.json();
    if (res.ok) {
      if (refresh) {
        toast(prop + ' 已设置为 ' + val);
        fetchDragonSouls().then(() => { renderSidebar(); if (showDragonSouls) renderContent(); });
      }
      return true;
    }
    toast(res.error || '设置失败', true);
    return false;
  } catch(e) { toast('操作失败', true); return false; }
}

// 一键设置五项强化（上限 SOUL_PART_MAX）
async function setAllSoulParts(idx) {
  const vInput = document.getElementById('soulall_' + idx);
  const val = Math.max(0, Math.min(SOUL_PART_MAX, parseInt(vInput.value) || 0));
  vInput.value = val;
  const keys = SOUL_PARTS_GRID.map(p => p.key);
  for (const k of keys) {
    const input = document.getElementById('soul_' + idx + '_' + k);
    if (input) input.value = val;
  }
  let ok = 0;
  for (const k of keys) {
    if (await postSoulProp(idx, k, val, false)) ok++;
  }
  if (ok === keys.length) {
    toast('五项强化已全部设为 ' + val);
    fetchDragonSouls().then(() => { renderSidebar(); if (showDragonSouls) renderContent(); });
  } else {
    toast('部分设置失败 (' + ok + '/' + keys.length + ')', true);
  }
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


// ===== 侧栏：召唤列表（按类型 + 天性选择） =====

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
    html += summonLevelSelectHtml('summonLv_' + i, 'width:48px;font-size:11px;padding:2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px');
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


function selectDragonView(view) {
  selectedChest = -1;
  dragonView = (dragonView === view) ? '' : view;
  showDragonSouls = (dragonView === 'souls');
  renderSidebar();
  renderContent();
}


// ===== 主内容区：地图龙面板 =====
// 卡片结构（约330px宽）：头部=图标+名字；中部双列=强化(左)|战斗属性(右)单行字段；底部=天性通栏

// 单行字段：label + 输入框 + 设（强化与战斗属性共用）
function dragonFieldRowHtml(label, inputHtml) {
  let h = '<div style="display:flex;align-items:center;gap:4px">';
  h += '<span style="font-size:10px;color:var(--text-muted);flex:0 0 auto">' + label + '</span>';
  h += inputHtml;
  h += '</div>';
  return h;
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
  html += '<div class="dragon-grid">';
  for (let i = 0; i < dragonSouls.length; i++) {
    const s = dragonSouls[i];
    if (!s.is_active) continue;
    const stuffId = s.StuffId || s.stuff_id || 0;
    const typeName = findDragonTypeName(stuffId);
    const typeIdx = findDragonTypeIndex(stuffId);
    const active = s.is_active ? '已召唤' : '待命';
    const activeColor = s.is_active ? '#4caf50' : '#888';

    // 头部：图标 + 名字 + 状态
    let head = '<div style="display:flex;align-items:center;gap:8px">';
    head += dragonIconHtml(typeIdx, 40, 36);
    head += '<div style="flex:1;min-width:0">';
    head += '<div class="iname" style="font-size:13px">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    head += '<div style="font-size:10px;color:' + activeColor + '">' + active + '</div>';
    head += '</div></div>';

    // 左列：强化（单行字段）+ 一键设置
    let left = '';
    left += '<div style="display:flex;align-items:center;gap:4px;margin-bottom:2px">';
    left += '<span style="font-size:10px;color:var(--text-muted);flex:1">强化(≤' + SOUL_PART_MAX + ')</span>';
    left += '<input type="number" id="soulall_' + i + '" value="' + SOUL_PART_MAX + '" min="0" max="' + SOUL_PART_MAX + '" onchange="clampSoulInput(this)" style="width:44px;font-size:10px;padding:1px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
    left += '<button class="btn-adj" onclick="setAllSoulParts(' + i + ')" style="font-size:9px;padding:1px 5px;width:auto;height:auto">一键设置</button>';
    left += '</div>';
    const get = (k) => s[k] ?? s[k.toLowerCase()] ?? 0;
    for (const p of SOUL_PARTS_GRID) {
      const v = get(p.key);
      const inpId = 'soul_' + i + '_' + p.key;
      const inp = '<input type="number" id="' + inpId + '" value="' + v + '" min="0"' + (p.max ? ' max="' + p.max + '"' : '') + ' onchange="clampSoulInput(this)" style="flex:1;min-width:0;font-size:10px;padding:1px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
      const btn = '<button class="btn-adj" onclick="setSoulProp(' + i + ',\'' + p.key + '\')" style="font-size:9px;padding:1px 5px;width:auto;height:auto;flex-shrink:0">设</button>';
      left += dragonFieldRowHtml('<span style="width:30px">' + p.label + '</span>', inp + btn);
    }

    // 右列：匹配实体的战斗属性（单行字段）+ 顶部细血条
    let right = '';
    let ents = [];
    try { ents = Array.isArray(dragonEntities) ? dragonEntities.filter(e => Number(e.stuff_id) === Number(stuffId) && Number(e.guid) > 0) : []; } catch(ex) {}
    if (ents.length === 0) {
      right += '<div style="color:var(--text-muted);font-size:11px;padding:6px 2px">未找到地图实体<br>点右上"刷新属性"扫描</div>';
    }
    for (const e of ents) {
      const hp = e.hp || 0;
      const hpTotal = e.hp_total || 0;
      const hpPct = hpTotal > 0 ? Math.round(hp / hpTotal * 100) : 0;
      right += '<div style="margin-bottom:4px">';
      right += '<div style="display:flex;justify-content:space-between;font-size:9px;color:var(--text-muted);margin-bottom:2px"><span>HP ' + Math.round(hp) + '/' + Math.round(hpTotal) + '</span><span>' + hpPct + '%</span></div>';
      right += '<div style="background:var(--bg-secondary);border-radius:3px;height:5px;overflow:hidden">';
      right += '<div style="background:' + (hpPct > 50 ? '#4caf50' : hpPct > 20 ? '#ff9800' : '#f44336') + ';height:100%;width:' + hpPct + '%"></div>';
      right += '</div></div>';
      const efields = [
        {key:'hp', label:'当前HP', step:1}, {key:'hp_total', label:'HP上限', step:1},
        {key:'atk_max', label:'物攻', step:1}, {key:'magic_atk_max', label:'魔攻', step:1},
        {key:'speed', label:'速度', step:0.01}, {key:'power', label:'力量', step:1},
      ];
      for (const f of efields) {
        const v = (e[f.key] || 0);
        const disp = f.step < 1 ? v.toFixed(2) : Math.round(v);
        const inp = '<input type="number" id="de_' + e.guid + '_' + f.key + '" value="' + disp + '" step="' + f.step + '" style="flex:1;min-width:0;font-size:10px;padding:1px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center">';
        const btn = '<button class="btn-adj" onclick="setDE(' + e.guid + ',\'' + f.key + '\')" style="font-size:9px;padding:1px 5px;width:auto;height:auto;flex-shrink:0">设</button>';
        right += dragonFieldRowHtml('<span style="width:38px">' + f.label + '</span>', inp + btn);
      }
    }

    html += '<div class="dragon-card">';
    // 头部分区框：图标 + 名字 + 状态
    html += '<div class="dragon-sec">' + head + '</div>';
    // 中部双列分区框：强化 | 战斗属性（等高拉伸）
    html += '<div style="display:flex;gap:8px;align-items:stretch;flex:1">';
    html += '<div class="dragon-sec" style="flex:1 1 50%;min-width:0;display:flex;flex-direction:column;gap:3px">' + left + '</div>';
    html += '<div class="dragon-sec" style="flex:1 1 50%;min-width:0;display:flex;flex-direction:column;gap:3px">' + right + '</div>';
    html += '</div>';
    html += natureTagsHtml(s.nature_list, 'margin-top:2px;width:100%');
    html += '</div>';
  }
  html += '</div></div>';
  el.innerHTML = html;
}


// ===== 主内容区：龙素材面板 =====

function renderDragonMaterialsPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el) return;
  let html = '';
  html += '<div class="plan-section">';
  html += '<div class="plan-header"><span>龙素材 (' + dragonItems.length + ')</span></div>';
  html += '<div class="items">';
  for (const it of dragonItems) {
    html += htmlItemCard(it.stuffId, it.name, it.count, {
      inputId: 'dragon_' + it.stuffId,
      buttons: [{label:'设置', onclick:'setDragonItem(' + it.stuffId + ')'}]
    });
  }
  if (dragonItems.length === 0) html += '<div style="padding:20px;text-align:center;color:var(--text-muted)">暂无龙素材</div>';
  html += '</div></div>';
  el.innerHTML = html;
}


// ===== 主内容区：召唤面板 =====

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
    html += dragonIconHtml(i, 40, 36);
    html += '<div style="flex:1;min-width:0">';
    html += '<div class="iname" style="font-size:11px">' + esc(dt.cn) + '</div>';
    html += '<div style="font-size:9px;color:var(--text-muted)">' + esc(dt.name) + ' ID:' + dt.baseId + '</div>';
    html += '</div></div>';
    // 第二行: 等级 + 召唤按钮
    html += '<div style="display:flex;align-items:center;gap:6px">';
    html += summonLevelSelectHtml('summonLv_' + i, 'flex:1;font-size:10px;padding:3px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px');
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
      html += dragonIconHtml(typeIdx, 40, 36);
      html += '<div style="flex:1;min-width:0;overflow:hidden">';
      html += '<div class="iname" style="font-size:11px">' + esc(typeName || ('龙魂#' + (si+1))) + '</div>';
      html += '<div style="font-size:9px;color:var(--text-muted)">待命</div>';
      html += '</div></div>';
      html += soulPartsEditorHtml(s, si, 'idle_soul_', SOUL_PARTS_GRID, true);
      html += natureTagsHtml(s.nature_list, 'width:100%');
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
  await fetchDragonSouls(); // 新召唤的龙：数据变化会自动重渲染
  renderDragonSoulsPanel(); // 无新龙时也重渲染一次，更新 HP 血条/属性显示
  toast('龙属性已刷新 (' + dragonEntities.length + '条)');
}
