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
        Plugin.LogInfo($"[AnimalService] Resolve: animalHelper={_animalHelper.ToInt64():X}");
        if (_animalHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper（未进存档？）");
        _destroyAnimal = FindMethodInHierarchy(GetClass(_animalHelper), "DestroyAnimal", 1);
        Plugin.LogInfo($"[AnimalService] Resolve: DestroyAnimal(1p)={_destroyAnimal.ToInt64():X}");
        if (_destroyAnimal == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper.DestroyAnimal(animal)");

        _mapStuffHelper = GameChainLocator.GetMapStuffHelper();
        if (_mapStuffHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper（未进存档？）");
        _destroyElement = FindMethodInHierarchy(GetClass(_mapStuffHelper), "DestroyElement", 1);
        Plugin.LogInfo($"[AnimalService] Resolve: mapStuffHelper={_mapStuffHelper.ToInt64():X}, DestroyElement(1p)={_destroyElement.ToInt64():X}");
        if (_destroyElement == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper.DestroyElement(guid)");
    }

    /// <summary>
    /// 删一只/一具。<see cref="Resolve"/> 必须已成功。
    /// 按 className 分派：Animal → DestroyAnimal(ptr)；AnimalDeadBody → DestroyElement(guid)。
    /// <para>⚠ 每步 LogInfo：闪退（native 崩溃没有托管异常）时，日志的最后一条就是崩点。</para>
    /// </summary>
    internal static bool DestroyOne(EntityScan.EditorEntity e)
    {
        Plugin.LogInfo($"[AnimalService] DestroyOne: {e.ClassName} ptrHash={e.PtrHash} ptr={e.Ptr.ToInt64():X} guid={e.Guid}");
        if (e.ClassName == "AnimalDeadBody")
        {
            if (e.Guid <= 0) { Plugin.LogInfo("[AnimalService]   guid<=0，跳过"); return false; }
            Plugin.LogInfo($"[AnimalService]   调 DestroyElement(guid={e.Guid})...");
            Invoke(_destroyElement, _mapStuffHelper, e.Guid);   // void 方法
            Plugin.LogInfo("[AnimalService]   DestroyElement 返回");
            return true;
        }
        // 活体：先读 is_dead 预检（扫描后可能已被别的系统注销；重复调 DestroyAnimal
        // 会走"注册表 Remove 失败直接 return"，虽安全但跳过更干净）
        if (e.FieldMeta.TryGetValue("is_dead", out var df) && !df.IsString && !df.IsPointer)
        {
            try
            {
                byte dead = ReadIl2CppByte(e.Ptr, df.Offset);
                Plugin.LogInfo($"[AnimalService]   is_dead={dead}");
                if (dead != 0) { Plugin.LogInfo("[AnimalService]   已死，跳过"); return false; }
            }
            catch (Exception ex) { Plugin.LogInfo($"[AnimalService]   is_dead 读取失败: {ex.Message}"); }
        }
        // 活体：传对象指针
        Plugin.LogInfo("[AnimalService]   调 DestroyAnimal(animal)...");
        Invoke(_destroyAnimal, _animalHelper, e.Ptr);
        Plugin.LogInfo("[AnimalService]   DestroyAnimal 返回");
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
        // ⚠ GetRandomLandPoint(Point center, int range) 是 2 参（0 参版本不存在，上一版按 0 参找失败）；
        // 用 GetRandomLandPointNotAtMapBorder()（0 参，全图随机陆地 + TerrainHelper 导航调整）。
        IntPtr getLandPoint = FindMethodInHierarchy(GetClass(areaMap), "GetRandomLandPointNotAtMapBorder", 0);
        if (getLandPoint == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AreaMap.GetRandomLandPointNotAtMapBorder()");

        // 每只独立随机陆地格（比 count 只叠在同一点自然）。
        // 随机取点失败时兜底：借用任意在场实体的 cur_point 字段引用（Point 对象，引用类型字段存指针）。
        for (int i = 0; i < count; i++)
        {
            // runtime_invoke 对引用类型返回值直接给对象指针（Point 是引用类型）
            IntPtr pos = Invoke(getLandPoint, areaMap);
            if (pos == IntPtr.Zero)
            {
                foreach (var e in EntityScan.Snapshot())
                {
                    if (e.FieldMeta.TryGetValue("cur_point", out var pf) && pf.IsPointer)
                    {
                        IntPtr p = ReadIl2CppPointer(e.Ptr, pf.Offset);
                        if (p != IntPtr.Zero) { pos = p; break; }
                    }
                }
            }
            if (pos == IntPtr.Zero)
                throw new InvalidOperationException("取不到召唤位置（随机陆地失败且地图上没有任何带坐标的实体）");
            Invoke(createAnimal, helper, pos, stuffId, 1);
        }
    }
}
