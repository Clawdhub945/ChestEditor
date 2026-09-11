using System;
using System.Collections.Generic;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 召唤小人 / 召唤士兵 —— 走游戏内置"上帝模式"同款创建路径（伪 C 证实，code_meta.json）：
/// · 小精灵  = <c>NpcHelper.CreateElf(Point)</c>            游戏自己的小精灵入口（npc_type=23，
///             内部 Point→Vector3 调 CreateNpc；自带命名/随机成年年龄/离场天数）。
/// · 石头人  = <c>NpcHelper.CreateStoneMan(Point)</c>       同上（npc_type=30）。
/// · 各种族  = <c>NpcHelper.CreateHireWorker(Point, race_id, count, leaveDays, ...)</c>
///             游戏自己的"各种族批量雇工"入口（伪 C 证实：内部随机姓名/成年年龄/性别，
///             npc_type=0 杂工；leave_time_days 给 99999 = 实际永久）。
///             ⚠ 不直调 CreateNpc：prefab/family_guid 语义猜不准，实测抛 IL2CPP 异常
///             （士兵路径能过是用了 soldier_equip_dic 里的官方 prefab 字段）。
/// · 士兵    = <c>NpcHelper.CreateSoldierBySummon(8参)</c>  游戏"召唤士兵"原装入口：
///             (type_id, 类型名, weapon, armor, shield, mount, Vector2 落点, exist_day_count)。
///             内部查 soldier_equip_dic 取种族/随机成年年龄，is_soldier_summon=1；
///             exist_day_count &lt;= 0 → leave_time_days=0 = 永久；weapon/armor/shield 传
///             装备表 id（405/407/408 段 stuff_id），0 = 默认（拳头/无）。
/// 位置：借在场实体的世界坐标（与 AnimalService.Spawn 同一套合法性过滤）——
///       精灵/石头人反推格子（Point.ToVector3 = (gx+0.5, gy+0.5, 0)），
///       村民/士兵直接用世界坐标（CreateNpc 只吃 Vector3，z 置 0）。
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

            // ⚠ 不直调 CreateNpc：prefab 字面量/family_guid 语义猜不准（实测抛 IL2CPP 异常，
            //   士兵路径能过是因为用 soldier_equip_dic 里的官方 prefab 字段）。
            // 改走游戏自己的"雇工"批量入口 CreateHireWorker(Point, race_id, hire_count,
            //   leave_time_days, is_educated, tool_stuff_id, clothes_stuff_id)（伪 C 证实：
            //   内部 NpcNameGenerator 随机姓名 + RandomAdultAgeOfHire + npc_type=0 杂工），
            //   leave_time_days 给 99999 = 实际永久。
            IntPtr hire = FindMethodInHierarchy(helperClass, "CreateHireWorker", 7);
            if (hire == IntPtr.Zero)
                throw new InvalidOperationException("找不到 NpcHelper.CreateHireWorker(7 参)");

            int gx = (int)Math.Floor(wx - 0.5f) + rnd.Next(-2, 3);
            int gy = (int)Math.Floor(wy - 0.5f) + rnd.Next(-2, 3);
            IntPtr listPtr = Invoke(hire, helper,
                new IntPtr(gx | (gy << 32)),   // Point 是 struct：槽直塞 8 字节
                raceId,
                count,
                99999,                         // 离场天数（游戏会写 leave_time_days = Days + N）
                false,                         // is_educated
                0, 0);                         // tool / clothes = 无
            int spawned = 0;
            if (listPtr != IntPtr.Zero)
            {
                IntPtr cntM = FindMethodInHierarchy(GetClass(listPtr), "get_Count", 0);
                if (cntM != IntPtr.Zero) spawned = InvokeInt(cntM, listPtr);
            }
            Plugin.LogInfo($"[NpcSpawn] 雇工村民({raceName}) ×{spawned}（参考 {wx:F1},{wy:F1}，格 {gx},{gy}）");
            return (spawned, wx, wy);
        }

        throw new InvalidOperationException($"未知 kind={kind}");
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
