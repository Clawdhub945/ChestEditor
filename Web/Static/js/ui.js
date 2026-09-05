// ui — 由 HtmlUI 单体拆分（原 app.js）
function renderSidebar() {
  const q = document.getElementById('searchChest').value.toLowerCase();
  const el = document.getElementById('chestList');
  let html = '';

  // 容器分类
  html += '<div class="category">';
  html += '<div class="category-header" onclick="toggleCategory()">';
  html += '<span class="arrow' + (categoryOpen ? ' open' : '') + '">&#9654;</span>';
  html += '<span>容器</span>';
  html += '<span style="margin-left:auto;font-size:11px;color:var(--text-muted)">' + chests.length + '</span>';
  html += '</div>';
  html += '<div class="category-items' + (categoryOpen ? ' open' : '') + '">';
  html += '<div class="filter-section">';
  html += '<div class="filter-title"><span>筛选设施</span>';
  html += '<span class="filter-actions"><a onclick="setAllFilters(true)">全选</a><a onclick="setAllFilters(false)">清空</a></span></div>';
  html += '<div class="filter-grid">';
  for (let fi = 0; fi < filters.length; fi++) {
    const f = filters[fi];
    html += '<span class="filter-tag' + (f.enabled ? ' on' : '') + '" onclick="toggleFilter(' + f.stuffId + ')">' + esc(f.name) + '</span>';
  }
  html += '</div></div>';

  if (chests.length === 0) {
    html += '<div style="padding:20px;text-align:center;color:var(--text-muted)">暂无箱子</div>';
  } else {
    for (let i = 0; i < chests.length; i++) {
      const c = chests[i];
      if (q && !c.name.toLowerCase().includes(q)) continue;
      const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
      const isActive = selectedChest === i;
      html += '<div class="chest-item' + (isActive ? ' active' : '') + '" onclick="selectChest(' + i + ')">';
      html += '<div class="ci-icon"><img src="/icon/' + c.stuffId + '" onerror="this.remove()"></div>';
      html += '<div class="ci-info">';
      html += '<div class="ci-name">' + esc(c.name) + '</div>';
      html += '<div class="ci-count">' + cap + ' 物品</div>';
      html += '</div></div>';
    }
  }

  html += '</div></div>';

  // 驯龙总分类
  html += '<div class="category">';
  html += '<div class="category-header" onclick="toggleDragonMain()">';
  html += '<span class="arrow' + (dragonMainOpen ? ' open' : '') + '">&#9654;</span>';
  html += '<span>驯龙</span>';
  html += '</div>';
  html += '<div class="category-items' + (dragonMainOpen ? ' open' : '') + '">';

  // 地图龙
  const activeSouls = dragonSouls.filter(s => s.is_active);
  const idleSouls = dragonSouls.filter(s => !s.is_active);
  html += '<div class="chest-item' + (dragonView === 'souls' ? ' active' : '') + '" onclick="selectDragonView(\'souls\')">';
  html += '<div class="ci-icon" style="font-size:20px;display:flex;align-items:center;justify-content:center">&#x1F409;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">地图龙</div>';
  html += '<div class="ci-count">' + activeSouls.length + ' 条</div>';
  html += '</div></div>';

  // 龙素材
  html += '<div class="chest-item' + (dragonView === 'materials' ? ' active' : '') + '" onclick="selectDragonView(\'materials\')">';
  html += '<div class="ci-icon" style="font-size:16px;display:flex;align-items:center;justify-content:center">&#x1F48A;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">龙素材</div>';
  html += '<div class="ci-count">' + dragonItems.length + ' 种</div>';
  html += '</div></div>';

  // 召唤龙
  const summonCount = dragonTypes.length + idleSouls.length;
  html += '<div class="chest-item' + (dragonView === 'summon' ? ' active' : '') + '" onclick="selectDragonView(\'summon\')">';
  html += '<div class="ci-icon" style="font-size:16px;display:flex;align-items:center;justify-content:center">&#x2728;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">召唤龙</div>';
  html += '<div class="ci-count">' + summonCount + '</div>';
  html += '</div></div>';

  html += '</div></div>';

  // NPC 分类（与容器同级）
  html += '<div class="category">';
  html += '<div class="category-header" onclick="toggleNpcPanel()">';
  html += '<span class="arrow' + (npcPanelOpen ? ' open' : '') + '">&#9654;</span>';
  html += '<span>NPC</span>';
  html += '<span style="margin-left:auto;font-size:11px;color:var(--text-muted)">' + npcListData.length + '</span>';
  html += '</div>';
  html += '<div class="category-items' + (npcPanelOpen ? ' open' : '') + '">';
  html += '<div class="chest-item' + (npcPanelOpen ? ' active' : '') + '" onclick="openNpcPanel()">';
  html += '<div class="ci-icon" style="font-size:20px;display:flex;align-items:center;justify-content:center">&#x1F464;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">我方 NPC</div>';
  html += '<div class="ci-count">' + (npcListData.length > 0 ? npcListData.length + ' 个' : '点击扫描') + '</div>';
  html += '</div></div></div></div>';

  // 怪物总分类
  html += '<div class="category">';
  html += '<div class="category-header" onclick="toggleNpcMain()">';
  html += '<span class="arrow' + (npcMainOpen ? ' open' : '') + '">&#9654;</span>';
  html += '<span>实体扫描</span>';
  html += '</div>';
  html += '<div class="category-items' + (npcMainOpen ? ' open' : '') + '">';

  // 修改
  html += '<div class="chest-item' + (npcView === 'editor' ? ' active' : '') + '" onclick="selectNpcView(\'editor\')">';
  html += '<div class="ci-icon" style="font-size:20px;display:flex;align-items:center;justify-content:center">&#x270F;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">修改</div>';
  html += '<div class="ci-count">' + (entityEditorData.length > 0 ? entityEditorData.length + ' 个实体' : '0 个') + '</div>';
  html += '</div></div>';

  html += '</div></div>';

  // 科技树
  html += '<div class="category">';
  html += '<div class="category-header" onclick="toggleTechTree()">';
  html += '<span class="arrow' + (techTreeOpen ? ' open' : '') + '">&#9654;</span>';
  html += '<span>科技树</span>';
  html += '</div>';
  html += '<div class="category-items' + (techTreeOpen ? ' open' : '') + '">';
  html += '<div class="chest-item' + (techTreeOpen ? ' active' : '') + '" onclick="openTechTree()">';
  html += '<div class="ci-icon" style="font-size:20px;display:flex;align-items:center;justify-content:center">&#x1F333;</div>';
  html += '<div class="ci-info">';
  html += '<div class="ci-name">查看科技树</div>';
  html += '<div class="ci-count">点击查看/修改</div>';
  html += '</div></div>';
  html += '</div></div>';

  el.innerHTML = html;
  renderDragonItems();
  renderDragonSummonList();
}


