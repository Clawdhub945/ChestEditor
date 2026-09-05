import sys, io, json, re
sys.path.insert(0, '_tools')
from splitter import write_file, strip_line, NL

src = io.open('Web/Static/js/app.js', encoding='utf-8').read()
lines = src.split(NL)

# ===== 1. 提取数据表 =====
def extract_var(name):
    for i, l in enumerate(lines):
        if l.startswith('var ' + name + '='):
            raw = l[len('var ' + name + '='):].strip()
            assert raw.endswith(';'), name
            return raw[:-1]
    raise SystemExit('not found: ' + name)

data_files = {
    'npc_types.json': extract_var('NPC_TYPES'),
    'tech_tree.json': extract_var('TECH_TREE'),
    'tech_info.json': extract_var('TECH_INFO'),
    'tech_icon.json': extract_var('TECH_ICON'),
}
for fname, content in data_files.items():
    if fname == 'tech_icon.json':
        # JS 对象数字键 → JSON 字符串键
        import re as _re
        content = _re.sub(r'([{,])(\d+):', r'\1"\2":', content)
    json.loads(content)  # 校验
    write_file('Web/Static/data/' + fname, content)

# ===== 2. 顶层块切分 =====
starts = []
for i, l in enumerate(lines):
    if re.match(r'^(async function|function|var|let|const)\b', l) and strip_line(l).count('{') - strip_line(l).count('}') >= 0:
        starts.append(i)
# 校验 depth==0
blocks = []  # (start_idx, name)
for idx, i in enumerate(starts):
    end = starts[idx + 1] if idx + 1 < len(starts) else len(lines)
    first = lines[i].strip()[:60]
    blocks.append((i, end, first))

# 模块归属：按声明起始行（0-based）
assign = {
    0: 'state',  # let chests = []; (1-22 行整体)
    # api
    23: 'api', 46: 'api', 56: 'api', 63: 'api', 121: 'api', 523: 'api', 1163: 'api', 1617: 'api', 1624: 'api',
    # ui
    195: 'ui', 327: 'ui', 332: 'ui', 337: 'ui', 342: 'ui', 347: 'ui', 1127: 'ui', 2349: 'ui', 2356: 'ui', 2364: 'ui', 2365: 'ui',
    # chest
    30: 'chest', 38: 'chest', 173: 'chest', 1818: 'chest', 1835: 'chest', 2171: 'chest', 2180: 'chest',
    2196: 'chest', 2203: 'chest', 2212: 'chest', 2226: 'chest', 2242: 'chest', 2251: 'chest', 2255: 'chest',
    2270: 'chest', 2287: 'chest', 2292: 'chest', 2307: 'chest', 2311: 'chest', 2320: 'chest', 2324: 'chest',
    2339: 'chest', 2345: 'chest',
    # dragon
    70: 'dragon', 88: 'dragon', 134: 'dragon', 154: 'dragon', 1637: 'dragon', 1699: 'dragon', 1718: 'dragon',
    1725: 'dragon', 1732: 'dragon', 1740: 'dragon', 1750: 'dragon', 1783: 'dragon', 1785: 'dragon',
    1790: 'dragon', 1799: 'dragon', 1827: 'dragon', 1925: 'dragon', 2024: 'dragon', 2048: 'dragon',
    2146: 'dragon', 2163: 'dragon',
    # npc
    365: 'npc', 485: 'npc', 489: 'npc', 521: 'npc', 522: 'npc', 532: 'npc', 538: 'npc', 542: 'npc', 629: 'npc',
    640: 'npc', 655: 'npc', 670: 'npc', 683: 'npc', 723: 'npc', 755: 'npc', 1119: 'npc',
    # tech
    790: 'tech', 795: 'tech', 811: 'tech', 819: 'tech', 823: 'tech', 828: 'tech', 866: 'tech',
    987: 'tech', 1029: 'tech', 1036: 'tech', 1053: 'tech', 1078: 'tech', 1106: 'tech',
    # entity
    1148: 'entity', 1171: 'entity', 1182: 'entity', 1198: 'entity', 1224: 'entity', 1237: 'entity',
    1255: 'entity', 1268: 'entity', 1286: 'entity', 1304: 'entity',
    # main
    2368: 'main',
    # 删除
    104: 'DELETE',  # summonDragon 死函数
    1612: 'DELETE', # toggleSoulsCategory 引用未声明变量且无调用
    # 外置数据（已提取为 json）
    484: 'DELETE', 787: 'DELETE', 788: 'DELETE', 789: 'DELETE',
}

# 0-based 起点校验：块的第一行必须能对上
expect = {
    0: 'let chests', 23: 'async function fetchFilters', 104: 'async function summonDragon',
    484: 'var NPC_TYPES', 787: 'var TECH_TREE', 788: 'var TECH_INFO', 789: 'var TECH_ICON',
    2364: 'function esc(', 2368: 'async function init(',
}
for k, v in expect.items():
    assert lines[k].startswith(v), (k, lines[k][:50], v)

modules = {}
missing = []
for (i, end, first) in blocks:
    mod = assign.get(i)
    if mod is None and i < 23:
        mod = 'state'  # 顶部 let 状态声明区
    if mod is None:
        missing.append((i + 1, first[:70]))
        continue
    if mod == 'DELETE':
        continue
    modules.setdefault(mod, []).append(NL.join(lines[i:end]))

if missing:
    print('未分配模块:')
    for (ln, t) in missing:
        print('  line %d: %s' % (ln, t))
    raise SystemExit(1)

# ===== 3. 补丁 =====
# esc() 修复（verbatim 转义遗留 bug：/""/ 应为 /"/）
for mod in modules:
    for j, blk in enumerate(modules[mod]):
        if 'function esc(' in blk:
            modules[mod][j] = blk.replace("replace(/\"\"/g,'&quot;')", "replace(/\"/g,'&quot;')")
            print('esc() 已修复')

# state.js 头部注释 + 外置数据表声明
modules['state'] = [
    '// ===== 全局状态（所有面板模块共享） =====',
    NL.join(lines[0:23]),
    '''
// 外置数据表（/static/data/*.json，init 时加载）
var NPC_TYPES = {};
var TECH_TREE = [];
var TECH_INFO = {};
var TECH_ICON = {};'''
]

# main.js: 注入数据表加载
main_text = modules['main'][0]
main_text = main_text.replace("async function init() {\n  document.getElementById('btnRefresh')",
'''async function loadDataTables() {
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
  document.getElementById('btnRefresh')''')
modules['main'] = [main_text]

# ===== 4. 输出模块文件 =====
header = '// %s — 由 HtmlUI 单体拆分（原 app.js）\n'
order = ['state', 'ui', 'api', 'chest', 'dragon', 'npc', 'tech', 'entity', 'main']
for mod in order:
    body = ('\n\n'.join(modules[mod])).rstrip() + '\n'
    write_file('Web/Static/js/%s.js' % mod, (header % mod) + body)

# ===== 5. 更新 index.html script 引用 =====
idx = io.open('Web/Static/index.html', encoding='utf-8').read()
old_tag = '<script src="/static/js/app.js"></script>'
tags = NL.join('<script src="/static/js/%s.js"></script>' % m for m in order)
assert old_tag in idx
idx = idx.replace(old_tag, tags)
io.open('Web/Static/index.html', 'w', encoding='utf-8', newline=NL).write(idx)
print('index.html script 引用已更新')
