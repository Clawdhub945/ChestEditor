using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ChestEditor;

internal class HttpServer
{
    private HttpListener? _listener;
    private Thread? _thread;
    private volatile bool _running;
    private readonly int _port;

    public HttpServer(int port = 8765)
    {
        _port = port;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            Plugin.LogError($"HTTP 服务器启动失败: {ex.Message}");
            _running = false;
            return;
        }

        _thread = new Thread(ListenLoop) { IsBackground = true, Name = "ChestEditorHttp" };
        _thread.Start();
        Plugin.LogInfo($"HTTP 服务器已启动: http://localhost:{_port}/");
    }

    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _thread?.Join(2000); } catch { }
        Plugin.LogInfo("HTTP 服务器已停止");
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                var context = _listener!.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch
            {
                if (!_running) break;
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var resp = context.Response;

        try
        {
            string path = req.Url?.AbsolutePath ?? "/";
            string method = req.HttpMethod;

            if (path == "/" && method == "GET")
            {
                SendHtml(resp, HtmlUI);
            }
            else if (path.StartsWith("/icon/") && method == "GET")
            {
                HandleIcon(resp, path);
            }
            else if (path == "/api/chests" && method == "GET")
            {
                SendJson(resp, GetChestsJson());
            }
            else if (path == "/api/items" && method == "GET")
            {
                SendJson(resp, GetItemsJson());
            }
            else if (path == "/api/refresh" && method == "POST")
            {
                HandleRefresh(resp);
            }
            else if (path.StartsWith("/api/chest/") && method == "POST")
            {
                HandleChestAction(resp, path, req);
            }
            else
            {
                resp.StatusCode = 404;
                SendJson(resp, "{\"error\":\"not found\"}");
            }
        }
        catch (Exception ex)
        {
            try
            {
                resp.StatusCode = 500;
                SendJson(resp, $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}");
            }
            catch { }
        }
        finally
        {
            try { resp.Close(); } catch { }
        }
    }

    private static string GetChestsJson()
    {
        var comp = ChestEditorComponent.Instance;
        return comp?.ChestsJson ?? "[]";
    }

    private static string GetItemsJson()
    {
        var comp = ChestEditorComponent.Instance;
        return comp?.ItemsJson ?? "[]";
    }

    private static void HandleIcon(HttpListenerResponse resp, string path)
    {
        // 路径: /icon/{stuffId}
        string idStr = path.Substring(6);
        if (!int.TryParse(idStr, out int stuffId))
        {
            resp.StatusCode = 404;
            return;
        }

        string filePath = $@"C:\AI\img\Icon\ui_{stuffId}.png";
        if (!File.Exists(filePath))
        {
            resp.StatusCode = 404;
            return;
        }

        resp.ContentType = "image/png";
        var bytes = File.ReadAllBytes(filePath);
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void HandleRefresh(HttpListenerResponse resp)
    {
        var comp = ChestEditorComponent.Instance;
        if (comp == null)
        {
            SendJson(resp, "{\"error\":\"mod not ready\"}");
            return;
        }

        // 通过主线程队列执行刷新
        var signal = new ManualResetEventSlim(false);
        var writeReq = new ChestEditorComponent.WriteRequest
        {
            ChestIndex = -1, // 特殊值表示刷新操作
            StuffId = 0,
            Count = 0,
            IsAdd = true,
            Signal = signal
        };
        comp.WriteQueue.Enqueue(writeReq);

        if (signal.Wait(5000))
        {
            SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
        }
        else
        {
            resp.StatusCode = 408;
            SendJson(resp, "{\"error\":\"timeout\"}");
        }
    }

    private static void HandleChestAction(HttpListenerResponse resp, string path, HttpListenerRequest req)
    {
        var comp = ChestEditorComponent.Instance;
        if (comp == null)
        {
            SendJson(resp, "{\"error\":\"mod not ready\"}");
            return;
        }

        // 解析路径: /api/chest/{index}/add 或 /api/chest/{index}/remove
        var parts = path.Split('/');
        if (parts.Length < 5 || !int.TryParse(parts[3], out int chestIndex))
        {
            resp.StatusCode = 400;
            SendJson(resp, "{\"error\":\"invalid path\"}");
            return;
        }

        string action = parts[4];
        if (action != "add" && action != "remove")
        {
            resp.StatusCode = 400;
            SendJson(resp, "{\"error\":\"action must be add or remove\"}");
            return;
        }

        // 读取请求体
        string body;
        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            body = reader.ReadToEnd();

        int stuffId = 0, count = 1;
        // 简单 JSON 解析
        foreach (var part in body.Trim('{', '}').Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().Trim('"');
            string val = kv[1].Trim().Trim('"');
            if (key == "stuffId" && int.TryParse(val, out int sid)) stuffId = sid;
            if (key == "count" && int.TryParse(val, out int c)) count = c;
        }

        if (stuffId <= 0)
        {
            resp.StatusCode = 400;
            SendJson(resp, "{\"error\":\"invalid stuffId\"}");
            return;
        }

        // 通过主线程队列执行操作
        var signal = new ManualResetEventSlim(false);
        var writeReq = new ChestEditorComponent.WriteRequest
        {
            ChestIndex = chestIndex,
            StuffId = stuffId,
            Count = count,
            IsAdd = action == "add",
            Signal = signal
        };
        comp.WriteQueue.Enqueue(writeReq);

        // 等待主线程处理（最多 5 秒）
        if (signal.Wait(5000))
        {
            SendJson(resp, writeReq.ResultJson ?? "{\"error\":\"no result\"}");
        }
        else
        {
            resp.StatusCode = 408;
            SendJson(resp, "{\"error\":\"timeout\"}");
        }
    }

    private static void SendJson(HttpListenerResponse resp, string json)
    {
        resp.ContentType = "application/json; charset=utf-8";
        var buf = Encoding.UTF8.GetBytes(json);
        resp.ContentLength64 = buf.Length;
        resp.OutputStream.Write(buf, 0, buf.Length);
    }

    private static void SendHtml(HttpListenerResponse resp, string html)
    {
        resp.ContentType = "text/html; charset=utf-8";
        var buf = Encoding.UTF8.GetBytes(html);
        resp.ContentLength64 = buf.Length;
        resp.OutputStream.Write(buf, 0, buf.Length);
    }

    private const string HtmlUI = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>ChestEditor</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Segoe UI', sans-serif; background: #1a1a2e; color: #eee; min-height: 100vh; }
.header { background: #16213e; padding: 12px 20px; display: flex; align-items: center; gap: 12px; position: sticky; top: 0; z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,.3); }
.header h1 { font-size: 18px; color: #ffd700; }
.header button { padding: 6px 14px; border: none; border-radius: 4px; cursor: pointer; font-size: 13px; background: #0f3460; color: #eee; transition: all 0.2s; }
.header button:hover { background: #1a5276; transform: scale(1.05); }
.header button:active { transform: scale(0.95); }
.header button.loading { background: #555; cursor: wait; }
.header button.on { background: #27ae60; }
.header button.off { background: #666; }
.header .status { margin-left: auto; font-size: 12px; color: #888; }
.search-bar { padding: 10px 20px; background: #16213e; border-bottom: 1px solid #333; }
.search-bar input { width: 100%; padding: 8px 12px; border: 1px solid #444; border-radius: 4px; background: #0f3460; color: #eee; font-size: 14px; }
.chest-list { padding: 10px 20px; }
.chest { background: #16213e; border-radius: 6px; margin-bottom: 8px; overflow: hidden; }
.chest-header { padding: 10px 14px; cursor: pointer; display: flex; align-items: center; gap: 8px; }
.chest-header:hover { background: #1a3a5c; }
.chest-header .arrow { font-size: 12px; color: #888; width: 16px; }
.chest-header .name { font-size: 14px; color: #87ceeb; flex: 1; }
.chest-header .cap { font-size: 12px; color: #888; }
.chest-header .btn { padding: 4px 10px; border: none; border-radius: 3px; cursor: pointer; font-size: 12px; }
.chest-header .btn-add { background: #27ae60; color: #fff; }
.chest-header .btn-add:hover { background: #2ecc71; }
.chest-body { padding: 8px 14px; display: none; }
.chest-body.open { display: block; }
.items { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 6px; }
.item { background: #0f3460; border-radius: 4px; padding: 8px; text-align: center; }
.item img { width: 40px; height: 40px; image-rendering: pixelated; margin-bottom: 4px; }
.item img:not([src]) { display: none; }
.item .iname { font-size: 12px; color: #ccc; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.item .iid { font-size: 10px; color: #666; }
.item .icount { font-size: 14px; color: #ffd700; margin: 4px 0; }
.item .btns { display: flex; gap: 4px; justify-content: center; }
.item .btns button { padding: 2px 8px; border: none; border-radius: 3px; cursor: pointer; font-size: 11px; }
.btn-rm { background: #c0392b; color: #fff; }
.btn-rm:hover { background: #e74c3c; }
.btn-add { background: #27ae60; color: #fff; }
.btn-add:hover { background: #2ecc71; }
.modal { display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,.6); z-index: 20; justify-content: center; align-items: center; }
.modal.show { display: flex; }
.modal-content { background: #16213e; border-radius: 8px; width: 420px; max-height: 80vh; display: flex; flex-direction: column; }
.modal-header { padding: 12px 16px; border-bottom: 1px solid #333; display: flex; align-items: center; }
.modal-header h2 { font-size: 15px; color: #ffd700; flex: 1; }
.modal-header .close { background: none; border: none; color: #888; font-size: 20px; cursor: pointer; }
.modal-search { padding: 10px 16px; }
.modal-search input { width: 100%; padding: 6px 10px; border: 1px solid #444; border-radius: 4px; background: #0f3460; color: #eee; font-size: 13px; }
.modal-count { padding: 0 16px 10px; display: flex; align-items: center; gap: 6px; }
.modal-count input { width: 80px; padding: 4px 8px; border: 1px solid #444; border-radius: 4px; background: #0f3460; color: #eee; font-size: 13px; }
.modal-count button { padding: 4px 8px; border: none; border-radius: 3px; background: #0f3460; color: #eee; cursor: pointer; font-size: 12px; }
.modal-count button:hover { background: #1a5276; }
.modal-list { flex: 1; overflow-y: auto; padding: 0 16px 16px; }
.modal-item { display: flex; align-items: center; padding: 6px 8px; border-radius: 4px; margin-bottom: 4px; }
.modal-item:hover { background: #1a3a5c; }
.modal-item img { width: 24px; height: 24px; image-rendering: pixelated; margin-right: 6px; }
.modal-item .mi-name { flex: 1; font-size: 13px; }
.modal-item .mi-id { font-size: 11px; color: #666; margin-right: 8px; }
.toast { position: fixed; bottom: 20px; right: 20px; padding: 10px 20px; border-radius: 6px; font-size: 13px; z-index: 30; animation: fadeIn 0.3s; }
.toast.ok { background: #27ae60; color: #fff; }
.toast.err { background: #c0392b; color: #fff; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>
</head>
<body>
<div class=""header"">
  <h1>ChestEditor</h1>
  <button id=""btnRefresh"">刷新</button>
  <button id=""btnAuto"">自动刷新: <span id=""autoLabel"">开</span></button>
  <span class=""status"" id=""status"">就绪</span>
</div>
<div class=""search-bar""><input id=""search"" placeholder=""搜索箱子或物品..."" oninput=""render()""></div>
<div class=""chest-list"" id=""chestList""></div>

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

<script>
let chests = [];
let items = [];
let autoRefresh = true;
let addChestIndex = -1;
let openChests = new Set();

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

async function refreshChests() {
  const btn = document.getElementById('btnRefresh');
  try {
    btn.classList.add('loading');
    btn.textContent = '刷新中...';
    document.getElementById('status').textContent = '刷新中...';
    await fetch('/api/refresh', {method:'POST'});
    await fetchChests();
    render();
    document.getElementById('status').textContent = chests.length + ' 个箱子';
    btn.textContent = '刷新';
  } catch(e) {
    document.getElementById('status').textContent = '刷新失败: ' + e.message;
    btn.textContent = '刷新';
  } finally {
    btn.classList.remove('loading');
  }
}

function render() {
  const q = document.getElementById('search').value.toLowerCase();
  const el = document.getElementById('chestList');
  let html = '';
  for (let i = 0; i < chests.length; i++) {
    const c = chests[i];
    if (q) {
      const match = c.name.toLowerCase().includes(q) || c.items.some(it => it.name.toLowerCase().includes(q));
      if (!match) continue;
    }
    const cap = c.maxCap > 0 ? c.usedCap + '/' + c.maxCap : c.items.length + '';
    const isOpen = openChests.has(c.guid);
    html += '<div class=""chest"">';
    html += '<div class=""chest-header"" onclick=""toggle(' + c.guid + ')"">';
    html += '<span class=""arrow"">' + (isOpen ? '&#9660;' : '&#9654;') + '</span>';
    html += '<span class=""name"">' + esc(c.name) + '</span>';
    html += '<span class=""cap"">' + cap + '</span>';
    html += '<button class=""btn btn-add"" onclick=""event.stopPropagation();openAddModal(' + i + ')"">+添加</button>';
    html += '</div>';
    html += '<div class=""chest-body' + (isOpen ? ' open' : '') + '"">';
    if (c.items.length === 0) {
      html += '<div style=""color:#666;padding:8px"">(空)</div>';
    } else {
      html += '<div class=""items"">';
      for (const it of c.items) {
        if (q && !it.name.toLowerCase().includes(q)) continue;
        html += '<div class=""item"">';
        html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
        html += '<div class=""iname"" title=""' + esc(it.name) + '"">' + esc(it.name) + '</div>';
        html += '<div class=""iid"">ID:' + it.stuffId + '</div>';
        html += '<div class=""icount"">x' + it.count + '</div>';
        html += '<div class=""btns"">';
        html += '<button class=""btn-rm"" onclick=""doRemove(' + i + ',' + it.stuffId + ',1)"">-1</button>';
        html += '<button class=""btn-rm"" onclick=""doRemove(' + i + ',' + it.stuffId + ',' + it.count + ')"">-All</button>';
        html += '</div></div>';
      }
      html += '</div>';
    }
    html += '</div></div>';
  }
  if (!chests.length) html = '<div style=""color:#666;padding:20px"">无箱子数据，请先在游戏中打开存档</div>';
  el.innerHTML = html;
}

function toggle(i) {
  if (openChests.has(i)) {
    openChests.delete(i);
  } else {
    openChests.add(i);
  }
  render();
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
    render();
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
    html += '<div class=""modal-item"">';
    html += '<img src=""/icon/' + it.stuffId + '"" onerror=""hideImg(this)"">';
    html += '<span class=""mi-name"">' + esc(it.name) + '</span>';
    html += '<span class=""mi-id"">ID:' + it.stuffId + '</span>';
    html += '<button class=""btn-add"" onclick=""doAdd(' + it.stuffId + ')"">添加</button>';
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
    render();
    toast('添加成功');
  } catch(e) { toast('操作失败', true); }
}

function addN(n) {
  document.getElementById('addCount').value = n;
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
  await refreshChests();
  setInterval(async () => {
    if (!autoRefresh) return;
    await fetchChests();
    render();
  }, 3000);
}

init();
</script>
</body>
</html>";
}
