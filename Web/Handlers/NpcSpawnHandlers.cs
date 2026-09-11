namespace ChestEditor.Web;

/// <summary>
/// 召唤小人/士兵接口（盒子1「小人数值修改」顶部召唤区）。
/// 创建路径见 NpcSpawnService 注释（游戏内置"上帝模式"同款）。
/// 召唤成功后服务端直接把相机飞到落点（ChestService.LocateFacility），前端只需重扫刷新。
/// </summary>
internal static class NpcSpawnHandlers
{
    internal static void Register()
    {
        // 下拉数据：{races:[[id,name]], soldierTypes:[[typeId,name,weaponGroup,armorGroup,shieldGroup]],
        //           weapons:[[id,name,group]], armors, shields} —— spawn_tables.json 原样透传
        Router.Add("GET", "/api/npcspawn/options", _ => DataTables.SpawnTablesJson());

        // 【诊断·临时】POST /api/npcspawn/debug/hover/{ptrHash}
        // 模拟游戏"鼠标悬停到 NPC"时执行的读取路径（NpcInfoPanel.UpdateSoldierInfo 前半段），
        // 每步调用前先写日志 —— 若崩溃，LogOutput.log 最后一行就是崩点。定位完可删。
        Router.Add("POST", "/api/npcspawn/debug/hover/{ptrHash}", ctx =>
            MainThread.Run(() => Game.NpcSpawnService.ProbeHover(ctx.Int("ptrHash")), 20000));

        // 【诊断·临时】POST /api/npcspawn/debug/probe-soldier/{typeId}
        // 分步执行 SoldierHelper.CreateSoldier 内部逻辑，定位 KeyNotFoundException。
        Router.Add("POST", "/api/npcspawn/debug/probe-soldier/{typeId}", ctx =>
            MainThread.Run(() => Game.NpcSpawnService.ProbeSoldierChain(ctx.Int("typeId"), 0), 30000));

        // 【诊断·临时】GET /api/npcspawn/debug/screenpos/{ptrHash} → 实体屏幕坐标（自动化悬停用）
        Router.Add("GET", "/api/npcspawn/debug/screenpos/{ptrHash}", ctx =>
            MainThread.Run(() => Game.NpcSpawnService.GetScreenPos(ctx.Int("ptrHash")), 8000));

        // 【诊断·临时】POST /api/npcspawn/debug/ue-start → 激活 UnityExplorer 鼠标悬停检查器
        Router.Add("POST", "/api/npcspawn/debug/ue-start", _ => MainThread.Run(() =>
        {
            System.Reflection.Assembly? ue = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if ((a.GetName().Name ?? "").IndexOf("UnityExplorer", StringComparison.OrdinalIgnoreCase) >= 0) { ue = a; break; } }
                catch { }
            }
            if (ue == null) return JsonBuilder.Error("no UnityExplorer");
            Type? t = ue.GetType("UnityExplorer.Inspectors.MouseInspector");
            if (t == null) return JsonBuilder.Error("no MouseInspector type");
            const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            object? inst = t.GetProperty("Instance", BF)?.GetValue(null);
            if (inst == null) return JsonBuilder.Error("no MouseInspector instance");
            var sb = new System.Text.StringBuilder();
            // StartInspect(MouseInspectMode) —— 枚举参数，不能按 int 找
            try
            {
                var modeType = ue.GetType("UnityExplorer.Inspectors.MouseInspectMode");
                sb.Append("modeType=" + (modeType != null) + "; ");
                if (modeType != null)
                {
                    System.Reflection.MethodInfo? m = null;
                    foreach (var x in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                        if (x.Name == "StartInspect" && x.GetParameters().Length == 1) { m = x; break; }
                    if (m != null)
                    {
                        var p0 = m.GetParameters()[0].ParameterType;
                        object val = p0.IsEnum ? Enum.Parse(p0, "World") : (object)1;
                        m.Invoke(inst, new[] { val });
                        sb.Append("StartInspect(" + val + ") ok; ");
                    }
                    else sb.Append("no StartInspect; ");
                }
            }
            catch (Exception ex) { sb.Append("StartInspect err: " + ex.GetBaseException().Message + "; "); }
            try
            {
                var p = t.GetProperty("Inspecting", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                p?.SetValue(inst, true); sb.Append("Inspecting=true; ");
            }
            catch (Exception ex) { sb.Append("Inspecting err: " + ex.GetBaseException().Message + "; "); }
            return JsonBuilder.Object(w => w.WriteString("result", sb.ToString()));
        }, 10000));

        // 【诊断·临时】POST /api/npcspawn/debug/hook-ue → 运行中挂 UnityExplorer 悬停检查器钩子
        Router.Add("POST", "/api/npcspawn/debug/hook-ue", _ => MainThread.Run(
            () => JsonBuilder.Object(w => w.WriteString("result", Interop.HoverProbe.ApplyUnityExplorer())), 10000));

        // 【诊断·临时】GET /api/npcspawn/debug/ue → 列出 UnityExplorer 相关类（悬停检查器崩溃定位）
        Router.Add("GET", "/api/npcspawn/debug/ue", _ => MainThread.Run(() =>
        {
            var asmNames = new List<string>();
            var types = new List<string>();
            var methods = new List<string>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = a.GetName().Name ?? ""; } catch { continue; }
                if (an.IndexOf("UnityExplorer", StringComparison.OrdinalIgnoreCase) < 0) continue;
                asmNames.Add(an);
                try
                {
                    foreach (var t in a.GetTypes())
                    {
                        string tn = t.Name;
                        if (tn.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0
                         || tn.IndexOf("Inspect", StringComparison.OrdinalIgnoreCase) >= 0
                         || tn.IndexOf("Hover", StringComparison.OrdinalIgnoreCase) >= 0
                         || tn.IndexOf("Highl", StringComparison.OrdinalIgnoreCase) >= 0)
                            types.Add(t.FullName ?? tn);
                        if (tn.IndexOf("MouseInspect", StringComparison.OrdinalIgnoreCase) >= 0
                         || tn.IndexOf("InspectorManager", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            const System.Reflection.BindingFlags BF =
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                                | System.Reflection.BindingFlags.DeclaredOnly;
                            foreach (var m in t.GetMethods(BF))
                                methods.Add(tn + "." + m.Name + "/" + m.GetParameters().Length);
                        }
                    }
                }
                catch { }
            }
            return JsonBuilder.Object(w =>
            {
                w.WriteString("assemblies", string.Join(" | ", asmNames));
                w.WriteString("types", string.Join(" | ", types));
                w.WriteString("methods", string.Join(" | ", methods));
            });
        }, 15000));

