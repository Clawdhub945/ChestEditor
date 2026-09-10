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
    /// <summary>兜底策略：BeforeDestroy + 管理器引用清理 + 隐藏视觉 + DestroyImmediate</summary>
    private static bool FallbackDestroy(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
        if (!called)
        {
            Plugin.LogInfo($"[EntityEditor] No game destroy method worked, trying fallback...");

            // Step 1: BeforeDestroy 清理
            {
                IntPtr bdCls = classPtr;
                int bdDepth = 0;
                while (bdCls != IntPtr.Zero && bdDepth < 15)
                {
                    IntPtr bdMth = Il2CppApi.GetMethodFromName(bdCls, "BeforeDestroy", 0);
                    if (bdMth != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr exBd = IntPtr.Zero;
                            unsafe { Il2CppApi.RuntimeInvoke(bdMth, e.Ptr, null, ref exBd); }
                            Plugin.LogInfo($"[EntityEditor] BeforeDestroy() at depth={bdDepth}");
                        }
                        catch { }
                        break;
                    }
                    bdCls = Il2CppApi.GetParent(bdCls);
                    bdDepth++;
                }
            }

            // Step 2: 从 PrefabManager 等管理器中移除引用
            RemoveFromManagers(e.Ptr, classPtr, className);

            // Step 3: 隐藏视觉
            string[] hideNames = { "SetBodyViewVisible", "SetBodyLogicVisible" };
            foreach (var hideName in hideNames)
            {
                IntPtr hCls = classPtr;
                int hDepth = 0;
                while (hCls != IntPtr.Zero && hDepth < 15)
                {
                    IntPtr hMth = Il2CppApi.GetMethodFromName(hCls, hideName, 1);
                    if (hMth != IntPtr.Zero)
                    {
                        try
                        {
                            Il2CppInvoke.InvokeWithArgs(hMth, e.Ptr, IntPtr.Zero); // false
                            Plugin.LogInfo($"[EntityEditor] {hideName}(false) at depth={hDepth}");
                        }
                        catch { }
                        break;
                    }
                    hCls = Il2CppApi.GetParent(hCls);
                    hDepth++;
                }
            }

            // Step 4: RecycleMySp 清理渲染资源
            IntPtr cls2 = classPtr;
            int d2 = 0;
            while (cls2 != IntPtr.Zero && d2 < 15)
            {
                IntPtr mp = Il2CppApi.GetMethodFromName(cls2, "RecycleMySp", 0);
                if (mp != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr ex2 = IntPtr.Zero;
                        unsafe { Il2CppApi.RuntimeInvoke(mp, e.Ptr, null, ref ex2); }
                    }
                    catch { }
                    break;
                }
                cls2 = Il2CppApi.GetParent(cls2);
                d2++;
            }

            // Step 5: 隐藏 + 销毁 GameObject
            try
            {
                e.GoRef!.SetActive(false);
                UnityEngine.Object.DestroyImmediate(e.GoRef);
                Plugin.LogInfo($"[EntityEditor] DestroyImmediate GO: {name}");
                called = true;
            }
            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] DestroyImmediate failed: {ex.Message}"); }
        }
        return called;
    }
}
