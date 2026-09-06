// ui — 通用 UI 构造器 + 侧栏 + 工具函数
// ===== 通用 HTML 片段（消除各面板的复制粘贴模板） =====

// 侧栏分类块：标题 + 展开箭头 + 可选计数 + 内容
function htmlCategory(open, toggleFn, title, countHtml, itemsHtml) {
  let h = '<div class="category">';
  h += '<div class="category-header" onclick="' + toggleFn + '()">';
  h += '<span class="arrow' + (open ? ' open' : '') + '">&#9654;</span>';
  h += '<span>' + title + '</span>';
  if (countHtml !== undefined && countHtml !== null && countHtml !== '')
    h += '<span style="margin-left:auto;font-size:11px;color:var(--text-muted)">' + countHtml + '</span>';
  h += '</div>';
  h += '<div class="category-items' + (open ? ' open' : '') + '">' + itemsHtml + '</div></div>';
  return h;
}

// 侧栏条目（图标 + 名称 + 计数）
const MENU_ICON_FLEX = 'font-size:16px;display:flex;align-items:center;justify-content:center';
function htmlMenuItem(iconHtml, name, countHtml, onclick, active, iconStyle) {
  let h = '<div class="chest-item' + (active ? ' active' : '') + '" onclick="' + onclick + '">';
  h += '<div class="ci-icon"' + (iconStyle ? ' style="' + iconStyle + '"' : '') + '>' + iconHtml + '</div>';
  h += '<div class="ci-info">';
  h += '<div class="ci-name">' + name + '</div>';
  h += '<div class="ci-count">' + countHtml + '</div>';
  h += '</div></div>';
  return h;
}

// 数量输入步进（外置 −/+ 按钮，原生箭头已全局隐藏）
function stepCount(btn, delta) {
  const input = btn.parentElement.querySelector('input');
  if (!input) return;
  const v = Math.max(0, (parseInt(input.value) || 0) + delta);
  input.value = v;
}

// 数据卡片（图标 + 名称 + ID + 自定义行），永恒神殿等纯数值编辑场景使用
function htmlDataCard(stuffId, name, rowsHtml) {
  let h = '<div class="item">';
  h += '<img src="/icon/' + stuffId + '" onerror="hideImg(this)">';
  h += '<div class="iname" title="' + esc(name) + '">' + esc(name) + '</div>';
  h += '<div class="iid">ID:' + stuffId + '</div>';
  h += rowsHtml;
  h += '</div>';
  return h;
}

// 单行字段：label + 数值输入框 + 设（红色按钮）
function htmlFieldRow(label, inputId, value, onclick, opts) {
  opts = opts || {};
  let h = '<div style="display:flex;align-items:center;justify-content:center;gap:4px;margin-bottom:4px">';
  h += '<span style="font-size:10px;color:var(--text-muted);flex:0 0 auto;' + (opts.labelWidth ? 'width:' + opts.labelWidth + ';' : '') + 'text-align:' + (opts.labelAlign || 'right') + '">' + label + '</span>';
  h += '<input type="number" id="' + inputId + '" value="' + value + '" min="0"' + (opts.max ? ' max="' + opts.max + '"' : '') + ' style="flex:1;min-width:0;font-size:11px;padding:2px 4px;background:var(--bg-input);border:1px solid var(--border);border-radius:3px;color:var(--text-primary);text-align:center;outline:none" onfocus="this.select()">';
  h += '<button class="btn-rm" onclick="' + onclick + '" style="padding:2px 8px;font-size:11px;flex:0 0 auto">设</button>';
  h += '</div>';
  return h;
}

// 物品卡片（图标 + 名称 + ID + 数量步进 + 按钮组）
// 容器物品 / 计划库存 / 龙素材 各列表共用
// opts: { inputId, inputCls, cardCls, buttons: [{label, onclick}] }
function htmlItemCard(stuffId, name, count, opts) {
  let h = '<div class="item' + (opts.cardCls ? ' ' + opts.cardCls : '') + '">';
  h += '<img src="/icon/' + stuffId + '" onerror="hideImg(this)">';
  h += '<div class="iname" title="' + esc(name) + '">' + esc(name) + '</div>';
  h += '<div class="iid">ID:' + stuffId + '</div>';
  h += '<div class="icount">';
  h += '<button class="cnt-step" onclick="stepCount(this,-1)">−</button>';
  h += '<input type="number" class="count-input' + (opts.inputCls ? ' ' + opts.inputCls : '') + '" value="' + count + '" min="0" id="' + opts.inputId + '">';
  h += '<button class="cnt-step" onclick="stepCount(this,1)">+</button>';
  h += '</div>';
  if (opts.extraHtml) h += opts.extraHtml;
  h += '<div class="btns">';
  for (const b of opts.buttons)
    h += '<button class="btn-rm" onclick="' + b.onclick + '">' + b.label + '</button>';
  h += '</div></div>';
  return h;
}