        // POST /api/npcspawn/npc {kind:"sprite"|"stoneman"|"race", raceId:0..9, count:1..10}
        Router.Add("POST", "/api/npcspawn/npc", ctx =>
        {
            string kind = ctx.JsonStr("kind") ?? "";
            int raceId = ctx.JsonInt("raceId");
            int count = Math.Max(1, ctx.JsonInt("count"));
            if (string.IsNullOrEmpty(kind)) throw new HttpError(400, "missing kind");
            return MainThread.Run(() =>
            {
                var (spawned, x, y) = Game.NpcSpawnService.SpawnNpc(kind, raceId, count);
                if (spawned > 0) ChestService.LocateFacility(x, y);
                return JsonBuilder.Object(w =>
                {
                    w.WriteBoolean("ok", true);
                    w.WriteNumber("spawned", spawned);
                    w.WriteNumber("x", JsonBuilder.Safe(x));
                    w.WriteNumber("y", JsonBuilder.Safe(y));
                });
            }, 30000);
        });

        // POST /api/npcspawn/soldier {soldierTypeId, weaponId, armorId, shieldId, count}（装备 0 = 默认）
        Router.Add("POST", "/api/npcspawn/soldier", ctx =>
        {
            int soldierTypeId = ctx.JsonInt("soldierTypeId");
            int weaponId = ctx.JsonInt("weaponId");
            int armorId = ctx.JsonInt("armorId");
            int shieldId = ctx.JsonInt("shieldId");
            int count = Math.Max(1, ctx.JsonInt("count"));
            if (soldierTypeId <= 0) throw new HttpError(400, "missing soldierTypeId");
            return MainThread.Run(() =>
            {
                var (spawned, x, y) = Game.NpcSpawnService.SpawnSoldier(soldierTypeId, weaponId, armorId, shieldId, count);
                if (spawned > 0) ChestService.LocateFacility(x, y);
                return JsonBuilder.Object(w =>
                {
                    w.WriteBoolean("ok", true);
                    w.WriteNumber("spawned", spawned);
                    w.WriteNumber("x", JsonBuilder.Safe(x));
                    w.WriteNumber("y", JsonBuilder.Safe(y));
                });
            }, 30000);
        });
    }
}
