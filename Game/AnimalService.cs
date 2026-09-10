using System;
using System.Collections.Generic;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;

namespace ChestEditor.Game;

/// <summary>
/// 动物（盒子4）删除：两类对象各走游戏自己的路径，源码核实过副作用。
/// <para>
/// · 活体（Animal）→ <c>territory.animal_helper.DestroyAnimal(animal)</c>（Animal.OnBeDestroy
///   就是这么调的；伪 C 证实：guid 注册表 Remove + animal_list Remove + ChangeState(Dead) +
///   DestroySelf —— 静默移除，不掉肉、不生成尸体）。
///   ⚠ 不能用通用兜底：只会碰运气调方法，不清 AnimalHelper 的注册表。
/// · 尸体（AnimalDeadBody）→ <c>MapStuffHelper.DestroyElement(guid)</c>（伪 C 证实对
///   AnimalDeadBody 有特判：animal_dead_body_dic_by_pos + map_element_list/dic 移除 + DestroySelf）。
/// </para>
/// </summary>
internal static class AnimalService
{
    private static IntPtr _animalHelper, _destroyAnimal;    // AnimalHelper.DestroyAnimal(1p)
    private static IntPtr _mapStuffHelper, _destroyElement; // MapStuffHelper.DestroyElement(1p)

