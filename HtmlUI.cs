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

.tech-card {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  width: 88px;
  padding: 8px 4px 6px;
  border-radius: 8px;
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
  text-align: center;
  position: relative;
  vertical-align: top;
}
.tech-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.4);
}
.tech-card .tc-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  background: var(--bg-input);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  margin-bottom: 4px;
  border: 2px solid var(--border);
}
.tech-card .tc-icon img {
  width: 44px;
  height: 44px;
  image-rendering: pixelated;
}
.tech-card .tc-name {
  font-size: 10px;
  line-height: 1.3;
  max-height: 2.6em;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  word-break: break-all;
}
.tech-card .tc-cost {
  font-size: 9px;
  opacity: 0.6;
  margin-top: 2px;
}
.tech-card .tc-badge {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 8px;
  padding: 1px 4px;
  border-radius: 6px;
  font-weight: 600;
}
.tech-card .tc-tip {
  position: absolute;
  bottom: 2px;
  left: 2px;
  right: 2px;
  font-size: 8px;
  color: var(--warning);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
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
let npcPanelOpen = false;
let npcListData = [];
let techTreeOpen = false;
let techTreeData = null;
let npcView = ''; // 'explore' | 'entities' | ''
let entityEditorData = [];
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

  // NPC 分类（与容器同级）
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleNpcPanel()"">';
  html += '<span class=""arrow' + (npcPanelOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>NPC</span>';
  html += '<span style=""margin-left:auto;font-size:11px;color:var(--text-muted)"">' + npcListData.length + '</span>';
  html += '</div>';
  html += '<div class=""category-items' + (npcPanelOpen ? ' open' : '') + '"">';
  html += '<div class=""chest-item' + (npcPanelOpen ? ' active' : '') + '"" onclick=""openNpcPanel()"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F464;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">我方 NPC</div>';
  html += '<div class=""ci-count"">' + (npcListData.length > 0 ? npcListData.length + ' 个' : '点击扫描') + '</div>';
  html += '</div></div></div></div>';

  // 怪物总分类
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleNpcMain()"">';
  html += '<span class=""arrow' + (npcMainOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>实体扫描</span>';
  html += '</div>';
  html += '<div class=""category-items' + (npcMainOpen ? ' open' : '') + '"">';

  // 修改
  html += '<div class=""chest-item' + (npcView === 'editor' ? ' active' : '') + '"" onclick=""selectNpcView(\'editor\')"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x270F;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">修改</div>';
  html += '<div class=""ci-count"">' + (entityEditorData.length > 0 ? entityEditorData.length + ' 个实体' : '0 个') + '</div>';
  html += '</div></div>';

  html += '</div></div>';

  // 科技树
  html += '<div class=""category"">';
  html += '<div class=""category-header"" onclick=""toggleTechTree()"">';
  html += '<span class=""arrow' + (techTreeOpen ? ' open' : '') + '"">&#9654;</span>';
  html += '<span>科技树</span>';
  html += '</div>';
  html += '<div class=""category-items' + (techTreeOpen ? ' open' : '') + '"">';
  html += '<div class=""chest-item' + (techTreeOpen ? ' active' : '') + '"" onclick=""openTechTree()"">';
  html += '<div class=""ci-icon"" style=""font-size:20px;display:flex;align-items:center;justify-content:center"">&#x1F333;</div>';
  html += '<div class=""ci-info"">';
  html += '<div class=""ci-name"">查看科技树</div>';
  html += '<div class=""ci-count"">点击查看/修改</div>';
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
  el.innerHTML = '<div style=""padding:40px;text-align:center;color:var(--text-muted)"">扫描我方 NPC 中...</div>';
  try {
    const r = await fetch('/api/npc/scan', {method:'POST'});
    npcListData = await r.json();
    renderSidebar();
    renderNpcPanel();
  } catch(e) {
    el.innerHTML = '<div style=""padding:40px;text-align:center;color:var(--danger)"">扫描失败: ' + esc(e.message) + '</div>';
  }
}

function renderNpcPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px;height:100%;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden"">';
  html += '<div style=""display:flex;align-items:center;gap:12px;margin-bottom:16px;flex-shrink:0"">';
  html += '<h2 style=""color:var(--accent-light);margin:0;font-size:18px"">&#x1F464; 我方 NPC</h2>';
  html += '<span style=""color:var(--text-muted);font-size:13px"">' + npcListData.length + ' 个</span>';
  html += '<button onclick=""openNpcPanel()"" style=""padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;margin-left:auto"">重新扫描</button>';
  html += '</div>';

  if (npcListData.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x1F464;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">未发现我方 NPC</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">确保游戏已加载存档且有己方 NPC 存在</div>';
    html += '</div>';
  } else {
    const grp={soldiers:[],children:[],prisoners:[],workers:[],nobles:[],lords:[],misc:[],outsiders:[],army:[],laborers:[]};
    for (const npc of npcListData) {
      const t = npc.npcType || 0;
      if (t===1901||t===9305||t===9307||t===9310||t===9311||t===9312||t===9313) grp.soldiers.push(npc);
      else if ((t>=-3&&t<=-1)||(t>=9001&&t<=9199)) grp.children.push(npc);
      else if (t===25) grp.prisoners.push(npc);
      else if ((t>=1&&t<=22)||t===24||(t>=26&&t<=29)||(t>=31&&t<=49)||t===9301||t===9302||t===9303||t===9304||t===9306||t===9308||t===9309) grp.workers.push(npc);
      else if (t===61) grp.nobles.push(npc);
      else if (t===70) grp.lords.push(npc);
      else if (t===23||t===30) grp.misc.push(npc);
      else if (t>=-15&&t<=-5) grp.outsiders.push(npc);
      else if (t===1001) grp.army.push(npc);
      else if (t===0||(t>=9201&&t<=9299)) grp.laborers.push(npc);
      else grp.laborers.push(npc);
    }
    const groups = [
      {label:'士兵', icon:'&#x2694;', color:'#e74c3c', items:grp.soldiers},
      {label:'工人', icon:'&#x1F527;', color:'#f39c12', items:grp.workers},
      {label:'杂工', icon:'&#x1F6E0;', color:'#95a5a6', items:grp.laborers},
      {label:'儿童', icon:'&#x1F476;', color:'#e91e63', items:grp.children},
      {label:'外来者', icon:'&#x1F464;', color:'#9b59b6', items:grp.outsiders},
      {label:'贵族', icon:'&#x1F451;', color:'#f1c40f', items:grp.nobles},
      {label:'领主', icon:'&#x1F3F0;', color:'#e67e22', items:grp.lords},
      {label:'俘虏', icon:'&#x1F512;', color:'#7f8c8d', items:grp.prisoners},
      {label:'石头人和小精灵', icon:'&#x1F47E;', color:'#1abc9c', items:grp.misc}
    ];
    html += '<div style=""flex:1;overflow-y:auto;min-height:0"">';
    for (const g of groups) {
      if (g.items.length === 0) continue;
      html += '<details style=""margin-bottom:8px"">';
      html += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
      html += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:' + g.color + ';color:#fff"">' + g.icon + ' ' + g.label + '</span>';
      html += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + g.items.length + ' 个</span>';
      html += '</summary>';
      html += '<div style=""padding:6px 0"">';
      for (const npc of g.items) {
        html += '<div style=""margin-bottom:6px"">' + renderNpcCard(npc) + '</div>';
      }
      html += '</div></details>';
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

var NPC_TYPES={""-15"":""外乡人"",""-14"":""流浪者"",""-13"":""赏金猎人"",""-12"":""刺客"",""-11"":""寻宝者"",""-10"":""旅客"",""-9"":""商人头领"",""-7"":""商人"",""-5"":""流民"",""-3"":""婴儿"",""-2"":""儿童"",""-1"":""学生"",""0"":""杂工"",""1"":""建筑工"",""2"":""铁匠"",""29"":""冶炼工"",""3"":""酿造师"",""4"":""农民"",""5"":""裁缝"",""6"":""牧民"",""7"":""搬运工"",""8"":""采集者"",""10"":""渔民"",""11"":""矿工"",""12"":""猎人"",""13"":""石匠"",""14"":""劈柴工"",""24"":""蜂农"",""15"":""学者"",""16"":""护林人"",""17"":""医生"",""18"":""采药师"",""28"":""炼药师"",""19"":""先知"",""20"":""马车夫"",""22"":""厨师"",""27"":""服务员"",""23"":""小精灵"",""25"":""俘虏"",""26"":""水手"",""30"":""石头人"",""1001"":""士兵"",""1901"":""敌兵"",""61"":""贵族"",""70"":""领主"",""9001"":""蚁人儿童"",""9002"":""鼠人儿童"",""9003"":""猫人儿童"",""9004"":""羊人儿童"",""9005"":""狼人儿童"",""9006"":""猪人儿童"",""9007"":""精灵族儿童"",""9008"":""三眼人儿童"",""9009"":""蜥蜴人儿童"",""9101"":""三眼人学生"",""9102"":""蜥蜴人学生"",""9201"":""蚁人杂工"",""9202"":""鼠人杂工"",""9203"":""猫人杂工"",""9204"":""羊人杂工"",""9205"":""狼人成人"",""9206"":""猪人杂工"",""9207"":""精灵族成人"",""9208"":""三眼人成人"",""9209"":""蜥蜴人成人"",""9301"":""蚁人搬运工"",""9302"":""鼠人矿工"",""9303"":""猫人渔民"",""9304"":""羊人牧民"",""9305"":""狼人士兵"",""9306"":""猪人农民"",""9307"":""精灵法师"",""9308"":""三眼人先知"",""9309"":""蜥蜴人学者"",""9310"":""敌人狼人士兵"",""9311"":""敌人精灵法师"",""9312"":""三眼法师"",""9313"":""敌人三眼法师""};
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

  h += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius)"">';

  // 头部：名称 + 兵种 + NPC类型
  h += '<div style=""display:flex;align-items:center;gap:8px;padding:10px 14px;border-bottom:1px solid var(--border)"">';
  h += '<span style=""font-weight:600;color:var(--text-primary);font-size:14px"">' + esc(displayName) + '</span>';
  if (npcTypeName) h += '<span style=""font-size:11px;padding:1px 8px;border-radius:8px;background:var(--warning,#e67e22);color:#fff"">' + esc(npcTypeName) + '</span>';
  if (soldierType) h += '<span style=""font-size:11px;padding:1px 8px;border-radius:8px;background:var(--accent);color:#fff"">' + esc(soldierType) + '</span>';
  h += '<span style=""font-size:11px;color:var(--text-muted);margin-left:auto"">GUID:' + npc.guid + '</span>';
  h += '<button onclick=""event.stopPropagation();locateEditorEntity(' + ptrHash + ')"" style=""padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">定位</button>';
  h += '</div>';

  // 勾选字段显示区域
  h += '<div id=""npc-checked-' + ptrHash + '""></div>';

  // 所有字段折叠（懒加载）
  h += '<details style=""border-top:1px solid var(--border)"" ontoggle=""loadNpcFields(this,' + ptrHash + ')"">';
  h += '<summary style=""cursor:pointer;padding:8px 14px;font-size:12px;color:var(--text-muted);user-select:none"">所有字段 (' + fieldCount + ')</summary>';
  h += '<div id=""npc-fields-' + ptrHash + '"" style=""padding:6px 14px 10px;color:var(--text-muted);font-size:12px"">点击展开加载...</div>';
  h += '</details>';

  h += '</div>';
  return h;
}

var _fieldTranslations = null;
var _npcFieldCache = {};
async function loadFieldTranslations() {
  if (_fieldTranslations !== null) return _fieldTranslations;
  try {
    const r = await fetch('/api/npc/translations?t=' + Date.now());
    _fieldTranslations = await r.json();
  } catch(e) { _fieldTranslations = {}; }
  return _fieldTranslations;
}

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
      container.innerHTML = '<span style=""color:var(--danger)"">实体已失效 (ptrHash: ' + ptrHash + ')，请<a href=""javascript:void(0)"" onclick=""openNpcPanel()"" style=""color:var(--accent)"">重新扫描</a></span>';
      return;
    }
    const checked = new Set(getCheckedFields(ptrHash));
    const numKeys = allKeys.filter(k => !fields[k].isString);
    const strKeys = allKeys.filter(k => fields[k].isString);
    const totalCount = numKeys.length + strKeys.length;

    let h = '';
    // 搜索框
    h += '<div style=""margin-bottom:8px"">';
    h += '<input id=""npc-search-' + ptrHash + '"" type=""text"" placeholder=""搜索字段..."" oninput=""filterNpcTable(this)"" ';
    h += 'style=""width:100%;padding:5px 8px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px;outline:none"" />';
    h += '</div>';
    // 全选/取消
    h += '<div style=""display:flex;gap:8px;margin-bottom:6px;font-size:11px"">';
    h += '<a href=""javascript:void(0)"" onclick=""toggleAllNpcCheckboxes(' + ptrHash + ',true)"" style=""color:var(--accent)"">全选</a>';
    h += '<a href=""javascript:void(0)"" onclick=""toggleAllNpcCheckboxes(' + ptrHash + ',false)"" style=""color:var(--text-muted)"">取消全选</a>';
    h += '</div>';
    // 表格
    h += '<table class=""npc-fields-table"" style=""width:100%;border-collapse:collapse;font-size:12px"">';
    h += '<thead><tr style=""border-bottom:1px solid var(--border)"">';
    h += '<th style=""width:30px;padding:4px 6px;text-align:center""><input type=""checkbox"" id=""npc-checkall-' + ptrHash + '"" onchange=""toggleAllNpcCheckboxes(' + ptrHash + ',this.checked)"" /></th>';
    h += '<th style=""padding:4px 8px;text-align:left;min-width:120px"">字段</th>';
    h += '<th style=""padding:4px 8px;text-align:left;min-width:80px"">翻译</th>';
    h += '<th style=""padding:4px 8px;text-align:right;min-width:80px"">数值</th>';
    h += '<th style=""width:50px;padding:4px 6px;text-align:center"">操作</th>';
    h += '</tr></thead><tbody>';

    // 数值字段
    for (const key of numKeys) {
      const f = fields[key];
      const isFloat = f.isFloat;
      const displayVal = (typeof f.value === 'number') ? (isFloat ? f.value.toFixed(2) : f.value) : (f.value || 0);
      const trans = translations[key] || '';
      const isChecked = checked.has(key);
      const inpId = 'npc_inp_' + ptrHash + '_' + key;
      h += '<tr class=""npc-field-row"" data-key=""' + esc(key).toLowerCase() + '"">';
      h += '<td style=""text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)""><input type=""checkbox"" class=""npc-field-cb"" data-ptr=""' + ptrHash + '"" data-key=""' + esc(key) + '"" ' + (isChecked ? 'checked' : '') + ' onchange=""onNpcCheckChange(this)"" /></td>';
      h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)"">' + esc(key) + '</td>';
      h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)"">' + esc(trans) + '</td>';
      h += '<td style=""padding:3px 6px;border-bottom:1px solid var(--border);text-align:right"">';
      h += '<input id=""' + inpId + '"" type=""number"" step=""' + (isFloat ? '0.1' : '1') + '"" value=""' + displayVal + '"" ';
      h += 'style=""width:80px;background:transparent;border:1px solid var(--border);border-radius:3px;color:var(--text-primary);font-size:12px;text-align:right;padding:2px 4px;outline:none"" onfocus=""this.select()"" />';
      h += '</td>';
      h += '<td style=""text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)"">';
      h += '<button onclick=""setNpcField(' + ptrHash + ',\'' + esc(key) + '\',document.getElementById(\'' + inpId + '\').value,' + (isFloat ? 'true' : 'false') + ')"" style=""padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:3px;cursor:pointer;font-size:11px"">OK</button>';
      h += '</td></tr>';
    }
    // 字符串字段
    for (const key of strKeys) {
      const f = fields[key];
      const trans = translations[key] || '';
      const isChecked = checked.has(key);
      h += '<tr class=""npc-field-row"" data-key=""' + esc(key).toLowerCase() + '"">';
      h += '<td style=""text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)""><input type=""checkbox"" class=""npc-field-cb"" data-ptr=""' + ptrHash + '"" data-key=""' + esc(key) + '"" ' + (isChecked ? 'checked' : '') + ' onchange=""onNpcCheckChange(this)"" /></td>';
      h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)"">' + esc(key) + '</td>';
      h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)"">' + esc(trans) + '</td>';
      h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted);text-align:right"">' + esc(String(f.value || '')) + '</td>';
      h += '<td style=""padding:3px 6px;border-bottom:1px solid var(--border)""></td>';
      h += '</tr>';
    }
    h += '</tbody></table>';

    container.innerHTML = h;
    // 更新 check-all 状态
    updateCheckAllState(ptrHash);
  } catch(e) {
    container.innerHTML = '<span style=""color:var(--danger)"">加载失败: ' + esc(String(e)) + '</span>';
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
  let h = '<table style=""width:100%;border-collapse:collapse;font-size:12px"">';
  h += '<thead><tr style=""border-bottom:1px solid var(--border)"">';
  h += '<th style=""padding:3px 8px;text-align:left;min-width:100px"">字段</th>';
  h += '<th style=""padding:3px 8px;text-align:left;min-width:60px"">翻译</th>';
  h += '<th style=""padding:3px 8px;text-align:right;min-width:70px"">数值</th>';
  h += '<th style=""width:50px;padding:3px 6px;text-align:center"">操作</th>';
  h += '</tr></thead><tbody>';
  for (const key of checked) {
    const f = fields ? fields[key] : null;
    const isFloat = f && f.isFloat;
    const val = f ? f.value : 0;
    const displayVal = (typeof val === 'number') ? (isFloat ? val.toFixed(2) : val) : (val || 0);
    const translation = trans[key] || '';
    const inpId = 'npc_chkd_' + ptrHash + '_' + key;
    h += '<tr>';
    h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-primary)"">' + esc(key) + '</td>';
    h += '<td style=""padding:3px 8px;border-bottom:1px solid var(--border);color:var(--text-muted)"">' + esc(translation) + '</td>';
    h += '<td style=""padding:3px 6px;border-bottom:1px solid var(--border);text-align:right"">';
    h += '<input id=""' + inpId + '"" type=""number"" step=""' + (isFloat ? '0.1' : '1') + '"" value=""' + displayVal + '"" ';
    h += 'style=""width:80px;background:transparent;border:1px solid var(--border);border-radius:3px;color:var(--text-primary);font-size:12px;text-align:right;padding:2px 4px;outline:none"" onfocus=""this.select()"" />';
    h += '</td>';
    h += '<td style=""text-align:center;padding:3px 6px;border-bottom:1px solid var(--border)"">';
    h += '<button onclick=""setNpcCheckedField(' + ptrHash + ',\'' + esc(key) + '\',document.getElementById(\'' + inpId + '\').value,' + (isFloat ? 'true' : 'false') + ')"" style=""padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:3px;cursor:pointer;font-size:11px"">OK</button>';
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

var TECH_TREE=[{""i"":902022,""d"":0,""p"":0,""s"":0},{""i"":902035,""d"":902022,""p"":100,""s"":0},{""i"":902024,""d"":902035,""p"":200,""s"":30},{""i"":902034,""d"":902022,""p"":100,""s"":0},{""i"":902023,""d"":902034,""p"":200,""s"":30},{""i"":902037,""d"":902034,""p"":60,""s"":0},{""i"":902038,""d"":902037,""p"":200,""s"":30},{""i"":902084,""d"":902038,""p"":200,""s"":50},{""i"":902036,""d"":902022,""p"":40,""s"":0},{""i"":902160,""d"":902036,""p"":200,""s"":50},{""i"":902017,""d"":902036,""p"":200,""s"":0},{""i"":902020,""d"":902017,""p"":200,""s"":50},{""i"":902018,""d"":902036,""p"":200,""s"":0},{""i"":902109,""d"":902018,""p"":200,""s"":50},{""i"":902019,""d"":902036,""p"":200,""s"":50},{""i"":902021,""d"":902019,""p"":200,""s"":50},{""i"":902012,""d"":902036,""p"":100,""s"":0},{""i"":902039,""d"":902012,""p"":200,""s"":30},{""i"":902116,""d"":902039,""p"":200,""s"":50},{""i"":902013,""d"":0,""p"":100,""s"":0},{""i"":902005,""d"":0,""p"":100,""s"":0,""t"":""完成主线剧情过程中解锁""},{""i"":902004,""d"":902013,""p"":100,""s"":0},{""i"":902007,""d"":902013,""p"":100,""s"":0},{""i"":902156,""d"":902007,""p"":200,""s"":500},{""i"":902113,""d"":902013,""p"":100,""s"":100},{""i"":902154,""d"":902113,""p"":100,""s"":200},{""i"":902155,""d"":902154,""p"":100,""s"":300},{""i"":902153,""d"":902155,""p"":100,""s"":0},{""i"":902130,""d"":902013,""p"":100,""s"":0},{""i"":902131,""d"":902130,""p"":200,""s"":1000},{""i"":902010,""d"":902013,""p"":100,""s"":0},{""i"":902011,""d"":902010,""p"":200,""s"":50},{""i"":902157,""d"":902011,""p"":200,""s"":100},{""i"":902158,""d"":902157,""p"":200,""s"":100},{""i"":902159,""d"":902158,""p"":200,""s"":200},{""i"":902147,""d"":902011,""p"":1000,""s"":2000},{""i"":902003,""d"":902013,""p"":100,""s"":0},{""i"":902099,""d"":902005,""p"":500,""s"":0},{""i"":902140,""d"":902099,""p"":500,""s"":1000},{""i"":902065,""d"":902005,""p"":300,""s"":0},{""i"":902066,""d"":902065,""p"":300,""s"":100},{""i"":902144,""d"":902005,""p"":100,""s"":0},{""i"":902061,""d"":902004,""p"":100,""s"":0},{""i"":902152,""d"":902061,""p"":1000,""s"":2000},{""i"":902075,""d"":902003,""p"":50,""s"":0},{""i"":902104,""d"":902003,""p"":500,""s"":1000},{""i"":902122,""d"":902104,""p"":500,""s"":2000},{""i"":902135,""d"":902003,""p"":500,""s"":100},{""i"":902136,""d"":902135,""p"":500,""s"":200},{""i"":902137,""d"":902136,""p"":500,""s"":300},{""i"":902138,""d"":902137,""p"":500,""s"":400},{""i"":902139,""d"":902138,""p"":500,""s"":500},{""i"":902002,""d"":902003,""p"":300,""s"":0},{""i"":902091,""d"":902002,""p"":300,""s"":100},{""i"":902014,""d"":902003,""p"":300,""s"":100},{""i"":902015,""d"":902003,""p"":100,""s"":100},{""i"":902102,""d"":902014,""p"":500,""s"":1000},{""i"":902063,""d"":902003,""p"":200,""s"":0},{""i"":902064,""d"":902063,""p"":300,""s"":0},{""i"":902009,""d"":0,""p"":100,""s"":0,""t"":""完成主线剧情过程中解锁""},{""i"":902161,""d"":902009,""p"":100,""s"":100},{""i"":902008,""d"":902009,""p"":400,""s"":50},{""i"":902112,""d"":902008,""p"":500,""s"":100},{""i"":902114,""d"":902112,""p"":500,""s"":100},{""i"":902016,""d"":902009,""p"":400,""s"":0},{""i"":902106,""d"":902016,""p"":500,""s"":0},{""i"":902134,""d"":902016,""p"":500,""s"":0},{""i"":902141,""d"":902106,""p"":500,""s"":1000},{""i"":902032,""d"":902009,""p"":400,""s"":100},{""i"":902033,""d"":902009,""p"":400,""s"":100},{""i"":902060,""d"":902114,""p"":500,""s"":500},{""i"":902120,""d"":902032,""p"":500,""s"":500},{""i"":902150,""d"":902120,""p"":500,""s"":500},{""i"":902059,""d"":902033,""p"":500,""s"":500},{""i"":902151,""d"":902059,""p"":500,""s"":500},{""i"":902111,""d"":902009,""p"":800,""s"":0},{""i"":902082,""d"":902111,""p"":500,""s"":500},{""i"":902083,""d"":902082,""p"":1000,""s"":500},{""i"":902125,""d"":902083,""p"":2000,""s"":1000},{""i"":902146,""d"":902125,""p"":2000,""s"":1000},{""i"":902126,""d"":902083,""p"":2000,""s"":1000},{""i"":902124,""d"":902082,""p"":1000,""s"":500},{""i"":902077,""d"":902009,""p"":500,""s"":0},{""i"":902117,""d"":902077,""p"":100,""s"":0},{""i"":902078,""d"":902077,""p"":1000,""s"":0},{""i"":902080,""d"":902077,""p"":500,""s"":0},{""i"":902110,""d"":902080,""p"":800,""s"":0},{""i"":902149,""d"":902110,""p"":500,""s"":500},{""i"":902081,""d"":902078,""p"":1000,""s"":0},{""i"":902025,""d"":"""",""p"":100,""s"":0},{""i"":902090,""d"":902025,""p"":500,""s"":200},{""i"":902143,""d"":902090,""p"":200,""s"":500},{""i"":902074,""d"":902025,""p"":200,""s"":0},{""i"":902115,""d"":902074,""p"":100,""s"":100},{""i"":902142,""d"":902115,""p"":500,""s"":1000},{""i"":902133,""d"":902142,""p"":9999,""s"":0,""t"":""完成主线剧情过程中解锁""},{""i"":902068,""d"":902046,""p"":5000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902069,""d"":902047,""p"":5000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902070,""d"":902048,""p"":5000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902071,""d"":902068,""p"":8000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902072,""d"":902069,""p"":8000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902073,""d"":902070,""p"":8000,""s"":0,""t"":""据说藏在某个神秘的地方""},{""i"":902052,""d"":902025,""p"":100,""s"":100},{""i"":902053,""d"":902052,""p"":200,""s"":200},{""i"":902057,""d"":902053,""p"":200,""s"":500},{""i"":902127,""d"":902053,""p"":8000,""s"":5000},{""i"":902055,""d"":902052,""p"":200,""s"":200},{""i"":902056,""d"":902055,""p"":200,""s"":300},{""i"":902128,""d"":902055,""p"":200,""s"":500},{""i"":902108,""d"":902052,""p"":200,""s"":200},{""i"":902103,""d"":902052,""p"":1000,""s"":1000},{""i"":902129,""d"":902103,""p"":1000,""s"":2000},{""i"":902092,""d"":902025,""p"":1000,""s"":1000},{""i"":902097,""d"":902092,""p"":500,""s"":500},{""i"":902123,""d"":902092,""p"":1000,""s"":1000},{""i"":902098,""d"":902097,""p"":1000,""s"":1000},{""i"":902100,""d"":902098,""p"":2000,""s"":3000},{""i"":902094,""d"":902025,""p"":500,""s"":1000},{""i"":902093,""d"":902094,""p"":1000,""s"":2000},{""i"":902095,""d"":902093,""p"":1000,""s"":3000},{""i"":902049,""d"":902025,""p"":200,""s"":0},{""i"":902050,""d"":902025,""p"":200,""s"":0},{""i"":902051,""d"":902025,""p"":200,""s"":0},{""i"":902026,""d"":902049,""p"":200,""s"":100},{""i"":902027,""d"":902050,""p"":200,""s"":100},{""i"":902028,""d"":902051,""p"":200,""s"":100},{""i"":902029,""d"":902026,""p"":300,""s"":300},{""i"":902030,""d"":902027,""p"":300,""s"":300},{""i"":902031,""d"":902028,""p"":300,""s"":300},{""i"":902040,""d"":902029,""p"":400,""s"":500},{""i"":902041,""d"":902030,""p"":400,""s"":500},{""i"":902042,""d"":902031,""p"":400,""s"":500},{""i"":902043,""d"":902040,""p"":1000,""s"":800},{""i"":902044,""d"":902041,""p"":1000,""s"":800},{""i"":902045,""d"":902042,""p"":1000,""s"":800},{""i"":902046,""d"":902043,""p"":1000,""s"":1000},{""i"":902047,""d"":902044,""p"":1000,""s"":1000},{""i"":902048,""d"":902045,""p"":1000,""s"":1000},{""i"":902132,""d"":"""",""p"":0,""s"":0},{""i"":902096,""d"":902132,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902101,""d"":902096,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902105,""d"":902101,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902118,""d"":902105,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902119,""d"":902118,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902148,""d"":902119,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902085,""d"":902132,""p"":9999,""s"":0,""t"":""无法研究解锁，需完成主线任务“宝石的秘密”后，由王国工匠传授""},{""i"":902086,""d"":902085,""p"":9999,""s"":0,""t"":""无法研究解锁，需完成主线任务“宝石的秘密”后，由王国工匠传授""},{""i"":902087,""d"":902086,""p"":9999,""s"":0,""t"":""无法研究解锁，需完成主线任务“宝石的秘密”后，由王国工匠传授""},{""i"":902088,""d"":902087,""p"":9999,""s"":0,""t"":""无法研究解锁，需完成主线任务“宝石的秘密”后，由王国工匠传授""},{""i"":902089,""d"":902088,""p"":9999,""s"":0,""t"":""无法研究解锁，需完成主线任务“宝石的秘密”后，由王国工匠传授""},{""i"":902162,""d"":902089,""p"":9999,""s"":0,""t"":""在世界某个地方""},{""i"":902145,""d"":902162,""p"":9999,""s"":0,""t"":""在世界某个地方""}];
var TECH_INFO={""1"":{""d"":[""供奉雕像，触发神奇魔法""],""f"":[105024],""s"":[]},""902002"":{""d"":[""让居民可以上山采集资源""],""f"":[102004],""s"":[]},""902003"":{""d"":[""进一步提高居民移动速度"",""建造更结实的墙""],""f"":[102002,102006,102013],""s"":[]},""902004"":{""d"":[""临时存储产品，然后批量运输，节省时间""],""f"":[103008],""s"":[]},""902005"":{""d"":[""与旅行商人进行交易"",""指定商人从什么位置，进入地图""],""f"":[103004,103006],""s"":[]},""902006"":{""d"":[""布施食物，招揽流民""],""f"":[103010],""s"":[]},""902007"":{""d"":[""酿造美酒，提升居民幸福"",""取水用于酿造""],""f"":[105012,105019],""s"":[]},""902008"":{""d"":[""让居民接受教育培训，提升工作效率，增加产量""],""f"":[104002,104015],""s"":[]},""902009"":{""d"":[""先知传递信仰，可提高居民幸福度，收集信仰之力，使用魔法。""],""f"":[104004,104031],""s"":[]},""902010"":{""d"":[""为居民提供医疗诊断"",""加快居民恢复身体健康"",""采集草药""],""f"":[104007,104008,105009],""s"":[]},""902011"":{""d"":[""炼制药物，增强居民体质，延年益寿""],""f"":[105025],""s"":[]},""902012"":{""d"":[""砍伐与植树并重，实现木材的可持续产出""],""f"":[105008],""s"":[]},""902013"":{""d"":[""制造工具和武器等"",""将皮毛制作成衣服""],""f"":[105010,105011],""s"":[]},""902014"":{""d"":[""在山地边缘建造矿井，开采铁矿，煤炭，或稀有金属矿""],""f"":[105013],""s"":[]},""902015"":{""d"":[""挖掘埋在地下的石料""],""f"":[105014],""s"":[]},""902016"":{""d"":[""安葬去世居民，减轻家属悲伤"",""组织附近居民集中就餐，提供特定食物"",""举办篝火晚会，让居民更容易找到另一半""],""f"":[104005,104016,104011],""s"":[]},""902017"":{""d"":[""让农民可就近取水，提高生产效率""],""f"":[104001],""s"":[]},""902018"":{""d"":[""将动物粪便和腐败食物，转化为肥料，撒在农田中增加产量""],""f"":[105015,104021],""s"":[]},""902019"":{""d"":[],""f"":[902019],""s"":[]},""902020"":{""d"":[],""f"":[902020],""s"":[]},""902021"":{""d"":[],""f"":[902021],""s"":[]},""902022"":{""d"":[""在野外获得食物""],""f"":[105006],""s"":[]},""902023"":{""d"":[""设置兽夹，捕获路过野生动物，杀死路过的怪物""],""f"":[105017],""s"":[]},""902024"":{""d"":[""设置蟹笼，捕捞水产，收获颇丰""],""f"":[105018],""s"":[]},""902025"":{""d"":[""招募士兵，保卫家园""],""f"":[106001],""s"":[]},""902026"":{""d"":[""可制造精良武器""],""f"":[413003],""s"":[]},""902027"":{""d"":[""可制造精良盔甲""],""f"":[415003],""s"":[]},""902028"":{""d"":[""可制造精良盾牌""],""f"":[414003],""s"":[]},""902029"":{""d"":[""可制造高级武器""],""f"":[413004],""s"":[]},""902030"":{""d"":[""可制造高级盔甲""],""f"":[415004],""s"":[]},""902031"":{""d"":[""可制造高级盾牌""],""f"":[414004],""s"":[]},""902032"":{""d"":[""可让人们信仰一神教""],""f"":[902032],""s"":[901001]},""902033"":{""d"":[""可让人们信仰仁义道""],""f"":[902033],""s"":[901002]},""902034"":{""d"":[""通过狩猎，获得肉类和皮毛""],""f"":[105005],""s"":[]},""902035"":{""d"":[""到水中捕鱼，获得肉类""],""f"":[105004],""s"":[]},""902036"":{""d"":[""种植农作物"",""取水用于农业灌溉，或酒类酿造""],""f"":[105001,105019],""s"":[]},""902037"":{""d"":[""可饲养动物鸡，牛，羊，马，猪或驯狼""],""f"":[105003],""s"":[]},""902038"":{""d"":[""可饲养蜜蜂，获得高级食材：蜂蜜""],""f"":[105023],""s"":[]},""902039"":{""d"":[""养花，赏花提高幸福度。也可作为蜂蜜的蜜源""],""f"":[104018,104028,104029,104030],""s"":[]},""902040"":{""d"":[""可制造稀有武器""],""f"":[413005],""s"":[]},""902041"":{""d"":[""可制造稀有盔甲""],""f"":[415005],""s"":[]},""902042"":{""d"":[""可制造稀有盾牌""],""f"":[414005],""s"":[]},""902043"":{""d"":[""可制造特殊武器""],""f"":[413006],""s"":[]},""902044"":{""d"":[""可制造特殊盔甲""],""f"":[415006],""s"":[]},""902045"":{""d"":[""可制造特殊盾牌""],""f"":[414006],""s"":[]},""902046"":{""d"":[""可制造极品武器""],""f"":[413007],""s"":[]},""902047"":{""d"":[""可制造极品盔甲""],""f"":[415007],""s"":[]},""902048"":{""d"":[""可制造极品盾牌""],""f"":[414007],""s"":[]},""902049"":{""d"":[""可制造普通武器""],""f"":[413002],""s"":[]},""902050"":{""d"":[""可制造普通盔甲""],""f"":[415002],""s"":[]},""902051"":{""d"":[""可制造普通盾牌""],""f"":[414002],""s"":[]},""902052"":{""d"":[""可招募剑士，武术高超"",""可招募长枪兵，拥有更大的攻击范围""],""f"":[202401,202405],""s"":[]},""902053"":{""d"":[""可招募弓箭兵，远距离袭击""],""f"":[202301],""s"":[]},""902055"":{""d"":[""可招募刀盾兵，攻守兼备""],""f"":[202501],""s"":[]},""902056"":{""d"":[""可招募巨盾兵，防守一流""],""f"":[202502],""s"":[]},""902057"":{""d"":[""可招募弓骑兵，机动灵活，远距离袭击""],""f"":[202601],""s"":[]},""902059"":{""d"":[],""f"":[],""s"":[424001]},""902060"":{""d"":[],""f"":[],""s"":[424002]},""902061"":{""d"":[""批量运输，集散物资，供居民使用，减少居民跑腿时间""],""f"":[103003],""s"":[]},""902063"":{""d"":[""可将水域改造为陆地""],""f"":[105022],""s"":[]},""902064"":{""d"":[""可将陆地挖掘成坑，引水成河""],""f"":[105021],""s"":[]},""902065"":{""d"":[""在边境上，与四方的商人进行稳定的贸易往来，大规模买卖固定种类的商品""],""f"":[103005],""s"":[]},""902066"":{""d"":[""可快速大批量运输货物""],""f"":[""103012""],""s"":[]},""902068"":{""d"":[""可制造传说武器""],""f"":[413008],""s"":[]},""902069"":{""d"":[""可制造传说盔甲""],""f"":[415008],""s"":[]},""902070"":{""d"":[""可制造传说盾牌""],""f"":[414008],""s"":[]},""902071"":{""d"":[""可制造史诗武器""],""f"":[413009],""s"":[]},""902072"":{""d"":[""可制造史诗盔甲""],""f"":[415009],""s"":[]},""902073"":{""d"":[""可制造史诗盾牌""],""f"":[414009],""s"":[]},""902074"":{""d"":[""关押，审讯，劝降战斗中俘虏的敌人""],""f"":[106002],""s"":[]},""902075"":{""d"":[""可更大范围供暖""],""f"":[104014],""s"":[]},""902077"":{""d"":[""居民可随身携带食物等，提高效率""],""f"":[420001],""s"":[]},""902078"":{""d"":[],""f"":[902078],""s"":[]},""902080"":{""d"":[],""f"":[902080],""s"":[]},""902081"":{""d"":[],""f"":[902081],""s"":[]},""902082"":{""d"":[],""f"":[902082],""s"":[]},""902083"":{""d"":[],""f"":[902083],""s"":[]},""902084"":{""d"":[""可吸引和投喂怪物""],""f"":[103013],""s"":[]},""902085"":{""d"":[],""f"":[],""s"":[705001]},""902086"":{""d"":[],""f"":[],""s"":[705002]},""902087"":{""d"":[],""f"":[],""s"":[705003]},""902088"":{""d"":[],""f"":[],""s"":[705004]},""902089"":{""d"":[],""f"":[],""s"":[705005]},""902090"":{""d"":[""高大的城墙，可阻挡一切敌人，包括飞行怪"",""城墙上的门""],""f"":[""102008"",""102009""],""s"":[]},""902091"":{""d"":[""开挖山中的石料，移除山地""],""f"":[105020],""s"":[]},""902092"":{""d"":[""大范围杀伤性武器""],""f"":[106003],""s"":[423001]},""902093"":{""d"":[""有战斗力的大船""],""f"":[],""s"":[706002]},""902094"":{""d"":[""可造船"",""可进行海上航行，开展海上贸易""],""f"":[105026,105027],""s"":[706001]},""902095"":{""d"":[""战斗力强悍的巨型舰船""],""f"":[],""s"":[706003]},""902096"":{""d"":[],""f"":[111001],""s"":[]},""902097"":{""d"":[],""f"":[],""s"":[423002]},""902098"":{""d"":[],""f"":[902098],""s"":[]},""902099"":{""d"":[""招待来往旅客，赚取利润，搜集外界的消息，接受或发布赏金任务""],""f"":[104019],""s"":[]},""902100"":{""d"":[],""f"":[],""s"":[423003]},""902101"":{""d"":[],""f"":[111002],""s"":[]},""902102"":{""d"":[],""f"":[],""s"":[902102]},""902103"":{""d"":[""可招募法师，使用魔法攻击敌人"",""可招募法师，使用魔法禁锢敌人"",""可招募法师，使用魔法为士兵回血"",""可招募法师，对敌人实施精神控制""],""f"":[202701,202702,202703,202704],""s"":[]},""902104"":{""d"":[""可为蚁人，鼠人和猪人等群居种族，提供居住空间""],""f"":[101004],""s"":[]},""902105"":{""d"":[""可利用魔法，跨空间传送士兵""],""f"":[102010],""s"":[]},""902106"":{""d"":[""可举办活动宴会""],""f"":[104020],""s"":[]},""902108"":{""d"":[""可招募狼战士，使用钩爪攻击或控制敌人""],""f"":[202404],""s"":[]},""902109"":{""d"":[],""f"":[902109],""s"":[]},""902110"":{""d"":[""民兵可使用武器装备，提升战斗力""],""f"":[902110,104022],""s"":[]},""902111"":{""d"":[""可用行政手段对人口进行控制""],""f"":[902111],""s"":[]},""902112"":{""d"":[],""f"":[424007],""s"":[]},""902113"":{""d"":[""可以磨面粉，方便制作高级食物""],""f"":[105032],""s"":[]},""902114"":{""d"":[""满足有文化的居民的精神需求""],""f"":[424008],""s"":[]},""902115"":{""d"":[""方便快捷的小军营""],""f"":[106004],""s"":[]},""902116"":{""d"":[""可在室内种蘑菇""],""f"":[105030],""s"":[]},""902117"":{""d"":[""先知可接受不幸的居民倾诉，并给其心灵安慰""],""f"":[902117],""s"":[]},""902118"":{""d"":[],""f"":[""111003"",906001,906002,906003,906004,906005],""s"":[]},""902119"":{""d"":[],""f"":[""111004"",906006,906007,906008,906009,906010,906011,906012],""s"":[]},""902120"":{""d"":[],""f"":[],""s"":[424003]},""902121"":{""d"":[""上下铺，供单身居民使用，节省空间""],""f"":[101005],""s"":[]},""902122"":{""d"":[""将多余的食物，存入地下冰窖，作为应急储备""],""f"":[103009],""s"":[]},""902123"":{""d"":[],""f"":[106006],""s"":[]},""902124"":{""d"":[],""f"":[902124],""s"":[]},""902125"":{""d"":[],""f"":[902125],""s"":[]},""902126"":{""d"":[],""f"":[902126],""s"":[]},""902127"":{""d"":[""可招募贵族为火枪手，火枪类型随士兵等级变化，等级越高，杀伤力越大。""],""f"":[202304],""s"":[428001]},""902128"":{""d"":[""可招募狼骑兵，机动灵活，智勇双全""],""f"":[202102],""s"":[]},""902129"":{""d"":[""主动感知危险，以关闭城门，或召唤士兵战斗""],""f"":[106007],""s"":[]},""902130"":{""d"":[],""f"":[105028],""s"":[]},""902131"":{""d"":[],""f"":[],""s"":[603002,603003,603004]},""902132"":{""d"":[],""f"":[902132],""s"":[]},""902133"":{""d"":[],""f"":[106005,101003,101006],""s"":[]},""902134"":{""d"":[],""f"":[902134],""s"":[]},""902135"":{""d"":[],""f"":[113001],""s"":[]},""902136"":{""d"":[],""f"":[113002],""s"":[]},""902137"":{""d"":[],""f"":[113003],""s"":[]},""902138"":{""d"":[],""f"":[113004],""s"":[]},""902139"":{""d"":[],""f"":[113005],""s"":[]},""902140"":{""d"":[""可供旅客玩轮盘赌。可吸引更多旅客来访。""],""f"":[104023,104024,104025,104026],""s"":[]},""902141"":{""d"":[],""f"":[104027],""s"":[]},""902142"":{""d"":[],""f"":[106008],""s"":[]},""902143"":{""d"":[],""f"":[""102011"",102012],""s"":[]},""902144"":{""d"":[""建造国库，集中物资，用于外交或上贡，禁止居民使用""],""f"":[109005],""s"":[]},""902145"":{""d"":[],""f"":[],""s"":[705006]},""902146"":{""d"":[],""f"":[902146],""s"":[]},""902147"":{""d"":[],""f"":[],""s"":[429001,429002,429003]},""902148"":{""d"":[],""f"":[111005],""s"":[]},""902149"":{""d"":[],""f"":[],""s"":[424004]},""902150"":{""d"":[],""f"":[],""s"":[424005]},""902151"":{""d"":[],""f"":[],""s"":[424006]},""902152"":{""d"":[],""f"":[902152],""s"":[]},""902153"":{""d"":[],""f"":[],""s"":[304009,304010]},""902154"":{""d"":[],""f"":[],""s"":[305005,305006]},""902155"":{""d"":[],""f"":[],""s"":[412001,412002]},""902156"":{""d"":[],""f"":[],""s"":[421001]},""902157"":{""d"":[],""f"":[],""s"":[412003]},""902158"":{""d"":[],""f"":[],""s"":[418001]},""902159"":{""d"":[],""f"":[],""s"":[418002]},""902160"":{""d"":[""种植果树""],""f"":[105002],""s"":[]},""902161"":{""d"":[],""f"":[902161],""s"":[]},""902162"":{""d"":[],""f"":[],""s"":[705007]},""902163"":{""d"":[],""f"":[902163],""s"":[901004]}};
var TECH_ICON={108012:111005,806005:416001,806006:416002,902004:103008,902017:104001,902018:105015,902032:901001,902033:901002,902065:103005,902074:106002,902085:705001,902086:705002,902087:705003,902088:705004,902089:705005,902099:104019,902101:111002,902104:101004,902106:104020,902118:111003,902119:111004,902129:106007,902132:108009,902133:106005,902135:113001,902136:113002,902137:113003,902138:113004,902139:113005,902140:104026,902141:104027,902142:106008,902143:102011,902144:109005,902145:705006,902148:111005,902149:424004,902150:424005,902151:424006,902156:421001,902157:412003,902158:418001,902159:418002,902160:105002,902162:705007,902163:901004};
function toggleTechTree() {
  techTreeOpen = !techTreeOpen;
  renderSidebar();
}

async function openTechTree() {
  selectedChest = -1;
  dragonView = '';
  npcView = '';
  renderSidebar();
  const el = document.getElementById('content');
  el.innerHTML = '<div style=""padding:20px;color:var(--text-muted)"">加载科技树...</div>';
  try {
    const r = await fetch('/api/techtree?t=' + Date.now());
    techTreeData = await r.json();
    renderTechTreePanel();
  } catch(e) {
    el.innerHTML = '<div style=""padding:20px;color:var(--danger)"">加载失败: ' + esc(e.message) + '</div>';
  }
}

function getTechName(tid) {
  if (typeof TECH_INFO === 'undefined') return '' + tid;
  const info = TECH_INFO[tid];
  if (!info) return '' + tid;
  if (info.d && info.d.length > 0) return info.d[0];
  return '' + tid;
}

function toggleTechBranch(el) {
  const det = el.parentElement;
  if (det.tagName === 'DETAILS') det.open = !det.open;
}
function techIconErr(el, tid) {
  el.onerror = null;
  el.parentElement.innerHTML = '<span style=""font-size:18px;color:var(--text-muted)"">' + (tid % 100) + '</span>';
}

function renderTechTreeNode(node, unlocked, paid, curTech) {
  const tid = node.id;
  const isUnlocked = unlocked.includes(tid);
  const isPaid = paid.includes(tid);
  const isResearch = tid === curTech;
  const name = getTechName(tid);
  const hasChildren = node.children && node.children.length > 0;
  let bg, borderCol;
  if (isResearch) { bg = 'rgba(99,102,241,0.25)'; borderCol = 'var(--accent)'; }
  else if (isUnlocked) { bg = 'rgba(16,185,129,0.2)'; borderCol = 'var(--success, #10b981)'; }
  else { bg = 'var(--bg-card)'; borderCol = 'var(--border)'; }
  const cost = [];
  if (node.p > 0) cost.push(node.p + '点');
  if (node.s > 0) cost.push(node.s + '银');
  const costStr = cost.join(' ');
  let h = '';
  h += '<div class=""tech-tree-node"" data-tid=""' + tid + '"" data-name=""' + esc(name).toLowerCase() + '"">';
  h += '<div class=""tech-card"" style=""background:' + bg + ';border:2px solid ' + borderCol + '"" onclick=""toggleTech(' + tid + ',' + (!isUnlocked) + ')"">';
  const iconId = (typeof TECH_ICON !== 'undefined' && TECH_ICON[tid]) ? TECH_ICON[tid] : tid;
  h += '<div class=""tc-icon"" style=""border-color:' + borderCol + '""><img src=""/icon/' + iconId + '"" onerror=""techIconErr(this,' + tid + ')""></div>';
  h += '<div class=""tc-name"" style=""color:' + (isUnlocked ? 'var(--text-primary)' : 'var(--text-muted)') + '"">' + esc(name) + '</div>';
  if (costStr) h += '<div class=""tc-cost"">' + costStr + '</div>';
  if (isPaid) h += '<span class=""tc-badge"" style=""background:var(--success-dark,#059669);color:#fff"">已付</span>';
  if (isResearch) h += '<span class=""tc-badge"" style=""background:var(--accent);color:#fff"">研究中</span>';
  if (node.t) h += '<span class=""tc-tip"" title=""' + esc(node.t) + '"">&#x26A0;</span>';
  h += '</div>';
  h += '</div>';
  if (hasChildren) {
    h += '<div data-children style=""margin-left:24px;margin-top:4px;margin-bottom:8px"">';
    h += '<div style=""display:flex;flex-wrap:wrap;gap:6px;align-items:flex-start"">';
    for (const child of node.children) {
      h += renderTechTreeNode(child, unlocked, paid, curTech);
    }
    h += '</div></div>';
  }
  return h;
}

function renderTechTreePanel() {
  const el = document.getElementById('content');
  if (!techTreeData) {
    el.innerHTML = '<div style=""padding:20px;color:var(--text-muted)"">暂无数据</div>';
    return;
  }

  const d = techTreeData;
  const unlocked = d.unlockTechList || [];
  const paid = d.techHasPaid || [];
  const queue = d.researchQueue || [];
  const curTech = d.curResearchTech || 0;
  const curProg = d.curResearchProgress || 0;

  let html = '<div style=""padding:16px"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x1F333; 科技树</h2>';

  // 当前研究
  if (curTech > 0) {
    html += '<div style=""margin-bottom:16px;padding:12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm)"">';
    html += '<div style=""font-weight:600;margin-bottom:8px;color:var(--text-primary)"">当前研究</div>';
    html += '<div style=""display:flex;align-items:center;gap:12px"">';
    html += '<span style=""color:var(--accent-light);font-size:15px"">' + curTech + ' ' + getTechName(curTech) + '</span>';
    html += '<span style=""color:var(--text-muted);font-size:13px"">进度: ' + curProg + '</span>';
    if (queue.length > 0) {
      html += '<span style=""color:var(--text-muted);font-size:12px"">队列: ' + queue.join(', ') + '</span>';
    }
    html += '</div></div>';
  }

  // 布尔解锁状态
  const boolFlags = [
    {key:'isUnlockTechInspiration', label:'灵感解锁'},
    {key:'isUnlockFreeLove', label:'自由恋爱'},
    {key:'isUnlockCourage', label:'勇气'},
    {key:'isUnlockPenaltySystem', label:'惩罚制度'},
    {key:'isUnlockRewardsSystem', label:'奖励制度'},
    {key:'isUnlockPersonalAwareness', label:'个人意识'},
    {key:'isUnlockHearken', label:'倾听'},
    {key:'isUnlockSocialSupport', label:'社会支持'},
    {key:'isUnlockEfficientStorage', label:'高效存储'},
    {key:'isUnlockEncyclopedia', label:'百科全书'}
  ];
  html += '<details style=""margin-bottom:16px"">';
  html += '<summary style=""cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px"">特殊解锁状态</summary>';
  html += '<div style=""display:flex;flex-wrap:wrap;gap:8px;padding:12px"">';
  for (const f of boolFlags) {
    const val = d[f.key];
    const bg = val ? 'var(--success-dark, #27ae60)' : 'var(--bg-input, #1a1a2e)';
    const fg = val ? '#fff' : 'var(--text-muted)';
    html += '<span style=""padding:4px 10px;border-radius:12px;font-size:12px;background:' + bg + ';color:' + fg + ';border:1px solid var(--border)"">' + f.label + ': ' + (val ? '是' : '否') + '</span>';
  }
  html += '</div></details>';

  // 搜索框
  html += '<input id=""techSearch"" placeholder=""搜索科技名称或ID..."" oninput=""filterTechList()"" style=""width:100%;padding:6px 10px;margin-bottom:12px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';

  // 依赖树展示
  html += '<details open style=""margin-bottom:16px"">';
  html += '<summary style=""cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px"">';
  html += '<span>科技依赖树</span>';
  html += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + unlocked.length + ' 已解锁</span>';
  html += '</summary>';
  html += '<div id=""techTreeContainer"" style=""padding:8px;max-height:60vh;overflow-y:auto"">';

  if (typeof TECH_TREE !== 'undefined') {
    // 构建依赖树
    const byId = {};
    const roots = [];
    for (const t of TECH_TREE) {
      byId[t.i] = {id:t.i, p:t.p, s:t.s, t:t.t||'', children:[], dep:t.d};
    }
    for (const t of TECH_TREE) {
      const node = byId[t.i];
      if (t.d && byId[t.d]) {
        byId[t.d].children.push(node);
      } else if (!t.d || t.d === 0 || t.d === '') {
        roots.push(node);
      }
    }
    // 排序子节点
    function sortTree(nodes) {
      nodes.sort((a,b) => a.id - b.id);
      for (const n of nodes) sortTree(n.children);
    }
    sortTree(roots);
    // 渲染 - 根节点用flex-wrap网格布局
    html += '<div style=""display:flex;flex-wrap:wrap;gap:8px;align-items:flex-start"">';
    for (const root of roots) {
      html += renderTechTreeNode(root, unlocked, paid, curTech);
    }
    html += '</div>';
  } else {
    html += '<div style=""color:var(--text-muted);padding:12px"">科技数据加载中...</div>';
  }

  html += '</div></details>';

  // 一键点亮
  html += '<div style=""margin-bottom:16px;display:flex;gap:8px;align-items:center"">';
  html += '<button onclick=""unlockAllTechs()"" style=""padding:8px 20px;background:linear-gradient(135deg,var(--accent),var(--accent-dark));color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;font-weight:600;box-shadow:0 2px 8px rgba(99,102,241,0.4)"">&#x2728; 一键点亮全部科技</button>';
  html += '<button onclick=""lockAllTechs()"" style=""padding:8px 20px;background:var(--danger);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;font-weight:600"">&#x1F512; 一键锁定全部科技</button>';
  html += '</div>';

  // 添加科技（手动输入ID）
  html += '<div style=""margin-bottom:16px;padding:12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm)"">';
  html += '<div style=""font-weight:600;margin-bottom:8px;color:var(--text-primary)"">手动添加/移除科技</div>';
  html += '<div style=""display:flex;gap:8px;align-items:center"">';
  html += '<input id=""techAddId"" type=""number"" placeholder=""科技ID"" style=""width:120px;padding:6px 10px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
  html += '<button onclick=""addTech(true)"" style=""padding:6px 12px;background:var(--success-dark,#27ae60);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px"">解锁</button>';
  html += '<button onclick=""addTech(false)"" style=""padding:6px 12px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px"">锁定</button>';
  html += '</div></div>';

  // 诊断按钮
  html += '<div style=""margin-top:16px""><button onclick=""diagnoseTechTree()"" style=""padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px"">诊断原始数据</button></div>';
  html += '<pre id=""techTreeDiag"" style=""margin-top:8px;padding:12px;background:var(--bg-card);border-radius:var(--radius-sm);font-size:11px;color:var(--text-muted);white-space:pre-wrap;max-height:400px;overflow-y:auto;display:none""></pre>';

  html += '</div>';
  el.innerHTML = html;
}

function filterTechList() {
  const q = document.getElementById('techSearch').value.toLowerCase().trim();
  const container = document.getElementById('techTreeContainer');
  if (!container) return;
  const nodes = container.querySelectorAll('.tech-tree-node');
  if (!q) {
    nodes.forEach(n => { n.style.display = ''; const p = n.parentElement; if (p) p.style.display = ''; });
    container.querySelectorAll('[data-children]').forEach(c => c.style.display = '');
    return;
  }
  // 先全部隐藏
  nodes.forEach(n => n.style.display = 'none');
  container.querySelectorAll('[data-children]').forEach(c => c.style.display = 'none');
  // 递归显示匹配节点及其祖先
  function showNode(el) {
    el.style.display = '';
    // 显示父级children容器
    const parent = el.closest('[data-children]');
    if (parent) parent.style.display = '';
  }
  function checkAndShow(el) {
    const tid = (el.dataset.tid || '').toLowerCase();
    const name = el.dataset.name || '';
    const match = tid.includes(q) || name.includes(q);
    // 找子节点容器（下一个兄弟元素）
    let childContainer = el.nextElementSibling;
    let childMatch = false;
    if (childContainer && childContainer.dataset.children !== undefined) {
      const childNodes = childContainer.querySelectorAll(':scope > .tech-tree-node');
      childNodes.forEach(cn => { if (checkAndShow(cn)) childMatch = true; });
    }
    if (match || childMatch) {
      showNode(el);
      return true;
    }
    return false;
  }
  // 找根节点（在顶层flex容器中）
  const rootNodes = container.querySelectorAll(':scope > div > .tech-tree-node, :scope > .tech-tree-node');
  rootNodes.forEach(n => checkAndShow(n));
}

function addTech(unlock) {
  const input = document.getElementById('techAddId');
  const techId = parseInt(input.value);
  if (!techId || isNaN(techId)) { toast('请输入有效的科技ID', true); return; }
  toggleTech(techId, unlock);
}

async function unlockAllTechs() {
  if (!confirm('确定要一键点亮全部科技吗？')) return;
  try {
    const r = await fetch('/api/techtree/unlockall', {method:'POST'});
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('已点亮 ' + d.added + ' 个科技（共 ' + d.total + ' 个）');
    const container = document.getElementById('techTreeContainer');
    const scrollTop = container ? container.scrollTop : 0;
    const r2 = await fetch('/api/techtree?t=' + Date.now());
    techTreeData = await r2.json();
    renderTechTreePanel();
    const c2 = document.getElementById('techTreeContainer');
    if (c2) c2.scrollTop = scrollTop;
  } catch(e) { toast('操作失败', true); }
}

async function lockAllTechs() {
  if (!confirm('确定要一键锁定全部科技吗？这会清除所有已解锁科技！')) return;
  try {
    const ids = (typeof TECH_TREE !== 'undefined') ? TECH_TREE.map(t => t.i) : [];
    if (!ids.length) { toast('科技数据未加载', true); return; }
    let removed = 0;
    for (const tid of ids) {
      const r = await fetch('/api/techtree/toggle', {
        method:'POST', headers:{'Content-Type':'application/json'},
        body: JSON.stringify({techId:tid, unlock:false})
      });
      const d = await r.json();
      if (d.action === 'removed') removed++;
    }
    toast('已锁定 ' + removed + ' 个科技');
    const container = document.getElementById('techTreeContainer');
    const scrollTop = container ? container.scrollTop : 0;
    const r2 = await fetch('/api/techtree?t=' + Date.now());
    techTreeData = await r2.json();
    renderTechTreePanel();
    const c2 = document.getElementById('techTreeContainer');
    if (c2) c2.scrollTop = scrollTop;
  } catch(e) { toast('操作失败', true); }
}

async function toggleTech(techId, unlock) {
  techId = parseInt(techId);
  if (!techId || isNaN(techId)) { toast('请输入有效的科技ID', true); return; }
  try {
    const r = await fetch('/api/techtree/toggle', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({techId, unlock})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('操作成功: ' + (d.action || 'ok'));
    const container = document.getElementById('techTreeContainer');
    const scrollTop = container ? container.scrollTop : 0;
    const searchInput = document.getElementById('techSearch');
    const searchVal = searchInput ? searchInput.value : '';
    try {
      const r2 = await fetch('/api/techtree?t=' + Date.now());
      techTreeData = await r2.json();
      renderTechTreePanel();
    } catch(e2) {}
    const container2 = document.getElementById('techTreeContainer');
    if (container2) container2.scrollTop = scrollTop;
    const searchInput2 = document.getElementById('techSearch');
    if (searchInput2 && searchVal) { searchInput2.value = searchVal; filterTechList(); }
  } catch(e) { toast('操作失败', true); }
}

async function diagnoseTechTree() {
  const pre = document.getElementById('techTreeDiag');
  pre.style.display = 'block';
  pre.textContent = '诊断中...';
  try {
    const r = await fetch('/api/techtree/diagnose?t=' + Date.now());
    const data = await r.json();
    pre.textContent = JSON.stringify(data, null, 2);
  } catch(e) {
    pre.textContent = '诊断失败: ' + e.message;
  }
}

function selectNpcView(view) {
  selectedChest = -1;
  dragonView = '';
  npcView = (npcView === view) ? '' : view;
  renderSidebar();
  renderContent();
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

async function fetchEntityEditorData() {
  try {
    const r = await fetch('/api/editor/entities?t=' + Date.now());
    const d = await r.json();
    if (Array.isArray(d)) entityEditorData = d;
  } catch(e) {}
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
  container.innerHTML = '<div style=""padding:8px;color:var(--text-muted);font-size:12px"">加载中...</div>';
  try {
    const r = await fetch('/api/editor/fields/' + ptrHash + '?t=' + Date.now());
    const fields = await r.json();
    if (fields.error) { container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">' + esc(fields.error) + '</div>'; return; }
    container.innerHTML = renderEditorFieldsTable(fields, ptrHash);
    container.dataset.loaded = '1';
  } catch(e) {
    container.innerHTML = '<div style=""padding:8px;color:var(--danger);font-size:12px"">加载失败</div>';
  }
}

function renderEditorFieldsTable(fields, ptrHash) {
  let html = '<table style=""width:100%;border-collapse:collapse;font-size:12px"">';
  html += '<tr style=""background:var(--bg-card)""><th style=""text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)"">字段</th><th style=""text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)"">类型</th><th style=""text-align:left;padding:4px 8px;border-bottom:1px solid var(--border)"">值</th><th style=""padding:4px 8px;border-bottom:1px solid var(--border)"">操作</th></tr>';
  for (const [name, f] of Object.entries(fields)) {
    const typeLabel = f.isFloat ? 'float' : (f.isString ? 'string' : 'int');
    const val = f.value !== undefined ? f.value : '';
    html += '<tr style=""border-bottom:1px solid var(--border-light,rgba(255,255,255,0.05))"">';
    html += '<td style=""padding:4px 8px;color:var(--text-primary)"">' + esc(name) + '</td>';
    html += '<td style=""padding:4px 8px;color:var(--text-muted)"">' + typeLabel + '</td>';
    html += '<td style=""padding:4px 8px"">';
    if (f.isString) {
      html += '<span style=""color:var(--accent-light)"">' + esc(String(val)) + '</span>';
    } else {
      html += '<input id=""editor_' + name + '_' + ptrHash + '"" type=""text"" value=""' + esc(String(val)) + '"" style=""width:120px;padding:2px 6px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px"">';
    }
    html += '</td>';
    html += '<td style=""padding:4px 8px;text-align:center"">';
    if (!f.isString) {
      html += '<button onclick=""setEntityEditorField(' + ptrHash + ', \'' + esc(name) + '\')"" style=""padding:2px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">OK</button>';
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

function renderEntityEditorPanel() {
  const el = document.getElementById('content');
  let html = '';
  html += '<div style=""padding:20px;height:100%;display:flex;flex-direction:column;overflow:hidden"">';
  html += '<h2 style=""color:var(--accent-light);margin-bottom:16px;font-size:18px"">&#x270F; 修改</h2>';

  html += '<div style=""display:flex;gap:12px;margin-bottom:20px;align-items:center"">';
  html += '<button id=""entityEditorScanBtn"" onclick=""entityEditorScan()"" style=""padding:10px 20px;background:var(--accent);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:14px;font-weight:500"">开始扫描</button>';
  html += '<span style=""color:var(--text-muted);font-size:13px"">' + entityEditorData.length + ' 个实体</span>';
  html += '</div>';

  if (entityEditorData.length === 0) {
    html += '<div style=""background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:40px;text-align:center"">';
    html += '<div style=""font-size:48px;margin-bottom:16px"">&#x270F;</div>';
    html += '<div style=""color:var(--text-secondary);font-size:16px;margin-bottom:8px"">暂无数据</div>';
    html += '<div style=""color:var(--text-muted);font-size:13px"">点击上方按钮扫描实体</div>';
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
      h += '<div style=""margin-bottom:4px"">';
      h += '<div style=""display:flex;align-items:center;gap:8px;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);cursor:pointer"" onclick=""toggleEditorEntity(' + ptrHash + ')"">';
      h += '<span style=""font-weight:600;color:var(--text-primary)"">' + esc(displayName) + (nameSuffix ? ' <span style=""font-weight:400;color:var(--text-muted)"">(' + esc(nameSuffix) + ')</span>' : '') + '</span>';
      if (kInfo) h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:' + kInfo.bg + ';color:' + kInfo.fg + '"">' + esc(kInfo.name) + '</span>';
      if (npcName && entityName) h += '<span style=""font-size:10px;color:var(--text-muted)"">' + esc(entityName) + '</span>';
      h += '<span style=""font-size:11px;color:var(--text-muted);margin-left:auto"">' + esc(e.className || '') + ' GUID:' + guid + '</span>';
      h += '<button onclick=""event.stopPropagation();listEntityMethods(' + ptrHash + ')"" style=""padding:3px 8px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap"">方法</button>';
      h += '<button onclick=""event.stopPropagation();locateEditorEntity(' + ptrHash + ')"" style=""padding:3px 8px;background:var(--info,#3498db);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap"">定位</button>';
      h += '<button onclick=""event.stopPropagation();destroyEditorEntity(' + ptrHash + ')"" style=""padding:3px 10px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px;white-space:nowrap"">消除</button>';
      h += '</div>';
      h += '<div id=""editor_fields_' + ptrHash + '"" style=""display:none;padding:4px 0 4px 12px""></div>';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--success-dark, #27ae60);color:#fff"">我方</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + mine.length + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(mineHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        // 工作者
        if (workers.length > 0) {
          const wHashes = workers.map(e => e.ptrHash);
          h += '<details style=""margin-bottom:8px;margin-left:12px"">';
          h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
          h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:#f39c12;color:#fff"">工作者</span>';
          h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + workers.length + ' 个</span>';
          h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(wHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
          h += '</summary>';
          h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
          for (const e of workers) h += renderEditorEntityItem(e);
          h += '</div></details>';
        }
        // 士兵
        if (soldiers.length > 0) {
          const sHashes = soldiers.map(e => e.ptrHash);
          h += '<details style=""margin-bottom:8px;margin-left:12px"">';
          h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
          h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:#3498db;color:#fff"">士兵</span>';
          h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + soldiers.length + ' 个</span>';
          h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(sHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
          h += '</summary>';
          h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:' + bg + ';color:' + fg + '"">' + esc(label) + '</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + group.length + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--success-dark, #27ae60);color:#fff"">我方</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + mine.length + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(mineHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:' + bg + ';color:' + fg + '"">' + esc(label) + '</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + group.length + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--danger, #e74c3c);color:#fff"">怪物建筑 备注：这里显示我方是因为可交互的</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + totalMonster + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify([].concat(...monsterKeys.map(k => monsterBuilds[k].map(e => e.ptrHash)))) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        for (const mn of monsterBuildNames) {
          const group = monsterBuilds[mn];
          if (!group || group.length === 0) continue;
          const groupHashes = group.map(e => e.ptrHash);
          h += '<details style=""margin-bottom:8px;margin-left:12px"">';
          h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
          h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--warning, #f39c12);color:#fff"">' + esc(mn) + '</span>';
          h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + group.length + ' 个</span>';
          h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
          h += '</summary>';
          h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
          for (const e of group) h += renderEditorEntityItem(e);
          h += '</div></details>';
        }
        h += '</details>';
      }
      if (normalBuilds.length > 0) {
        if (monsterKeys.length > 0) h += '<div style=""margin:8px 0 4px 12px;font-size:12px;font-weight:600;color:var(--text-muted)"">普通建筑 (' + normalBuilds.length + ')</div>';
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
        h += '<details style=""margin-bottom:8px;margin-left:12px"">';
        h += '<summary style=""cursor:pointer;padding:8px 12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:13px;display:flex;align-items:center;gap:8px"">';
        h += '<span style=""font-size:10px;padding:1px 6px;border-radius:8px;background:var(--warning, #f39c12);color:#fff"">' + esc(label) + '</span>';
        h += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + group.length + ' 个</span>';
        h += '<button onclick=""event.stopPropagation();destroyEditorEntities(' + JSON.stringify(groupHashes) + ')"" style=""padding:2px 8px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:11px"">一键消除</button>';
        h += '</summary>';
        h += '<div style=""display:flex;flex-direction:column;gap:4px;padding:6px 0"">';
        for (const e of group) h += renderEditorEntityItem(e);
        h += '</div></details>';
      }
      return h;
    }

    html += '<div style=""flex:1;overflow-y:auto;padding-right:8px"">';
    for (const cd of catDefs) {
      const items = groups[cd.key];
      if (items.length === 0) continue;
      const catHashes = items.map(e => e.ptrHash);
      html += '<details style=""margin-bottom:12px"">';
      html += '<summary style=""cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px"">';
      html += '<span>' + cd.icon + '</span>';
      html += '<span style=""color:' + cd.color + '"">' + cd.label + '</span>';
      html += '<span style=""margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400"">' + items.length + ' 个</span>';
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
        html += '<div style=""display:flex;flex-direction:column;gap:6px;padding:8px 0"">';
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
  npcView = '';
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
  if (npcView === 'editor') {
    renderEntityEditorPanel();
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
  await fetchEntityEditorData();
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
