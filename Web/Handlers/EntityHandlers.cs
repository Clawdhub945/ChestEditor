using System.Text.Json.Nodes;
using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>统一实体编辑器接口：扫描、字段读写、销毁、定位、方法枚举</summary>
internal static class EntityHandlers
{
    /// <summary>扫描每帧处理的 GameObject 个数（分片：全量扫描要几秒，单帧做完会明显卡死）</summary>
    private const int ScanObjectsPerFrame = 150;

    /// <summary>
    /// 单个主线程任务里最多花多少毫秒做销毁/写值 —— 超出的留给下一帧。
    /// <para>销毁单个单位本身很便宜，但"一次几十上百个"堆在同一帧照样会顿一下；
    /// 按时间预算摊到多帧后，可以放心把每批数量调大（少几次 HTTP 往返）。</para>
    /// </summary>
    private const int FrameBudgetMs = 4;

    internal static void Register()
    {
        // 游戏/存档状态：网页端据此判断"现在能不能开安全发展模式"
        Router.Add("GET", "/api/editor/state", _ => MainThread.Run(() => JsonBuilder.Object(w =>
        {
            w.WriteBoolean("inSave", GameChainLocator.IsWorldReady());   // 能定位到 AreaMap 才算进了世界
            w.WriteBoolean("loading", Core.AppState.Loading);             // 正在读档
            w.WriteNumber("saveLoads", Core.AppState.SaveLoads);          // 读档成功次数（变了 = 读过档）
            w.WriteBoolean("scanning", EntityScan.IsScanning);            // 正在全量扫描
        }), 10000));

        // 全量扫描：分片推进（每帧一批），扫完再在主线程上应用待写入的修改。
        // ⚠⚠ 开扫那一步（BeginScan → Resources.FindObjectsOfTypeAll）**必须跑在主线程**上：
        // 之前直接写在请求处理体里（HTTP 线程池线程）→ AccessViolationException → 游戏闪退。
        // 现在把 BeginScan 放进分片任务的第一拍（分片任务由主线程 Pump 驱动）。
        Router.Add("POST", "/api/editor/scan", _ =>
        {
            bool begun = false;
            MainThread.RunPaced(() =>
            {
                if (!begun) { begun = true; EntityScan.BeginScan(); }
                return EntityScan.StepScan(ScanObjectsPerFrame);
            }, 120000);
            return MainThread.Run(() =>
            {
                ModificationStore.ApplyPendingModifications();
                return JsonBuilder.Ok();
            }, 30000);
        });

        Router.Add("GET", "/api/editor/entities", _ =>
            MainThread.Run(() => EntityScan.GetAllJson(), 10000));

        Router.Add("GET", "/api/editor/fields/{ptrHash}", ctx =>
            MainThread.Run(() => EntityScan.GetFieldsJson(ctx.Int("ptrHash")), 10000));

        Router.Add("POST", "/api/editor/set", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            string? field = ctx.JsonStr("field");
            float value = ctx.JsonFloat("value");
            if (ptrHash == 0 || string.IsNullOrEmpty(field)) throw new HttpError(400, "missing ptrHash/field");
            return MainThread.Run(() =>
            {
                string result = EntityScan.SetField(ptrHash, field, value);
                return result == "ok" ? JsonBuilder.Ok() : JsonBuilder.Error(result);
            });
        });