    /// <summary>解析两个 helper 与删除方法。主线程调用，一个批次开头调一次。</summary>
    internal static void Resolve()
    {
        _animalHelper = IntPtr.Zero; _destroyAnimal = IntPtr.Zero;
        _mapStuffHelper = IntPtr.Zero; _destroyElement = IntPtr.Zero;

        _animalHelper = GameChainLocator.GetAnimalHelper();
        Plugin.LogVerbose($"[AnimalService] Resolve: animalHelper={_animalHelper.ToInt64():X}");
        if (_animalHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper（未进存档？）");
        _destroyAnimal = FindMethodInHierarchy(GetClass(_animalHelper), "DestroyAnimal", 1);
        Plugin.LogVerbose($"[AnimalService] Resolve: DestroyAnimal(1p)={_destroyAnimal.ToInt64():X}");
        if (_destroyAnimal == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper.DestroyAnimal(animal)");

        _mapStuffHelper = GameChainLocator.GetMapStuffHelper();
        if (_mapStuffHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper（未进存档？）");
        _destroyElement = FindMethodInHierarchy(GetClass(_mapStuffHelper), "DestroyElement", 1);
        Plugin.LogVerbose($"[AnimalService] Resolve: mapStuffHelper={_mapStuffHelper.ToInt64():X}, DestroyElement(1p)={_destroyElement.ToInt64():X}");
        if (_destroyElement == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper.DestroyElement(guid)");
    }

    /// <summary>
    /// 删一只/一具。<see cref="Resolve"/> 必须已成功。
    /// 按 className 分派：Animal → DestroyAnimal(ptr)；AnimalDeadBody → DestroyElement(guid)。
    /// <para>⚠ 每步 LogInfo：闪退（native 崩溃没有托管异常）时，日志的最后一条就是崩点。</para>
    /// </summary>
    /// <summary>删一只/一具（mode 诊断分派）。</summary>
    internal static bool DestroyOne(EntityScan.EditorEntity e, string mode)
    {
        Plugin.LogVerbose($"[AnimalService] DestroyOne({mode}): {e.ClassName} ptrHash={e.PtrHash} ptr={e.Ptr.ToInt64():X} guid={e.Guid}");
        // ⚠ 防护：GO 未激活的对象（远处幽灵/池化残留）DestroySelf 会访问未创建的渲染组件
        // → native 崩溃。跳过不删（这类对象建议用"重新读档"让游戏自己清理，或移动到其所在区域后再删）。
        if (e.GoRef != null)
        {
            try
            {
                bool active = e.GoRef.activeInHierarchy;
                if (!active) { Plugin.LogVerbose($"[AnimalService]   GO 未激活（幽灵/池化），跳过"); return false; }
            }
            catch (Exception ex) { Plugin.LogVerbose($"[AnimalService]   active 检查异常: {ex.Message}，继续"); }
        }
        if (e.ClassName == "AnimalDeadBody")
        {
            if (e.Guid <= 0) { Plugin.LogVerbose("[AnimalService]   guid<=0，跳过"); return false; }
            Plugin.LogVerbose($"[AnimalService]   调 DestroyElement(guid={e.Guid})...");
            Invoke(_destroyElement, _mapStuffHelper, e.Guid);
            Plugin.LogVerbose("[AnimalService]   DestroyElement 返回");
            return true;
        }

        if (mode == "destroySelf")
        {
            // 对照实验：只调 Animal.DestroySelf()（跳过注册表/状态池）
            IntPtr ds = FindMethodInHierarchy(GetClass(e.Ptr), "DestroySelf", 0);
            Plugin.LogVerbose($"[AnimalService]   DestroySelf={ds.ToInt64():X}，调用...");
            Invoke(ds, e.Ptr);
            Plugin.LogVerbose("[AnimalService]   DestroySelf 返回");
            return true;
        }

        // 活体：先读 is_dead 预检
        if (e.FieldMeta.TryGetValue("is_dead", out var df) && !df.IsString && !df.IsPointer)
        {
            try
            {
                byte dead = ReadIl2CppByte(e.Ptr, df.Offset);
                Plugin.LogVerbose($"[AnimalService]   is_dead={dead}");
                if (dead != 0) { Plugin.LogVerbose("[AnimalService]   已死，跳过"); return false; }
            }
            catch (Exception ex) { Plugin.LogVerbose($"[AnimalService]   is_dead 读取失败: {ex.Message}"); }
        }

        if (mode == "onBeDestroy")
        {
            // 推荐：游戏完整移除流程 ExitFacility + DestroyAnimal
            IntPtr obd = FindMethodInHierarchy(GetClass(e.Ptr), "OnBeDestroy", 1);
            Plugin.LogVerbose($"[AnimalService]   OnBeDestroy={obd.ToInt64():X}，调用...");
            Invoke(obd, e.Ptr, 0);
            Plugin.LogVerbose("[AnimalService]   OnBeDestroy 返回");
            return true;
        }

        // mode = destroyAnimal（原路径，闪退）
        Plugin.LogVerbose($"[AnimalService]   调 DestroyAnimal(animal)...");
        Invoke(_destroyAnimal, _animalHelper, e.Ptr);
        Plugin.LogVerbose("[AnimalService]   DestroyAnimal 返回");
        return true;
    }

    /// <summary>
    /// 召唤动物：照抄游戏创建路径 <c>AnimalHelper.CreateAnimal(Point pos, int stuff_id, int count)</c>
    /// （3 参重载，伪 C 证实内部循环 count 次、按 AnimalInfo 默认年龄/寿命）。
    /// 位置 = <c>AreaMap.GetRandomLandPoint()</c>（地图随机陆地格，与野生动物刷新同源思路）。
    /// </summary>
    /// <param name="stuffId">动物种类（animal.json 的 animal_id，如 501005=猪）</param>
    /// <param name="count">数量（前端夹取 1..10）</param>
    /// <returns>(gx, gy) 实际召唤的格子坐标；newGuids = 新动物 guid 列表（用于前端定位）</returns>
    internal static (int gx, int gy, int spawned) Spawn(int stuffId, int count)
    {
        IntPtr helper = GameChainLocator.GetAnimalHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper（未进存档？）");
        IntPtr createAnimal = FindMethodInHierarchy(GetClass(helper), "CreateAnimal", 3);
        if (createAnimal == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper.CreateAnimal(pos, stuff_id, count)");

        // ⚠ 位置策略：全图随机会落在几屏幕外的未加载区块 → 幽灵动物（逻辑存在看不见），
        // 且删它们会崩。改为：**借在场实体的世界坐标反推格子**（必然在玩家领地/视野内）：
        //   Point.ToVector3 = (gx+0.5, gy+0.5, 0)（伪 C 证实）→ 逆运算 gx=floor(wx-0.5)。
        // Point 对象用 il2cpp_object_new 构造并直写 x/y 字段（纯数据类，ToVector3 只读 x/y）。
        // ⚠ 坐标合法性过滤：实测有设施的 transform.position.x = 1.3e9（脏数据/地图外对象），
        //   参考到它就把召唤点带飞。地图坐标量级在 ±1000 内，|x|>5000 的一律不采信。
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
        Plugin.LogInfo($"[AnimalService] 召唤参考扫描: 实体 {total} 个 → 候选 {candidates.Count} 个"
            + $"（类型不符 {skipNotMatch} / 未激活 {skipInactive} / 坐标超限 {skipCoord} / 零坐标 {skipZero} / 异常 {skipEx} / 无GO {skipNoGo}）");
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"没有坐标合法的参考实体（实体 {total}：类型不符 {skipNotMatch}、未激活 {skipInactive}、坐标超限 {skipCoord}、零坐标 {skipZero}、异常 {skipEx}、无GO {skipNoGo}）——请先重新扫描");
        Plugin.LogInfo($"[AnimalService] 召唤参考候选 {candidates.Count} 个，首个 ({candidates[0].x:F1},{candidates[0].y:F1})");

        // 随机挑一个参考建筑，并在其周围 ±2 格落地（不压在建筑正中）
        var rnd = new System.Random();
        var (rx, ry) = candidates[rnd.Next(candidates.Count)];
        int gx = (int)Math.Floor(rx - 0.5f) + rnd.Next(-2, 3);
        int gy = (int)Math.Floor(ry - 0.5f) + rnd.Next(-2, 3);
        Plugin.LogInfo($"[AnimalService] Spawn {count} 只 stuffId={stuffId} 于格子({gx},{gy})（参考 {rx:F1},{ry:F1}）");

        // ⚠⚠ Point 是 struct（值类型）：CreateAnimal 的 Point 参数槽里要放 {x,y} 8 字节数据本身
        //（x 低 32 位、y 高 32 位，小端）。传 il2cpp_object_new 的"对象指针"会把 klass 头
        // 读成坐标（实测 x=1.37e9 = klass 低 32 位），这就是"幽灵动物"的根因。
        IntPtr posSlot = new IntPtr(gx | (gy << 32));
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (Invoke(createAnimal, helper, posSlot, stuffId, 1) != IntPtr.Zero) spawned++;
        }
        // ⚠ 不在这里读新动物的 guid：CreateAnimal 返回时 guid 还没分配（实测读到 0），
        // 前端按 guid 找实体会匹配到 2266 个 guid=0 实体里的任意一个（相机飞错地方）。
        // 前端定位改用 ptrHash 差集（召唤前快照 vs 重扫后）。
        return (gx, gy, spawned);
    }
}
