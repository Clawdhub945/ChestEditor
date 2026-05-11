namespace ChestEditor;

internal static class HtmlUI
{
    internal const string Html = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>领地修改器</title>
<link rel=""icon"" href=""/favicon.png"" type=""image/png"">
<link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap"" rel=""stylesheet"">
<style>
:root {
  --bg-primary: #0a0a1a;
  --bg-secondary: #111128;
  --bg-card: #161638;
  --bg-input: #1c1c48;
  --bg-hover: #222260;
  --accent: #6366f1;
  --accent-light: #818cf8;
  --accent-dark: #4f46e5;
  --success: #10b981;
  --success-dark: #059669;
  --danger: #ef4444;
  --danger-dark: #dc2626;
  --warning: #f59e0b;
  --text-primary: #f1f5f9;
  --text-secondary: #94a3b8;
  --text-muted: #64748b;
  --border: #1e293b;
  --border-light: #334155;
  --shadow: 0 1px 3px rgba(0,0,0,.3);
  --shadow-lg: 0 10px 40px rgba(0,0,0,.4);
  --radius: 10px;
  --radius-sm: 6px;
}

* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Inter', sans-serif; background: var(--bg-primary); color: var(--text-primary); height: 100vh; overflow: hidden; display: flex; flex-direction: column; }

.header {
  background: var(--bg-secondary);
  padding: 12px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
}

.header h1 {
  font-size: 18px;
  font-weight: 600;
  color: var(--accent-light);
  letter-spacing: -0.5px;
  margin-right: 8px;
}

.header button {
  padding: 6px 14px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  background: var(--bg-card);
  color: var(--text-secondary);
  transition: all 0.15s ease;
}

.header button:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
  border-color: var(--accent);
}

.header button:active { transform: scale(0.97); }

.header button.loading {
  opacity: 0.5;
  cursor: wait;
}

.header button.on {
  background: var(--success-dark);
  border-color: var(--success);
  color: #fff;
}

.header .status {
  margin-left: auto;
  font-size: 12px;
  color: var(--text-muted);
  padding: 4px 10px;
  background: var(--bg-card);
  border-radius: var(--radius-sm);
  border: 1px solid var(--border);
}

.main { display: flex; flex: 1; overflow: hidden; }

.sidebar {
  width: 280px;
  background: var(--bg-secondary);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
}

.sidebar-search {
  padding: 12px;
  border-bottom: 1px solid var(--border);
}

.sidebar-search input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-input);
  color: var(--text-primary);
  font-size: 13px;
  transition: all 0.15s ease;
}

.sidebar-search input:focus {
  outline: none;
  border-color: var(--accent);
}

.sidebar-search input::placeholder { color: var(--text-muted); }

.chest-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.category {
  margin-bottom: 4px;
}

.category-header {
  padding: 8px 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  transition: all 0.15s ease;
  border-radius: var(--radius-sm);
}

.category-header:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.category-header .arrow {
  font-size: 10px;
  transition: transform 0.2s ease;
}

.category-header .arrow.open {
  transform: rotate(90deg);
}

.category-items {
  display: none;
  padding-left: 8px;
}

.category-items.open {
  display: block;
}

.filter-section {
  padding: 6px 12px 8px 12px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 4px;
}

.filter-title {
  font-size: 11px;
  color: var(--text-muted);
  margin-bottom: 6px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.filter-title .filter-actions {
  display: flex;
  gap: 6px;
}

.filter-title .filter-actions a {
  font-size: 10px;
  color: var(--accent);
  cursor: pointer;
  text-decoration: none;
}

.filter-title .filter-actions a:hover {
  text-decoration: underline;
}

.filter-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.filter-tag {
  font-size: 11px;
  padding: 3px 8px;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.15s ease;
  border: 1px solid var(--border);
  background: var(--bg-card);
  color: var(--text-muted);
  user-select: none;
}

.filter-tag:hover {
  border-color: var(--accent);
}

.filter-tag.on {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.plan-section {
  margin-top: 16px;
  border-top: 1px solid var(--border);
  padding-top: 12px;
}

.plan-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.plan-header span {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
}

.btn-add-plan {
  font-size: 12px;
  padding: 4px 10px;
  border-radius: var(--radius-sm);
  background: var(--accent);
  color: #fff;
  border: none;
  cursor: pointer;
}

.plan-item {
  background: rgba(255, 193, 7, 0.05);
  border-left: 3px solid #ffc107;
}

.btn-adj {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  border: 1px solid var(--border);
  background: var(--bg-card);
  color: var(--text-primary);
  cursor: pointer;
  font-size: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.btn-adj:hover {
  background: var(--accent);
  color: #fff;
}

.plan-item .icount {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 80px;
}

.count-input {
  width: 60px;
  padding: 3px 6px;
  border-radius: 4px;
  border: 1px solid var(--border);
  background: var(--bg-card);
  color: var(--text-primary);
  font-size: 12px;
  text-align: center;
}

.count-input:focus {
  outline: none;
  border-color: var(--accent);
}

.plan-input {
  border-color: #ffc107;
}

.plan-input:focus {
  border-color: #ffc107;
  box-shadow: 0 0 0 2px rgba(255,193,7,0.2);
}

.chest-item {
  padding: 10px 12px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 4px;
  transition: all 0.15s ease;
  border: 1px solid transparent;
}

.chest-item:hover {
  background: var(--bg-hover);
}

.chest-item.active {
  background: var(--accent-dark);
  border-color: var(--accent);
}

.chest-item .ci-icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  background: var(--bg-input);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.chest-item .ci-icon img {
  width: 28px;
  height: 28px;
  image-rendering: pixelated;
}

.chest-item .ci-info { flex: 1; min-width: 0; }

.chest-item .ci-name {
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.chest-item .ci-count {
  font-size: 11px;
  color: var(--text-muted);
}

.content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

#dragonViewContent {
  overflow-y: auto;
  flex: 1;
}

.content-header {
  padding: 16px 20px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border);
  display: flex;
  align-items: center;
  gap: 12px;
}

.content-header .ch-title {
  font-size: 16px;
  font-weight: 600;
  flex: 1;
}

.content-header .ch-cap {
  font-size: 13px;
  color: var(--text-muted);
  padding: 4px 10px;
  background: var(--bg-card);
  border-radius: 20px;
  border: 1px solid var(--border);
}

.content-header .btn-add {
  padding: 8px 16px;
  border: 1px solid var(--success);
  border-radius: var(--radius-sm);
  background: var(--success-dark);
  color: #fff;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.15s ease;
}

.content-header .btn-add:hover {
  background: var(--success);
  transform: translateY(-1px);
}

.content-header .btn-locate {
  padding: 8px 16px;
  border: 1px solid var(--accent);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--accent);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.15s ease;
}

.content-header .btn-locate:hover {
  background: var(--accent);
  color: #fff;
  transform: translateY(-1px);
}

.content-search {
  padding: 12px 20px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border);
}

.content-search input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-input);
  color: var(--text-primary);
  font-size: 13px;
  transition: all 0.15s ease;
}

.content-search input:focus {
  outline: none;
  border-color: var(--accent);
}

.content-search input::placeholder { color: var(--text-muted); }

.items {
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 10px;
  align-content: start;
}

.item {
  background: var(--bg-card);
  border-radius: var(--radius);
  padding: 14px;
  text-align: center;
  border: 1px solid var(--border);
  transition: all 0.15s ease;
  cursor: default;
  min-height: 90px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.item:hover {
  border-color: var(--accent);
  background: var(--bg-hover);
}

.item img {
  width: 48px;
  height: 48px;
  image-rendering: pixelated;
  margin-bottom: 8px;
  border-radius: 8px;
  background: var(--bg-input);
  padding: 4px;
}

.item img:not([src]) { display: none; }

.item .iname {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-bottom: 2px;
  min-height: 16px;
  line-height: 16px;
}

.item .iid {
  font-size: 10px;
  color: var(--text-muted);
  margin-bottom: 8px;
}

.item .icount {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 80px;
  margin-bottom: 10px;
}

.item .btns {
  display: flex;
  gap: 6px;
  justify-content: center;
}

.item .btns button {
  padding: 5px 12px;
  border: none;
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 11px;
  font-weight: 500;
  transition: all 0.15s ease;
}

.btn-rm {
  background: var(--danger-dark);
  color: #fff;
}

.btn-rm:hover {
  background: var(--danger);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--text-muted);
  text-align: center;
  padding: 40px;
}

.empty-state .icon {
  font-size: 48px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.empty-state .title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 8px;
  color: var(--text-secondary);
}

.empty-state .desc {
  font-size: 13px;
  line-height: 1.6;
}

.modal {
  display: none;
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0,0,0,.6);
  backdrop-filter: blur(4px);
  z-index: 20;
  justify-content: center;
  align-items: center;
}

.modal.show { display: flex; }

.modal-content {
  background: var(--bg-card);
  border-radius: var(--radius);
  width: 440px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border);
  animation: scaleIn 0.15s ease;
}

@keyframes scaleIn {
  from { transform: scale(0.95); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}

.modal-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--border);
  display: flex;
  align-items: center;
}

.modal-header h2 {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  flex: 1;
}

.modal-header .close {
  background: none;
  border: none;
  color: var(--text-muted);
  font-size: 20px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.15s ease;
}

.modal-header .close:hover {
  background: var(--bg-hover);
  color: var(--danger);
}

.modal-search { padding: 12px 20px; }

.modal-search input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-input);
  color: var(--text-primary);
  font-size: 13px;
  transition: all 0.15s ease;
}

.modal-search input:focus {
  outline: none;
  border-color: var(--accent);
}

.modal-search input::placeholder { color: var(--text-muted); }

.modal-count {
  padding: 0 20px 12px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.modal-count span {
  font-size: 13px;
  color: var(--text-secondary);
}

.modal-count input {
  width: 80px;
  padding: 6px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-input);
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 600;
  text-align: center;
  transition: all 0.15s ease;
}

.modal-count input:focus {
  outline: none;
  border-color: var(--accent);
}

.modal-count button {
  padding: 6px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-card);
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 12px;
  transition: all 0.15s ease;
}

.modal-count button:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.modal-list {
  flex: 1;
  overflow-y: auto;
  padding: 0 20px 16px;
}

