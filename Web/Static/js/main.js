// main — 由 HtmlUI 单体拆分（原 app.js）
async function loadDataTables() {
  try {
    const [npct, tree, info, icon] = await Promise.all([
      fetch('/static/data/npc_types.json').then(r => r.json()),
      fetch('/static/data/tech_tree.json').then(r => r.json()),
      fetch('/static/data/tech_info.json').then(r => r.json()),
      fetch('/static/data/tech_icon.json').then(r => r.json())
    ]);
    NPC_TYPES = npct; TECH_TREE = tree; TECH_INFO = info; TECH_ICON = icon;
  } catch (e) { console.error('数据表加载失败', e); }
}

async function init() {
  await loadDataTables();
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
