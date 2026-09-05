// api — 由 HtmlUI 单体拆分（原 app.js）
async function fetchFilters() {
  try {
    const r = await fetch('/api/filters');
    filters = await r.json();
  } catch(e) {}
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


async function loadFieldTranslations() {
  if (_fieldTranslations !== null) return _fieldTranslations;
  try {
    const r = await fetch('/api/npc/translations?t=' + Date.now());
    _fieldTranslations = await r.json();
  } catch(e) { _fieldTranslations = {}; }
  return _fieldTranslations;
}


async function fetchEntityEditorData() {
  try {
    const r = await fetch('/api/editor/entities?t=' + Date.now());
    const d = await r.json();
    if (Array.isArray(d)) entityEditorData = d;
  } catch(e) {}
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
