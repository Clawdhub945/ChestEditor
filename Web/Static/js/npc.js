// npc — 由 HtmlUI 单体拆分（原 app.js）
function renderNpcPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style="padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden">';
  html += '<div style="display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0">';
  html += '<h2 style="color:var(--accent-light);margin:0;font-size:18px">&#x1F464; 我方 NPC</h2>';
  html += '<span style="color:var(--text-muted);font-size:13px">' + npcListData.length + ' 个</span>';
  html += '<button onclick="openNpcPanel()" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto">重新扫描</button>';
  html += '</div>';

  if (npcListData.length === 0) {
    html += '<div style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center">';
    html += '<div style="font-size:48px;margin-bottom:16px">&#x1F464;</div>';
    html += '<div style="color:var(--text-secondary);font-size:16px;margin-bottom:8px">未发现我方 NPC</div>';
    html += '<div style="color:var(--text-muted);font-size:13px">确保游戏已加载存档且有己方 NPC 存在</div>';
    html += '</div>';
  } else {
    var byKingdom = {};
    for (var ni = 0; ni < npcListData.length; ni++) {
      var npc = npcListData[ni];
      var kid = npc.hometownKingdomId || 0;
      if (!byKingdom[kid]) byKingdom[kid] = [];
      byKingdom[kid].push(npc);
    }
    var kingdomOrder = [1, 97, 98, 99, 88, 89];
    var allKingdoms = Object.keys(byKingdom).map(Number);
    var sortedKingdoms = kingdomOrder.filter(function(k) { return allKingdoms.indexOf(k) >= 0; })
      .concat(allKingdoms.filter(function(k) { return kingdomOrder.indexOf(k) < 0; }).sort(function(a,b){return a-b;}));

    function classifyMineByType(items) {
      var sub = {soldiers:[],workers:[],children:[],prisoners:[],nobles:[],lords:[],misc:[],outsiders:[],laborers:[],others:[]};
      for (var si = 0; si < items.length; si++) {
        var npc = items[si];
        var t = npc.npcType !== undefined && npc.npcType !== null ? npc.npcType : -9999;
        if (t===1001) sub.soldiers.push(npc);
        else if ((t>=-3&&t<=-1)||(t>=9001&&t<=9199)) sub.children.push(npc);
        else if (t===25) sub.prisoners.push(npc);
        else if ((t>=1&&t<=22)||t===24||(t>=26&&t<=29)||(t>=31&&t<=49)||t===9301||t===9302||t===9303||t===9304||t===9306||t===9308||t===9309) sub.workers.push(npc);
        else if (t===61) sub.nobles.push(npc);
        else if (t===70) sub.lords.push(npc);
        else if (t===23||t===30) sub.misc.push(npc);
        else if (t>=-15&&t<=-5) sub.outsiders.push(npc);
        else if (t===0||(t>=9201&&t<=9299)) sub.laborers.push(npc);
        else sub.others.push(npc);
      }
      return sub;
    }
    var kingdomColors = {1:'#27ae60',97:'#7f8c8d',98:'#c0392b',99:'#e74c3c',88:'#e74c3c',89:'#2980b9'};
    var kingdomNames = {1:'我方',97:'蛮族',98:'强盗',99:'怪物',88:'红蚂蚁',89:'蓝蚂蚁'};
    var groups = [];
    for (var gi = 0; gi < sortedKingdoms.length; gi++) {
      var kid = sortedKingdoms[gi];
      var items = byKingdom[kid];
      var kName = kingdomNames[kid] || ('王国' + kid);
      var kColor = kingdomColors[kid] || '#7f8c8d';
      var sub = classifyMineByType(items);
      var subDefs;
      if (kid === 1) {
        subDefs = [
          {label:'士兵', icon:'&#x2694;', color:'#e74c3c', items:sub.soldiers},
          {label:'工人', icon:'&#x1F527;', color:'#f39c12', items:sub.workers},
          {label:'杂工', icon:'&#x1F6E0;', color:'#95a5a6', items:sub.laborers},
          {label:'儿童', icon:'&#x1F476;', color:'#e91e63', items:sub.children},
          {label:'外来者', icon:'&#x1F464;', color:'#9b59b6', items:sub.outsiders},
          {label:'贵族', icon:'&#x1F451;', color:'#f1c40f', items:sub.nobles},
          {label:'领主', icon:'&#x1F3F0;', color:'#e67e22', items:sub.lords},
          {label:'俘虏', icon:'&#x1F512;', color:'#7f8c8d', items:sub.prisoners},
          {label:'石头人和小精灵', icon:'&#x1F47E;', color:'#1abc9c', items:sub.misc},
          {label:'其他', icon:'&#x2753;', color:'#34495e', items:sub.others}
        ];
        groups.push({label:kName, color:kColor, icon:'&#x1F3F0;', items:items, subs:subDefs});
      } else {
        subDefs = [
          {label:'士兵', icon:'&#x2694;', color:'#e74c3c', items:sub.soldiers},
          {label:'工人', icon:'&#x1F527;', color:'#f39c12', items:sub.workers},
          {label:'其他', icon:'&#x2753;', color:'#34495e', items:sub.others.concat(sub.laborers,sub.children,sub.outsiders,sub.nobles,sub.lords,sub.prisoners,sub.misc)}
        ];
        groups.push({label:kName, color:kColor, icon:'&#x2694;', items:items, subs:subDefs});
      }
    }
    html += '<div style="flex:1;overflow-y:auto;min-height:0">';
    for (const g of groups) {
      // 阵营大组
      html += '<details style="margin-bottom:8px">';
      html += '<summary style="cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px">';
      html += '<span style="font-size:10px;padding:1px 6px;border-radius:8px;background:' + g.color + ';color:#fff">' + g.icon + ' ' + g.label + '</span>';
      html += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + g.items.length + ' 个</span>';
      html += '</summary>';
      if (g.subs && g.subs.length > 0) {
        html += '<div style="padding:4px 0 4px 8px">';
        for (const sg of g.subs) {
          if (sg.items.length === 0) continue;
          // 职业子分组
          html += '<details style="margin-bottom:6px">';
          html += '<summary style="cursor:pointer;padding:6px 10px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:500;font-size:12px;display:flex;align-items:center;gap:6px">';
          html += '<span style="font-size:10px;padding:1px 5px;border-radius:8px;background:' + sg.color + ';color:#fff">' + sg.icon + ' ' + sg.label + '</span>';
          html += '<span style="margin-left:auto;font-size:11px;color:var(--text-muted);font-weight:400">' + sg.items.length + ' 个</span>';
          html += '</summary>';
          html += '<div style="padding:4px 0">';
          for (const npc of sg.items) {
            html += '<div style="margin-bottom:6px">' + renderNpcCard(npc) + '</div>';
          }
          html += '</div></details>';
        }
        html += '</div>';
      }
      html += '</details>';
    }
    html += '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
  // 初始化勾选字段显示
  for (const npc of npcListData) {
    refreshNpcCheckedDisplay(npc.ptrHash || 0);
  }
}


