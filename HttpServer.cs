using System;
using System.Collections.Generic;
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
                SendHtml(resp, HtmlUI.Html);
            }
            else if (path == "/favicon.png" && method == "GET")
            {
                HandleFavicon(resp);
            }
            else if (path.StartsWith("/icon/") && method == "GET")
            {
                HandleIcon(resp, path);
            }
            else if (path.StartsWith("/api/dragon/icon/") && method == "GET")
            {
                HandleDragonIcon(resp, path);
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
            else if (path.StartsWith("/api/chest/") && path.EndsWith("/locate") && method == "POST")
            {
                var parts = path.Split('/');
                if (parts.Length < 5 || !int.TryParse(parts[3], out int ci))
                {
                    resp.StatusCode = 400; SendJson(resp, "{\"error\":\"invalid path\"}"); return;
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -5, ExtraIndex = ci, Signal = signal });
                SendJson(resp, signal.Wait(5000) ? "{\"ok\":true}" : "{\"error\":\"timeout\"}");
            }
            else if (path.StartsWith("/api/chest/") && path.EndsWith("/plan") && method == "POST")
            {
                // POST /api/chest/{index}/plan  body: {stuffId, count}  count<=0表示删除
                var parts = path.Split('/');
                if (parts.Length < 5 || !int.TryParse(parts[3], out int ci))
                {
                    resp.StatusCode = 400; SendJson(resp, "{\"error\":\"invalid path\"}"); return;
                }
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int sid = 0, cnt = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string val = kv[1].Trim().Trim('"');
                    if (key == "stuffId" && int.TryParse(val, out int v1)) sid = v1;
                    if (key == "count" && int.TryParse(val, out int v2)) cnt = v2;
                }
                var comp2 = ChestEditorComponent.Instance;
                if (comp2 == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal2 = new ManualResetEventSlim(false);
                comp2.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -4, ExtraIndex = ci, StuffId = sid, Count = cnt, IsAdd = cnt > 0, Signal = signal2
                });
                SendJson(resp, signal2.Wait(5000) ? "{\"ok\":true}" : "{\"error\":\"timeout\"}");
            }
            else if (path.StartsWith("/api/chest/") && method == "POST")
            {
                HandleChestAction(resp, path, req);
            }
            else if (path == "/api/dragon" && method == "GET")
            {
                var comp = ChestEditorComponent.Instance;
                SendJson(resp, comp?.DragonBagJson ?? "[]");
            }
            else if (path == "/api/dragon/set" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int sid = 0, cnt = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string val = kv[1].Trim().Trim('"');
                    if (key == "stuffId" && int.TryParse(val, out int v1)) sid = v1;
                    if (key == "count" && int.TryParse(val, out int v2)) cnt = v2;
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -6, ExtraIndex = sid, Count = cnt, Signal = signal
                });
                SendJson(resp, signal.Wait(5000) ? (comp.DragonBagJson ?? "{\"ok\":true}") : "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/dragon/summon" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int typeIdx = 0, level = 1;
                var natureIds = new System.Collections.Generic.List<int>();
                // 简单解析 JSON
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string val = kv[1].Trim().Trim('"');
                    if (key == "typeIndex" && int.TryParse(val, out int v1)) typeIdx = v1;
                    if (key == "level" && int.TryParse(val, out int v2)) level = v2;
                }
                // 解析 natures 数组: "natures":[1,2,3]
                int naturesStart = body.IndexOf("\"natures\"");
                if (naturesStart >= 0)
                {
                    int arrStart = body.IndexOf('[', naturesStart);
                    int arrEnd = body.IndexOf(']', arrStart);
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        var arrStr = body.Substring(arrStart + 1, arrEnd - arrStart - 1);
                        foreach (var item in arrStr.Split(','))
                        {
                            if (int.TryParse(item.Trim().Trim('"'), out int nid))
                                natureIds.Add(nid);
                        }
                    }
                }
                level = Math.Clamp(level, 1, 10);
                var types = Il2CppHelper.DragonTypes;
                if (typeIdx < 0 || typeIdx >= types.Length) { SendJson(resp, "{\"error\":\"invalid typeIndex\"}"); return; }
                int dragonStuffId = types[typeIdx].BaseId + level - 1;
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -7, ExtraIndex = dragonStuffId, NatureIds = natureIds.Count > 0 ? natureIds.ToArray() : null, Signal = signal
                });
                SendJson(resp, signal.Wait(5000) ? (comp.LastSummonResult ?? "{\"ok\":true}") : "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/dragon/types" && method == "GET")
            {
                SendJson(resp, Il2CppHelper.GetDragonTypesJson());
            }
            else if (path == "/api/dragon/souls" && method == "GET")
            {
                SendJson(resp, Il2CppHelper.GetDragonSoulsJson());
            }
            else if (path == "/api/dragon/soul/set" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int idx = 0, val = 0;
                string prop = "";
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "index" && int.TryParse(v, out int v1)) idx = v1;
                    if (key == "property") prop = v;
                    if (key == "value" && int.TryParse(v, out int v2)) val = v2;
                }
                if (string.IsNullOrEmpty(prop)) { SendJson(resp, "{\"error\":\"missing property\"}"); return; }
                string result = Il2CppHelper.SetDragonSoulProperty(idx, prop, val);
                SendJson(resp, result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}");
            }
            else if (path == "/api/dragon/searchmap" && method == "POST")
            {
                Il2CppHelper.SearchMapDragons();
                SendJson(resp, "{\"ok\":true}");
            }
            else if (path == "/api/dragon/searchmap2" && method == "POST")
            {
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -8, Signal = signal });
                SendJson(resp, signal.Wait(10000) ? "{\"ok\":true}" : "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/dragon/entities" && method == "GET")
            {
                Plugin.LogInfo("[HTTP] /api/dragon/entities 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -8, Signal = signal });
                if (signal.Wait(10000))
                {
                    Plugin.LogInfo($"[HTTP] entities 返回, 长度={comp.DragonEntitiesJson.Length}");
                    SendJson(resp, comp.DragonEntitiesJson);
                }
                else
                {
                    Plugin.LogError("[HTTP] entities 超时");
                    SendJson(resp, "{\"error\":\"timeout\"}");
                }
            }
            else if (path == "/api/dragon/entity/set" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int guid = 0;
                string field = "";
                float val = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "guid" && int.TryParse(v, out int g)) guid = g;
                    if (key == "field") field = v;
                    if (key == "value" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv)) val = fv;
                }
                if (guid == 0 || string.IsNullOrEmpty(field)) { SendJson(resp, "{\"error\":\"missing guid/field\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -9, ExtraIndex = guid,
                    Count = BitConverter.SingleToInt32Bits(val),
                    ResultJson = field, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npc/explore" && method == "POST")
            {
                // NPC 探索扫描（主线程执行）
                Plugin.LogInfo("[HTTP] /api/npc/explore 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -10, Signal = signal });
                if (signal.Wait(15000))
                    SendJson(resp, "{\"ok\":true,\"message\":\"扫描完成，请查看 BepInEx 控制台日志\"}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npc/scan" && method == "POST")
            {
                // NPC 战斗实体扫描（主线程执行）
                Plugin.LogInfo("[HTTP] /api/npc/scan 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -11, Signal = signal });
                if (signal.Wait(15000))
                    SendJson(resp, "{\"ok\":true,\"message\":\"扫描完成，请查看 BepInEx 控制台日志\"}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npc/entities" && method == "GET")
            {
                // 读取 NPC 实体
                Plugin.LogInfo("[HTTP] /api/npc/entities 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -12, Signal = signal });
                if (signal.Wait(10000))
                {
                    Plugin.LogInfo($"[HTTP] npc entities 返回, 长度={comp.NpcEntitiesJson.Length}");
                    SendJson(resp, comp.NpcEntitiesJson);
                }
                else
                {
                    Plugin.LogError("[HTTP] npc entities 超时");
                    SendJson(resp, "{\"error\":\"timeout\"}");
                }
            }
            else if (path == "/api/npc/faction" && method == "POST")
            {
                // 阵营扫描（主线程执行）
                Plugin.LogInfo("[HTTP] /api/npc/faction 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -14, Signal = signal });
                if (signal.Wait(30000))
                    SendJson(resp, "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npc/faction/entities" && method == "GET")
            {
                // 读取阵营扫描结果
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -15, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.FactionEntitiesJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/monster/names" && method == "GET")
            {
                SendJson(resp, MonsterNames.GetAllJson());
            }
            else if (path == "/api/npc/entity/set" && method == "POST")
            {
                // 设置 NPC 实体属性
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int guid = 0;
                string field = "";
                float val = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "guid" && int.TryParse(v, out int g)) guid = g;
                    if (key == "field") field = v;
                    if (key == "value" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv)) val = fv;
                }
                if (guid == 0 || string.IsNullOrEmpty(field)) { SendJson(resp, "{\"error\":\"missing guid/field\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -13, ExtraIndex = guid,
                    Count = BitConverter.SingleToInt32Bits(val),
                    ResultJson = field, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/entity/scan" && method == "POST")
            {
                // 实体扫描（主线程执行）
                Plugin.LogInfo("[HTTP] /api/entity/scan 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -16, Signal = signal });
                if (signal.Wait(30000))
                    SendJson(resp, "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/entity/entities" && method == "GET")
            {
                // 读取实体扫描结果
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -17, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.EntityScanJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/entity/set" && method == "POST")
            {
                // 设置实体属性
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int ptrHash = 0;
                string field = "";
                float val = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "ptrHash" && int.TryParse(v, out int g)) ptrHash = g;
                    if (key == "field") field = v;
                    if (key == "value" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv)) val = fv;
                }
                if (ptrHash == 0 || string.IsNullOrEmpty(field)) { SendJson(resp, "{\"error\":\"missing ptrHash/field\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -18, ExtraIndex = ptrHash,
                    Count = BitConverter.SingleToInt32Bits(val),
                    ResultJson = field, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npcfinder/scan" && method == "POST")
            {
                Plugin.LogInfo("[HTTP] /api/npcfinder/scan 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -19, Signal = signal });
                if (signal.Wait(30000))
                    SendJson(resp, "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npcfinder/npcs" && method == "GET")
            {
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -20, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.NpcFinderJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/npcfinder/set" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int ptrHash = 0;
                string field = "";
                float val = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "ptrHash" && int.TryParse(v, out int g)) ptrHash = g;
                    if (key == "field") field = v;
                    if (key == "value" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv)) val = fv;
                }
                if (ptrHash == 0 || string.IsNullOrEmpty(field)) { SendJson(resp, "{\"error\":\"missing ptrHash/field\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -21, ExtraIndex = ptrHash,
                    Count = BitConverter.SingleToInt32Bits(val),
                    ResultJson = field, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path.StartsWith("/api/entity/fields/") && method == "GET")
            {
                var parts = path.Split('/');
                if (parts.Length < 5 || !int.TryParse(parts[4], out int ph))
                {
                    resp.StatusCode = 400; SendJson(resp, "{\"error\":\"invalid ptrHash\"}"); return;
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -22, ExtraIndex = ph, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.EntityFieldsJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path.StartsWith("/api/npcfinder/fields/") && method == "GET")
            {
                var parts = path.Split('/');
                if (parts.Length < 5 || !int.TryParse(parts[4], out int ph))
                {
                    resp.StatusCode = 400; SendJson(resp, "{\"error\":\"invalid ptrHash\"}"); return;
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -23, ExtraIndex = ph, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.NpcFieldsJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/editor/scan" && method == "POST")
            {
                Plugin.LogInfo("[HTTP] /api/editor/scan 请求");
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -24, Signal = signal });
                if (signal.Wait(30000))
                    SendJson(resp, "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/editor/entities" && method == "GET")
            {
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -25, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.EntityEditorJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path.StartsWith("/api/editor/fields/") && method == "GET")
            {
                var parts = path.Split('/');
                if (parts.Length < 5 || !int.TryParse(parts[4], out int ph))
                {
                    resp.StatusCode = 400; SendJson(resp, "{\"error\":\"invalid ptrHash\"}"); return;
                }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                comp.WriteQueue.Enqueue(new ChestEditorComponent.WriteRequest { ChestIndex = -26, ExtraIndex = ph, Signal = signal });
                if (signal.Wait(10000))
                    SendJson(resp, comp.EntityEditorFieldsJson);
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/editor/set" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int ptrHash = 0;
                string field = "";
                float val = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "ptrHash" && int.TryParse(v, out int g)) ptrHash = g;
                    if (key == "field") field = v;
                    if (key == "value" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv)) val = fv;
                }
                if (ptrHash == 0 || string.IsNullOrEmpty(field)) { SendJson(resp, "{\"error\":\"missing ptrHash/field\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -27, ExtraIndex = ptrHash,
                    Count = BitConverter.SingleToInt32Bits(val),
                    ResultJson = field, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/editor/destroy" && method == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = reader.ReadToEnd();
                int ptrHash = 0;
                foreach (var part in body.Trim('{', '}').Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"');
                    string v = kv[1].Trim().Trim('"');
                    if (key == "ptrHash" && int.TryParse(v, out int g)) ptrHash = g;
                }
                if (ptrHash == 0) { SendJson(resp, "{\"error\":\"missing ptrHash\"}"); return; }
                var comp = ChestEditorComponent.Instance;
                if (comp == null) { SendJson(resp, "{\"error\":\"mod not ready\"}"); return; }
                var signal = new ManualResetEventSlim(false);
                var writeReq = new ChestEditorComponent.WriteRequest
                {
                    ChestIndex = -28, ExtraIndex = ptrHash, Signal = signal
                };
                comp.WriteQueue.Enqueue(writeReq);
                if (signal.Wait(5000))
                    SendJson(resp, writeReq.ResultJson ?? "{\"ok\":true}");
                else
                    SendJson(resp, "{\"error\":\"timeout\"}");
            }
            else if (path == "/api/dragon/natures" && method == "GET")
            {
                SendJson(resp, Il2CppHelper.GetDragonNaturesJson());
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

    private static readonly Dictionary<string, byte[]> _resourceCache = new();

    private static byte[]? GetEmbeddedResource(string name)
    {
        if (_resourceCache.TryGetValue(name, out var cached)) return cached;
        using var stream = typeof(HttpServer).Assembly.GetManifestResourceStream(name);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        _resourceCache[name] = bytes;
        return bytes;
    }

    private static void SendPng(HttpListenerResponse resp, byte[]? bytes)
    {
        if (bytes == null) { resp.StatusCode = 404; return; }
        resp.ContentType = "image/png";
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void HandleFavicon(HttpListenerResponse resp)
    {
        SendPng(resp, GetEmbeddedResource("ChestEditor.ic_app.png"));
    }

    private static void HandleIcon(HttpListenerResponse resp, string path)
    {
        // 路径: /icon/{stuffId}
        string idStr = path.Substring(6);
        if (!int.TryParse(idStr, out int stuffId)) { resp.StatusCode = 404; return; }
        SendPng(resp, GetEmbeddedResource($"ChestEditor.Icon.ui_{stuffId}.png"));
    }

    private static void HandleDragonIcon(HttpListenerResponse resp, string path)
    {
        // 路径: /api/dragon/icon/{index}  (index 0-15)
        string idStr = path.Substring("/api/dragon/icon/".Length);
        if (!int.TryParse(idStr, out int index) || index < 0 || index > 15) { resp.StatusCode = 404; return; }
        SendPng(resp, GetEmbeddedResource($"ChestEditor.Dragon.ic_dragon{index + 1}.png"));
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
}
