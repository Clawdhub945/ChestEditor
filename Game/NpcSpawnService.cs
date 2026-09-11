using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 召唤小人 / 召唤士兵 —— 走游戏内置"上帝模式"同款创建路径（伪 C 证实，code_meta.json）：
/// · 小精灵  = <c>NpcHelper.CreateElf(Point)</c>            游戏自己的小精灵入口（npc_type=23，
///             自带命名/随机成年年龄/离场天数）。
/// · 石头人  = <c>NpcHelper.CreateStoneMan(Point)</c>       同上（npc_type=30）。
/// · 各种族  = <c>CreateHireWorker(Point, race, count, 99999, ...)</c> 造人 + 洗成居民：
///             造完立即清 is_hire_worker（bool 单字节 0；字段在基类 BattleUnit，
///             须沿父类链查偏移）。不做传送/逐帧位置钉住——长期持 Transform 原始指针
///             在 NPC 死亡/回收后悬垂 → 原生 AV（多次闪退嫌疑）；雇工入场位置另行处理。
///             ⚠ 不直调 CreateNpc：实参矩阵（含与 CreateElf 完全一致的实参）全部在方法体内
///             NullReferenceException —— runtime_invoke 调用上下文问题，游戏自己的包装器链没事。
/// · 士兵    = <c>NpcHelper.CreateSoldierBySummon(8参)</c>  游戏"召唤士兵"原装入口：
///             (type_id, 类型名, weapon, armor, shield, mount, Vector2 落点, exist_day_count)。
///             内部查 soldier_equip_dic 取种族/随机成年年龄，is_soldier_summon=1；
///             exist_day_count 传 99999（leave=天数+99999 永不触发离场链；传 0 会得到
///             leave=0，游戏内跨天 DoOnNewDay 置 is_time_to_leave → 离场链原生 AV）；
///             weapon/armor/shield 传装备表 id（405/407/408 段 stuff_id），0 = 默认（拳头/无）。
/// 位置：借在场实体的世界坐标（与 AnimalService.Spawn 同一套合法性过滤）——
///       精灵/石头人反推格子（Point.ToVector3 = (gx+0.5, gy+0.5, 0)）。
/// ⚠ marshaling：Point/Vector2 是 8 字节 struct → 参数槽直塞 64 位打包值；
///   Vector3 是 12 字节 → 必须走 RawArg（非托管缓冲，槽装不下）。
/// </summary>
internal static class NpcSpawnService
{
    private static readonly string[] RaceNames =
        { "矮人", "蚁人", "鼠人", "猫人", "羊人", "狼人", "猪人", "精灵族", "三眼人", "蜥蜴人" };

    /// <summary>召唤小人。kind = "sprite" | "stoneman" | "race"（raceId 0..9）。</summary>
    internal static (int spawned, float x, float y) SpawnNpc(string kind, int raceId, int count)
    {
        IntPtr helper = GameChainLocator.GetNpcHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper（未进存档？）");
        IntPtr helperClass = GetClass(helper);
        count = Math.Clamp(count, 1, 10);

        var (wx, wy) = PickReferencePosition();
        var rnd = new Random();

        if (kind == "sprite" || kind == "stoneman")
        {
            string methodName = kind == "sprite" ? "CreateElf" : "CreateStoneMan";
            IntPtr m = FindMethodInHierarchy(helperClass, methodName, 1);
            if (m == IntPtr.Zero)
                throw new InvalidOperationException($"找不到 NpcHelper.{methodName}(Point)");
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // 借参考坐标反推格子（Point.ToVector3 = (gx+0.5, gy+0.5, 0)），±2 格落地
                int gx = (int)Math.Floor(wx - 0.5f) + rnd.Next(-2, 3);
                int gy = (int)Math.Floor(wy - 0.5f) + rnd.Next(-2, 3);
                // Point 是 struct：参数槽直塞 {gx, gy} 8 字节值（x 低 32 / y 高 32）
                IntPtr npc = Invoke(m, helper, new IntPtr(gx | (gy << 32)));
                if (npc != IntPtr.Zero) { spawned++; SanitizeNpcName(npc, kind == "sprite" ? "小精灵" : "石头人"); }
            }
            Plugin.LogInfo($"[NpcSpawn] {methodName} ×{spawned}（参考 {wx:F1},{wy:F1}）");
            return (spawned, wx, wy);
        }

