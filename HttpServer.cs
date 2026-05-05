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
            else if (path == "/api/filters" && method == "GET")
            {
                var inst = ChestEditorComponent.Instance;
                SendJson(resp, inst != null ? ChestEditorComponent.GetFiltersJson() : "[]");
            }
            else if (path == "/api/filters/toggle" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int sid = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Trim('"') == "stuffId")
                        int.TryParse(kv[1].Trim().Trim('"'), out sid);
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -2, StuffId = sid, Signal = signal });
                SendJson(resp, signal.Wait(5000) ? "{\"ok\":true}" : "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/filters/all" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                bool enabled = body.Contains("true");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -3, IsAdd = enabled, Signal = signal });
                SendJson(resp, signal.Wait(5000) ? "{\"ok\":true}" : "{\"error\":\"timeout\"}");
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
}

.item .iid {
  font-size: 10px;
  color: var(--text-muted);
  margin-bottom: 8px;
}

.item .icount {
  font-size: 18px;
  font-weight: 600;
  color: var(--accent-light);
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
  <h1>ChestEditor</h1>
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

<script>
let chests = [];
let items = [];
let autoRefresh = true;
let selectedChest = -1;
let searchQuery = '';
let categoryOpen = true;
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

async function refreshChests() {
  const btn = document.getElementById('btnRefresh');
  try {
    btn.classList.add('loading');
    btn.textContent = '刷新中...';
    document.getElementById('status').textContent = '刷新中...';
    await fetch('/api/refresh', {method:'POST'});
    await fetchFilters();
    await fetchChests();
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

  html += '</div></div>';

  if (!chests.length) {
    html = '<div style=""padding:20px;text-align:center;color:var(--text-muted)"">暂无箱子</div>';
  }
  el.innerHTML = html;
}

function toggleCategory() {
  categoryOpen = !categoryOpen;
  renderSidebar();
}

function selectChest(i) {
  selectedChest = i;
  renderSidebar();
  renderContent();
}

function renderContent() {
  const el = document.getElementById('content');
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
      html += '<div class=""icount"">x' + it.count + '</div>';
      html += '<div class=""btns"">';
      html += '<button class=""btn-rm"" onclick=""doRemove(' + selectedChest + ',' + it.stuffId + ',1)"">-1</button>';
      html += '<button class=""btn-rm"" onclick=""doRemove(' + selectedChest + ',' + it.stuffId + ',' + it.count + ')"">-All</button>';
      html += '</div></div>';
    }
    html += '</div>';
  }

  el.innerHTML = html;
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
  await fetchFilters();
  await refreshChests();
  renderSidebar();
  renderContent();
  setInterval(async () => {
    if (!autoRefresh) return;
    await fetchChests();
    renderSidebar();
    if (selectedChest >= 0) renderContent();
  }, 3000);
}

init();
</script>
</body>
</html>";
}