function renderSidebar() {
  const q = document.getElementById('searchChest').value.toLowerCase();
  const el = document.getElementById('chestList');
  let html = '';

  // 容器分类
  let chestItems = '<div class="filter-section">';
  chestItems += '<div class="filter-title"><span>筛选设施</span>';
  chestItems += '<span class="filter-actions"><a onclick="setAllFilters(true)">全选</a><a onclick="setAllFilters(false)">清空</a></span></div>';
  chestItems += '<div class="filter-grid">';
  for (const f of filters)
    chestItems += '<span class="filter-tag' + (f.enabled ? ' on' : '') + '" onclick="toggleFilter(' + f.stuffId + ')">' + esc(f.name) + '</span>';
  chestItems += '</div></div>';

  if (chests.length === 0) {
    chestItems += '<div style="padding:20px;text-align:center;color:var(--text-muted)">暂无箱子</div>';
  } else {
    for (let i = 0; i < chests.length; i++) {
      const c = chests[i];
      if (q && !c.name.toLowerCase().includes(q)) continue;
      const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
      chestItems += htmlMenuItem('<img src="/icon/' + c.stuffId + '" onerror="this.remove()">',
        esc(c.name), cap + ' 物品', 'selectChest(' + i + ')', selectedChest === i);
    }
  }
  html += htmlCategory(categoryOpen, 'toggleCategory', '容器', chests.length, chestItems);

  // 驯龙总分类
  const activeSouls = dragonSouls.filter(s => s.is_active);
  const idleSouls = dragonSouls.filter(s => !s.is_active);
  let dragonHtml = '';
  dragonHtml += htmlMenuItem('&#x1F409;', '地图龙', activeSouls.length + ' 条',
    "selectDragonView('souls')", dragonView === 'souls', 'font-size:20px;' + MENU_ICON_FLEX);
  dragonHtml += htmlMenuItem('&#x1F48A;', '龙素材', dragonItems.length + ' 种',
    "selectDragonView('materials')", dragonView === 'materials', MENU_ICON_FLEX);
  const summonCount = dragonTypes.length + idleSouls.length;
  dragonHtml += htmlMenuItem('&#x2728;', '召唤龙', summonCount,
    "selectDragonView('summon')", dragonView === 'summon', MENU_ICON_FLEX);
  html += htmlCategory(dragonMainOpen, 'toggleDragonMain', '驯龙', null, dragonHtml);

  // NPC 分类
  const npcItem = htmlMenuItem('&#x1F464;', '我方 NPC',
    (npcListData.length > 0 ? npcListData.length + ' 个' : '点击扫描'),
    'openNpcPanel()', npcPanelOpen, 'font-size:20px;' + MENU_ICON_FLEX);
  html += htmlCategory(npcPanelOpen, 'toggleNpcPanel', 'NPC', npcListData.length, npcItem);

  // 实体扫描分类
  const editorItem = htmlMenuItem('&#x270F;', '修改',
    (entityEditorData.length > 0 ? entityEditorData.length + ' 个实体' : '0 个'),
    "selectNpcView('editor')", npcView === 'editor', 'font-size:20px;' + MENU_ICON_FLEX);
  html += htmlCategory(npcMainOpen, 'toggleNpcMain', '实体扫描', null, editorItem);

  // 科技树
  const techItem = htmlMenuItem('&#x1F333;', '查看科技树', '点击查看/修改',
    'openTechTree()', techTreeOpen, 'font-size:20px;' + MENU_ICON_FLEX);
  html += htmlCategory(techTreeOpen, 'toggleTechTree', '科技树', null, techItem);

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