function getNpcTypeName(typeId) {
  return NPC_TYPES[String(typeId)] || ('类型' + typeId);
}


function renderNpcCard(npc) {
  const ptrHash = npc.ptrHash || 0;
  const displayName = npc.npcName || npc.name || ('NPC#' + npc.guid);
  const soldierType = npc.soldierTypeName || '';
  const npcTypeName = getNpcTypeName(npc.npcType || 0);
  const fieldCount = npc.fieldCount || 0;
  let h = '';

  h += '<div style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius)">';

  // 头部：名称 + 兵种 + NPC类型
  h += '<div style="display:flex;align-items:center;gap:8px;padding:10px 14px;border-bottom:1px solid var(--border)">';
  h += '<span style="font-weight:600;color:var(--text-primary);font-size:14px">' + esc(displayName) + '</span>';
  if (npcTypeName) h += '<span style="font-size:11px;padding:1px 8px;border-radius:8px;background:var(--warning,#e67e22);color:#fff">' + esc(npcTypeName) + '</span>';
  if (soldierType) h += '<span style="font-size:11px;padding:1px 8px;border-radius:8px;background:var(--accent);color:#fff">' + esc(soldierType) + '</span>';
  h += '<span style="font-size:11px;color:var(--text-muted);margin-left:auto">GUID:' + npc.guid + '</span>';
  h += '<button onclick="event.stopPropagation();locateEditorEntity(' + ptrHash + ')" style="padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px">定位</button>';
  h += '</div>';

  // 勾选字段显示区域
  h += '<div id="npc-checked-' + ptrHash + '"></div>';

  // 所有字段折叠（懒加载）
  h += '<details style="border-top:1px solid var(--border)" ontoggle="loadNpcFields(this,' + ptrHash + ')">';
  h += '<summary style="cursor:pointer;padding:8px 14px;font-size:12px;color:var(--text-muted);user-select:none">所有字段 (' + fieldCount + ')</summary>';
  h += '<div id="npc-fields-' + ptrHash + '" style="padding:6px 14px 10px;color:var(--text-muted);font-size:12px">点击展开加载...</div>';
  h += '</details>';

  h += '</div>';
  return h;
}


