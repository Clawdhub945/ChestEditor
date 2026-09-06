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


let _lastSoulsSig = null;

async function fetchDragonSouls() {
  try {
    const r = await fetch('/api/dragon/souls?t=' + Date.now());
    const data = await r.json();
    // 数据变化检测：召唤/移除龙后自动重渲染，无变化时不打断正在输入的值
    const sig = JSON.stringify(data);
    const changed = sig !== _lastSoulsSig;
    _lastSoulsSig = sig;
    dragonSouls = data;
    if (changed) {
      renderSidebar();
      if (dragonView === 'souls') renderDragonSoulsPanel();
    }
  } catch(e) {}
}


async function fetchTemple() {
  try {
    const r = await fetch('/api/temple?t=' + Date.now());
    const data = await r.json();
    if (!data || typeof data.found !== 'boolean') return; // 404/错误响应不覆盖
    const changed = JSON.stringify(data) !== JSON.stringify(templeItems);
    templeItems = data;
    if (changed && dragonView === 'materials') renderDragonMaterialsPanel();
  } catch(e) {}
}

async function setTempleItem(stuffId) {
  const input = document.getElementById('temple_' + stuffId);
  const count = parseInt(input && input.value) || 0;
  try {
    const r = await fetch('/api/temple/set', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({stuffId, count})
    });
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    templeItems = d;
    renderDragonMaterialsPanel();
    toast('已设置');
  } catch(e) { toast('设置失败', true); }
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
