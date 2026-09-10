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
    internal static void Spawn(int stuffId, int count)
    {
        IntPtr helper = GameChainLocator.GetAnimalHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper（未进存档？）");
        IntPtr createAnimal = FindMethodInHierarchy(GetClass(helper), "CreateAnimal", 3);
        if (createAnimal == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper.CreateAnimal(pos, stuff_id, count)");

        IntPtr areaMap = GameChainLocator.GetAreaMap();
        if (areaMap == IntPtr.Zero)
            throw new InvalidOperationException("未进入存档，找不到 AreaMap");
        IntPtr getLandPoint = FindMethodInHierarchy(GetClass(areaMap), "GetRandomLandPointNotAtMapBorder", 0);
        if (getLandPoint == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AreaMap.GetRandomLandPointNotAtMapBorder()");

        // ⚠ 位置策略：GetRandomLandPointNotAtMapBorder 是全图随机 —— 可能落在几屏幕之外、
        // 区块未加载 → 动物逻辑存在但看不见（用户实测"幽灵生物"），且对它调 DestroySelf
        // 会因渲染组件未创建而 native 崩溃。
        // 位置来源：**在场动物的 get_CurPoint()**（0 参属性 getter 返回 Point 对象，沿继承链可找）
        // → 召唤到现有动物旁边（玩家领地内、看得见）。无动物时退回全图随机。
        IntPtr pos = IntPtr.Zero;
        string posSrc = "";
        foreach (var e in EntityScan.Snapshot())
        {
            if (e.ClassName != "Animal" || e.Ptr == IntPtr.Zero) continue;
            IntPtr getCur = FindMethodInHierarchy(GetClass(e.Ptr), "get_CurPoint", 0);
            if (getCur == IntPtr.Zero) continue;
            IntPtr p = Invoke(getCur, e.Ptr);
            if (p != IntPtr.Zero) { pos = p; posSrc = $"动物 guid={e.Guid} 的 CurPoint"; break; }
        }
        if (pos == IntPtr.Zero)
        {
            pos = Invoke(getLandPoint, areaMap);
            posSrc = "全图随机陆地";
        }
        if (pos == IntPtr.Zero)
            throw new InvalidOperationException("取不到召唤位置");
        Plugin.LogVerbose($"[AnimalService] Spawn {count} 只 stuffId={stuffId}，位置来源：{posSrc}");

        for (int i = 0; i < count; i++)
        {
            Invoke(createAnimal, helper, pos, stuffId, 1);
        }
    }
}