var _fieldTranslations = null;

var _npcFieldCache = {};

function getCheckedFields(ptrHash) {
  try {
    const raw = localStorage.getItem('npc_checked_' + ptrHash);
    return raw ? JSON.parse(raw) : [];
  } catch(e) { return []; }
}

function saveCheckedFields(ptrHash, arr) {
  localStorage.setItem('npc_checked_' + ptrHash, JSON.stringify(arr));
}


async function loadNpcFields(details, ptrHash) {
  if (!details.open) return;
  const container = document.getElementById('npc-fields-' + ptrHash);
  if (!container || container.dataset.loaded) return;
  container.dataset.loaded = '1';
  container.innerHTML = '加载中...';
  try {
    const [fields, translations] = await Promise.all([
      fetch('/api/npc/fields/' + ptrHash + '?t=' + Date.now()).then(r => r.json()),
      loadFieldTranslations()
    ]);
    const allKeys = Object.keys(fields);
    console.log('[NPC fields] ptrHash=' + ptrHash + ' keys=' + allKeys.length + ' error=' + (fields.error || 'none'));
    _npcFieldCache[ptrHash] = { fields: fields, translations: translations };
    if (fields.error || allKeys.length === 0) {
      container.innerHTML = '<span style="color:var(--danger)">实体已失效 (ptrHash: ' + ptrHash + ')，请<a href="javascript:void(0)" onclick="openNpcPanel()" style="color:var(--accent)">重新扫描</a></span>';
      return;
    }
    const checked = new Set(getCheckedFields(ptrHash));
    const numKeys = allKeys.filter(k => !fields[k].isString);
    const strKeys = allKeys.filter(k => fields[k].isString);
    const totalCount = numKeys.length + strKeys.length;

    let h = '';
    // 搜索框
    h += '<div style="margin-bottom:8px">';
    h += '<input id="npc-search-' + ptrHash + '" type="text" placeholder="搜索字段..." oninput="filterNpcTable(this)" ';
    h += 'style="width:100%;padding:5px 8px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px;outline:none" />';
    h += '</div>';
    // 全选/取消
    h += '<div style="display:flex;gap:8px;margin-bottom:6px;font-size:11px">';
    h += '<a href="javascript:void(0)" onclick="toggleAllNpcCheckboxes(' + ptrHash + ',true)" style="color:var(--accent)">全选</a>';
    h += '<a href="javascript:void(0)" onclick="toggleAllNpcCheckboxes(' + ptrHash + ',false)" style="color:var(--text-muted)">取消全选</a>';
    h += '</div>';
    // 表格
    h += '<table class="npc-fields-table" style="width:100%;border-collapse:collapse;font-size:12px">';
    h += '<thead><tr style="border-bottom:1px solid var(--border)">';
    h += '<th style="width:30px;padding:4px 6px;text-align:center"><input type="checkbox" id="npc-checkall-' + ptrHash + '" onchange="toggleAllNpcCheckboxes(' + ptrHash + ',this.checked)" /></th>';
    h += '<th style="padding:4px 8px;text-align:left;min-width:120px">字段</th>';
    h += '<th style="padding:4px 8px;text-align:left;min-width:80px">翻译</th>';
    h += '<th style="padding:4px 8px;text-align:right;min-width:80px">数值</th>';
    h += '<th style="width:50px;padding:4px 6px;text-align:center">操作</th>';
    h += '</tr></thead><tbody>';

    // 数值字段
    for (const key of numKeys) {
      const f = fields[key];
      const isFloat = f.isFloat;
      const displayVal = (typeof f.value === 'number') ? (isFloat ? f.value.toFixed(2) : f.value) : (f.value || 0);
      const trans = translations[key] || '';
      const isChecked = checked.has(key);
      const inpId = 'npc_inp_' + ptrHash + '_' + key;
      h += '<tr class="npc-field-row" data-key="' + esc(key).toLowerCase() + '">';
      h += '<td style="text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)"><input type="checkbox" class="npc-field-cb" data-ptr="' + ptrHash + '" data-key="' + esc(key) + '" ' + (isChecked ? 'checked' : '') + ' onchange="onNpcCheckChange(this)" /></td>';
      h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)">' + esc(key) + '</td>';
      h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)">' + esc(trans) + '</td>';
      h += '<td style="padding:3px 6px;border-bottom:1px solid var(--border);text-align:right">';
      h += '<input id="' + inpId + '" type="number" step="' + (isFloat ? '0.1' : '1') + '" value="' + displayVal + '" ';
      h += 'style="width:80px;background:transparent;border:1px solid var(--border);border-radius:3px;color:var(--text-primary);font-size:12px;text-align:right;padding:2px 4px;outline:none" onfocus="this.select()" />';
      h += '</td>';
      h += '<td style="text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)">';
      h += '<button onclick="setNpcField(' + ptrHash + ',\'' + esc(key) + '\',document.getElementById(\'' + inpId + '\').value,' + (isFloat ? 'true' : 'false') + ')" style="padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:3px;cursor:pointer;font-size:11px">OK</button>';
      h += '</td></tr>';
    }
    // 字符串字段
    for (const key of strKeys) {
      const f = fields[key];
      const trans = translations[key] || '';
      const isChecked = checked.has(key);
      h += '<tr class="npc-field-row" data-key="' + esc(key).toLowerCase() + '">';
      h += '<td style="text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)"><input type="checkbox" class="npc-field-cb" data-ptr="' + ptrHash + '" data-key="' + esc(key) + '" ' + (isChecked ? 'checked' : '') + ' onchange="onNpcCheckChange(this)" /></td>';
      h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)">' + esc(key) + '</td>';
      h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)">' + esc(trans) + '</td>';
      h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted);text-align:right">' + esc(String(f.value || '')) + '</td>';
      h += '<td style="padding:3px 6px;border-bottom:1px solid var(--border)"></td>';
      h += '</tr>';
    }
    h += '</tbody></table>';

    container.innerHTML = h;
    // 更新 check-all 状态
    updateCheckAllState(ptrHash);
  } catch(e) {
    container.innerHTML = '<span style="color:var(--danger)">加载失败: ' + esc(String(e)) + '</span>';
  }
}


