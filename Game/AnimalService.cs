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
}
