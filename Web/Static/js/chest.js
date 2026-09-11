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
  // 显式白名单（用户定义的 181 条，见 ADD_ITEM_IDS）；料堆额外可加 804001 尸体。
  const _chest = chests[_modalChestIndex];
  const isPile = _chest && _chest.name && _chest.name.indexOf('料堆') === 0;
  let html = '';
  for (const it of items) {
    if (!ADD_ITEM_IDS.has(it.stuffId) && !(isPile && it.stuffId === 804001)) continue;
    if (q && !it.name.toLowerCase().includes(q)) continue;
    html += '<div class="modal-item" onclick="' + cfg.applyFn + '(' + it.stuffId + ')">';
    html += '<img src="/icon/' + it.stuffId + '" onerror="hideImg(this)">';
    html += '<span class="mi-name">' + esc(it.name) + '</span>';
    html += '<span class="mi-id">ID:' + it.stuffId + '</span>';
    html += '</div>';
  }
  el.innerHTML = html;
}

// ===== 添加物品/计划库存 显式白名单（用户逐条确认的 181 条）=====
// 料堆额外可加 804001（尸体）。调整白名单直接改这张表。
const ADD_ITEM_IDS = new Set([
  301001,301002,301003,301004,301005,301006,301007,
  302001,302002,302003,302004,
  303001,303002,303004,303006,303007,303009,
  304001,304002,304005,304007,304008,304009,304010,
  305001,305002,305003,305004,305005,305006,
  306001,
  401001,401002,401003,
  402001,
  403001,403002,403003,
  404001,404002,404003,
  409003,
  410001,410002,410003,410004,
  412001,412002,412003,
  413001,413002,413003,413004,413005,413006,413007,413008,413009,
  414001,414002,414003,414004,414005,414006,414007,414008,414009,
  415001,415002,415003,415004,415005,415006,415007,415008,415009,
  416001,416002,
  417001,417002,417003,417004,417005,417006,417007,417008,417009,
  417010,417011,417012,417013,417014,417015,417016,417017,417018,
  417019,417020,417021,417022,417023,417024,417025,417026,417027,
  418001,418002,
  419001,
  420001,
  421001,
  422001,
  423001,423002,423003,
  424001,424002,424003,424004,424005,424006,424007,424008,
  425001,425002,
  426001,
  427001,
  428001,
  429001,429002,429003,
  430001,430002,
  601001,
  602001,602002,602003,602004,
  603001,603002,603003,603004,
  604001,
  605001,
  606001,606002,
  607001,
  608001,
  609001,
  610001,610002,610003,
  611001,611002,611003,611004,611005,
  612001,612002,612003,
  613001,
  614001,
  615001,615002,615003,
  616001,616002,616003,616004,616005,616006,
  617001,
  618001,
  619001,
  620001,620002,
  621001,621002,621003,621004,621005,621006
]);

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