        if (kind == "race")
        {
            if (raceId < 0 || raceId >= RaceNames.Length)
                throw new InvalidOperationException($"未知种族 raceId={raceId}");
            string raceName = RaceNames[raceId];

            // ⚠ 不再尝试 CreateNpc 直调（15 参）：实测每次必在方法体内抛 IL2CPP 异常
            //  （消息解不出），而且**失败路径会留下半成品对象/注册状态**——游戏随后
            //  在主 tick 处理这些残留 → 随机时刻原生 AV（实测：召唤后约 3 分钟崩，
            //  崩溃日志断点无任何前缀行）。这类"部分成功再抛异常"的调用一律禁止。
            //  正确路径：游戏自己的 CreateHireWorker（见下），一次成功、无残留。

            // ===== CreateHireWorker 造人 + 洗成居民（唯一的村民创建路径）=====
            // 雇工出生在 start_pos（该入口本身接受坐标），只需清掉 is_hire_worker 标记
            // 让它变成普通居民。⚠ 不做传送/逐帧钉位置（长持 Transform 指针 = 原生 AV），
            //   ⚠ 也不动 leave_time_days（CreateHireWorker 已写 天数+99999；清 0 会触发离场链 AV）。
            IntPtr hire = FindMethodInHierarchy(helperClass, "CreateHireWorker", 7);
            if (hire == IntPtr.Zero)
                throw new InvalidOperationException("找不到 NpcHelper.CreateHireWorker(7 参)");

            int gx = (int)Math.Floor(wx - 0.5f) + rnd.Next(-2, 3);
            int gy = (int)Math.Floor(wy - 0.5f) + rnd.Next(-2, 3);
            IntPtr listPtr = Invoke(hire, helper,
                new IntPtr(gx | (gy << 32)),   // Point 是 struct：槽直塞 8 字节
                raceId,
                count,
                99999,                         // 离场天数（下面会显式清 0）
                false,                         // is_educated
                0, 0);                         // tool / clothes = 无
            if (listPtr == IntPtr.Zero)
                throw new InvalidOperationException("CreateHireWorker 返回空");

            IntPtr cntM = FindMethodInHierarchy(GetClass(listPtr), "get_Count", 0);
            IntPtr itemM = FindMethodInHierarchy(GetClass(listPtr), "get_Item", 1);
            int made = cntM != IntPtr.Zero ? InvokeInt(cntM, listPtr) : 0;
            int spawned2 = 0;
            for (int i = 0; i < made; i++)
            {
                IntPtr npcPtr = itemM != IntPtr.Zero ? Invoke(itemM, listPtr, i) : IntPtr.Zero;
                if (npcPtr == IntPtr.Zero) continue;
                spawned2 += MakeResident(npcPtr);
            }
            Plugin.LogInfo($"[NpcSpawn] 居民({raceName}) ×{spawned2}（参考 {wx:F1},{wy:F1}，格 {gx},{gy}；雇工洗白）");
            return (spawned2, wx, wy);
        }

