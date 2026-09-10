using System;
using System.Collections.Generic;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;

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
        if (_animalHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper（未进存档？）");
        _destroyAnimal = FindMethodInHierarchy(GetClass(_animalHelper), "DestroyAnimal", 1);
        if (_destroyAnimal == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AnimalHelper.DestroyAnimal(animal)");

        _mapStuffHelper = GameChainLocator.GetMapStuffHelper();
        if (_mapStuffHelper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper（未进存档？）");
        _destroyElement = FindMethodInHierarchy(GetClass(_mapStuffHelper), "DestroyElement", 1);
        if (_destroyElement == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper.DestroyElement(guid)");
    }

    /// <summary>
    /// 删一只/一具。<see cref="Resolve"/> 必须已成功。
    /// 按 className 分派：Animal → DestroyAnimal(ptr)；AnimalDeadBody → DestroyElement(guid)。
    /// </summary>
    internal static bool DestroyOne(EntityScan.EditorEntity e)
    {
        if (e.ClassName == "AnimalDeadBody")
        {
            if (e.Guid <= 0) return false;
            Invoke(_destroyElement, _mapStuffHelper, e.Guid);   // void 方法
            return true;
        }
        // 活体：传对象指针
        Invoke(_destroyAnimal, _animalHelper, e.Ptr);
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
        IntPtr getLandPoint = FindMethodInHierarchy(GetClass(areaMap), "GetRandomLandPoint", 0);
        if (getLandPoint == IntPtr.Zero)
            throw new InvalidOperationException("找不到 AreaMap.GetRandomLandPoint()");

        // 每只独立随机陆地格（比 count 只叠在同一点自然）
        for (int i = 0; i < count; i++)
        {
            // runtime_invoke 对引用类型返回值直接给对象指针（Point 是引用类型）
            IntPtr pos = Invoke(getLandPoint, areaMap);
            if (pos == IntPtr.Zero)
                throw new InvalidOperationException("GetRandomLandPoint 返回空（地图满了？）");
            Invoke(createAnimal, helper, pos, stuffId, 1);
        }
    }
}