        Router.Add("POST", "/api/editor/destroy", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            return MainThread.Run(() =>
            {
                string result = EntityDestroyer.DestroyEntity(ptrHash);
                return result == "ok" ? JsonBuilder.Ok() : JsonBuilder.Error(result);
            });
        });

        // 批量销毁（一键清除/安全发展模式用）；按帧时间预算分片执行，不占用单帧太久
        Router.Add("POST", "/api/editor/destroy/batch", ctx =>
        {
            var arr = ctx.Json?["ptrHashes"] as System.Text.Json.Nodes.JsonArray;
            if (arr == null || arr.Count == 0) throw new HttpError(400, "missing ptrHashes");
            var hashes = new List<int>();
            foreach (var n in arr)
                if (n != null) hashes.Add(n.GetValue<int>());

            int ok = 0, fail = 0, i = 0, frames = 0;
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            MainThread.RunPaced(() =>
            {
                frames++;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (i < hashes.Count && sw.ElapsedMilliseconds < FrameBudgetMs)
                {
                    if (EntityDestroyer.DestroyEntity(hashes[i++]) == "ok") ok++; else fail++;
                }
                return i < hashes.Count;   // true = 还没做完，下帧继续
            }, 120000);
            Plugin.LogInfo($"[EntityDestroyer] 批量销毁: {ok} 成功 / {fail} 失败 / 共 {hashes.Count} 个, "
                + $"分 {frames} 帧, 墙钟 {swTotal.ElapsedMilliseconds}ms（其中主线程占用已按帧摊开）");
            // 顺带把游戏状态带回给网页端：安全发展模式靠它发现"读档了/退出存档了"并自动关闭
            return JsonBuilder.Object(w =>
            {
                w.WriteBoolean("ok", true);
                w.WriteNumber("destroyed", ok);
                w.WriteNumber("failed", fail);
                w.WriteBoolean("inSave", GameChainLocator.IsWorldReady());
                w.WriteBoolean("loading", Core.AppState.Loading);
                w.WriteNumber("saveLoads", Core.AppState.SaveLoads);
            });
        });

        // 战斗力批量缩放（×10 / ÷10；一次主线程任务循环执行，界面上的「战斗力×10 / ÷10」用）
        Router.Add("POST", "/api/editor/scale/batch", ctx =>
        {
            var arr = ctx.Json?["ptrHashes"] as System.Text.Json.Nodes.JsonArray;
            if (arr == null || arr.Count == 0) throw new HttpError(400, "missing ptrHashes");
            float factor = ctx.JsonFloat("factor");
            if (factor <= 0) throw new HttpError(400, "invalid factor");
            var hashes = new List<int>();
            foreach (var n in arr)
                if (n != null) hashes.Add(n.GetValue<int>());
            int entities = 0, fields = 0, i = 0, frames = 0;
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            MainThread.RunPaced(() =>
            {
                frames++;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (i < hashes.Count && sw.ElapsedMilliseconds < FrameBudgetMs)
                {
                    int n = EntityScan.ScaleCombatStats(hashes[i++], factor);
                    if (n > 0) { entities++; fields += n; }
                }
                return i < hashes.Count;
            }, 60000);
            Plugin.LogInfo($"[EntityEditor] 战斗力缩放 x{factor}: {entities}/{hashes.Count} 个实体, {fields} 个字段, "
                + $"分 {frames} 帧, 墙钟 {swAll.ElapsedMilliseconds}ms");
            return JsonBuilder.Object(w =>
            {
                w.WriteBoolean("ok", true);
                w.WriteNumber("entities", entities);
                w.WriteNumber("fields", fields);
            });
        });

        Router.Add("POST", "/api/editor/locate", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            var posJson = MainThread.Run(() => EntityScan.GetEntityPositionJson(ptrHash));
            float px = 0, py = 0;
            bool parsed = false;
            try
            {
                var node = JsonNode.Parse(posJson);
                if (node?["x"] != null && node["y"] != null)
                {
                    px = node["x"]!.GetValue<float>();
                    py = node["y"]!.GetValue<float>();
                    parsed = true;
                }
            }
            catch { }

            if (!parsed) return posJson;

            MainThread.Run(() => ChestService.LocateFacility(px, py), 3000);
            return JsonBuilder.Object(w => { w.WriteBoolean("ok", true); w.WriteNumber("x", JsonBuilder.Safe(px)); w.WriteNumber("y", JsonBuilder.Safe(py)); });
        });

        Router.Add("POST", "/api/editor/listmethods", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            return MainThread.Run(() =>
            {
                string methods = EntityScan.ListMethods(ptrHash);
                Plugin.LogInfo($"[EntityEditor] ListMethods ptrHash={ptrHash}:\n{methods}");
                return JsonBuilder.Object(w => w.WriteString("methods", methods));
            }, 10000);
        });
    }
}