function filterNpcTable(input) {
  const q = input.value.toLowerCase();
  const table = input.closest('div').querySelector('table');
  if (!table) return;
  const rows = table.querySelectorAll('.npc-field-row');
  for (const row of rows) {
    const key = row.dataset.key || '';
    row.style.display = key.includes(q) ? '' : 'none';
  }
}


function onNpcCheckChange(cb) {
  const ptrHash = parseInt(cb.dataset.ptr);
  const key = cb.dataset.key;
  let arr = getCheckedFields(ptrHash);
  if (cb.checked) {
    if (!arr.includes(key)) arr.push(key);
  } else {
    arr = arr.filter(k => k !== key);
  }
  saveCheckedFields(ptrHash, arr);
  updateCheckAllState(ptrHash);
  // 更新卡片上的勾选字段显示
  refreshNpcCheckedDisplay(ptrHash);
}


function toggleAllNpcCheckboxes(ptrHash, checked) {
  const cbs = document.querySelectorAll('.npc-field-cb');
  let arr = [];
  for (const cb of cbs) {
    if (parseInt(cb.dataset.ptr) !== ptrHash) continue;
    if (cb.closest('tr').style.display === 'none') continue;
    cb.checked = checked;
    if (checked) arr.push(cb.dataset.key);
  }
  saveCheckedFields(ptrHash, arr);
  const checkAll = document.getElementById('npc-checkall-' + ptrHash);
  if (checkAll) checkAll.checked = checked;
  refreshNpcCheckedDisplay(ptrHash);
}


function updateCheckAllState(ptrHash) {
  const cbs = document.querySelectorAll('.npc-field-cb');
  const checkAll = document.getElementById('npc-checkall-' + ptrHash);
  if (!checkAll) return;
  let total = 0, checkedCount = 0;
  for (const cb of cbs) {
    if (parseInt(cb.dataset.ptr) !== ptrHash) continue;
    total++;
    if (cb.checked) checkedCount++;
  }
  checkAll.checked = (total > 0 && checkedCount === total);
}


