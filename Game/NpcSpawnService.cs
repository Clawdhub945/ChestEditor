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
/// · 各种族  = <c>CreateHireWorker(Point, race, count, ...) 造人 + 洗成居民</c>：
///             造完立即清 is_hire_worker（bool 单字节 0）+ leave_time_days=0（永久）+
///             transform.position 传送到参考点旁 → 与直造居民无差别。
///             ⚠ 不直调 CreateNpc：实参矩阵（含与 CreateElf 完全一致的实参）全部在方法体内
///             NullReferenceException —— runtime_invoke 调用上下文问题，游戏自己的包装器链没事。
///             雇工本身会无视 start_pos 从地图边缘走进来，所以传送是必需步骤。
/// · 士兵    = <c>NpcHelper.CreateSoldierBySummon(8参)</c>  游戏"召唤士兵"原装入口：
///             (type_id, 类型名, weapon, armor, shield, mount, Vector2 落点, exist_day_count)。
///             内部查 soldier_equip_dic 取种族/随机成年年龄，is_soldier_summon=1；
///             exist_day_count &lt;= 0 → leave_time_days=0 = 永久；weapon/armor/shield 传
///             装备表 id（405/407/408 段 stuff_id），0 = 默认（拳头/无）。
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
                if (Invoke(m, helper, new IntPtr(gx | (gy << 32))) != IntPtr.Zero) spawned++;
            }
            Plugin.LogInfo($"[NpcSpawn] {methodName} ×{spawned}（参考 {wx:F1},{wy:F1}）");
            return (spawned, wx, wy);
        }

        if (kind == "race")
        {
            if (raceId < 0 || raceId >= RaceNames.Length)
                throw new InvalidOperationException($"未知种族 raceId={raceId}");
            string raceName = RaceNames[raceId];

            // ===== 尝试 A：CreateNpc 直调（真居民 + 精确坐标）=====
            // 历史失败均在挂有 Harmony 捕获补丁的构建上（NRE 来自补丁桥嫌疑）；
            // 当前构建无补丁，重新验证。成功 = 最优解。
            IntPtr createNpc = FindMethodInHierarchy(helperClass, "CreateNpc", 15);
            if (createNpc != IntPtr.Zero)
            {
                int spawned = 0;
                for (int i = 0; i < count; i++)
                {
                    int familyGuid = NextGuid();
                    if (familyGuid == 0) break;
                    float px = wx + (float)(rnd.NextDouble() * 4.0 - 2.0);
                    float py = wy + (float)(rnd.NextDouble() * 4.0 - 2.0);
                    IntPtr vec = Marshal.AllocHGlobal(12);
                    try
                    {
                        Marshal.WriteInt32(vec, 0, BitConverter.SingleToInt32Bits(px));
                        Marshal.WriteInt32(vec, 4, BitConverter.SingleToInt32Bits(py));
                        Marshal.WriteInt32(vec, 8, 0);
                        if (Invoke(createNpc, helper, "召唤", raceName + "·" + seq(i), new RawArg(vec),
                            20 + rnd.Next(21), rnd.Next(2) == 0, familyGuid, true,
                            0, raceId, "npc", null, false, 0, 0, false) != IntPtr.Zero) spawned++;
                    }
                    finally { Marshal.FreeHGlobal(vec); }
                }
                if (spawned > 0)
                {
                    Plugin.LogInfo($"[NpcSpawn] 居民({raceName}) ×{spawned}（CreateNpc 直调，参考 {wx:F1},{wy:F1}）");
                    return (spawned, wx, wy);
                }
                Plugin.LogInfo("[NpcSpawn] CreateNpc 直调失败 → 回落雇工洗白");
            }

            // ===== 尝试 B：CreateHireWorker 造人 + 洗成居民 =====
            // 雇工会无视 start_pos 从地图边缘走进来（入口状态机每帧驱动），因此：
            // ① is_hire_worker=false ② leave_time_days=0 ③ 传送到参考点旁。
            // 传送可能与行走状态竞争（实测一次不生效）→ 传送后读回 position 验证并记录。
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
                MakeResident(npcPtr, wx, wy, i, rnd);
                spawned2++;
            }
            Plugin.LogInfo($"[NpcSpawn] 居民({raceName}) ×{spawned2}（参考 {wx:F1},{wy:F1}，格 {gx},{gy}；雇工洗白）");
            return (spawned2, wx, wy);
        }

        throw new InvalidOperationException($"未知 kind={kind}");
    }

    private static string seq(int i) => i.ToString();

    /// <summary>
    /// 雇工 → 居民：清 is_hire_worker（bool 单字节写 0）、leave_time_days=0（永久）、
    /// 传送到目标点旁并加入"钉住"队列（每帧 set_position 压过入场行走状态，
    /// 180 秒后释放——入场流程走完后居民就留在目标点生活）。
    /// </summary>
    private static void MakeResident(IntPtr npcPtr, float wx, float wy, int seq, Random rnd)
    {
        IntPtr npcClass = GetClass(npcPtr);
        foreach (var f in EnumerateFields(npcClass))
        {
            if (f.Name == "is_hire_worker") WriteIl2CppByte(npcPtr, f.Offset, 0);
            else if (f.Name == "leave_time_days") WriteIl2CppInt(npcPtr, f.Offset, 0);
        }
        try
        {
            IntPtr getGo = FindMethodInHierarchy(npcClass, "get_gameObject", 0);
            IntPtr go = getGo != IntPtr.Zero ? Invoke(getGo, npcPtr) : IntPtr.Zero;
            if (go == IntPtr.Zero) { Plugin.LogInfo("[NpcSpawn] 传送: get_gameObject 为空"); return; }
            IntPtr getTr = FindMethodInHierarchy(GetClass(go), "get_transform", 0);
            IntPtr tr = getTr != IntPtr.Zero ? Invoke(getTr, go) : IntPtr.Zero;
            if (tr == IntPtr.Zero) { Plugin.LogInfo("[NpcSpawn] 传送: get_transform 为空"); return; }
            IntPtr setPos = FindMethodInHierarchy(GetClass(tr), "set_position", 1);
            if (setPos == IntPtr.Zero) { Plugin.LogInfo("[NpcSpawn] 传送: set_position 未找到"); return; }
            float px = wx + (float)(rnd.NextDouble() * 4.0 - 2.0);
            float py = wy + (float)(rnd.NextDouble() * 4.0 - 2.0);
            _pinned.Add(new PinnedResident { Transform = tr, SetPos = setPos, X = px, Y = py,
                UntilMs = Environment.TickCount64 + 180_000 });
            TeleportNow(tr, setPos, px, py);
        }
        catch (Exception ex) { Plugin.LogInfo($"[NpcSpawn] 传送居民 {seq} 失败: {ex.Message}"); }
    }

    // ===== 位置钉住队列：入场行走状态每帧把雇工拖回边缘，逐帧 set_position 压过去 =====

    private sealed class PinnedResident { public IntPtr Transform; public IntPtr SetPos; public float X, Y; public long UntilMs; }
    private static readonly List<PinnedResident> _pinned = new();

    /// <summary>ChestEditorComponent.Update 每帧调用（主线程）。</summary>
    internal static void PumpPinnedResidents()
    {
        if (_pinned.Count == 0) return;
        long now = Environment.TickCount64;
        for (int i = _pinned.Count - 1; i >= 0; i--)
        {
            var p = _pinned[i];
            if (now >= p.UntilMs) { _pinned.RemoveAt(i); continue; }
            try { TeleportNow(p.Transform, p.SetPos, p.X, p.Y); }
            catch { _pinned.RemoveAt(i); }
        }
    }

    private static void TeleportNow(IntPtr tr, IntPtr setPos, float px, float py)
    {
        IntPtr vec = Marshal.AllocHGlobal(12);
        try
        {
            Marshal.WriteInt32(vec, 0, BitConverter.SingleToInt32Bits(px));
            Marshal.WriteInt32(vec, 4, BitConverter.SingleToInt32Bits(py));
            Marshal.WriteInt32(vec, 8, 0);
            Invoke(setPos, tr, new RawArg(vec));
        }
        finally { Marshal.FreeHGlobal(vec); }
    }

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

        IntPtr m = FindMethodInHierarchy(GetClass(helper), "CreateSoldierBySummon", 8);
        if (m == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper.CreateSoldierBySummon(8 参)");

        var (wx, wy) = PickReferencePosition();
        var rnd = new Random();
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            float px = wx + (float)(rnd.NextDouble() * 4.0 - 2.0);
            float py = wy + (float)(rnd.NextDouble() * 4.0 - 2.0);
            // Vector2 是 8 字节 struct：槽里塞 {x, y} 两个 float 的位模式（x 低 32 / y 高 32）
            long packed = (long)(uint)BitConverter.SingleToInt32Bits(px)
                        | ((long)(uint)BitConverter.SingleToInt32Bits(py) << 32);
            // exist_day_count = 0 → leave_time_days = 0 = 永久（>0 时 = Days+N 后离场）
            if (Invoke(m, helper, soldierTypeId, typeName, weaponId, armorId, shieldId,
                       0, packed, 0) != IntPtr.Zero) spawned++;
        }
        Plugin.LogInfo($"[NpcSpawn] 士兵({typeName}) ×{spawned} 武器{weaponId} 盔甲{armorId} 盾牌{shieldId}（参考 {wx:F1},{wy:F1}）");
        return (spawned, wx, wy);
    }

    private static int NextGuid()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return 0;
            IntPtr wPtr = GetIl2CppPtr(w);
            if (wPtr == IntPtr.Zero) return 0;
            IntPtr m = FindMethodInHierarchy(GetClass(wPtr), "NextGuid", 0);
            if (m == IntPtr.Zero) return 0;
            return InvokeInt(m, wPtr);
        }
        catch { return 0; }
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
}