.modal-item {
  display: flex;
  align-items: center;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  margin-bottom: 4px;
  transition: all 0.15s ease;
  cursor: pointer;
}

.modal-item:hover { background: var(--bg-hover); }

.modal-item img {
  width: 28px;
  height: 28px;
  image-rendering: pixelated;
  margin-right: 10px;
  border-radius: 4px;
  background: var(--bg-input);
  padding: 2px;
}

.modal-item .mi-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
}

.modal-item .mi-id {
  font-size: 11px;
  color: var(--text-muted);
  margin-right: 10px;
  padding: 2px 6px;
  background: var(--bg-card);
  border-radius: 4px;
}

.toast {
  position: fixed;
  bottom: 20px;
  right: 20px;
  padding: 10px 20px;
  border-radius: var(--radius-sm);
  font-size: 13px;
  font-weight: 500;
  z-index: 30;
  animation: slideUp 0.2s ease;
  box-shadow: var(--shadow-lg);
}

.toast.ok {
  background: var(--success-dark);
  color: #fff;
}

.toast.err {
  background: var(--danger-dark);
  color: #fff;
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

::-webkit-scrollbar { width: 6px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: var(--border-light); border-radius: 3px; }
::-webkit-scrollbar-thumb:hover { background: var(--text-muted); }
</style>
</head>
<body>
<div class=""header"">
  <h1>领地修改器</h1>
  <button id=""btnRefresh"">刷新</button>
  <button id=""btnAuto"">自动: <span id=""autoLabel"">开</span></button>
  <span class=""status"" id=""status"">就绪</span>
</div>
<div class=""main"">
  <div class=""sidebar"">
    <div class=""sidebar-search""><input id=""searchChest"" placeholder=""搜索箱子..."" oninput=""render()""></div>
    <div class=""chest-list"" id=""chestList""></div>
  </div>
  <div class=""content"" id=""content"">
    <div class=""empty-state"">
      <div class=""icon"">&#128230;</div>
      <div class=""title"">选择一个箱子</div>
      <div class=""desc"">从左侧列表中选择箱子查看物品</div>
    </div>
  </div>
</div>

<div class=""modal"" id=""addModal"">
  <div class=""modal-content"">
    <div class=""modal-header"">
      <h2 id=""addTitle"">添加物品</h2>
      <button class=""close"" onclick=""closeAddModal()"">&times;</button>
    </div>
    <div class=""modal-search""><input id=""addSearch"" placeholder=""搜索物品..."" oninput=""renderAddList()""></div>
    <div class=""modal-count"">
      <span>数量:</span>
      <input id=""addCount"" type=""number"" value=""1"" min=""1"">
      <button onclick=""addN(1)"">+1</button>
      <button onclick=""addN(10)"">+10</button>
      <button onclick=""addN(100)"">+100</button>
      <button onclick=""addN(99999)"">最大</button>
    </div>
    <div class=""modal-list"" id=""addList""></div>
  </div>
</div>

<div class=""modal"" id=""planModal"">
  <div class=""modal-content"">
    <div class=""modal-header"">
      <h2 id=""planTitle"">添加计划库存</h2>
      <button class=""close"" onclick=""closePlanModal()"">&times;</button>
    </div>
    <div class=""modal-search""><input id=""planSearch"" placeholder=""搜索物品..."" oninput=""renderPlanList()""></div>
    <div class=""modal-count"">
      <span>计划数量:</span>
      <input id=""planCount"" type=""number"" value=""1"" min=""1"">
      <button onclick=""planN(1)"">+1</button>
      <button onclick=""planN(10)"">+10</button>
      <button onclick=""planN(100)"">+100</button>
    </div>
    <div class=""modal-list"" id=""planList""></div>
  </div>
</div>

<script>
let chests = [];
let items = [];
let dragonItems = [];
let dragonTypes = [];
let dragonEntities = [];
let dragonNatures = [];
let autoRefresh = true;
let selectedChest = -1;
let showDragonSouls = false;
let searchQuery = '';
let categoryOpen = false;
let dragonMainOpen = false;
let dragonView = ''; // 'souls' | 'materials' | 'summon' | ''
let dragonSouls = [];
let npcMainOpen = false;
let npcView = ''; // 'explore' | 'entities' | ''
let npcScanResult = '';
let npcEntities = [];
let factionEntities = [];
let entityScanData = [];
let npcFinderData = [];
let factionFieldName = '';
let factionAllFields = [];
let filters = [];

async function fetchFilters() {
  try {
    const r = await fetch('/api/filters');
    filters = await r.json();
  } catch(e) {}
}

async function toggleFilter(stuffId) {
  await fetch('/api/filters/toggle', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({stuffId})});
  await fetchFilters();
  await fetchChests();
  renderSidebar();
  if (selectedChest >= 0) renderContent();
}

async function setAllFilters(enabled) {
  await fetch('/api/filters/all', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({enabled})});
  await fetchFilters();
  await fetchChests();
  renderSidebar();
  if (selectedChest >= 0) renderContent();
}

async function fetchChests() {
  try {
    const r = await fetch('/api/chests');
    chests = await r.json();
    document.getElementById('status').textContent = chests.length + ' 个箱子';
  } catch(e) {
    document.getElementById('status').textContent = '连接失败';
  }
}

async function fetchItems() {
  try {
    const r = await fetch('/api/items');
    items = await r.json();
  } catch(e) {}
}

async function fetchDragonItems() {
  try {
    const r = await fetch('/api/dragon');
    dragonItems = await r.json();
  } catch(e) {}
}

function renderDragonItems() {
  const el = document.getElementById('dragonItems');
  if (!el) return;
  let html = '';
  for (const it of dragonItems) {
    html += '<div style=""display:flex;align-items:center;gap:8px;padding:6px 0;border-bottom:1px solid var(--border)"">';
    html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"" style=""width:24px;height:24px;image-rendering:pixelated;border-radius:4px;background:var(--bg-input);padding:2px"">';
    html += '<div style=""flex:1;min-width:0"">';
    html += '<div style=""font-size:12px;font-weight:500;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"">' + esc(it.name) + '</div>';
    html += '<div style=""font-size:10px;color:var(--text-muted)"">ID:' + it.stuffId + '</div>';
    html += '</div>';
    html += '<input type=""number"" class=""count-input"" value=""' + it.count + '"" min=""0"" id=""dragon_' + it.stuffId + '"" style=""width:60px"">';
    html += '<button class=""btn-adj"" onclick=""setDragonItem(' + it.stuffId + ')"" style=""font-size:11px;padding:3px 8px;width:auto;height:auto"">设置</button>';
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

async function summonDragon(stuffId) {
  try {
    const r = await fetch('/api/dragon/summon', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:stuffId})
    });
    const res = await r.json();
    if (res.result === 'ok' || res.result === 'True') {
      toast('召唤成功!');
    } else {
      toast('召唤结果: ' + (res.result || '未知'), res.result !== 'ok');
    }
    fetchDragonItems();
  } catch(e) { toast('召唤失败', true); }
}

async function fetchDragonTypes() {
  try {
    const r = await fetch('/api/dragon/types');
    dragonTypes = await r.json();
  } catch(e) {}
  try {
    const r2 = await fetch('/api/dragon/natures');
    dragonNatures = await r2.json();
  } catch(e) {}
  renderDragonSummon();
  renderDragonSummonList();
}

