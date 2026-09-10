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
    /// <summary>Facility 策略：BuildHelper 手动执行 Dismantle 全流程（CloseWindow → Dismantle → 列表移除 → RecycleMySp → 销毁 GO）</summary>
    private static bool TryDestroyFacility(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
        if (!called && IsFacilityClass(className))
        {
            Plugin.LogInfo($"[EntityEditor] [Facility] Looking for BuildHelper via Territory...");
            // IDA: territory->fields.build_helper -> BuildHelper__RemoveFacility(buildHelper, facility)
            // build_helper 不是单例，是 Territory 的实例字段
            IntPtr buildHelperInst = GameChainLocator.GetBuildHelper();
            Plugin.LogInfo($"[EntityEditor] BuildHelper instance={buildHelperInst.ToInt64():X}");
            if (buildHelperInst != IntPtr.Zero)
            {
                // 手动执行 Dismantle 的关键步骤（避免直接调用 Dismantle 导致崩溃）
                // IDA 流程: Facility.CloseWindow -> SetJobCount(0) -> RemoveFacility -> AfterDismantle
                IntPtr bhClass = Il2CppApi.GetClass(buildHelperInst);

                // Step 1: Facility.CloseWindow(f)
                {
                    IntPtr closeWindowMth = Il2CppApi.GetMethodFromName(classPtr, "CloseWindow", 0);
                    if (closeWindowMth == IntPtr.Zero)
                    {
                        IntPtr searchCls = classPtr;
                        int d = 0;
                        while (searchCls != IntPtr.Zero && d < 10 && closeWindowMth == IntPtr.Zero)
                        {
                            closeWindowMth = Il2CppApi.GetMethodFromName(searchCls, "CloseWindow", 0);
                            searchCls = Il2CppApi.GetParent(searchCls);
                            d++;
                        }
                    }
                    if (closeWindowMth != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr ex = IntPtr.Zero;
                            unsafe { Il2CppApi.RuntimeInvoke(closeWindowMth, e.Ptr, null, ref ex); }
                            Plugin.LogInfo($"[EntityEditor] Facility.CloseWindow() done, ex={ex != IntPtr.Zero}");
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] CloseWindow failed: {ex.Message}"); }
                    }
                }

                // Step 2: 尝试 Facility.Dismantle(6p) — 之前测试过不崩溃
                {
                    IntPtr disMth = Il2CppApi.GetMethodFromName(classPtr, "Dismantle", 6);
                    if (disMth == IntPtr.Zero)
                    {
                        IntPtr searchCls = classPtr;
                        int d = 0;
                        while (searchCls != IntPtr.Zero && d < 10 && disMth == IntPtr.Zero)
                        {
                            disMth = Il2CppApi.GetMethodFromName(searchCls, "Dismantle", 6);
                            searchCls = Il2CppApi.GetParent(searchCls);
                            d++;
                        }
                    }
                    if (disMth != IntPtr.Zero)
                    {
                        Plugin.LogInfo($"[EntityEditor] Found Facility.Dismantle(6p), calling...");
                        try
                        {
                            bool ok = Il2CppInvoke.InvokeWithDefaults(disMth, e.Ptr, 6);
                            Plugin.LogInfo($"[EntityEditor] Facility.Dismantle(6p) done, exception={!ok}");
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Facility.Dismantle(6p) failed: {ex.Message}"); }
                    }
                }

                // Step 3: 从 BuildHelper 的 facility 列表中移除
                // 遍历 BuildHelper 的所有 List<Facility> 字段，移除目标 Facility
                {
                    IntPtr fi = IntPtr.Zero;
                    IntPtr f;
                    while ((f = Il2CppApi.ClassGetFields(bhClass, ref fi)) != IntPtr.Zero)
                    {
                        string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                        IntPtr ft = Il2CppApi.FieldGetType(f);
                        string? ftn = ft != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(ft)) : null;
                        if (fn == null || ftn == null) continue;
                        if (!ftn.Contains("List<Facility") && !ftn.Contains("List<Facility")) continue;

                        int offset = (int)Il2CppApi.FieldGetOffset(f);
                        IntPtr listPtr;
                        unsafe { listPtr = *(IntPtr*)(buildHelperInst + offset); }
                        if (listPtr == IntPtr.Zero) continue;

                        // 尝试调用 List.Remove(item)
                        IntPtr listClass = Il2CppApi.GetClass(listPtr);
                        IntPtr removeMth = Il2CppApi.GetMethodFromName(listClass, "Remove", 1);
                        if (removeMth != IntPtr.Zero)
                        {
                            try
                            {
                                IntPtr ex = IntPtr.Zero;
                                IntPtr[] args = new IntPtr[1];
                                IntPtr[] stor = new IntPtr[1];
                                stor[0] = e.Ptr;
                                unsafe
                                {
                                    fixed (IntPtr* sp = stor)
                                    {
                                        args[0] = (IntPtr)(&sp[0]);
                                        fixed (IntPtr* ap = args)
                                        {
                                            Il2CppApi.RuntimeInvoke(removeMth, listPtr, (void**)ap, ref ex);
                                        }
                                    }
                                }
                                Plugin.LogInfo($"[EntityEditor] BH.{fn}.Remove(facility) done, ex={ex != IntPtr.Zero}");
                            }
                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] BH.{fn}.Remove failed: {ex.Message}"); }
                        }
                    }
                }

                // Step 4: RecycleMySp 清理渲染资源
                {
                    IntPtr rCls = classPtr;
                    int rDepth = 0;
                    while (rCls != IntPtr.Zero && rDepth < 10)
                    {
                        IntPtr rMth = Il2CppApi.GetMethodFromName(rCls, "RecycleMySp", 0);
                        if (rMth != IntPtr.Zero)
                        {
                            try
                            {
                                IntPtr ex = IntPtr.Zero;
                                unsafe { Il2CppApi.RuntimeInvoke(rMth, e.Ptr, null, ref ex); }
                                Plugin.LogInfo($"[EntityEditor] RecycleMySp() done, ex={ex != IntPtr.Zero}");
                            }
                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] RecycleMySp failed: {ex.Message}"); }
                            break;
                        }
                        rCls = Il2CppApi.GetParent(rCls);
                        rDepth++;
                    }
                }

                // Step 5: 隐藏 + 销毁 GameObject
                try
                {
                    e.GoRef!.SetActive(false);
                    UnityEngine.Object.DestroyImmediate(e.GoRef);
                    Plugin.LogInfo($"[EntityEditor] DestroyImmediate done");
                }
                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] DestroyImmediate failed: {ex.Message}"); }

                Plugin.LogInfo($"[EntityEditor] Manual dismantle steps completed");
                called = true;
            }
                else
                    Plugin.LogInfo($"[EntityEditor] Could not get BuildHelper instance");
            }
        return called;
    }

    /// <summary>Facility 兜底策略：在其他管理器类上按名搜索 RemoveFacility / DelFacility / DestroyFacility</summary>
    private static bool TryDestroyFacilityViaManagers(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
        if (!called && IsFacilityClass(className))
        {
            Plugin.LogInfo($"[EntityEditor] [Facility] Trying other manager classes...");
            string[] managerClassNames = { "Territory", "AreaMap", "FacilityManager", "FacilityCtrl", "GameCtrl", "MainScene" };
            foreach (var mgrName in managerClassNames)
            {
                if (called) break;
                IntPtr mgrClass = FindClassByName(mgrName);
                if (mgrClass == IntPtr.Zero) continue;
                string[] removeNames = { "RemoveFacility", "DelFacility", "DestroyFacility" };
                foreach (var methodName in removeNames)
                {
                    if (called) break;
                    for (int pc = 1; pc <= 3; pc++)
                    {
                        IntPtr mth = Il2CppApi.GetMethodFromName(mgrClass, methodName, pc);
                        if (mth != IntPtr.Zero)
                        {
                            IntPtr inst = GameChainLocator.FindClassInstance(mgrClass);
                            if (inst != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr[] args = new IntPtr[pc];
                                    args[0] = e.Ptr;
                                    if (Il2CppInvoke.InvokeWithArgs(mth, inst, args))
                                    {
                                        Plugin.LogInfo($"[EntityEditor] ✓ Called {mgrName}.{methodName}({pc}p)");
                                        called = true;
                                    }
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mgrName}.{methodName}({pc}p) CRASH: {ex.Message}"); }
                            }
                        }
                    }
                }
            }
        }
        return called;
    }
}
