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

internal static partial class EntityDestroyer
{
    /// <summary>NPC 策略：在类继承链上枚举虚方法 LeaveMapAndDestroy / OnDead / LeaveMap 等并调用</summary>
    private static bool TryDestroyNpc(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
        if (className.Contains("Npc") && !className.Contains("NpcHelper"))
        {
            string[] targetNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead", "LeaveMap", "OnLeaveMap" };
            IntPtr searchCls = classPtr;
            int depth = 0;
            while (searchCls != IntPtr.Zero && depth < 15 && !called)
            {
                string clsName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(searchCls)) ?? "?";
                IntPtr iter = IntPtr.Zero;
                IntPtr mth;
                while ((mth = Il2CppApi.ClassGetMethods(searchCls, ref iter)) != IntPtr.Zero)
                {
                    string? mName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(mth));
                    if (mName == null) continue;
                    foreach (var target in targetNames)
                    {
                        if (mName == target)
                        {
                            uint pCount = Il2CppApi.GetMethodParamCountRaw(mth);
                            Plugin.LogInfo($"[EntityEditor] [NPC] Found {mName}({pCount}p) at depth={depth} cls={clsName}");
                            try
                            {
                                if (Il2CppInvoke.InvokeWithDefaults(mth, e.Ptr, (int)pCount))
                                {
                                    Plugin.LogInfo($"[EntityEditor] ✓ Called {mName}() on {name} (depth={depth})");
                                    called = true;
                                    break;
                                }
                                Plugin.LogInfo($"[EntityEditor] {mName}() exception on {name}");
                            }
                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mName}() CRASH: {ex.Message}"); }
                        }
                    }
                    if (called) break;
                }
                searchCls = Il2CppApi.GetParent(searchCls);
                depth++;
            }
        }
        return called;
    }
}
