// chest — 由 HtmlUI 单体拆分（原 app.js）
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


function selectChest(i) {
  selectedChest = i;
  dragonView = '';
  npcView = '';
  npcfixView = '';
  showDragonSouls = false;
  renderSidebar();
  renderContent();
}


function renderContent() {
  const el = document.getElementById('content');

  // 驯龙视图
  if (dragonView === 'souls') {
    el.innerHTML = '<div id="dragonViewContent"></div>';
    renderDragonSoulsPanel();
    return;
  }
  if (dragonView === 'materials') {
    el.innerHTML = '<div id="dragonViewContent"></div>';
    renderDragonMaterialsPanel();
    return;
  }
  if (dragonView === 'summon') {
    el.innerHTML = '<div id="dragonViewContent"></div>';
    renderDragonSummonPanel();
    return;
  }

  // NPC 视图
  if (npcView === 'editor') {
    renderEntityEditorPanel();
    return;
  }

  // NPC修改 视图
  if (npcfixView === 'box1') { renderNpcfixBox1(); return; }
  if (npcfixView === 'box2') { renderNpcfixBox2(); return; }
  if (npcfixView === 'box3') { renderNpcfixBox3(); return; }
  if (npcfixView === 'box4') { renderNpcfixBox4(); return; }
  if (npcfixView === 'box5') { renderNpcfixBox5(); return; }

  if (selectedChest < 0 || selectedChest >= chests.length) {
    el.innerHTML = '<div class="empty-state"><div class="icon">&#128230;</div><div class="title">选择一个箱子</div><div class="desc">从左侧列表中选择箱子查看物品</div></div>';
    return;
  }

  const c = chests[selectedChest];
  const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
  const q = searchQuery.toLowerCase();

  let html = '';
  html += '<div class="content-header">';
  html += '<span class="ch-title">' + esc(c.name) + '</span>';
  html += '<span class="ch-cap">' + cap + '</span>';
  html += '<button class="btn-locate" onclick="locateChest(' + selectedChest + ')">定位</button>';
  html += '<button class="btn-add" onclick="openAddModal(' + selectedChest + ')">+ 添加物品</button>';
  if (c.planStock != null)
    html += '<button class="btn-add-plan" style="margin-left:8px" onclick="openPlanModal(' + selectedChest + ')">+ 添加计划</button>';
  html += '</div>';
  html += '<div class="content-search"><input id="searchItem" placeholder="搜索物品..." value="' + esc(searchQuery) + '" oninput="searchQuery=this.value;renderContent()"></div>';

  // 合并物品与计划库存（部分容器带计划库存功能：c.planStock != null）
  const hasPlan = c.planStock != null;
  const planMap = {};
  if (hasPlan) for (const ps of c.planStock) planMap[ps.stuffId] = ps;
  const ids = [];
  const seen = {};
  for (const it of c.items) if (!seen[it.stuffId]) { seen[it.stuffId] = 1; ids.push(it.stuffId); }
  if (hasPlan) for (const ps of c.planStock) if (!seen[ps.stuffId]) { seen[ps.stuffId] = 1; ids.push(ps.stuffId); }

  if (ids.length === 0) {
    html += '<div class="empty-state"><div class="icon">&#128230;</div><div class="title">箱子为空</div><div class="desc">点击上方按钮添加物品</div></div>';
  } else {
    html += '<div class="items">';
    for (const sid of ids) {
      const bagIt = c.items.find(x => x.stuffId === sid);
      const planIt = planMap[sid];
      const name = (bagIt || planIt || {name:'ID:' + sid}).name;
      if (q && !name.toLowerCase().includes(q)) continue;
      const bagCount = bagIt ? bagIt.count : 0;
      // 库存行：设=写入输入值，删=清空该物品
      let rows = htmlFieldRow('库存', 'cnt_' + sid, bagCount, [
        {label:'设', onclick:'setBagItem(' + selectedChest + ',' + sid + ')'},
        {label:'删', onclick:'doRemove(' + selectedChest + ',' + sid + ',' + bagCount + ')'}
      ], {compact:true});
      // 计划库存行（仅带计划功能的容器显示）
      if (hasPlan)
        rows += htmlFieldRow('计划', 'plan_' + sid, planMap[sid] ? planMap[sid].count : 0, [
          {label:'设', onclick:'setPlanItem(' + selectedChest + ',' + sid + ')'},
          {label:'删', onclick:'doRemovePlan(' + selectedChest + ',' + sid + ')'}
        ], {compact:true, labelWidth:'30px', labelAlign:'left'});
      html += htmlDataCard(sid, name, rows);
    }
    html += '</div>';
  }

  el.innerHTML = html;
}


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