function refreshNpcCheckedDisplay(ptrHash) {
  const el = document.getElementById('npc-checked-' + ptrHash);
  if (!el) return;
  const checked = getCheckedFields(ptrHash);
  if (checked.length === 0) {
    el.innerHTML = '';
    return;
  }
  const cache = _npcFieldCache[ptrHash];
  const fields = cache ? cache.fields : null;
  const trans = cache ? cache.translations : {};
  let h = '<table style="width:100%;border-collapse:collapse;font-size:12px">';
  h += '<thead><tr style="border-bottom:1px solid var(--border)">';
  h += '<th style="padding:3px 8px;text-align:left;min-width:100px">字段</th>';
  h += '<th style="padding:3px 8px;text-align:left;min-width:60px">翻译</th>';
  h += '<th style="padding:3px 8px;text-align:right;min-width:70px">数值</th>';
  h += '<th style="width:50px;padding:3px 6px;text-align:center">操作</th>';
  h += '</tr></thead><tbody>';
  for (const key of checked) {
    const f = fields ? fields[key] : null;
    const isFloat = f && f.isFloat;
    const val = f ? f.value : 0;
    const displayVal = (typeof val === 'number') ? (isFloat ? val.toFixed(2) : val) : (val || 0);
    const translation = trans[key] || '';
    const inpId = 'npc_chkd_' + ptrHash + '_' + key;
    h += '<tr>';
    h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)">' + esc(key) + '</td>';
    h += '<td style="padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)">' + esc(translation) + '</td>';
    h += '<td style="padding:3px 6px;border-bottom:1px solid var(--border);text-align:right">';
    h += '<input id="' + inpId + '" type="number" step="' + (isFloat ? '0.1' : '1') + '" value="' + displayVal + '" ';
    h += 'style="width:80px;background:transparent;border:1px solid var(--border);border-radius:3px;color:var(--text-primary);font-size:12px;text-align:right;padding:2px 4px;outline:none" onfocus="this.select()" />';
    h += '</td>';
    h += '<td style="text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)">';
    h += '<button onclick="setNpcCheckedField(' + ptrHash + ',\'' + esc(key) + '\',document.getElementById(\'' + inpId + '\').value,' + (isFloat ? 'true' : 'false') + ')" style="padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:3px;cursor:pointer;font-size:11px">OK</button>';
    h += '</td></tr>';
  }
  h += '</tbody></table>';
  el.innerHTML = h;
}


async function setNpcCheckedField(ptrHash, field, value, isFloat) {
  const v = isFloat ? parseFloat(value) : parseInt(value);
  if (isNaN(v)) return;
  try {
    const r = await fetch('/api/npc/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash, field, value: v})
    });
    const data = await r.json();
    if (data.error) { toast('设置失败: ' + data.error, true); return; }
    // 更新缓存
    if (_npcFieldCache[ptrHash] && _npcFieldCache[ptrHash].fields[field]) {
      _npcFieldCache[ptrHash].fields[field].value = v;
    }
    // 同步到所有字段表格的输入框
    var allInput = document.getElementById('npc_inp_' + ptrHash + '_' + field);
    if (allInput) allInput.value = isFloat ? v.toFixed(2) : v;
    // 更新本地数据
    for (const npc of npcListData) {
      if (npc.ptrHash === ptrHash) {
        if (field === 'speed') npc.speed = v;
        else if (field === 'hp') npc.hp = v;
        else if (field === 'hp_total') npc.hpTotal = v;
        if (npc.fields && npc.fields[field]) npc.fields[field].value = v;
        break;
      }
    }
    toast(field + ' = ' + v);
  } catch(e) { toast('设置失败', true); }
}


async function setNpcField(ptrHash, field, value, isFloat) {
  const v = isFloat ? parseFloat(value) : parseInt(value);
  if (isNaN(v)) return;
  try {
    const r = await fetch('/api/npc/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash, field, value: v})
    });
    const data = await r.json();
    if (data.error) { toast('设置失败: ' + data.error, true); return; }
    // 更新缓存
    if (_npcFieldCache[ptrHash] && _npcFieldCache[ptrHash].fields[field]) {
      _npcFieldCache[ptrHash].fields[field].value = v;
    }
    // 同步到勾选表格的输入框
    var chkdInput = document.getElementById('npc_chkd_' + ptrHash + '_' + field);
    if (chkdInput) chkdInput.value = isFloat ? v.toFixed(2) : v;
    // 更新本地数据
    for (const npc of npcListData) {
      if (npc.ptrHash === ptrHash) {
        if (field === 'speed') npc.speed = v;
        else if (field === 'hp') npc.hp = v;
        else if (field === 'hp_total') npc.hpTotal = v;
        if (npc.fields && npc.fields[field]) npc.fields[field].value = v;
        break;
      }
    }
    toast(field + ' = ' + v);
  } catch(e) { toast('设置失败', true); }
}


function selectNpcView(view) {
  selectedChest = -1;
  dragonView = '';
  npcView = (npcView === view) ? '' : view;
  renderSidebar();
  renderContent();
}
