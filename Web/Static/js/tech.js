// tech — 由 HtmlUI 单体拆分（原 app.js）
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
  el.innerHTML = '<div style="padding:20px;color:var(--text-muted)">加载科技树...</div>';
  try {
    const r = await fetch('/api/techtree?t=' + Date.now());
    techTreeData = await r.json();
    renderTechTreePanel();
  } catch(e) {
    el.innerHTML = '<div style="padding:20px;color:var(--danger)">加载失败: ' + esc(e.message) + '</div>';
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
  el.parentElement.innerHTML = '<span style="font-size:18px;color:var(--text-muted)">' + (tid % 100) + '</span>';
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
  h += '<div class="tech-tree-node" data-tid="' + tid + '" data-name="' + esc(name).toLowerCase() + '">';
  h += '<div class="tech-card" style="background:' + bg + ';border:2px solid ' + borderCol + '" onclick="toggleTech(' + tid + ',' + (!isUnlocked) + ')">';
  const iconId = (typeof TECH_ICON !== 'undefined' && TECH_ICON[tid]) ? TECH_ICON[tid] : tid;
  h += '<div class="tc-icon" style="border-color:' + borderCol + '"><img src="/icon/' + iconId + '" onerror="techIconErr(this,' + tid + ')"></div>';
  h += '<div class="tc-name" style="color:' + (isUnlocked ? 'var(--text-primary)' : 'var(--text-muted)') + '">' + esc(name) + '</div>';
  if (costStr) h += '<div class="tc-cost">' + costStr + '</div>';
  if (isPaid) h += '<span class="tc-badge" style="background:var(--success-dark,#059669);color:#fff">已付</span>';
  if (isResearch) h += '<span class="tc-badge" style="background:var(--accent);color:#fff">研究中</span>';
  if (node.t) h += '<span class="tc-tip" title="' + esc(node.t) + '">&#x26A0;</span>';
  h += '</div>';
  h += '</div>';
  if (hasChildren) {
    h += '<div data-children style="margin-left:24px;margin-top:4px;margin-bottom:8px">';
    h += '<div style="display:flex;flex-wrap:wrap;gap:6px;align-items:flex-start">';
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
    el.innerHTML = '<div style="padding:20px;color:var(--text-muted)">暂无数据</div>';
    return;
  }

  const d = techTreeData;
  const unlocked = d.unlockTechList || [];
  const paid = d.techHasPaid || [];
  const queue = d.researchQueue || [];
  const curTech = d.curResearchTech || 0;
  const curProg = d.curResearchProgress || 0;

  let html = '<div style="padding:16px">';
  html += '<h2 style="color:var(--accent-light);margin-bottom:16px;font-size:18px">&#x1F333; 科技树</h2>';

  // 当前研究
  if (curTech > 0) {
    html += '<div style="margin-bottom:16px;padding:12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm)">';
    html += '<div style="font-weight:600;margin-bottom:8px;color:var(--text-primary)">当前研究</div>';
    html += '<div style="display:flex;align-items:center;gap:12px">';
    html += '<span style="color:var(--accent-light);font-size:15px">' + curTech + ' ' + getTechName(curTech) + '</span>';
    html += '<span style="color:var(--text-muted);font-size:13px">进度: ' + curProg + '</span>';
    if (queue.length > 0) {
      html += '<span style="color:var(--text-muted);font-size:12px">队列: ' + queue.join(', ') + '</span>';
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
  html += '<details style="margin-bottom:16px">';
  html += '<summary style="cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px">特殊解锁状态</summary>';
  html += '<div style="display:flex;flex-wrap:wrap;gap:8px;padding:12px">';
  for (const f of boolFlags) {
    const val = d[f.key];
    const bg = val ? 'var(--success-dark, #27ae60)' : 'var(--bg-input, #1a1a2e)';
    const fg = val ? '#fff' : 'var(--text-muted)';
    html += '<span style="padding:4px 10px;border-radius:12px;font-size:12px;background:' + bg + ';color:' + fg + ';border:1px solid var(--border)">' + f.label + ': ' + (val ? '是' : '否') + '</span>';
  }
  html += '</div></details>';

  // 搜索框
  html += '<input id="techSearch" placeholder="搜索科技名称或ID..." oninput="filterTechList()" style="width:100%;padding:6px 10px;margin-bottom:12px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px">';

  // 依赖树展示
  html += '<details open style="margin-bottom:16px">';
  html += '<summary style="cursor:pointer;padding:10px 14px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm);font-weight:600;font-size:14px;display:flex;align-items:center;gap:8px">';
  html += '<span>科技依赖树</span>';
  html += '<span style="margin-left:auto;font-size:12px;color:var(--text-muted);font-weight:400">' + unlocked.length + ' 已解锁</span>';
  html += '</summary>';
  html += '<div id="techTreeContainer" style="padding:8px;max-height:60vh;overflow-y:auto">';

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
    html += '<div style="display:flex;flex-wrap:wrap;gap:8px;align-items:flex-start">';
    for (const root of roots) {
      html += renderTechTreeNode(root, unlocked, paid, curTech);
    }
    html += '</div>';
  } else {
    html += '<div style="color:var(--text-muted);padding:12px">科技数据加载中...</div>';
  }

  html += '</div></details>';

  // 一键点亮
  html += '<div style="margin-bottom:16px;display:flex;gap:8px;align-items:center">';
  html += '<button onclick="unlockAllTechs()" style="padding:8px 20px;background:linear-gradient(135deg,var(--accent),var(--accent-dark));color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;font-weight:600;box-shadow:0 2px 8px rgba(99,102,241,0.4)">&#x2728; 一键点亮全部科技</button>';
  html += '<button onclick="lockAllTechs()" style="padding:8px 20px;background:var(--danger);color:#fff;border:none;border-radius:var(--radius-sm);cursor:pointer;font-size:13px;font-weight:600">&#x1F512; 一键锁定全部科技</button>';
  html += '</div>';

  // 添加科技（手动输入ID）
  html += '<div style="margin-bottom:16px;padding:12px;background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius-sm)">';
  html += '<div style="font-weight:600;margin-bottom:8px;color:var(--text-primary)">手动添加/移除科技</div>';
  html += '<div style="display:flex;gap:8px;align-items:center">';
  html += '<input id="techAddId" type="number" placeholder="科技ID" style="width:120px;padding:6px 10px;background:var(--bg-input,#1a1a2e);border:1px solid var(--border);border-radius:4px;color:var(--text-primary);font-size:12px">';
  html += '<button onclick="addTech(true)" style="padding:6px 12px;background:var(--success-dark,#27ae60);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px">解锁</button>';
  html += '<button onclick="addTech(false)" style="padding:6px 12px;background:var(--danger,#e74c3c);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px">锁定</button>';
  html += '</div></div>';

  // 诊断按钮
  html += '<div style="margin-top:16px"><button onclick="diagnoseTechTree()" style="padding:6px 16px;background:var(--accent);color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px">诊断原始数据</button></div>';
  html += '<pre id="techTreeDiag" style="margin-top:8px;padding:12px;background:var(--bg-card);border-radius:var(--radius-sm);font-size:11px;color:var(--text-muted);white-space:pre-wrap;max-height:400px;overflow-y:auto;display:none"></pre>';

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


// 刷新科技树数据并保持滚动与搜索状态（解锁/锁定/切换单个 共用）
async function refreshTechTreePreservingState() {
  const container = document.getElementById('techTreeContainer');
  const scrollTop = container ? container.scrollTop : 0;
  const searchInput = document.getElementById('techSearch');
  const searchVal = searchInput ? searchInput.value : '';
  try {
    const r = await fetch('/api/techtree?t=' + Date.now());
    techTreeData = await r.json();
    renderTechTreePanel();
  } catch(e) {}
  const c2 = document.getElementById('techTreeContainer');
  if (c2) c2.scrollTop = scrollTop;
  const s2 = document.getElementById('techSearch');
  if (s2 && searchVal) { s2.value = searchVal; filterTechList(); }
}


async function unlockAllTechs() {
  if (!confirm('确定要一键点亮全部科技吗？')) return;
  try {
    const r = await fetch('/api/techtree/unlockall', {method:'POST'});
    const d = await r.json();
    if (d.error) { toast(d.error, true); return; }
    toast('已点亮全部科技');
    await refreshTechTreePreservingState();
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
    await refreshTechTreePreservingState();
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
    await refreshTechTreePreservingState();
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