// ===== 添加物品 / 计划库存 通用 Modal =====
// 两套 Modal 结构完全相同，只有 DOM id 前缀与应用动作不同
const ITEM_MODALS = {
  add:  {modalId:'addModal',  titleId:'addTitle',  countId:'addCount',  searchId:'addSearch',  listId:'addList',
         titlePrefix:'向 [{name}] 添加物品', applyFn:'doAdd',  nFn:'addN'},
  plan: {modalId:'planModal', titleId:'planTitle', countId:'planCount', searchId:'planSearch', listId:'planList',
         titlePrefix:'向 [{name}] 添加计划库存', applyFn:'doAddPlan', nFn:'planN'}
};

let _modalChestIndex = -1;

function openItemModal(kind, ci) {
  const cfg = ITEM_MODALS[kind];
  _modalChestIndex = ci;
  document.getElementById(cfg.titleId).textContent = cfg.titlePrefix.replace('{name}', chests[ci].name);
  document.getElementById(cfg.countId).value = 1;
  document.getElementById(cfg.searchId).value = '';
  document.getElementById(cfg.modalId).classList.add('show');
  renderModalList(kind);
}

function closeItemModal(kind) {
  document.getElementById(ITEM_MODALS[kind].modalId).classList.remove('show');
}

function renderModalList(kind) {
  const cfg = ITEM_MODALS[kind];
  const q = document.getElementById(cfg.searchId).value.toLowerCase();
  const el = document.getElementById(cfg.listId);
  // 按官方子类型（stuff_type_name.json）过滤：只显示真实物品类，
  // 排除 种族(812)/商人(806)/龙魂石(815)/未知(808)/图标/场景元素 等伪物品。
  // 料堆额外收尸体类（801=死亡的动物 804=尸体 805=已宰杀的动物），普通容器不收。
  const _chest = chests[_modalChestIndex];
  const isPile = _chest && _chest.name && _chest.name.indexOf('料堆') === 0;
  let html = '';
  for (const it of items) {
    if (!subTypeAllowed(it.subType)) continue;
    if (!isPile && (it.subType === 801 || it.subType === 804 || it.subType === 805)) continue;
    if (q && !it.name.toLowerCase().includes(q)) continue;
    html += '<div class="modal-item" onclick="' + cfg.applyFn + '(' + it.stuffId + ')">';
    html += '<img src="/icon/' + it.stuffId + '" onerror="hideImg(this)">';
    html += '<span class="mi-name">' + esc(it.name) + '</span>';
    html += '<span class="mi-id">ID:' + it.stuffId + '</span>';
    html += '</div>';
  }
  el.innerHTML = html;
}

// 官方子类型白名单：食物(301-306) 加工品(401-408,410-430) 原料(601-621) 种子/经济作物(701-704,707)
function subTypeAllowed(t) {
  if (t >= 301 && t <= 306) return true;
  if (t >= 401 && t <= 408) return true;
  if (t >= 410 && t <= 430) return true;
  if (t >= 601 && t <= 621) return true;
  if ((t >= 701 && t <= 704) || t === 707) return true;
  return false;
}

function modalSetN(kind, n) {
  document.getElementById(ITEM_MODALS[kind].countId).value = n;
}

// ===== 旧入口（index.html 静态 DOM 引用的函数名，保持兼容） =====
function openAddModal(ci) { openItemModal('add', ci); }
function closeAddModal() { closeItemModal('add'); }
function renderAddList() { renderModalList('add'); }
function addN(n) { modalSetN('add', n); }

function openPlanModal(ci) { openItemModal('plan', ci); }
function closePlanModal() { closeItemModal('plan'); }
function renderPlanList() { renderModalList('plan'); }
function planN(n) { modalSetN('plan', n); }

async function doAdd(sid) {
  const cnt = parseInt(document.getElementById(ITEM_MODALS.add.countId).value) || 1;
  try {
    const r = await fetch('/api/chest/' + _modalChestIndex + '/add', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId:sid, count:cnt})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    chests[_modalChestIndex] = d;
    renderSidebar();
    renderContent();
    toast('添加成功');
  } catch(e) { toast('操作失败', true); }
}

async function doAddPlan(sid) {
  const cnt = parseInt(document.getElementById(ITEM_MODALS.plan.countId).value) || 1;
  await adjPlan(_modalChestIndex, sid, cnt);
  closeItemModal('plan');
}
