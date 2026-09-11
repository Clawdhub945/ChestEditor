using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 游戏对象图定位器：沿 Game → MainScene → AreaMap → Territory / Helper 的固定链路取回实例。
/// <para>原先 EntityDestroyer 内三处重复实现了同一条链路，此处统一收敛。</para>
/// </summary>
internal static class GameChainLocator
{
    // ===== 公共链路 =====

    /// <summary>
    /// 是否已进入游戏世界（能沿 Game → MainScene → AreaMap 定位到对象才算）。
    /// <para>主菜单 / 读档过程中取不到 → false。网页端的"现在能不能操作"以它为准。</para>
    /// <para>⚠ 只能在主线程调用（会 Invoke IL2CPP 静态方法）。</para>
    /// </summary>
    internal static bool IsWorldReady()
    {
        try { return GetAreaMap() != IntPtr.Zero; }
        catch { return false; }
    }

    /// <summary>
    /// Game.get_main_scene()，含候选方法名回退。失败返回 <see cref="IntPtr.Zero"/>。
    /// </summary>
    internal static IntPtr GetMainScene()
    {
        try
        {
            IntPtr gameClass = Il2CppApi.FindClassByName("Game");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            IntPtr mth = Il2CppApi.GetMethodFromName(gameClass, "get_main_scene", 0);
            if (mth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    mth = Il2CppApi.GetMethodFromName(gameClass, alt, 0);
                    if (mth != IntPtr.Zero) break;
                }
            }
            if (mth == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try { unsafe { mainScene = (IntPtr)Il2CppApi.RuntimeInvoke(mth, IntPtr.Zero, null, ref exception); } }
            catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] get_main_scene() CRASH: {ex.Message}"); }
            return mainScene;
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetMainScene error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// main_scene.area_map。失败返回 <see cref="IntPtr.Zero"/>。
    /// </summary>
    internal static IntPtr GetAreaMap()
    {
        IntPtr mainScene = GetMainScene();
        if (mainScene == IntPtr.Zero) return IntPtr.Zero;
        return Il2CppApi.ReadFieldSafe(mainScene, Il2CppApi.GetClass(mainScene), "area_map");
    }

    /// <summary>
    /// 查找游戏管理类的实例：静态单例字段 → get_Instance() → 场景中挂载该组件的 GameObject。
    /// </summary>
    internal static IntPtr FindClassInstance(IntPtr classPtr)
    {
        try
        {
            string className = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(classPtr)) ?? "?";
            // 方法1：查找静态 Instance/s_instance 字段并读取值
            foreach (var fh in Il2CppApi.EnumerateFieldHandles(classPtr))
            {
                if (!Il2CppApi.IsInstanceFieldName(fh.Name)) continue;

                Plugin.LogVerbose($"[EntityEditor] Found field {fh.Name} on {className}, isStatic={fh.IsStatic}, attrs=0x{Il2CppApi.FieldGetFlags(fh.Field):X}");
                if (!fh.IsStatic) continue;

                try
                {
                    IntPtr value = Il2CppApi.ReadStaticFieldValue(fh.Field);
                    Plugin.LogVerbose($"[EntityEditor] Static field {fh.Name} value={value.ToInt64():X}");
                    if (value == IntPtr.Zero) continue;

                    // 验证这个指针是否是一个有效的 IL2CPP 对象
                    IntPtr objClass = Il2CppApi.GetClass(value);
                    if (objClass == IntPtr.Zero) continue;

                    string? objClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(objClass));
                    Plugin.LogVerbose($"[EntityEditor] ✓ Got instance from {fh.Name}, class={objClassName}");
                    return value;
                }
                catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] Read static field {fh.Name} error: {ex.Message}"); }
            }
            // 方法2：通过 Resources.FindObjectsOfTypeAll 查找
            // 我们需要通过 IL2CPP 的类型系统来查找
            // 尝试直接调用 FindObjectOfType
            IntPtr findMethod = Il2CppApi.GetMethodFromName(classPtr, "get_Instance", 0);
            if (findMethod != IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] Found get_Instance() on {className}");
                IntPtr exception = IntPtr.Zero;
                unsafe
                {
                    IntPtr result = (IntPtr)Il2CppApi.RuntimeInvoke(findMethod, IntPtr.Zero, null, ref exception);
                    if (exception == IntPtr.Zero && result != IntPtr.Zero)
                    {
                        Plugin.LogVerbose($"[EntityEditor] get_Instance() returned valid object");
                        return result;
                    }
                }
            }
            // 方法3：遍历场景中的 GameObject 查找组件
            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            foreach (var go in allGOs)
            {
                try
                {
                    var comps = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        IntPtr compClass = Il2CppApi.GetClass(comp.Pointer);
                        if (compClass == classPtr)
                        {
                            Plugin.LogVerbose($"[EntityEditor] Found instance of {className} on GO: {go.name}");
                            return comp.Pointer;
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] FindClassInstance error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    // ===== 具体节点 =====

    /// <summary>
    /// 玩家 Territory 实例：area_map.my_territory 字段优先，回落 get_my_territory() 方法。
    /// </summary>
    internal static IntPtr GetTerritory()
    {
        try
        {
            IntPtr areaMapPtr = GetAreaMap();
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr areaMapClass = Il2CppApi.GetClass(areaMapPtr);
            IntPtr territoryPtr = Il2CppApi.ReadFieldSafe(areaMapPtr, areaMapClass, "my_territory");
            if (territoryPtr == IntPtr.Zero)
                territoryPtr = Il2CppApi.ReadFieldSafe(areaMapPtr, areaMapClass, "_my_territory");

            if (territoryPtr == IntPtr.Zero)
            {
                IntPtr getMyTerritoryMth = Il2CppApi.GetMethodFromName(areaMapClass, "get_my_territory", 0);
                if (getMyTerritoryMth == IntPtr.Zero)
                {
                    string[] altNames = { "GetMyTerritory", "get_MyTerritory", "my_territory" };
                    foreach (var alt in altNames)
                    {
                        getMyTerritoryMth = Il2CppApi.GetMethodFromName(areaMapClass, alt, 0);
                        if (getMyTerritoryMth != IntPtr.Zero) break;
                    }
                }
                if (getMyTerritoryMth != IntPtr.Zero)
                {
                    IntPtr exception = IntPtr.Zero;
                    try { unsafe { territoryPtr = (IntPtr)Il2CppApi.RuntimeInvoke(getMyTerritoryMth, areaMapPtr, null, ref exception); } }
                    catch { }
                }
            }

            if (territoryPtr != IntPtr.Zero)
            {
                IntPtr tc = Il2CppApi.GetClass(territoryPtr);
                string? tn = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(tc));
                Plugin.LogVerbose($"[EntityEditor] ✓ Got Territory, class={tn}");
            }
            return territoryPtr;
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 玩家 Territory 上的 BuildHelper 实例：territory.build_helper 字段优先，回落 _build_helper。
    /// </summary>
    internal static IntPtr GetBuildHelper()
    {
        try
        {
            Plugin.LogVerbose($"[EntityEditor] Finding BuildHelper via Game chain...");

            IntPtr gameClass = Il2CppApi.FindClassByName("Game");
            Plugin.LogVerbose($"[EntityEditor] Game class={gameClass.ToInt64():X}");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            // 列出 Game 类的相关方法，便于排查
            IntPtr iter = IntPtr.Zero;
            IntPtr mth;
            while ((mth = Il2CppApi.ClassGetMethods(gameClass, ref iter)) != IntPtr.Zero)
            {
                string? mName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(mth));
                uint pc = Il2CppApi.GetMethodParamCountRaw(mth);
                if (mName != null && (mName.Contains("main_scene") || mName.Contains("MainScene") || mName.Contains("instance") || mName.Contains("Instance")))
                    Plugin.LogVerbose($"[EntityEditor] Game.{mName}({pc}p)");
            }

            IntPtr mainScene = GetMainScene();
            if (mainScene == IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] Game.get_main_scene() not found / null");
                return IntPtr.Zero;
            }
            Plugin.LogVerbose($"[EntityEditor] main_scene={mainScene.ToInt64():X}");

            // main_scene.area_map（列出相关字段便于排查）
            IntPtr mainSceneClass = Il2CppApi.GetClass(mainScene);
            Plugin.LogVerbose($"[EntityEditor] mainScene class={mainSceneClass.ToInt64():X}");
            IntPtr fi2 = IntPtr.Zero;
            IntPtr f2;
            while ((f2 = Il2CppApi.ClassGetFields(mainSceneClass, ref fi2)) != IntPtr.Zero)
            {
                string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f2));
                int offset = (int)Il2CppApi.FieldGetOffset(f2);
                IntPtr ft = Il2CppApi.FieldGetType(f2);
                string? ftn = ft != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(ft)) : "?";
                if (fn != null && (fn.Contains("area") || fn.Contains("map") || fn.Contains("territory") || fn.Contains("build")))
                    Plugin.LogVerbose($"[EntityEditor] MainScene.{fn} offset={offset} type={ftn}");
            }

            IntPtr areaMapPtr = Il2CppApi.ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            Plugin.LogVerbose($"[EntityEditor] area_map={areaMapPtr.ToInt64():X}");
            if (areaMapPtr == IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] main_scene.area_map is null");
                return IntPtr.Zero;
            }

            IntPtr territoryPtr = GetTerritory();
            if (territoryPtr == IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] Could not get Territory");
                return IntPtr.Zero;
            }

            IntPtr territoryClass = Il2CppApi.GetClass(territoryPtr);
            Plugin.LogVerbose($"[EntityEditor] territory class={territoryClass.ToInt64():X}");

            IntPtr buildHelperPtr = Il2CppApi.ReadFieldSafe(territoryPtr, territoryClass, "build_helper");
            if (buildHelperPtr == IntPtr.Zero)
                buildHelperPtr = Il2CppApi.ReadFieldSafe(territoryPtr, territoryClass, "_build_helper");
            Plugin.LogVerbose($"[EntityEditor] territory.build_helper = {buildHelperPtr.ToInt64():X}");

            if (buildHelperPtr != IntPtr.Zero)
            {
                IntPtr bhClass = Il2CppApi.GetClass(buildHelperPtr);
                string? bhClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(bhClass));
                Plugin.LogVerbose($"[EntityEditor] ✓ Got BuildHelper, class={bhClassName}");
            }
            return buildHelperPtr;
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetBuildHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// area_map.map_stuff_helper 实例。
    /// </summary>
    internal static IntPtr GetMapStuffHelper()
    {
        try
        {
            Plugin.LogVerbose($"[EntityEditor] Finding MapStuffHelper via Game chain...");

            IntPtr areaMapPtr = GetAreaMap();
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr areaMapClass = Il2CppApi.GetClass(areaMapPtr);
            IntPtr mapStuffHelperPtr = Il2CppApi.ReadFieldSafe(areaMapPtr, areaMapClass, "map_stuff_helper");
            if (mapStuffHelperPtr != IntPtr.Zero)
            {
                IntPtr mshClass = Il2CppApi.GetClass(mapStuffHelperPtr);
                string? mshClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(mshClass));
                Plugin.LogVerbose($"[EntityEditor] ✓ Got MapStuffHelper, class={mshClassName}");
            }
            else
                Plugin.LogVerbose($"[EntityEditor] area_map.map_stuff_helper is null");
            return mapStuffHelperPtr;
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetMapStuffHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// territory.animal_helper（AnimalHelper 实例）——动物的标准移除路径
    /// <c>AnimalHelper.DestroyAnimal(animal)</c> 的宿主（OnBeDestroy 里就是这么调的）。
    /// </summary>
    internal static IntPtr GetAnimalHelper()
    {
        try
        {
            IntPtr territory = GetTerritory();
            if (territory == IntPtr.Zero) return IntPtr.Zero;
            IntPtr territoryClass = Il2CppApi.GetClass(territory);
            return Il2CppApi.ReadFieldSafe(territory, territoryClass, "animal_helper");
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetAnimalHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 我方领地的 NpcHelper（Territory.npc_helper）—— 创建 NPC/士兵的宿主对象
    /// （CreateElf / CreateStoneMan / CreateNpc / CreateSoldierBySummon 都挂在它上面，
    /// 伪 C 证实其内部用 this.area_map / this.territory，取我方的才能把小人归进我方领地）。
    /// </summary>
    internal static IntPtr GetNpcHelper()
    {
        try
        {
            IntPtr territory = GetTerritory();
            if (territory == IntPtr.Zero) return IntPtr.Zero;
            IntPtr territoryClass = Il2CppApi.GetClass(territory);
            return Il2CppApi.ReadFieldSafe(territory, territoryClass, "npc_helper");
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetNpcHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Territory.soldier_helper —— 正规士兵招募（兵营）所用的创建器实例。
    /// SoldierHelper.CreateSoldier(config, troop, team, Point, race, null) 走游戏原生路径：
    /// 内部生成人名/成年年龄/随机性别 + Tile.GridIndexToPositionWithRandomOffset 坐标换算，
    /// is_soldier_summon=0（本地兵，无离场时限）。
    /// </summary>
    internal static IntPtr GetSoldierHelper()
    {
        try
        {
            IntPtr territory = GetTerritory();
            if (territory == IntPtr.Zero) return IntPtr.Zero;
            IntPtr territoryClass = Il2CppApi.GetClass(territory);
            return Il2CppApi.ReadFieldSafe(territory, territoryClass, "soldier_helper");
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GetSoldierHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 用游戏自己的 <c>NpcNameGenerator</c> 生成一个"本地居民风格"的人名。
    /// 伪 C 证实（SoldierHelper.CreateSoldier = 正规招募士兵入口）：
    ///   family = NpcNameGenerator.GetRandomFamilyName(race_id)
    ///   name   = NpcNameGenerator.GetName(1, race_id, family)
    /// 而魔法召唤入口 CreateSoldierBySummon 的第 2 参会被**直接写进 Npc.npc_name**，
    /// 所以必须给它"人名"，否则士兵会顶着兵种名（如"骑士"）满地图跑。
    /// 两方法都是静态；返回托管字符串。
    /// </summary>
    internal static string? GenerateNpcName(int raceId) => GenerateNpcNameParts(raceId).full;

    /// <summary>同 <see cref="GenerateNpcName"/>，但同时给出 姓 与 全名（Npc.family_name 存姓、npc_name 存全名）。</summary>
    internal static (string? family, string? full) GenerateNpcNameParts(int raceId)
    {
        try
        {
            IntPtr cls = Il2CppApi.FindClassByName("NpcNameGenerator");
            if (cls == IntPtr.Zero) { Plugin.LogVerbose("[EntityEditor] 找不到 NpcNameGenerator"); return (null, null); }

            IntPtr mFamily = Il2CppApi.GetMethodFromName(cls, "GetRandomFamilyName", 1);
            if (mFamily == IntPtr.Zero) mFamily = FindMethodInHierarchy(cls, "GetRandomFamilyName", 1);
            if (mFamily == IntPtr.Zero) { Plugin.LogVerbose("[EntityEditor] 找不到 GetRandomFamilyName"); return (null, null); }

            IntPtr family = Invoke(mFamily, IntPtr.Zero, raceId);   // 静态：objPtr 传 0
            if (family == IntPtr.Zero) return (null, null);

            // ⚠ family 必须登记 GC 根：它只被本地变量/参数槽（.NET 托管堆）引用，
            //   而 il2cpp 的 Boehm GC 不扫描托管堆 —— 紧接着的调用内部一旦分配
            //   触发 GC，family 就被回收，后续只会拿到空串（实测踩过）。
            IntPtr root = GcHandleNew(family, true);
            try
            {
                string? fam = Il2CppMemory.ReadStringObject(family);
                // 路线1：GetNextMaleName()/GetNextFemaleName()（0 参，直接从名字表按序取）
                foreach (var mn in new[] { "GetNextMaleName", "GetNextFemaleName" })
                {
                    IntPtr m = FindMethodInHierarchy(cls, mn, 0);
                    if (m == IntPtr.Zero) continue;
                    IntPtr o = Invoke(m, IntPtr.Zero);
                    string? got = o != IntPtr.Zero ? Il2CppMemory.ReadStringObject(o) : null;
                    if (!string.IsNullOrEmpty(got))
                    {
                        // GetNextXxxName 只返回"名"，完整人名 = 姓 + 名（实测原生士兵即"詹"+"轩宇"）
                        string full = string.IsNullOrEmpty(fam) ? got : fam + got;
                        Plugin.LogInfo($"[NpcName] {mn}() → 名='{got}' 姓='{fam}' ⇒ '{full}'");
                        return (fam, full);
                    }
                }
                // 路线2：GetName(is_male, race_id, family_name)（3 参）
                IntPtr mName = FindMethodInHierarchy(cls, "GetName", 3);
                if (mName != IntPtr.Zero)
                {
                    IntPtr nameObj = Invoke(mName, IntPtr.Zero, 1, raceId, family);
                    string? nm = nameObj != IntPtr.Zero ? Il2CppMemory.ReadStringObject(nameObj) : null;
                    Plugin.LogInfo($"[NpcName] GetName(3) race={raceId} 姓='{fam}' 名='{nm}'");
                    if (!string.IsNullOrEmpty(nm)) return (fam, nm);
                }
                return (null, null);
            }
            finally { GcHandleFree(root); }
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] GenerateNpcName error: {ex.Message}"); }
        return (null, null);
    }
}