        throw new InvalidOperationException($"未知 kind={kind}");
    }

    private static string seq(int i) => i.ToString();

    /// <summary>
    /// 雇工 → 居民：清 is_hire_worker（bool 单字节写 0，字段在基类 BattleUnit，沿父类链查偏移）。
    /// ⚠ 不做传送/位置钉住：逐帧 set_position 需要长时间持有 Transform 原始指针，
    ///   NPC 在战斗中死亡/被回收后即悬垂 → 原生 AV（多次闪退嫌疑）。
    ///   位置问题（雇工从地图边缘入场）另行处理——玩家反馈"坐标先不理"。
    /// </summary>
    private static int MakeResident(IntPtr npcPtr)
    {
        IntPtr npcClass = GetClass(npcPtr);
        // 名字兜底：悬停显示时 TMPro 解析损坏名字会崩（见 SanitizeNpcName 注释）
        SanitizeNpcName(npcPtr, "新人");
        if (TryFindFieldOffset(npcClass, "is_hire_worker", out int offHire) && offHire > 0)
        {
            WriteIl2CppByte(npcPtr, offHire, 0);
            return 1;
        }
        Plugin.LogInfo("[NpcSpawn] 未找到 is_hire_worker 字段（雇工标记未清除）");
        return 0;
    }

    // ===== 召唤士兵离场时间修复（存量 + 兜底） =====
    // ⚠ 这里原有 FixSummonedLeaveTime 存量修复泵（每 5s 遍历实体快照的裸指针改 leave），已移除。
    // 原因（崩溃实锤）：EntityScan 快照里的 Ptr / GoRef 是**扫描瞬间**的缓存值，
    // NPC 阵亡或被销毁后即悬垂；GetClass(e.Ptr) / ReadIl2CppByte(e.Ptr,..) 走裸指针，
    // 绕过 Unity 的销毁保护（GoRef 访问会抛可捕获的托管异常，Ptr 直读则是裸 AV，catch 不住），
    // 于是每 5s 的一次全量遍历就成了随机时刻的原生崩溃点。
    // 新建士兵已在源头用 exist_day_count=99999 修好，无需存量泵；长周期写游戏对象一律禁止。

    /// <summary>召唤士兵（上帝模式同款：兵种 + 武器/盔甲/盾牌，0 = 默认）。</summary>
    internal static (int spawned, float x, float y) SpawnSoldier(
        int soldierTypeId, int weaponId, int armorId, int shieldId, int count)
    {
        IntPtr helper = GameChainLocator.GetNpcHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper（未进存档？）");
        count = Math.Clamp(count, 1, 10);

        string typeName = DataTables.SoldierTypeName(soldierTypeId);
        if (string.IsNullOrEmpty(typeName)) typeName = soldierTypeId.ToString();

        // ★ 名字必须用游戏自己的生成器造"人名"：
        //   CreateSoldierBySummon 的第 2 参会**直接写进 Npc.npc_name**（伪 C 与实测均证实），
        //   传兵种名会让士兵顶着职业名（用户实测："这些名字按职业来命名了"）。
        //   游戏正规招募入口 SoldierHelper.CreateSoldier 用的就是
        //   NpcNameGenerator.GetRandomFamilyName + GetName。
        string nameParam = GameChainLocator.GenerateNpcName(0) ?? typeName;
        Plugin.LogInfo($"[NpcSpawn] 人名生成: '{nameParam}'（兵种 {typeName}）");

        IntPtr m = FindMethodInHierarchy(GetClass(helper), "CreateSoldierBySummon", 8);
        if (m == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper.CreateSoldierBySummon(8 参)");

        var (wx, wy) = PickReferencePosition();
        var rnd = new Random();
        int spawned = 0;

        // ⚠⚠ 字符串参数必须登记 il2cpp GC 根，否则名字字段必成垃圾（崩溃根因）：
        //   CreateSoldierBySummon 的第 2 参（→ 游戏写进 Npc.npc_name）是托管字符串，
        //   经 StringToIl2Cpp 复制成 il2cpp 字符串后**只被 .NET 参数数组引用**；
        //   而 il2cpp 的 Boehm GC 不扫描 .NET 托管堆 —— CreateNpc 内部大量分配一旦触发 GC，
        //   这个字符串就被回收，游戏存进 npc_name 的就是悬垂指针（实测回读即乱码/含 \0）。
        //   后果：鼠标悬停 → Npc.OnPointerEnter → UI.ShowMouseTip(name) → TMPro 解析这个
        //   损坏字符串时 Array.Resize(newSize<0) → ArgumentOutOfRangeException → 原生 AV。
        //   ⚠ GC 根**故意不释放**：游戏对名字可能是"先存指针、稍后才规范化"，
        //   提前释放会让字符串在窗口期被回收（实测仍有部分 NPC 名字为空/乱码）。
        //   每次召唤泄漏一个短字符串，可忽略。
        for (int i = 0; i < count; i++)
        {
            // 每个单位单独取一个游戏生成的人名（与正规招募一致，避免全叫同一个名字）
            string nm = GameChainLocator.GenerateNpcName(0) ?? nameParam;
            IntPtr namePtr = StringToIl2Cpp(nm);
            GcHandleNew(namePtr, true);   // pinned：连地址都锁住，最长保护

            float px = wx + (float)(rnd.NextDouble() * 4.0 - 2.0);
            float py = wy + (float)(rnd.NextDouble() * 4.0 - 2.0);
            // Vector2 是 8 字节 struct：槽里塞 {x, y} 两个 float 的位模式（x 低 32 / y 高 32）
            long packed = (long)(uint)BitConverter.SingleToInt32Bits(px)
                        | ((long)(uint)BitConverter.SingleToInt32Bits(py) << 32);
            // exist_day_count 传 99999 → leave_time_days = 当前天数+99999，永不触发离场链。
            // ⚠ 传 0 会得到 leave_time_days=0 的士兵：is_soldier_summon=1 的 NPC 每日结算
            //   (NpcDoOnNewDay) 必进 Clock.Days > leave_time_days → is_time_to_leave=1 →
            //   离场处理链原生 AV。
            IntPtr npc = Invoke(m, helper, soldierTypeId, namePtr, weaponId, armorId, shieldId,
                       0, packed, 99999);
            if (npc != IntPtr.Zero) { spawned++; SanitizeNpcName(npc, nm); }
        }
        Plugin.LogInfo($"[NpcSpawn] 士兵({typeName}) ×{spawned} 武器{weaponId} 盔甲{armorId} 盾牌{shieldId}（参考 {wx:F1},{wy:F1}）");
        return (spawned, wx, wy);
    }

    /// <summary>
    /// 校验并修正 NPC 的显示名（npc_name）。
    /// 为什么必须做：鼠标悬停到 NPC 时游戏走
    /// `Npc.OnPointerEnter → UI.ShowMouseTip(npc名) → MyUtil.ForceRebuildLayoutImmediate
    ///  → TMPro.TMP_Text.ParseInputText`，若名字是空/含 NUL/未配对代理项，
    /// TMP 计算文本长度会得到负数 → `Array.Resize(newSize)` 抛
    /// ArgumentOutOfRangeException → 原生 AV（实测崩溃偏移固定 GameAssembly+0x445291）。
    /// 兜底：名字不合法就写入一个安全中文名（名字重复无副作用，优先保证不崩）。
    /// </summary>
    private static void SanitizeNpcName(IntPtr npcPtr, string fallback)
    {
        try
        {
            IntPtr cls = GetClass(npcPtr);
            if (!TryFindFieldOffset(cls, "npc_name", out int off) || off <= 0) return;
            string? nm = ReadIl2CppString(npcPtr, off);
            if (IsSafeDisplayName(nm)) return;

            IntPtr s = StringToIl2Cpp(fallback);
            GcHandleNew(s, true);              // 保护到写入完成（NPC 字段持有后即可达）
            WriteIl2CppPointer(npcPtr, off, s);
            Plugin.LogInfo($"[NpcSpawn] npc_name 不合法({Describe(nm)}) → 已修正为 '{fallback}'");
        }
        catch (Exception ex) { Plugin.LogInfo($"[NpcSpawn] SanitizeNpcName 失败: {ex.Message}"); }
    }

    /// <summary>名字是否能安全交给 TMPro 渲染：非空、无 NUL/控制字符、无未配对代理项、长度合理。</summary>
    private static bool IsSafeDisplayName(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > 24) return false;
        foreach (char c in s)
        {
            if (c == '\0' || char.IsSurrogate(c) || char.IsControl(c)) return false;
        }
        return true;
    }

    private static string Describe(string? s)
    {
        if (s == null) return "null";
        if (s.Length == 0) return "空串";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            if (sb.Length >= 12) { sb.Append("…"); break; }
            sb.Append(c < 0x20 || char.IsSurrogate(c) ? $"\\u{(int)c:X4}" : c.ToString());
        }
        return $"len={s.Length} '{sb}'";
    }

    /// <summary>
    /// 参考落点：从实体快照里挑一个坐标合法的在场实体（我方建筑/动物/NPC）。
    /// 与 AnimalService.Spawn 同一套过滤（±5000 之外是脏数据；全图随机会召唤到未加载区块）。
    /// </summary>
    private static (float x, float y) PickReferencePosition()
    {
        var candidates = new List<(float x, float y)>();
        int total = 0, skipNotMatch = 0, skipInactive = 0, skipCoord = 0, skipZero = 0, skipEx = 0, skipNoGo = 0;
        foreach (var e in EntityScan.Snapshot())
        {
            total++;
            if (e.GoRef == null) { skipNoGo++; continue; }
            string? cn = e.ClassName;
            bool okSrc = cn == "Animal" || cn == "Npc"
                || (cn != null && cn.IndexOf("Facility", StringComparison.Ordinal) == 0);
            if (!okSrc) { skipNotMatch++; continue; }
            try
            {
                if (!e.GoRef.activeInHierarchy) { skipInactive++; continue; }
                var p = e.GoRef.transform.position;
                if (p.x == 0 && p.y == 0) { skipZero++; continue; }
                if (Math.Abs(p.x) > 5000f || Math.Abs(p.y) > 5000f) { skipCoord++; continue; }
                candidates.Add((p.x, p.y));
            }
            catch { skipEx++; }
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"没有坐标合法的参考实体（实体 {total}：类型不符 {skipNotMatch}、未激活 {skipInactive}、" +
                $"坐标超限 {skipCoord}、零坐标 {skipZero}、异常 {skipEx}、无GO {skipNoGo}）——请先重新扫描");
        var rnd = new Random();
        var pick = candidates[rnd.Next(candidates.Count)];
        Plugin.LogInfo($"[NpcSpawn] 参考候选 {candidates.Count} 个，取 ({pick.x:F1},{pick.y:F1})");
        return pick;
    }

    /// <summary>
    /// 【诊断·临时】把实体世界坐标转成屏幕坐标 —— 供自动化把鼠标精确移到它上面
    /// （悬停复现用）。注意 Unity 屏幕坐标原点在左下，Win32 SetCursorPos 在左上，
    /// 调用方需 y = 屏幕高 - sy。
    /// </summary>
    internal static string GetScreenPos(int ptrHash)
    {
        UnityEngine.Transform? tr = null;
        foreach (var e in EntityScan.Snapshot())
            if (e.PtrHash == ptrHash) { tr = e.GoRef != null ? e.GoRef.transform : null; break; }
        if (tr == null) throw new InvalidOperationException($"未找到 ptrHash={ptrHash}（或对象已销毁）");
        var cam = UnityEngine.Camera.main;
        if (cam == null) throw new InvalidOperationException("Camera.main 为空");
        var wp = tr.position;
        var sp = cam.WorldToScreenPoint(wp);
        int h = UnityEngine.Screen.height;
        Plugin.LogInfo($"[ScreenPos] world=({wp.x:F2},{wp.y:F2},{wp.z:F2}) screen=({sp.x:F1},{sp.y:F1}) z={sp.z:F1} cam={cam.name} h={h} w={UnityEngine.Screen.width}");
        return JsonBuilder.Object(w =>
        {
            w.WriteNumber("sx", JsonBuilder.Safe(sp.x));
            w.WriteNumber("sy", JsonBuilder.Safe(sp.y));
            w.WriteNumber("z", JsonBuilder.Safe(sp.z));
            w.WriteNumber("screenH", h);
            w.WriteNumber("screenW", UnityEngine.Screen.width);
        });
    }

    /// <summary>
    /// 【诊断·临时】模拟游戏"鼠标悬停到该 NPC"时执行的读取路径
    /// （NpcInfoPanel.UpdateSoldierInfo 前半段：读装备字段 → IsSoldier → GetSoldierAttrDesc）。
    /// 每步调用**前**先写日志 —— 若崩溃，LogOutput.log 最后一行即崩点。定位完可删。
    /// </summary>
    internal static string ProbeHover(int ptrHash)
    {
        IntPtr npc = IntPtr.Zero;
        foreach (var e in EntityScan.Snapshot())
            if (e.PtrHash == ptrHash) { npc = e.Ptr; break; }
        if (npc == IntPtr.Zero) throw new InvalidOperationException($"未找到 ptrHash={ptrHash}");

        IntPtr cls = GetClass(npc);
        var sb = new System.Text.StringBuilder();
        sb.Append($"cls={(cls != IntPtr.Zero ? "ok" : "NULL")} ");
        TryFindFieldOffset(cls, "soldier_type_id", out int offSid);
        TryFindFieldOffset(cls, "weapon", out int offW);
        TryFindFieldOffset(cls, "armor", out int offA);
        TryFindFieldOffset(cls, "shield", out int offSh);
        TryFindFieldOffset(cls, "weapon_level_stuff_id", out int offWl);
        TryFindFieldOffset(cls, "armor_level_stuff_id", out int offAl);
        TryFindFieldOffset(cls, "shield_level_stuff_id", out int offSl);
        TryFindFieldOffset(cls, "npc_type", out int offTyp);
        if (offSid > 0) sb.Append($"sid={ReadIl2CppInt(npc, offSid)} ");
        if (offTyp > 0) sb.Append($"npc_type={ReadIl2CppInt(npc, offTyp)} ");
        if (offW > 0) sb.Append($"weapon={ReadIl2CppInt(npc, offW)} ");
        if (offA > 0) sb.Append($"armor={ReadIl2CppInt(npc, offA)} ");
        if (offSh > 0) sb.Append($"shield={ReadIl2CppInt(npc, offSh)} ");
        if (offWl > 0) sb.Append($"wl_stuff={ReadIl2CppInt(npc, offWl)} ");
        if (offAl > 0) sb.Append($"al_stuff={ReadIl2CppInt(npc, offAl)} ");
        if (offSl > 0) sb.Append($"sl_stuff={ReadIl2CppInt(npc, offSl)} ");
        Plugin.LogInfo($"[Probe] ①字段读取: {sb}");

        // 状态对象指针：若为 NULL，悬停时读状态名可能空/崩
        if (TryFindFieldOffset(cls, "_cur_state", out int offSt) && offSt > 0)
        {
            IntPtr st = ReadIl2CppPointer(npc, offSt);
            Plugin.LogInfo($"[Probe] ②状态指针 _cur_state = {(st != IntPtr.Zero ? "0x" + st.ToString("X") : "NULL")}");
        }
        else Plugin.LogInfo("[Probe] ②未找到 _cur_state 字段");

        IntPtr mIs = FindMethodInHierarchy(cls, "IsSoldier", 0);
        Plugin.LogInfo($"[Probe] ③准备调用 IsSoldier (方法={(mIs != IntPtr.Zero ? "ok" : "NULL")})");
        if (mIs != IntPtr.Zero)
            Plugin.LogInfo($"[Probe] ③IsSoldier 返回 {InvokeInt(mIs, npc)}");

        IntPtr mDesc = FindMethodInHierarchy(cls, "GetSoldierAttrDesc", 4);
        Plugin.LogInfo($"[Probe] ④准备调用 GetSoldierAttrDesc (方法={(mDesc != IntPtr.Zero ? "ok" : "NULL")})");
        if (mDesc != IntPtr.Zero)
        {
            IntPtr buf = Marshal.AllocHGlobal(32);
            try
            {
                for (int i = 0; i < 4; i++) Marshal.WriteIntPtr(buf, i * 8, IntPtr.Zero);
                Invoke(mDesc, npc, buf, buf + 8, buf + 16, buf + 24);
                Plugin.LogInfo($"[Probe] ④GetSoldierAttrDesc → hp='{ReadStringObject(Marshal.ReadIntPtr(buf))}' " +
                    $"weapon='{ReadStringObject(Marshal.ReadIntPtr(buf, 8))}' " +
                    $"armor='{ReadStringObject(Marshal.ReadIntPtr(buf, 16))}' " +
                    $"shield='{ReadStringObject(Marshal.ReadIntPtr(buf, 24))}'");
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        Plugin.LogInfo("[Probe] ⑤全部读取通过（未崩溃）");
        return JsonBuilder.Object(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteBoolean("allReadPassed", true);
        });
    }
}
