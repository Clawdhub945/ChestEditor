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
/// 实体销毁调度器。
/// <para>按固定优先级依次尝试各路策略，任一策略成功即停止：</para>
/// <list type="number">
///   <item>NPC    —— <see cref="TryDestroyNpc"/></item>
///   <item>Facility —— <see cref="TryDestroyFacility"/></item>
///   <item>Ship   —— <see cref="TryDestroyShip"/></item>
///   <item>Monster —— <see cref="TryDestroyMonster"/>（静默移除，跳过死亡动画）</item>
///   <item>通用（动物/掉落物等）—— <see cref="TryDestroyGeneric"/></item>
///   <item>Facility 管理器兜底 —— <see cref="TryDestroyFacilityViaManagers"/></item>
///   <item>最终兜底 —— <see cref="FallbackDestroy"/></item>
/// </list>
/// 各策略实现按职责拆分在 <c>Game/Destroy/EntityDestroyer.*.cs</c>。
/// </summary>
internal static partial class EntityDestroyer
{
    /// <summary>
    /// 销毁指定实体（按 ptrHash 匹配），调用游戏的 LeaveMapAndDestroy / DestroyGo / OnDead 等入口。
    /// </summary>
    internal static string DestroyEntity(int ptrHash)
    {
        try
        {
            Plugin.LogInfo($"[EntityEditor] DestroyEntity called, ptrHash={ptrHash}, entities.Count={EntityScan.Entities.Count}");
            for (int i = 0; i < EntityScan.Entities.Count; i++)
            {
                var e = EntityScan.Entities[i];
                if (e.PtrHash != ptrHash) continue;

                Plugin.LogInfo($"[EntityEditor] Matched entity: {e.GoName} class={e.ClassName} ptrHash={e.PtrHash}");

                if (e.GoRef == null)
                    return "GameObject reference lost (rescan needed)";

                string name = e.GoRef.name;
                IntPtr classPtr = Il2CppApi.GetClass(e.Ptr);
                string className = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(classPtr)) ?? "?";
                Plugin.LogInfo($"[EntityEditor] === DestroyEntity START: {name} class={className} ptrHash={ptrHash} ===");

                // === 第一优先：NPC 类型用 LeaveMapAndDestroy（虚方法，需在类链上枚举）===
                bool called = TryDestroyNpc(e, classPtr, className, name);

                // === 第二优先：Facility 类型用 BuildHelper.RemoveFacility ===
                if (!called) called = TryDestroyFacility(e, classPtr, className, name);

                // === Ship 专用处理 ===
                if (!called) called = TryDestroyShip(e, classPtr, className, name);

                // === Monster 专用处理（静默移除；必须早于通用兜底，否则会被 Leave() 挑走）===
                if (!called) called = TryDestroyMonster(e, classPtr, className, name);

                // === 第三优先：通用销毁方法（非 NPC / 非 Facility）===
                if (!called) called = TryDestroyGeneric(e, classPtr, className, name);

                // === 第四优先：Facility 在其它管理类上搜索 Remove/Del ===
                if (!called) called = TryDestroyFacilityViaManagers(e, classPtr, className, name);

                // === 兜底：BeforeDestroy + 从管理器移除 + 隐藏视觉 + DestroyImmediate ===
                if (!called) called = FallbackDestroy(e, classPtr, className, name);

                Plugin.LogInfo($"[EntityEditor] === DestroyEntity END: {name} called={called} ===");
                EntityScan.Entities.RemoveAt(i);
                return "ok";
            }
            return $"entity not found: ptrHash={ptrHash}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>
    /// 判断类名是否属于 Facility 层级。
    /// </summary>
    private static bool IsFacilityClass(string className)
    {
        return className.Contains("Facility") || className.Contains("FacilityAncientTomb")
            || className.Contains("FacilityForMonster") || className.Contains("FacilityBarracks")
            || className.Contains("FacilityCityWall") || className.Contains("FacilityHive")
            || className.Contains("FacilityMine") || className.Contains("FacilityQuarry");
    }

    /// <summary>
    /// 在类继承链上枚举方法（可见虚方法）并调用首个同名方法，忽略异常。
    /// </summary>
    private static void CallVoidMethod(IntPtr classPtr, IntPtr objPtr, string methodName, int maxDepth)
    {
        IntPtr cls = classPtr;
        int d = 0;
        while (cls != IntPtr.Zero && d <= maxDepth)
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr m;
            while ((m = Il2CppApi.ClassGetMethods(cls, ref iter)) != IntPtr.Zero)
            {
                string? name = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(m));
                if (name == methodName)
                {
                    try
                    {
                        IntPtr ex = IntPtr.Zero;
                        unsafe { Il2CppApi.RuntimeInvoke(m, objPtr, null, ref ex); }
                        Plugin.LogInfo($"[EntityEditor] {methodName}() ex={ex != IntPtr.Zero}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {methodName}() failed: {ex.Message}"); }
                    return;
                }
            }
            cls = Il2CppApi.GetParent(cls);
            d++;
        }
        Plugin.LogInfo($"[EntityEditor] {methodName}() not found");
    }
}