function toggleCategory() {
  categoryOpen = !categoryOpen;
  renderSidebar();
}


function toggleDragonMain() {
  dragonMainOpen = !dragonMainOpen;
  renderSidebar();
}


function toggleNpcMain() {
  npcMainOpen = !npcMainOpen;
  renderSidebar();
}


function toggleNpcPanel() {
  npcPanelOpen = !npcPanelOpen;
  renderSidebar();
}


async function openNpcPanel() {
  selectedChest = -1;
  dragonView = '';
  npcView = '';
  npcPanelOpen = true;
  renderSidebar();
  const el = document.getElementById('content');
  el.innerHTML = '<div style="padding:40px;text-align:center;color:var(--text-muted)">扫描我方 NPC 中...</div>';
  try {
    const r = await fetch('/api/npc/scan', {method:'POST'});
    npcListData = await r.json();
    renderSidebar();
    renderNpcPanel();
  } catch(e) {
    el.innerHTML = '<div style="padding:40px;text-align:center;color:var(--danger)">扫描失败: ' + esc(e.message) + '</div>';
  }
}


function getKingdomInfo(id) {
  const map = {
    1:   {name:'我方',   bg:'var(--success-dark, #27ae60)', fg:'#fff'},
    100: {name:'国王',   bg:'#f1c40f', fg:'#333'},
    101: {name:'南王',   bg:'#3498db', fg:'#fff'},
    102: {name:'宣王',   bg:'#9b59b6', fg:'#fff'},
    103: {name:'北王',   bg:'#2980b9', fg:'#fff'},
    104: {name:'逍遥王', bg:'#1abc9c', fg:'#fff'},
    105: {name:'南洋王', bg:'#16a085', fg:'#fff'},
    106: {name:'西洋王', bg:'#2c3e50', fg:'#fff'},
    107: {name:'商王',   bg:'#d35400', fg:'#fff'},
    97:  {name:'蛮族',   bg:'#7f8c8d', fg:'#fff'},
    98:  {name:'强盗',   bg:'#c0392b', fg:'#fff'},
    99:  {name:'怪物',   bg:'var(--danger, #e74c3c)', fg:'#fff'},
    89:  {name:'蓝蚂蚁', bg:'#2980b9', fg:'#fff'},
    88:  {name:'红蚂蚁', bg:'#e74c3c', fg:'#fff'}
  };
  return map[id] || null;
}

// ===== 统一实体编辑器 =====

function toggleAuto() {
  autoRefresh = !autoRefresh;
  const btn = document.getElementById('btnAuto');
  document.getElementById('autoLabel').textContent = autoRefresh ? '开' : '关';
  btn.className = autoRefresh ? 'on' : 'off';
}


function toast(msg, err) {
  const el = document.createElement('div');
  el.className = 'toast ' + (err ? 'err' : 'ok');
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 2000);
}


function esc(s) { return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function hideImg(el) { el.style.display='none'; }