function renderDragonSummon() {
  const el = document.getElementById('dragonSummon');
  if (!el || dragonTypes.length === 0) return;
  let html = '<div style=""font-size:11px;font-weight:600;color:var(--text-muted);margin-bottom:4px"">召唤龙</div>';
  html += '<div style=""display:flex;gap:6px;align-items:center;flex-wrap:wrap"">';
  html += '<select id=""dragonTypeSelect"" style=""flex:1;min-width:80px;font-size:11px;padding:2px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px"">';
  for (let i = 0; i < dragonTypes.length; i++) {
    html += '<option value=""' + i + '"">' + esc(dragonTypes[i].cn) + '</option>';
  }
  html += '</select>';
  html += '<select id=""dragonLevelSelect"" style=""width:50px;font-size:11px;padding:2px 4px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px"">';
  for (let lv = 1; lv <= 10; lv++) {
    html += '<option value=""' + lv + '"">' + lv + '级</option>';
  }
  html += '</select>';
  html += '<button class=""btn-adj"" onclick=""doSummonDragon()"" style=""font-size:11px;padding:3px 8px;background:#2a4a2a;color:#6f6"">召唤</button>';
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

async function refreshChests() {
  const btn = document.getElementById('btnRefresh');
  try {
    btn.classList.add('loading');
    btn.textContent = '刷新中...';
    document.getElementById('status').textContent = '刷新中...';
    await fetch('/api/refresh', {method:'POST'});
    await fetchFilters();
    await fetchChests();
    await fetchDragonItems();
    renderSidebar();
    if (selectedChest >= 0) renderContent();
    document.getElementById('status').textContent = chests.length + ' 个箱子';
    btn.textContent = '刷新';
  } catch(e) {
    document.getElementById('status').textContent = '刷新失败: ' + e.message;
    btn.textContent = '刷新';
  } finally {
    btn.classList.remove('loading');
  }
}

function renderSidebar() {
  const q = document.getElementById('searchChest').value.toLowerCase();
  const el = document.getElementById('chestList');
  let html = '';

  // 容器分类
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleCategory()"">';
  html += '<span class=""arrow' + (categoryOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>容器</span>';
  html += '<span style=""margin-left:auto;font-size:11px;color:var(--text-muted)"">' + chests.length + '</span>';
  html += '</div>';
  html += '<div class=""category-items' + (categoryOpen ? ' open' : '') + '"">';
  html += '<div class=""filter-section"">';
  html += '<div class=""filter-title""><span>筛选设施</span>';
  html += '<span class=""filter-actions""><a onclick=""setAllFilters(true)"">全选</a><a onclick=""setAllFilters(false)"">清空</a></span></div>';
  html += '<div class=""filter-grid"">';
  for (let fi = 0; fi < filters.length; fi++) {
    const f = filters[fi];
    html += '<span class=""filter-tag' + (f.enabled ? ' on' : '') + '"" onclick=""toggleFilter(' + f.stuffId + ')"">' + esc(f.name) + '</span>';
  }
  html += '</div></div>';

  if (chests.length === 0) {
    html += '<div style=""padding:20px;text-align:center;color:var(--text-muted)"">暂无箱子</div>';
  } else {
    for (let i = 0; i < chests.length; i++) {
      const c = chests[i];
      if (q && !c.name.toLowerCase().includes(q)) continue;
      const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
      const isActive = selectedChest === i;
      html += '<div class=""chest-item' + (isActive ? ' active' : '') + '"" onclick=""selectChest(' + i + ')"">';
      html += '<div class=""ci-icon""><img src=""/icon/' + c.stuffId + '"" onerror=""this.remove()""></div>';
      html += '<div class=""ci-info"">';
      html += '<div class=""ci-name"">' + esc(c.name) + '</div>';
      html += '<div class=""ci-count"">' + cap + ' 物品</div>';
      html += '</div></div>';
    }
  }

  html += '</div></div>';

  // 驯龙总分类
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleDragonMain()"">';
  html += '<span class=""arrow' + (dragonMainOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>驯龙</span>';
  html += '</div>';
  html += '<div class=""category-items' + (dragonMainOpen ? ' open' : '') + '"">';

  // 地图龙
  const activeSouls = dragonSouls.filter(s => s.is_active);
  const idleSouls = dragonSouls.filter(s => !s.is_active);
  html += '<div class=""chest-item' + (dragonView === 'souls' ? ' active' : '') + '"" onclick=""selectDragonView(\'souls\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F409;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">地图龙</div>';
  html += '<div class=""ci-count"">' + activeSouls.length + ' 条</div>';
  html += '</div></div>';

  // 龙素材
  html += '<div class=""chest-item' + (dragonView === 'materials' ? ' active' : '') + '"" onclick=""selectDragonView(\'materials\')"">';
  html += '<div class=""ci-icon"" style=""font-size:16px;display:flex;align-items:center;justify-content:center"">&#x1F48A;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">龙素材</div>';
  html += '<div class=""ci-count"">' + dragonItems.length + ' 种</div>';
  html += '</div></div>';

  // 召唤龙
  const summonCount = dragonTypes.length + idleSouls.length;
  html += '<div class=""chest-item' + (dragonView === 'summon' ? ' active' : '') + '"" onclick=""selectDragonView(\'summon\')"">';
  html += '<div class=""ci-icon"" style=""font-size:16px;display:flex;align-items:center;justify-content:center"">&#x2728;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">召唤龙</div>';
  html += '<div class=""ci-count"">' + summonCount + '</div>';
  html += '</div></div>';

  html += '</div></div>';

  // 怪物总分类
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleNpcMain()"">';
  html += '<span class=""arrow' + (npcMainOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>怪物</span>';
  html += '</div>';
  html += '<div class=""category-items' + (npcMainOpen ? ' open' : '') + '"">';

  // 怪物探索
  html += '<div class=""chest-item' + (npcView === 'explore' ? ' active' : '') + '"" onclick=""selectNpcView(\'explore\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F50D;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">怪物探索</div>';
  html += '<div class=""ci-count"">扫描实体数据</div>';
  html += '</div></div>';

  // 怪物实体
  html += '<div class=""chest-item' + (npcView === 'entities' ? ' active' : '') + '"" onclick=""selectNpcView(\'entities\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F47E;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">怪物实体</div>';
  html += '<div class=""ci-count"">' + npcEntities.length + ' 个</div>';
  html += '</div></div>';

  // 阵营查找
  html += '<div class=""chest-item' + (npcView === 'faction' ? ' active' : '') + '"" onclick=""selectNpcView(\'faction\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x2694;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">阵营查找</div>';
  html += '<div class=""ci-count"">' + factionEntities.length + ' 个实体</div>';
  html += '</div></div>';

  // 实体扫描
  html += '<div class=""chest-item' + (npcView === 'entityScan' ? ' active' : '') + '"" onclick=""selectNpcView(\'entityScan\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F50E;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">实体扫描</div>';
  html += '<div class=""ci-count"">' + entityScanData.length + ' 个实体</div>';
  html += '</div></div>';

  // 找NPC
  html += '<div class=""chest-item' + (npcView === 'npcFinder' ? ' active' : '') + '"" onclick=""selectNpcView(\'npcFinder\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F464;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">找NPC</div>';
  html += '<div class=""ci-count"">' + (npcFinderData.length > 0 ? npcFinderData.slice(0,3).map(e => e.npcName || e.goName).join(', ') + (npcFinderData.length > 3 ? '...' : '') : '0 个') + '</div>';
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

function selectNpcView(view) {
  selectedChest = -1;
  dragonView = '';
  npcView = (npcView === view) ? '' : view;
  renderSidebar();
  renderContent();
}

async function npcExplore() {
  const btn = document.getElementById('npcExploreBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    const r = await fetch('/api/npc/explore', {method:'POST'});
    const d = await r.json();
    npcScanResult = '探索扫描完成，请查看 BepInEx 控制台日志 (LogOutput.log)';
    document.getElementById('status').textContent = 'NPC 探索完成';
  } catch(e) {
    npcScanResult = '扫描失败: ' + e.message;
    document.getElementById('status').textContent = 'NPC 探索失败';
  } finally {
    if (btn) { btn.textContent = '开始探索扫描'; btn.disabled = false; }
    renderContent();
  }
}

async function npcScanCombat() {
  const btn = document.getElementById('npcScanBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    const r = await fetch('/api/npc/scan', {method:'POST'});
    const d = await r.json();
    npcScanResult = '战斗实体扫描完成，请查看 BepInEx 控制台日志 (LogOutput.log)';
    document.getElementById('status').textContent = 'NPC 扫描完成';
  } catch(e) {
    npcScanResult = '扫描失败: ' + e.message;
    document.getElementById('status').textContent = 'NPC 扫描失败';
  } finally {
    if (btn) { btn.textContent = '扫描战斗实体'; btn.disabled = false; }
    renderContent();
  }
}

async function fetchNpcEntities() {
  try {
    const r = await fetch('/api/npc/entities');
    const d = await r.json();
    if (Array.isArray(d)) {
      npcEntities = d;
      document.getElementById('status').textContent = npcEntities.length + ' 个 NPC';
    }
  } catch(e) {
    document.getElementById('status').textContent = 'NPC 加载失败';
  }
}

async function setNpcField(guid, field) {
  const input = document.getElementById('npc_' + field + '_' + guid);
  const val = parseFloat(input.value) || 0;
  try {
    const r = await fetch('/api/npc/entity/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({guid, field, value: val})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('设置成功');
    await fetchNpcEntities();
    renderContent();
  } catch(e) { toast('设置失败', true); }
}

// ===== 阵营扫描 =====
async function factionScan() {
  const btn = document.getElementById('factionScanBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    await fetch('/api/npc/faction', {method:'POST'});
    await fetchFactionEntities();
    toast('阵营扫描完成 (' + factionEntities.length + ' 个实体)');
  } catch(e) {
    toast('扫描失败: ' + e.message, true);
  } finally {
    if (btn) { btn.textContent = '开始阵营扫描'; btn.disabled = false; }
    renderContent();
  }
}

async function fetchFactionEntities() {
  try {
    const r = await fetch('/api/npc/faction/entities');
    const d = await r.json();
    if (d.entities) {
      factionEntities = d.entities;
      factionFieldName = d.factionField || '';
      factionAllFields = d.allFields || [];
    }
  } catch(e) {}
}

function renderFactionPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px;height:100%;display:flex;flex-direction:column;overflow:hidden"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x2694; 阵营查找</h2>';
  html += '<p style=""color:var(--text-secondary);margin-bottom:12px;font-size:13px"">通过 owner_facility_guid 关联建筑类型判断阵营。FacilityBarracks=玩家(1), FacilityEnemyBarracks/FacilityBigTree=敌方(2), 未匹配=未知(-1)。</p>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px;align-items:center"">';
  html += '<button id=""factionScanBtn"" onclick=""factionScan()"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">开始阵营扫描</button>';
  if (factionFieldName) html += '<span style=""color:var(--success);font-size:13px"">阵营字段: <b>' + esc(factionFieldName) + '</b></span>';
  html += '<span style=""color:var(--text-muted);font-size:13px"">' + factionEntities.length + ' 个实体</span>';
  html += '</div>';

  if (factionAllFields.length > 0) {
    html += '<details style=""margin-bottom:16px"">';
    html += '<summary style=""cursor:pointer;color:var(--text-muted);font-size:12px"">查看所有字段 (' + factionAllFields.length + ')</summary>';
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);padding:8px 12px;margin-top:6px;font-size:11px;color:var(--text-muted);max-height:200px;overflow-y:auto"">';
    for (const f of factionAllFields) {
      html += '<span style=""display:inline-block;padding:2px 6px;margin:2px;background:var(--bg-input);border-radius:3px"">' + esc(f) + '</span>';
    }
    html += '</div></details>';
  }

  if (factionEntities.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x2694;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">暂无数据</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">点击上方按钮扫描阵营</div>';
    html += '</div>';
  } else {
    html += '<div style=""flex:1;overflow-y:auto;padding-right:8px"">';
    // 按阵营分组
    const groups = {};
    for (const e of factionEntities) {
      const f = e.faction !== undefined ? e.faction : -1;
      if (!groups[f]) groups[f] = [];
      groups[f].push(e);
    }
    const sortedFactions = Object.keys(groups).map(Number).sort((a,b) => a - b);

    for (const fId of sortedFactions) {
      const ents = groups[fId];
      const isPlayer = fId === 1;
      const isEnemy = fId === 2;
      const isUnknown = fId === -1;
      const headerColor = isPlayer ? 'var(--success)' : isEnemy ? 'var(--danger, #e74c3c)' : 'var(--text-muted)';
      let badge = '';
      if (isPlayer) badge = ' <span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--success-dark);color:#fff;margin-left:6px"">玩家</span>';
      else if (isEnemy) badge = ' <span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--danger, #e74c3c);color:#fff;margin-left:6px"">敌方</span>';
      else if (isUnknown) badge = ' <span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--text-muted);color:#fff;margin-left:6px"">未知</span>';
      const factionLabel = isUnknown ? '未知来源' : '阵营 ' + fId;

      html += '<details open style=""margin-bottom:12px"">';
      html += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;color:' + headerColor + ';font-size:14px;display:flex;align-items:center"">';
      html += factionLabel + badge + ' <span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + ents.length + ' 个</span>';
      html += '</summary>';

      html += '<div style=""display:flex;flex-direction:column;gap:8px;padding:8px 0"">';
      for (const npc of ents) {
        const goName = npc.goName || 'unknown';
        const stuffId = npc.stuff_id || 0;
        const guid = npc.guid || 0;
        const hp = npc.hp || 0;
        const hpTotal = npc.hp_total || 0;
        const atkMin = npc.atk_min || 0;
        const atkMax = npc.atk_max || 0;
        const speed = npc.speed || 0;
        const monsterName = getMonsterName(stuffId);

        html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);padding:12px"">';
        html += '<div style=""display:flex;justify-content:space-between;align-items:center;margin-bottom:8px"">';
        html += '<div><span style=""font-weight:600;color:var(--text-primary);font-size:13px"">' + esc(monsterName) + '</span>';
        html += '<span style=""font-size:10px;color:var(--text-muted);margin-left:8px"">' + esc(goName) + '</span></div>';
        html += '<div style=""font-size:11px;color:var(--text-muted)"">GUID:' + guid + ' ID:' + stuffId + '</div>';
        html += '</div>';
        html += '<div style=""display:flex;gap:12px;font-size:12px;color:var(--text-secondary)"">';
        html += '<span>HP: ' + Math.round(hp) + '/' + Math.round(hpTotal) + '</span>';
        html += '<span>ATK: ' + Math.round(atkMin) + '-' + Math.round(atkMax) + '</span>';
        html += '<span>Speed: ' + speed.toFixed(1) + '</span>';
        html += '</div></div>';
      }
      html += '</div></details>';
    }
    html += '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
}

// ===== 实体扫描 =====
async function entityScan() {
  const btn = document.getElementById('entityScanBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    await fetch('/api/entity/scan', {method:'POST'});
    await fetchEntityScanData();
    toast('实体扫描完成 (' + entityScanData.length + ' 个实体)');
    document.getElementById('status').textContent = '实体扫描完成';
  } catch(e) {
    toast('扫描失败: ' + e.message, true);
    document.getElementById('status').textContent = '实体扫描失败';
  } finally {
    if (btn) { btn.textContent = '开始扫描'; btn.disabled = false; }
    renderContent();
  }
}

async function fetchEntityScanData() {
  try {
    const r = await fetch('/api/entity/entities');
    const d = await r.json();
    if (Array.isArray(d)) {
      entityScanData = d;
    }
  } catch(e) {}
}

async function setEntityField(ptrHash, field) {
  const input = document.getElementById('entity_' + field + '_' + ptrHash);
  const val = parseFloat(input.value) || 0;
  try {
    const r = await fetch('/api/entity/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash, field, value: val})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('设置成功');
    loadEntityFields(ptrHash);
  } catch(e) { toast('设置失败', true); }
}

async function loadEntityFields(ptrHash) {
  const container = document.getElementById('entity_fields_' + ptrHash);
  if (!container) return;
  container.innerHTML = '<div style=""padding:8px;color:var(--text-muted);font-size:12px"">加载中...</div>';
  try {
    const r = await fetch('/api/entity/fields/' + ptrHash);
    const fields = await r.json();
    if (fields.error) { container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">' + fields.error + '</div>'; return; }
    container.innerHTML = renderFieldsTable(fields, ptrHash, 'entity', setEntityField);
  } catch(e) { container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">加载失败</div>'; }
}

function renderFieldsTable(fields, ptrHash, prefix, setFn) {
  let html = '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);overflow:hidden"">';
  html += '<table style=""width:100%;border-collapse:collapse;font-size:12px"">';
  html += '<tr style=""background:var(--bg-input)""><th style=""text-align:left;padding:6px 10px;color:var(--text-muted);font-weight:500;width:30%"">字段</th><th style=""text-align:left;padding:6px 10px;color:var(--text-muted);font-weight:500;width:20%"">类型</th><th style=""text-align:left;padding:6px 10px;color:var(--text-muted);font-weight:500;width:30%"">值</th><th style=""padding:6px 10px;width:20%""></th></tr>';
  const sortedKeys = Object.keys(fields).sort();
  for (const k of sortedKeys) {
    const f = fields[k];
    const isFloat = f.isFloat;
    const isString = f.isString;
    const isPointer = f.isPointer;
    const val = f.value;
    const typeName = f.typeName || '';
    const typeLabel = isPointer ? typeName : (isString ? 'string' : (isFloat ? 'float' : 'int'));
    html += '<tr style=""border-top:1px solid var(--border)"">';
    html += '<td style=""padding:4px 10px;color:var(--text-primary);font-family:monospace"">' + esc(k) + '</td>';
    html += '<td style=""padding:4px 10px;color:var(--text-muted)"">' + esc(typeLabel) + '</td>';
    if (isPointer) {
      html += '<td style=""padding:4px 10px;color:var(--text-muted);font-style:italic"">-</td>';
      html += '<td style=""padding:4px 10px""></td>';
    } else {
      html += '<td style=""padding:4px 10px;color:var(--text-primary);font-family:monospace"">' + (isString ? esc(val || '') : (isFloat ? val.toFixed(2) : val)) + '</td>';
      html += '<td style=""padding:4px 10px;text-align:center"">';
      if (!isString) {
        html += '<div style=""display:flex;gap:4px;align-items:center;justify-content:center"">';
        html += '<input id=""' + prefix + '_' + k + '_' + ptrHash + '"" type=""text"" value=""' + (isFloat ? val.toFixed(2) : val) + '"" style=""width:70px;padding:2px 4px;font-size:11px;background:var(--bg-input);border:1px solid var(--border);border-radius:3px;color:var(--text-primary)"">';
        html += '<button onclick=""' + setFn.name + '(' + ptrHash + ', \'' + esc(k) + '\')"" style=""padding:2px 6px;font-size:10px;background:var(--accent);color:#fff;border:none;border-radius:3px;cursor:pointer"">OK</button>';
        html += '</div>';
      }
      html += '</td>';
    }
    html += '</tr>';
  }
  html += '</table></div>';
  return html;
}

function getKingdomInfo(id) {
  const map = {
    1:   {name:'我方',   bg:'var(--success-dark, #27ae60)', fg:'#fff'},
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

function classifyEntity(className) {
  if (className.startsWith('Stuff')) return 'drop';
  if (className.startsWith('WildAnimal')) return 'animal';
  if (className.startsWith('Facility')) return 'building';
  if (className.startsWith('Monster')) return 'monster';
  return 'other';
}

function renderEntityScanPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px;height:100%;display:flex;flex-direction:column;overflow:hidden"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x1F50E; 实体扫描</h2>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px;align-items:center"">';
  html += '<button id=""entityScanBtn"" onclick=""entityScan()"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">开始扫描</button>';
  html += '<span style=""color:var(--text-muted);font-size:13px"">' + entityScanData.length + ' 个实体</span>';
  html += '</div>';

  if (entityScanData.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x1F50E;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">暂无数据</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">点击上方按钮扫描实体</div>';
    html += '</div>';
  } else {
    // 按类别分组
    const groups = {drop:[], animal:[], building:[], monster:[], other:[]};
    for (const e of entityScanData) {
      const cat = classifyEntity(e.className || '');
      groups[cat].push(e);
    }
    const cats = [
      {key:'monster', label:'怪物', icon:'&#x1F47E;', color:'var(--danger, #e74c3c)'},
      {key:'building', label:'建筑物', icon:'&#x1F3D7;', color:'var(--success)'},
      {key:'drop', label:'掉落物', icon:'&#x1F4E6;', color:'var(--accent)'},
      {key:'animal', label:'野兽', icon:'&#x1F43E;', color:'var(--warning, #f39c12)'},
      {key:'other', label:'其他', icon:'&#x2753;', color:'var(--text-muted)'}
    ];

    html += '<div style=""flex:1;overflow-y:auto;padding-right:8px"">';
    for (const cat of cats) {
      const items = groups[cat.key];
      if (items.length === 0) continue;
      html += '<details open style=""margin-bottom:12px"">';
      html += '<summary style=""cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px"">';
      html += '<span>' + cat.icon + '</span>';
      html += '<span style=""color:' + cat.color + '"">' + cat.label + '</span>';
      html += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + items.length + ' 个</span>';
      html += '</summary>';
      html += '<div style=""display:flex;flex-direction:column;gap:6px;padding:8px 0"">';
      for (const e of items) {
        const goName = e.goName || 'unknown';
        const className = e.className || '';
        const stuffId = e.stuffId || 0;
        const guid = e.guid || 0;
        const ptrHash = e.ptrHash || 0;
        const entityName = e.name || '';
        const displayName = entityName || goName;
        const fieldCount = e.fieldCount || 0;

        html += '<details style=""margin-bottom:4px"" ontoggle=""if(this.open)loadEntityFields(' + ptrHash + ')"">';
        html += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px;display:flex;align-items:center;gap:8px"">';
        html += '<span style=""font-weight:600;color:var(--text-primary)"">' + esc(displayName) + '</span>';
        html += '<span style=""font-size:10px;color:var(--accent-light);padding:1px 6px;border-radius:8px;background:var(--accent-bg)"">' + esc(className) + '</span>';
        html += '<span style=""font-size:11px;color:var(--text-muted);margin-left:auto"">GUID:' + guid + ' ID:' + stuffId + ' (' + fieldCount + '字段)</span>';
        html += '</summary>';
        html += '<div id=""entity_fields_' + ptrHash + '"" style=""padding:8px 0""><div style=""padding:8px;color:var(--text-muted);font-size:12px"">点击展开加载字段...</div></div></details>';
      }
      html += '</div></details>';
    }
    html += '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
}

// ===== 找NPC =====
async function npcFinderScan() {
  const btn = document.getElementById('npcFinderScanBtn');
  if (btn) { btn.textContent = '扫描中...'; btn.disabled = true; }
  try {
    await fetch('/api/npcfinder/scan', {method:'POST'});
    await fetchNpcFinderData();
    toast('NPC 查找完成 (' + npcFinderData.length + ' 个)');
  } catch(e) {
    toast('扫描失败: ' + e.message, true);
  } finally {
    if (btn) { btn.textContent = '开始查找'; btn.disabled = false; }
    renderContent();
  }
}

async function fetchNpcFinderData() {
  try {
    const r = await fetch('/api/npcfinder/npcs?t=' + Date.now());
    const d = await r.json();
    console.log('[NpcFinder] fetched', d.length, 'npcs, sample:', d.length > 0 ? JSON.stringify({hometownKingdomId: d[0].hometownKingdomId, npcName: d[0].npcName}) : 'empty');
    if (Array.isArray(d)) npcFinderData = d;
  } catch(e) { console.error('[NpcFinder] fetch error:', e); }
}

async function setNpcFinderField(ptrHash, field) {
  const input = document.getElementById('npcfinder_' + field + '_' + ptrHash);
  const val = parseFloat(input.value) || 0;
  try {
    const r = await fetch('/api/npcfinder/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ptrHash, field, value: val})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('设置成功');
    loadNpcFinderFields(ptrHash);
  } catch(e) { toast('设置失败', true); }
}

async function loadNpcFinderFields(ptrHash) {
  const container = document.getElementById('npcfinder_fields_' + ptrHash);
  if (!container) return;
  container.innerHTML = '<div style=""padding:8px;color:var(--text-muted);font-size:12px"">加载中...</div>';
  try {
    const r = await fetch('/api/npcfinder/fields/' + ptrHash);
    const fields = await r.json();
    if (fields.error) { container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">' + fields.error + '</div>'; return; }
    container.innerHTML = renderFieldsTable(fields, ptrHash, 'npcfinder', setNpcFinderField);
  } catch(e) { container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">加载失败</div>'; }
}

function renderNpcFinderPanel() {
  const el = document.getElementById('content');
  console.log('[NpcFinder] render, data count:', npcFinderData.length, 'sample:', npcFinderData.length > 0 ? JSON.stringify({hometownKingdomId: npcFinderData[0].hometownKingdomId, npcName: npcFinderData[0].npcName}) : 'empty');
  let html = '';
  html += '<div style=""padding:20px;height:100%;display:flex;flex-direction:column;overflow:hidden"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x1F464; 找NPC</h2>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px;align-items:center"">';
  html += '<button id=""npcFinderScanBtn"" onclick=""npcFinderScan()"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">开始查找</button>';
  html += '<span style=""color:var(--text-muted);font-size:13px"">' + npcFinderData.length + ' 个 NPC</span>';
  html += '</div>';

  if (npcFinderData.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x1F464;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">暂无数据</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">点击上方按钮查找 NPC 实体</div>';
    html += '</div>';
  } else {
    // 按组件类名分组
    const groups = {};
    for (const e of npcFinderData) {
      const cn = e.className || 'unknown';
      if (!groups[cn]) groups[cn] = [];
      groups[cn].push(e);
    }
    const sortedClasses = Object.keys(groups).sort();

    html += '<div style=""flex:1;overflow-y:auto;padding-right:8px"">';
    for (const cn of sortedClasses) {
      const items = groups[cn];
      html += '<details style=""margin-bottom:12px"">';
      html += '<summary style=""cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px"">';
      html += '<span style=""color:var(--accent-light)"">' + esc(cn) + '</span>';
      html += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + items.length + ' 个</span>';
      html += '</summary>';
      html += '<div style=""display:flex;flex-direction:column;gap:6px;padding:8px 0"">';
      for (const e of items) {
        const goName = e.goName || 'unknown';
        const npcName = e.npcName || '';
        const hometownKingdomId = e.hometownKingdomId || 0;
        const guid = e.guid || 0;
        const stuffId = e.stuffId || 0;
        const ptrHash = e.ptrHash || 0;
        const fieldCount = e.fieldCount || 0;
        const displayName = npcName || goName;
        const kInfo = getKingdomInfo(hometownKingdomId);

        html += '<details style=""margin-bottom:4px"" ontoggle=""if(this.open)loadNpcFinderFields(' + ptrHash + ')"">';
        html += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:13px;display:flex;align-items:center;gap:8px"">';
        html += '<span style=""font-weight:600;color:var(--text-primary)"">' + esc(displayName) + '</span>';
        if (kInfo) html += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:' + kInfo.bg + ';color:' + kInfo.fg + '"">' + esc(kInfo.name) + '</span>';
        if (npcName) html += '<span style=""font-size:10px;color:var(--text-muted)"">' + esc(goName) + '</span>';
        html += '<span style=""font-size:11px;color:var(--text-muted);margin-left:auto"">GUID:' + guid + ' stuffId:' + stuffId + ' (' + fieldCount + '字段)</span>';
        html += '</summary>';
        html += '<div id=""npcfinder_fields_' + ptrHash + '"" style=""padding:8px 0""><div style=""padding:8px;color:var(--text-muted);font-size:12px"">点击展开加载字段...</div></div></details>';
      }
      html += '</div></details>';
    }
    html += '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
}

function toggleSoulsCategory() {
  soulsCategoryOpen = !soulsCategoryOpen;
  renderSidebar();
}

async function fetchDragonSouls() {
  try {
    const r = await fetch('/api/dragon/souls');
    dragonSouls = await r.json();
  } catch(e) {}
}

async function fetchDragonEntities() {
  try {
    const r = await fetch('/api/dragon/entities');
    const d = await r.json();
    if (Array.isArray(d) && d.length > 0) {
      dragonEntities = d;
      console.log('龙实体加载:', d.length, '条');
    } else {
      console.log('龙实体API返回:', JSON.stringify(d).substring(0, 200));
    }
  } catch(e) { console.log('龙实体请求失败:', e); }
}

function renderDragonSoulsList() {
  const el = document.getElementById('dragonSoulsList');
  if (!el) return;
  if (dragonSouls.length === 0) {
    el.innerHTML = '<div style=""padding:8px;color:var(--text-muted);font-size:11px;text-align:center"">暂无龙魂</div>';
    return;
  }
  let html = '';
  // 搜索地图龙按钮
  html += '<div style=""margin-bottom:6px""><button class=""btn-adj"" onclick=""searchMapDragons()"" style=""font-size:11px;padding:3px 8px;width:auto;height:auto"">搜索地图龙</button></div>';
  for (let i = 0; i < dragonSouls.length; i++) {
    const s = dragonSouls[i];
    const active = s.is_active ? '已召唤' : '待命';
    const activeColor = s.is_active ? '#4caf50' : '#888';
    const stuffId = s.StuffId || s.stuff_id || 0;
    const typeName = findDragonTypeName(stuffId);
    const typeIdx = findDragonTypeIndex(stuffId);
    html += '<div style=""padding:6px 0;border-bottom:1px solid var(--border)"">';
    html += '<div style=""display:flex;align-items:center;gap:8px"">';
    html += '<div style=""width:32px;height:32px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden"">';
    if (typeIdx >= 0) html += '<img src=""/api/dragon/icon/' + typeIdx + '"" style=""width:28px;height:28px;object-fit:contain"" onerror=""hideImg(this)"">';
    else html += '<span style=""font-size:14px"">&#x1F409;</span>';
    html += '</div>';
    html += '<div style=""flex:1;min-width:0"">';
    html += '<div style=""font-size:12px;font-weight:500"">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    html += '<div style=""font-size:10px;color:' + activeColor + '"">' + active + '</div>';
    html += '</div></div>';
    // 强化编辑
    const parts = [
      {key:'Head', label:'头', enhanceRange:[1001,1050]},
      {key:'Claw', label:'爪', enhanceRange:[3001,3050]},
      {key:'Shield', label:'甲', enhanceRange:[2001,2050]},
      {key:'Cloud', label:'魂', enhanceRange:[4001,4050]},
    ];
    html += '<div style=""display:flex;flex-wrap:wrap;gap:4px;margin-top:4px;margin-left:36px;align-items:center"">';
    for (const p of parts) {
      const v = s[p.key] ?? s[p.key.toLowerCase()] ?? 0;
      html += '<span style=""font-size:10px;color:var(--text-muted)"">' + p.label + ':</span>';
      html += '<input type=""number"" id=""soul_' + i + '_' + p.key + '"" value=""' + v + '"" min=""0"" max=""50"" style=""width:36px;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px"">';
      html += '<button class=""btn-adj"" onclick=""setSoulProp(' + i + ',\'' + p.key + '\',' + i + ')"" style=""font-size:9px;padding:1px 4px;width:auto;height:auto"">设</button>';
    }
    // potentiality
    const pot = s.Potentiality ?? s.potentiality ?? 0;
    html += '<span style=""font-size:10px;color:var(--text-muted)"">潜力:</span>';
    html += '<input type=""number"" id=""soul_' + i + '_Potentiality"" value=""' + pot + '"" min=""0"" style=""width:36px;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px"">';
    html += '<button class=""btn-adj"" onclick=""setSoulProp(' + i + ',\'Potentiality\',' + i + ')"" style=""font-size:9px;padding:1px 4px;width:auto;height:auto"">设</button>';
    html += '</div>';
    // nature
    const natures = s.NatureList || s.nature_list;
    if (natures && natures.length > 0) {
      html += '<div style=""display:flex;flex-wrap:wrap;gap:3px;margin-top:3px;margin-left:36px"">';
      for (const nid of natures) {
        const n = dragonNatures.find(x => x.id === nid);
        html += '<span style=""font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf"">' + (n ? n.name : '#' + nid) + '</span>';
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
    html += '<div style=""padding:8px 0;border-bottom:1px solid var(--border)"">';
    html += '<div style=""display:flex;align-items:center;gap:8px"">';
    html += '<div style=""width:28px;height:28px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;font-size:14px"">&#x1F409;</div>';
    html += '<div style=""flex:1;min-width:0"">';
    html += '<div style=""font-size:12px;font-weight:500"">' + esc(dt.cn) + '</div>';
    html += '<div style=""font-size:10px;color:var(--text-muted)"">' + esc(dt.name) + ' (ID:' + dt.baseId + '~' + (dt.baseId+9) + ')</div>';
    html += '</div>';
    html += '<select id=""summonLv_' + i + '"" style=""width:48px;font-size:11px;padding:2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:4px"">';
    for (let lv = 1; lv <= 10; lv++) {
      html += '<option value=""' + lv + '"">' + lv + '级</option>';
    }
    html += '</select>';
    html += '<button class=""btn-adj"" onclick=""doSummonDragonAt(' + i + ')"" style=""font-size:11px;padding:3px 8px;background:#2a4a2a;color:#6f6;width:auto;height:auto"">召唤</button>';
    html += '</div>';
    // nature 选择标签
    html += '<div style=""display:flex;flex-wrap:wrap;gap:3px;margin-top:4px;margin-left:36px"">';
    for (const n of dragonNatures) {
      const checked = natureSelected(i, n.id);
      html += '<span class=""filter-tag' + (checked ? ' on' : '') + '"" onclick=""toggleSummonNature(' + i + ',' + n.id + ',this)"" style=""font-size:10px;padding:1px 5px"">' + n.name + '</span>';
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

function selectChest(i) {
  selectedChest = i;
  dragonView = '';
  showDragonSouls = false;
  renderSidebar();
  renderContent();
}

function selectDragonView(view) {
  selectedChest = -1;
  dragonView = (dragonView === view) ? '' : view;
  showDragonSouls = (dragonView === 'souls');
  renderSidebar();
  renderContent();
}

function renderContent() {
  const el = document.getElementById('content');

  // 驯龙视图
  if (dragonView === 'souls') {
    el.innerHTML = '<div id=""dragonViewContent""></div>';
    renderDragonSoulsPanel();
    return;
  }
  if (dragonView === 'materials') {
    el.innerHTML = '<div id=""dragonViewContent""></div>';
    renderDragonMaterialsPanel();
    return;
  }
  if (dragonView === 'summon') {
    el.innerHTML = '<div id=""dragonViewContent""></div>';
    renderDragonSummonPanel();
    return;
  }

  // NPC 视图
  if (npcView === 'explore') {
    renderNpcExplorePanel();
    return;
  }
  if (npcView === 'entities') {
    renderNpcEntitiesPanel();
    return;
  }
  if (npcView === 'faction') {
    renderFactionPanel();
    return;
  }
  if (npcView === 'entityScan') {
    renderEntityScanPanel();
    return;
  }
  if (npcView === 'npcFinder') {
    renderNpcFinderPanel();
    return;
  }

  if (selectedChest < 0 || selectedChest >= chests.length) {
    el.innerHTML = '<div class=""empty-state""><div class=""icon"">&#128230;</div><div class=""title"">选择一个箱子</div><div class=""desc"">从左侧列表中选择箱子查看物品</div></div>';
    return;
  }

  const c = chests[selectedChest];
  const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
  const q = searchQuery.toLowerCase();

  let html = '';
  html += '<div class=""content-header"">';
  html += '<span class=""ch-title"">' + esc(c.name) + '</span>';
  html += '<span class=""ch-cap"">' + cap + '</span>';
  html += '<button class=""btn-locate"" onclick=""locateChest(' + selectedChest + ')"">定位</button>';
  html += '<button class=""btn-add"" onclick=""openAddModal(' + selectedChest + ')"">+ 添加物品</button>';
  html += '</div>';
  html += '<div class=""content-search""><input id=""searchItem"" placeholder=""搜索物品..."" value=""' + esc(searchQuery) + '"" oninput=""searchQuery=this.value;renderContent()""></div>';

  if (c.items.length === 0) {
    html += '<div class=""empty-state""><div class=""icon"">&#128230;</div><div class=""title"">箱子为空</div><div class=""desc"">点击上方按钮添加物品</div></div>';
  } else {
    html += '<div class=""items"">';
    for (const it of c.items) {
      if (q && !it.name.toLowerCase().includes(q)) continue;
      html += '<div class=""item"">';
      html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
      html += '<div class=""iname"" title=""' + esc(it.name) + '"">' + esc(it.name) + '</div>';
      html += '<div class=""iid"">ID:' + it.stuffId + '</div>';
      html += '<div class=""icount"">';
      html += '<input type=""number"" class=""count-input"" value=""' + it.count + '"" min=""0"" id=""cnt_' + it.stuffId + '"">';
      html += '</div>';
      html += '<div class=""btns"">';
      html += '<button class=""btn-rm"" onclick=""setBagItem(' + selectedChest + ',' + it.stuffId + ')"">设置</button>';
      html += '<button class=""btn-rm"" onclick=""doRemove(' + selectedChest + ',' + it.stuffId + ',' + it.count + ')"">清空</button>';
      html += '</div></div>';
    }
    html += '</div>';
  }

  // 计划库存区域
  if (c.planStock && c.planStock.length > 0) {
    html += '<div class=""plan-section"">';
    html += '<div class=""plan-header""><span>计划库存</span>';
    html += '<button class=""btn-add-plan"" onclick=""openPlanModal(' + selectedChest + ')"">+ 添加</button></div>';
    html += '<div class=""items"">';
    for (const ps of c.planStock) {
      html += '<div class=""item plan-item"">';
      html += '<img src=""/icon/' + ps.stuffId + '"" onerror=""hideImg(this)"">';
      html += '<div class=""iname"" title=""' + esc(ps.name) + '"">' + esc(ps.name) + '</div>';
      html += '<div class=""iid"">ID:' + ps.stuffId + '</div>';
      html += '<div class=""icount"">';
      html += '<input type=""number"" class=""count-input plan-input"" value=""' + ps.count + '"" min=""0"" id=""plan_' + ps.stuffId + '"">';
      html += '</div>';
      html += '<div class=""btns"">';
      html += '<button class=""btn-rm"" onclick=""setPlanItem(' + selectedChest + ',' + ps.stuffId + ')"">设置</button>';
      html += '<button class=""btn-rm"" onclick=""doRemovePlan(' + selectedChest + ',' + ps.stuffId + ')"">删除</button>';
      html += '</div></div>';
    }
    html += '</div></div>';
  }

  el.innerHTML = html;
}

function renderDragonSoulsPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el) return;
  const activeSouls = dragonSouls.filter(s => s.is_active);
  if (activeSouls.length === 0) {
    el.innerHTML = '<div style=""padding:20px;text-align:center;color:var(--text-muted)"">暂无活跃龙魂</div>';
    return;
  }
  let html = '';
  html += '<div class=""plan-section"" style=""margin-top:12px"">';
  html += '<div class=""plan-header""><span>地图龙 (' + activeSouls.length + ')</span>';
  html += '<button class=""btn-adj"" onclick=""refreshDragonEntities()"" style=""font-size:11px;padding:3px 8px;width:auto;height:auto;margin-left:auto"">刷新属性</button></div>';
  html += '<div class=""items"" style=""max-height:500px;overflow-y:auto"">';
  for (let i = 0; i < dragonSouls.length; i++) {
    const s = dragonSouls[i];
    if (!s.is_active) continue;
    const stuffId = s.StuffId || s.stuff_id || 0;
    const typeName = findDragonTypeName(stuffId);
    const typeIdx = findDragonTypeIndex(stuffId);
    const active = s.is_active ? '已召唤' : '待命';
    const activeColor = s.is_active ? '#4caf50' : '#888';

    html += '<div class=""item"" style=""flex-direction:column;align-items:flex-start;gap:4px"">';
    html += '<div style=""display:flex;align-items:center;gap:8px;width:100%"">';
    html += '<div style=""width:48px;height:48px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0"">';
    if (typeIdx >= 0) html += '<img src=""/api/dragon/icon/' + typeIdx + '"" style=""width:44px;height:44px;object-fit:contain"" onerror=""hideImg(this)"">';
    else html += '<span style=""font-size:24px"">&#x1F409;</span>';
    html += '</div>';
    html += '<div style=""flex:1;min-width:0"">';
    html += '<div class=""iname"">' + esc(typeName || ('龙魂#' + (i+1))) + '</div>';
    html += '<div style=""font-size:10px;color:' + activeColor + '"">' + active + '</div>';
    html += '</div></div>';

    // 强化编辑
    const parts = [
      {key:'head', label:'龙头'}, {key:'claw', label:'龙爪'},
      {key:'shield', label:'龙甲'}, {key:'cloud', label:'龙魂'},
      {key:'potentiality', label:'潜力'},
    ];
    html += '<div style=""display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%"">';
    for (const p of parts) {
      const v = s[p.key] ?? 0;
      html += '<div style=""background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px"">';
      html += '<span style=""font-size:9px;color:var(--text-muted)"">' + p.label + '</span>';
      html += '<div style=""display:flex;align-items:center;gap:2px"">';
      html += '<input type=""number"" id=""soul_' + i + '_' + p.key + '"" value=""' + v + '"" min=""0"" max=""50"" style=""width:36px;font-size:10px;padding:1px 2px;background:var(--bg-secondary);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center"">';
      html += '<button class=""btn-adj"" onclick=""setSoulProp(' + i + ',\'' + p.key + '\')"" style=""font-size:9px;padding:1px 4px;width:auto;height:auto"">设</button>';
      html += '</div></div>';
    }
    html += '</div>';

    // nature
    const natures = s.nature_list;
    if (natures && natures.length > 0) {
      html += '<div style=""display:flex;flex-wrap:wrap;gap:3px;width:100%"">';
      for (const nid of natures) {
        const n = dragonNatures.find(x => x.id === nid);
        html += '<span style=""font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf"">' + (n ? n.name : '#' + nid) + '</span>';
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
      html += '<div style=""width:100%;background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:6px"">';
      html += '<div style=""display:flex;justify-content:space-between;font-size:9px;color:var(--text-muted);margin-bottom:3px""><span>HP (GUID:' + e.guid + ')</span><span>' + Math.round(hp) + ' / ' + Math.round(hpTotal) + ' (' + hpPct + '%)</span></div>';
      html += '<div style=""background:var(--bg-secondary);border-radius:3px;height:6px;overflow:hidden;margin-bottom:4px"">';
      html += '<div style=""background:' + (hpPct > 50 ? '#4caf50' : hpPct > 20 ? '#ff9800' : '#f44336') + ';height:100%;width:' + hpPct + '%""></div>';
      html += '</div>';
      const efields = [
        {key:'hp', label:'当前HP', step:1}, {key:'hp_total', label:'HP上限', step:1},
        {key:'atk_max', label:'物攻', step:1}, {key:'magic_atk_max', label:'魔攻', step:1},
        {key:'speed', label:'速度', step:0.01}, {key:'power', label:'力量', step:1},
      ];
      html += '<div style=""display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%"">';
      for (const f of efields) {
        const v = (e[f.key] || 0);
        const disp = f.step < 1 ? v.toFixed(2) : Math.round(v);
        html += '<div style=""background:var(--bg-secondary);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px"">';
        html += '<span style=""font-size:9px;color:var(--text-muted)"">' + f.label + '</span>';
        html += '<div style=""display:flex;align-items:center;gap:2px;width:100%"">';
        html += '<input type=""number"" id=""de_' + e.guid + '_' + f.key + '"" value=""' + disp + '"" step=""' + f.step + '"" style=""flex:1;min-width:0;font-size:10px;padding:1px 2px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center"">';
        html += '<button class=""btn-adj"" onclick=""setDE(' + e.guid + ',\'' + f.key + '\')"" style=""font-size:9px;padding:1px 4px;width:auto;height:auto;flex-shrink:0"">设</button>';
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
  html += '<div class=""plan-section"">';
  html += '<div class=""plan-header""><span>龙素材 (' + dragonItems.length + ')</span></div>';
  html += '<div class=""items"">';
  for (const it of dragonItems) {
    html += '<div class=""item"">';
    html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
    html += '<div class=""iname"" title=""' + esc(it.name) + '"">' + esc(it.name) + '</div>';
    html += '<div class=""iid"">ID:' + it.stuffId + '</div>';
    html += '<div class=""icount"">';
    html += '<input type=""number"" class=""count-input"" value=""' + it.count + '"" min=""0"" id=""dragon_' + it.stuffId + '"">';
    html += '</div>';
    html += '<div class=""btns"">';
    html += '<button class=""btn-rm"" onclick=""setDragonItem(' + it.stuffId + ')"">设置</button>';
    html += '</div></div>';
  }
  if (dragonItems.length === 0) html += '<div style=""padding:20px;text-align:center;color:var(--text-muted)"">暂无龙素材</div>';
  html += '</div></div>';
  el.innerHTML = html;
}

function renderDragonSummonPanel() {
  const el = document.getElementById('dragonViewContent');
  if (!el || dragonTypes.length === 0) return;
  const idleSouls = dragonSouls.filter(s => !s.is_active);
  let html = '';

  // 召唤新龙
  html += '<div class=""plan-section"">';
  html += '<div class=""plan-header""><span>召唤新龙 (' + dragonTypes.length + ')</span></div>';
  html += '<div class=""items"">';
  for (let i = 0; i < dragonTypes.length; i++) {
    const dt = dragonTypes[i];
    html += '<div class=""item"" style=""flex-direction:column;align-items:stretch;gap:6px;padding:10px"">';
    // 第一行: 图标 + 名字 + ID
    html += '<div style=""display:flex;align-items:center;gap:6px"">';
    html += '<div style=""width:40px;height:40px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0"">';
    html += '<img src=""/api/dragon/icon/' + i + '"" style=""width:36px;height:36px;object-fit:contain"" onerror=""hideImg(this)"">';
    html += '</div>';
    html += '<div style=""flex:1;min-width:0"">';
    html += '<div class=""iname"" style=""font-size:11px"">' + esc(dt.cn) + '</div>';
    html += '<div style=""font-size:9px;color:var(--text-muted)"">' + esc(dt.name) + ' ID:' + dt.baseId + '</div>';
    html += '</div></div>';
    // 第二行: 等级 + 召唤按钮
    html += '<div style=""display:flex;align-items:center;gap:6px"">';
    html += '<select id=""summonLv_' + i + '"" style=""flex:1;font-size:10px;padding:3px;background:var(--bg-input);color:var(--text);border:1px solid var(--border);border-radius:3px"">';
    for (let lv = 1; lv <= 10; lv++) {
      html += '<option value=""' + lv + '"">' + lv + '级</option>';
    }
    html += '</select>';
    html += '<button class=""btn-adj"" onclick=""doSummonDragonAt(' + i + ')"" style=""font-size:10px;padding:4px 10px;background:#2a4a2a;color:#6f6;width:auto;height:auto;white-space:nowrap"">召唤</button>';
    html += '</div>';
    // nature 选择
    html += '<div style=""display:flex;flex-wrap:wrap;gap:3px"">';
    for (const n of dragonNatures) {
      const checked = natureSelected(i, n.id);
      html += '<span class=""filter-tag' + (checked ? ' on' : '') + '"" onclick=""toggleSummonNature(' + i + ',' + n.id + ',this)"" style=""font-size:9px;padding:1px 4px"">' + n.name + '</span>';
    }
    html += '</div>';
    html += '</div>';
  }
  html += '</div></div>';

  // 待命龙魂
  if (idleSouls.length > 0) {
    html += '<div class=""plan-section"">';
    html += '<div class=""plan-header""><span>待命龙魂 (' + idleSouls.length + ')</span></div>';
    html += '<div class=""items"">';
    for (let si = 0; si < dragonSouls.length; si++) {
      const s = dragonSouls[si];
      if (s.is_active) continue;
      const stuffId = s.StuffId || s.stuff_id || 0;
      const typeName = findDragonTypeName(stuffId);
      const typeIdx = findDragonTypeIndex(stuffId);
      html += '<div class=""item"" style=""flex-direction:column;align-items:stretch;gap:6px;padding:10px"">';
      html += '<div style=""display:flex;align-items:center;gap:6px"">';
      html += '<div style=""width:40px;height:40px;border-radius:4px;background:var(--bg-input);display:flex;align-items:center;justify-content:center;overflow:hidden;flex-shrink:0"">';
      if (typeIdx >= 0) html += '<img src=""/api/dragon/icon/' + typeIdx + '"" style=""width:36px;height:36px;object-fit:contain"" onerror=""hideImg(this)"">';
      else html += '<span style=""font-size:20px"">&#x1F409;</span>';
      html += '</div>';
      html += '<div style=""flex:1;min-width:0;overflow:hidden"">';
      html += '<div class=""iname"" style=""font-size:11px"">' + esc(typeName || ('龙魂#' + (si+1))) + '</div>';
      html += '<div style=""font-size:9px;color:var(--text-muted)"">待命</div>';
      html += '</div></div>';
      // 属性
      const parts = [
        {key:'head', label:'龙头'}, {key:'claw', label:'龙爪'},
        {key:'shield', label:'龙甲'}, {key:'cloud', label:'龙魂'},
        {key:'potentiality', label:'潜力'},
      ];
      html += '<div style=""display:grid;grid-template-columns:repeat(auto-fill,minmax(80px,1fr));gap:4px;width:100%"">';
      for (const p of parts) {
        const v = s[p.key] ?? 0;
        html += '<div style=""background:var(--bg-input);border:1px solid var(--border);border-radius:4px;padding:4px 6px;display:flex;flex-direction:column;align-items:center;gap:2px"">';
        html += '<span style=""font-size:9px;color:var(--text-muted)"">' + p.label + '</span>';
        html += '<div style=""display:flex;align-items:center;gap:2px"">';
        html += '<input type=""number"" id=""idle_soul_' + si + '_' + p.key + '"" value=""' + v + '"" min=""0"" max=""50"" style=""width:36px;font-size:10px;padding:1px 2px;background:var(--bg-secondary);color:var(--text);border:1px solid var(--border);border-radius:3px;text-align:center"">';
        html += '<button class=""btn-adj"" onclick=""setSoulProp(' + si + ',\'' + p.key + '\')"" style=""font-size:9px;padding:1px 4px;width:auto;height:auto"">设</button>';
        html += '</div></div>';
      }
      html += '</div>';
      // nature
      const natures = s.nature_list;
      if (natures && natures.length > 0) {
        html += '<div style=""display:flex;flex-wrap:wrap;gap:3px;width:100%"">';
        for (const nid of natures) {
          const n = dragonNatures.find(x => x.id === nid);
          html += '<span style=""font-size:9px;padding:1px 4px;border-radius:3px;background:rgba(100,180,255,0.15);color:#8cf"">' + (n ? n.name : '#' + nid) + '</span>';
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
async function locateChest(ci) {
  try {
    const r = await fetch('/api/chest/' + ci + '/locate', {method:'POST'});
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('已定位到 (' + d.posX.toFixed(1) + ', ' + d.posY.toFixed(1) + ')');
  } catch(e) { toast('定位失败', true); }
}

async function setBagItem(ci, sid) {
  const input = document.getElementById('cnt_' + sid);
  const newCnt = parseInt(input.value) || 0;
  const oldItem = chests[ci].items.find(x => x.stuffId === sid);
  const oldCnt = oldItem ? oldItem.count : 0;
  if (newCnt === oldCnt) return;
  if (newCnt <= 0) {
    await doRemove(ci, sid, oldCnt);
  } else {
    // 先删除全部，再添加新数量
    if (oldCnt > 0) await doRemoveRaw(ci, sid, oldCnt);
    await doAddRaw(ci, sid, newCnt);
  }
}

// 设置计划库存
async function setPlanItem(ci, sid) {
  const input = document.getElementById('plan_' + sid);
  const newCnt = parseInt(input.value) || 0;
  await adjPlan(ci, sid, newCnt);
}

// 原始删除（不刷新UI）
async function doRemoveRaw(ci, sid, cnt) {
  await fetch('/api/chest/' + ci + '/remove', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({stuffId:sid, count:cnt})
  });
}

// 原始添加（不刷新UI）
async function doAddRaw(ci, sid, cnt) {
  const r = await fetch('/api/chest/' + ci + '/add', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({stuffId:sid, count:cnt})
  });
  const d = await r.json();
  if (d.error) { toast(d.error, true); return; }
  chests[ci] = d;
  renderSidebar();
  renderContent();
  toast('设置成功');
}

async function doRemove(ci, sid, cnt) {
  try {
    const r = await fetch('/api/chest/' + ci + '/remove', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:sid, count:cnt})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    chests[ci] = d;
    renderSidebar();
    renderContent();
    toast((cnt===1?'-1 ':'-All ') + '成功');
  } catch(e) { toast('操作失败', true); }
}

function openAddModal(ci) {
  addChestIndex = ci;
  document.getElementById('addTitle').textContent = '向 [' + chests[ci].name + '] 添加物品';
  document.getElementById('addCount').value = 1;
  document.getElementById('addSearch').value = '';
  document.getElementById('addModal').classList.add('show');
  renderAddList();
}

function closeAddModal() {
  document.getElementById('addModal').classList.remove('show');
}

function renderAddList() {
  const q = document.getElementById('addSearch').value.toLowerCase();
  const el = document.getElementById('addList');
  let html = '';
  for (const it of items) {
    if (q && !it.name.toLowerCase().includes(q)) continue;
    html += '<div class=""modal-item"" onclick=""doAdd(' + it.stuffId + ')"">';
    html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
    html += '<span class=""mi-name"">' + esc(it.name) + '</span>';
    html += '<span class=""mi-id"">ID:' + it.stuffId + '</span>';
    html += '</div>';
  }
  el.innerHTML = html;
}

async function doAdd(sid) {
  const cnt = parseInt(document.getElementById('addCount').value) || 1;
  try {
    const r = await fetch('/api/chest/' + addChestIndex + '/add', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:sid, count:cnt})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    chests[addChestIndex] = d;
    renderSidebar();
    renderContent();
    toast('添加成功');
  } catch(e) { toast('操作失败', true); }
}

function addN(n) {
  document.getElementById('addCount').value = n;
}

// ===== 计划库存操作 =====
async function adjPlan(ci, sid, cnt) {
  try {
    const r = await fetch('/api/chest/' + ci + '/plan', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:sid, count:cnt})
    });
    await r.json();
    await fetchChests();
    renderSidebar();
    renderContent();
    toast(cnt <= 0 ? '已删除' : '已更新');
  } catch(e) { toast('操作失败', true); }
}

async function doRemovePlan(ci, sid) {
  await adjPlan(ci, sid, 0);
}

function openPlanModal(ci) {
  addChestIndex = ci;
  document.getElementById('planTitle').textContent = '向 [' + chests[ci].name + '] 添加计划库存';
  document.getElementById('planCount').value = 1;
  document.getElementById('planSearch').value = '';
  document.getElementById('planModal').classList.add('show');
  renderPlanList();
}

function closePlanModal() {
  document.getElementById('planModal').classList.remove('show');
}

function renderPlanList() {
  const q = document.getElementById('planSearch').value.toLowerCase();
  const el = document.getElementById('planList');
  let html = '';
  for (const it of items) {
    if (q && !it.name.toLowerCase().includes(q)) continue;
    html += '<div class=""modal-item"" onclick=""doAddPlan(' + it.stuffId + ')"">';
    html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
    html += '<span class=""mi-name"">' + esc(it.name) + '</span>';
    html += '<span class=""mi-id"">ID:' + it.stuffId + '</span>';
    html += '</div>';
  }
  el.innerHTML = html;
}

async function doAddPlan(sid) {
  const cnt = parseInt(document.getElementById('planCount').value) || 1;
  await adjPlan(addChestIndex, sid, cnt);
  closePlanModal();
}

function planN(n) {
  document.getElementById('planCount').value = n;
}

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

function esc(s) { return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""""/g,'&quot;'); }
function hideImg(el) { el.style.display='none'; }

// 怪物名称映射（从后端API获取）
let monsterNameMap = {};

async function fetchMonsterNames() {
  try {
    const r = await fetch('/api/monster/names');
    monsterNameMap = await r.json();
  } catch(e) {}
}

function getMonsterName(stuffId) {
  return monsterNameMap[stuffId] || '未知怪物(' + stuffId + ')';
}

// ===== NPC 面板 =====
function renderNpcExplorePanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x1F50D; NPC 实体探索</h2>';
  html += '<p style=""color:var(--text-secondary);margin-bottom:20px;font-size:13px"">扫描游戏中的所有 GameObject，查找 NPC 相关实体并输出其组件和字段信息。扫描结果将输出到 BepInEx 控制台日志。</p>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px"">';
  html += '<button id=""npcExploreBtn"" onclick=""npcExplore()"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">开始探索扫描</button>';
  html += '<button id=""npcScanBtn"" onclick=""npcScanCombat()"" style=""padding:10px 20px;background:var(--success);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">扫描战斗实体</button>';
  html += '</div>';

  html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:16px"">';
  html += '<h3 style=""color:var(--text-secondary);font-size:14px;margin-bottom:12px"">扫描说明</h3>';
  html += '<ul style=""color:var(--text-muted);font-size:12px;line-height:1.8;padding-left:20px"">';
  html += '<li><b>探索扫描</b>：按关键词匹配 GO（npc, character, unit, soldier, worker, enemy, mob 等），打印匹配 GO 的所有组件和字段</li>';
  html += '<li><b>战斗实体扫描</b>：扫描所有含 hp_total/atk_min/atk_max 字段的组件，汇总打印 GO 名和组件类名</li>';
  html += '<li>扫描结果保存在游戏目录的 <code>BepInEx/LogOutput.log</code> 文件中</li>';
  html += '<li>关键词包括：npc, character, unit, soldier, worker, enemy, mob, merchant, trader, guard, villager, creature, animal, monster, boss, ally, friendly, hostile, human</li>';
  html += '</ul></div>';

  if (npcScanResult) {
    html += '<div style=""margin-top:16px;padding:12px;background:var(--bg-input);border:1px solid var(--border);border-radius:var(--radius-sm);color:var(--text-primary);font-size:13px"">' + esc(npcScanResult) + '</div>';
  }

  html += '</div>';
  el.innerHTML = html;
}

function renderNpcEntitiesPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x1F47E; 怪物实体管理</h2>';
  html += '<p style=""color:var(--text-secondary);margin-bottom:12px;font-size:13px"">查看和编辑地图上怪物的战斗属性（HP、攻击力、速度等）。</p>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px"">';
  html += '<button onclick=""fetchNpcEntities().then(()=>renderContent())"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">刷新 NPC 列表</button>';
  html += '<span style=""color:var(--text-muted);font-size:13px;display:flex;align-items:center"">' + npcEntities.length + ' 个实体</span>';
  html += '</div>';

  if (npcEntities.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x1F464;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">暂无 NPC 实体</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">点击上方按钮加载 NPC 列表</div>';
    html += '</div>';
  } else {
    html += '<div style=""display:flex;flex-direction:column;gap:12px;max-height:calc(100vh - 200px);overflow-y:auto;padding-right:8px"">';
    for (const npc of npcEntities) {
      const goName = npc.goName || 'unknown';
      const stuffId = npc.stuff_id || 0;
      const guid = npc.guid || 0;
      const hp = npc.hp || 0;
      const hpTotal = npc.hp_total || 0;
      const atkMin = npc.atk_min || 0;
      const atkMax = npc.atk_max || 0;
      const speed = npc.speed || 0;
      const monsterName = getMonsterName(stuffId);

      html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:16px"">';
      html += '<div style=""display:flex;justify-content:space-between;align-items:center;margin-bottom:12px"">';
      html += '<div><span style=""font-weight:600;color:var(--text-primary)"">' + esc(monsterName) + '</span><span style=""font-size:11px;color:var(--text-muted);margin-left:8px"">' + esc(goName) + '</span></div>';
      html += '<div style=""font-size:12px;color:var(--text-muted)"">GUID: ' + guid + ' | ID: ' + stuffId + '</div>';
      html += '</div>';

      html += '<div style=""display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:12px"">';

      // HP
      html += '<div style=""background:var(--bg-input);padding:8px 12px;border-radius:var(--radius-sm)"">';
      html += '<div style=""font-size:11px;color:var(--text-muted);margin-bottom:4px"">HP / HP Total</div>';
      html += '<div style=""display:flex;gap:6px;align-items:center"">';
      html += '<input id=""npc_hp_' + guid + '"" type=""number"" value=""' + hp + '"" style=""width:70px;padding:4px;background:var(--bg-card);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
      html += '<span style=""color:var(--text-muted)"">/</span>';
      html += '<input id=""npc_hp_total_' + guid + '"" type=""number"" value=""' + hpTotal + '"" style=""width:70px;padding:4px;background:var(--bg-card);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
      html += '<button onclick=""setNpcField(' + guid + ',\'hp\');setNpcField(' + guid + ',\'hp_total\')"" style=""padding:4px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">设置</button>';
      html += '</div></div>';

      // ATK
      html += '<div style=""background:var(--bg-input);padding:8px 12px;border-radius:var(--radius-sm)"">';
      html += '<div style=""font-size:11px;color:var(--text-muted);margin-bottom:4px"">ATK Min / Max</div>';
      html += '<div style=""display:flex;gap:6px;align-items:center"">';
      html += '<input id=""npc_atk_min_' + guid + '"" type=""number"" value=""' + atkMin + '"" style=""width:70px;padding:4px;background:var(--bg-card);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
      html += '<span style=""color:var(--text-muted)"">-</span>';
      html += '<input id=""npc_atk_max_' + guid + '"" type=""number"" value=""' + atkMax + '"" style=""width:70px;padding:4px;background:var(--bg-card);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
      html += '<button onclick=""setNpcField(' + guid + ',\'atk_min\');setNpcField(' + guid + ',\'atk_max\')"" style=""padding:4px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">设置</button>';
      html += '</div></div>';

      // Speed
      html += '<div style=""background:var(--bg-input);padding:8px 12px;border-radius:var(--radius-sm)"">';
      html += '<div style=""font-size:11px;color:var(--text-muted);margin-bottom:4px"">Speed</div>';
      html += '<div style=""display:flex;gap:6px;align-items:center"">';
      html += '<input id=""npc_speed_' + guid + '"" type=""number"" value=""' + speed + '"" step=""0.1"" style=""width:80px;padding:4px;background:var(--bg-card);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
      html += '<button onclick=""setNpcField(' + guid + ',\'speed\')"" style=""padding:4px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">设置</button>';
      html += '</div></div>';

      html += '</div>'; // grid end
      html += '</div>'; // card end
    }
    html += '</div>'; // list end
  }

  html += '</div>';
  el.innerHTML = html;
}

async function init() {
  document.getElementById('btnRefresh').addEventListener('click', refreshChests);
  document.getElementById('btnAuto').addEventListener('click', toggleAuto);
  document.getElementById('btnAuto').className = 'on';
  await fetchItems();
  await fetchDragonItems();
  await fetchDragonTypes();
  await fetchDragonSouls();
  await fetchDragonEntities();
  await fetchFilters();
  await fetchMonsterNames();
  await fetchFactionEntities();
  await fetchEntityScanData();
  await fetchNpcFinderData();
  await refreshChests();
  renderSidebar();
  renderContent();
  setInterval(async () => {
    if (!autoRefresh) return;
    await fetchChests();
    await fetchDragonItems();
    await fetchDragonSouls();
    if (!dragonView) renderSidebar();
  }, 3000);
}

init();
</script>
</body>
</html>";
}
