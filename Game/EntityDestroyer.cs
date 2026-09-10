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
/// 实体销毁：按 Npc / Facility / Ship / 通用 四级策略销毁并清理管理器引用
/// </summary>
internal static class EntityDestroyer
{

    /// <summary>
    /// 消除实体：调用游戏的 LeaveMapAndDestroy / DestroyGo / OnDead
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

                bool called = false;

                // === 第一优先：NPC 类型用 LeaveMapAndDestroy ===
                // LeaveMapAndDestroy 是虚方法，il2cpp_class_get_method_from_name 搜不到
                // 需要用 il2cpp_class_get_methods 枚举
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
                                        IntPtr exception = IntPtr.Zero;
                                        if (pCount == 0)
                                        {
                                            unsafe { Il2CppApi.RuntimeInvoke(mth, e.Ptr, null, ref exception); }
                                        }
                                        else
                                        {
                                            // 有参数的方法，传默认值
                                            IntPtr[] argPtrs = new IntPtr[pCount];
                                            IntPtr[] storage = new IntPtr[pCount];
                                            for (int a = 0; a < (int)pCount; a++)
                                            {
                                                IntPtr paramType = Il2CppApi.GetMethodParam(mth, (uint)a);
                                                string? tn = paramType != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(paramType)) : null;
                                                bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                                storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                            }
                                            unsafe
                                            {
                                                fixed (IntPtr* storPtr = storage)
                                                {
                                                    for (int a = 0; a < (int)pCount; a++)
                                                        argPtrs[a] = (IntPtr)(&storPtr[a]);
                                                    fixed (IntPtr* argsArr = argPtrs)
                                                    {
                                                        Il2CppApi.RuntimeInvoke(mth, e.Ptr, (void**)argsArr, ref exception);
                                                    }
                                                }
                                            }
                                        }
                                        if (exception != IntPtr.Zero)
                                            Plugin.LogInfo($"[EntityEditor] {mName}() exception on {name}");
                                        else
                                        {
                                            Plugin.LogInfo($"[EntityEditor] ✓ Called {mName}() on {name} (depth={depth})");
                                            called = true;
                                            break;
                                        }
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

                // === 第二优先：Facility 类型用 BuildHelper.RemoveFacility ===
                // IDA 伪代码显示 BuildHelper__Dismantle 的核心流程是:
                //   BuildHelper__RemoveFacility(this, f)
                //   f->vtable._85_AfterDismantle(f, WagonOutPos, rotation, is_destroy, method)
                if (!called && IsFacilityClass(className))
                {
                    Plugin.LogInfo($"[EntityEditor] [Facility] Looking for BuildHelper via Territory...");
                    // IDA: territory->fields.build_helper -> BuildHelper__RemoveFacility(buildHelper, facility)
                    // build_helper 不是单例，是 Territory 的实例字段
                    IntPtr buildHelperInst = FindBuildHelperFromTerritory();
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
                                    IntPtr exception = IntPtr.Zero;
                                    int pc = 6;
                                    IntPtr[] argPtrs = new IntPtr[pc];
                                    IntPtr[] storage = new IntPtr[pc];
                                    for (int a = 0; a < pc; a++)
                                    {
                                        IntPtr paramType = Il2CppApi.GetMethodParam(disMth, (uint)a);
                                        string? tn = paramType != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(paramType)) : null;
                                        bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                        storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                    }
                                    unsafe
                                    {
                                        fixed (IntPtr* storPtr = storage)
                                        {
                                            for (int a = 0; a < pc; a++)
                                                argPtrs[a] = (IntPtr)(&storPtr[a]);
                                            fixed (IntPtr* argsArr = argPtrs)
                                            {
                                                Il2CppApi.RuntimeInvoke(disMth, e.Ptr, (void**)argsArr, ref exception);
                                            }
                                        }
                                    }
                                    Plugin.LogInfo($"[EntityEditor] Facility.Dismantle(6p) done, exception={exception != IntPtr.Zero}");
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
                            e.GoRef.SetActive(false);
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




                // === Ship专用处理 ===
                // 从船对象自身读取所属 territory（可能是敌方 territory），而非玩家 territory
                if (!called && (className.Contains("Ship") || className.Contains("BattleUnit") || className.Contains("Soldier")))
                {
                    Plugin.LogInfo($"[EntityEditor] [Ship] Ship destroy start...");
                    bool shipDestroyed = false;

                    int guid = 0;
                    if (e.FieldMeta.TryGetValue("guid", out var guidFe))
                        try { guid = ReadIl2CppInt(e.Ptr, guidFe.Offset); } catch { }
                    Plugin.LogInfo($"[EntityEditor] [Ship] entity guid={guid}");

                    // 从船对象自身读取 territory 字段（Ship 继承自 MyMonoBehaviour，有 territory 引用）
                    IntPtr shipTerritoryPtr = ReadFieldSafe(e.Ptr, classPtr, "territory");
                    if (shipTerritoryPtr == IntPtr.Zero)
                    {
                        // 递归搜索父类找 territory 字段
                        IntPtr searchCls = classPtr;
                        int depth = 0;
                        while (searchCls != IntPtr.Zero && depth < 10)
                        {
                            IntPtr fi = IntPtr.Zero;
                            IntPtr f;
                            while ((f = Il2CppApi.ClassGetFields(searchCls, ref fi)) != IntPtr.Zero)
                            {
                                string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                                if (fn == "territory" || fn == "_territory")
                                {
                                    int offset = (int)Il2CppApi.FieldGetOffset(f);
                                    if (offset >= 0x10 && offset < 0x10000)
                                        unsafe { shipTerritoryPtr = *(IntPtr*)(e.Ptr + offset); }
                                    if (shipTerritoryPtr != IntPtr.Zero) break;
                                }
                            }
                            if (shipTerritoryPtr != IntPtr.Zero) break;
                            searchCls = Il2CppApi.GetParent(searchCls);
                            depth++;
                        }
                    }

                    if (shipTerritoryPtr != IntPtr.Zero)
                    {
                        IntPtr shipTerritoryClass = Il2CppApi.GetClass(shipTerritoryPtr);
                        string? territoryName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(shipTerritoryClass));
                        Plugin.LogInfo($"[EntityEditor] [Ship] Ship's territory class={territoryName}, ptr={shipTerritoryPtr.ToInt64():X}");

                        // 从船的所属 territory 读取 ship_list 和 ship_dic
                        IntPtr shipListPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_list");
                        IntPtr shipDicPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_dic");
                        Plugin.LogInfo($"[EntityEditor] [Ship] ship_list={shipListPtr.ToInt64():X}, ship_dic={shipDicPtr.ToInt64():X}");

                        // 按指针从 ship_list 中找到并移除
                        if (shipListPtr != IntPtr.Zero)
                        {
                            Plugin.LogInfo($"[EntityEditor] [Ship] Removing from ship_list by pointer match...");
                            shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                            Plugin.LogInfo($"[EntityEditor] [Ship] RemoveFromListByPtr result={shipDestroyed}");
                        }

                        // 从 ship_dic 按 guid 移除
                        if (shipDicPtr != IntPtr.Zero && guid != 0)
                        {
                            IntPtr dicClass = Il2CppApi.GetClass(shipDicPtr);
                            IntPtr removeMth = Il2CppApi.GetMethodFromName(dicClass, "Remove", 1);
                            if (removeMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exRm = IntPtr.Zero;
                                    unsafe
                                    {
                                        int guidArg = guid;
                                        IntPtr* rmArgs = stackalloc IntPtr[1];
                                        rmArgs[0] = (IntPtr)(&guidArg);
                                        Il2CppApi.RuntimeInvoke(removeMth, shipDicPtr, (void**)rmArgs, ref exRm);
                                    }
                                    Plugin.LogInfo($"[EntityEditor] [Ship] ship_dic.Remove({guid}) ex={exRm != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [Ship] ship_dic.Remove failed: {ex.Message}"); }
                            }
                        }

                        // 调用 ShipHelper.DestroyShip 做完整清理
                        IntPtr shipHelperPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_helper");
                        if (shipHelperPtr != IntPtr.Zero)
                        {
                            IntPtr shClass = Il2CppApi.GetClass(shipHelperPtr);
                            IntPtr destroyShipMth = IntPtr.Zero;
                            {
                                IntPtr shIter = IntPtr.Zero;
                                IntPtr shM;
                                while ((shM = Il2CppApi.ClassGetMethods(shClass, ref shIter)) != IntPtr.Zero)
                                {
                                    string? shName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(shM));
                                    if (shName == "DestroyShip") { destroyShipMth = shM; break; }
                                }
                            }
                            if (destroyShipMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exDestroy = IntPtr.Zero;
                                    unsafe
                                    {
                                        int boolArg = 0;
                                        IntPtr* destroyArgs = stackalloc IntPtr[2];
                                        destroyArgs[0] = e.Ptr;
                                        destroyArgs[1] = (IntPtr)(&boolArg);
                                        Il2CppApi.RuntimeInvoke(destroyShipMth, shipHelperPtr, (void**)destroyArgs, ref exDestroy);
                                    }
                                    Plugin.LogInfo($"[EntityEditor] [Ship] DestroyShip call ex={exDestroy != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [Ship] DestroyShip failed: {ex.Message}"); }
                            }
                            else
                            {
                                CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                                CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                                CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                            }
                        }
                        else
                        {
                            CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                            CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                            CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                        }

                        // 最终验证
                        if (shipListPtr != IntPtr.Zero)
                        {
                            IntPtr listClass = Il2CppApi.GetClass(shipListPtr);
                            int finalSize = ReadIntFieldSafe(shipListPtr, listClass, "_size", -1);
                            Plugin.LogInfo($"[EntityEditor] [Ship] Final: ship_list._size={finalSize}");
                        }
                    }
                    else
                    {
                        Plugin.LogInfo($"[EntityEditor] [Ship] Could not find territory field on ship, trying FindTerritory fallback...");
                        // 回退到 FindTerritory
                        IntPtr territoryPtr = FindTerritory();
                        if (territoryPtr != IntPtr.Zero)
                        {
                            IntPtr tc = Il2CppApi.GetClass(territoryPtr);
                            IntPtr shipListPtr = ReadFieldSafe(territoryPtr, tc, "ship_list");
                            if (shipListPtr != IntPtr.Zero)
                                shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                        }
                        CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                    }

                    // 兜底清理
                    if (!shipDestroyed)
                    {
                        Plugin.LogInfo($"[EntityEditor] [Ship] Fallback cleanup...");
                        CallVoidMethod(classPtr, e.Ptr, "UnIndexUnitByPos", 0);
                        CallVoidMethod(classPtr, e.Ptr, "BeforeDestroy", 0);
                        // is_dead
                        {
                            IntPtr idCls = classPtr;
                            int idD = 0;
                            while (idCls != IntPtr.Zero && idD < 10)
                            {
                                IntPtr fi = IntPtr.Zero;
                                IntPtr f;
                                while ((f = Il2CppApi.ClassGetFields(idCls, ref fi)) != IntPtr.Zero)
                                {
                                    string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                                    if (fn == "is_dead")
                                    {
                                        int offset = (int)Il2CppApi.FieldGetOffset(f);
                                        if (offset >= 0x10 && offset < 0x10000)
                                            unsafe { *(int*)(e.Ptr + offset) = 1; }
                                        break;
                                    }
                                }
                                idCls = Il2CppApi.GetParent(idCls);
                                idD++;
                            }
                        }
                        try { e.GoRef.SetActive(false); } catch { }
                    }
                    called = true;
                }
                // === 第三优先：通用销毁方法（非NPC/非Facility） ===
                // 动物、船、掉落物等：先在自身类链搜索虚方法，再搜索管理器类
                if (!called && !className.Contains("Npc") && !IsFacilityClass(className))
                {
                    // 3a: 在实体自身的类继承链中搜索销毁相关虚方法
                    string[] targetNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead", "LeaveMap", "OnLeaveMap", "Despawn", "RecycleSelf", "Dead", "Die", "OnDie", "OnDestroy", "BeforeRecycle", "Leave", "DeadOnBattle", "BeSeckill" };
                    IntPtr searchCls = classPtr;
                    int depth = 0;
                    // 先记录找到的方法名用于日志
                    var foundMethods = new List<(string name, uint paramCount, IntPtr methodPtr, int d, string cls)>();
                    while (searchCls != IntPtr.Zero && depth < 15)
                    {
                        string clsName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(searchCls)) ?? "?";
                        IntPtr iter = IntPtr.Zero;
                        IntPtr mth;
                        while ((mth = Il2CppApi.ClassGetMethods(searchCls, ref iter)) != IntPtr.Zero)
                        {
                            string? mName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(mth));
                            if (mName == null) continue;
                            uint pc = Il2CppApi.GetMethodParamCountRaw(mth);
                            foreach (var target in targetNames)
                            {
                                if (mName == target)
                                {
                                    foundMethods.Add((mName, pc, mth, depth, clsName));
                                    break;
                                }
                            }
                        }
                        searchCls = Il2CppApi.GetParent(searchCls);
                        depth++;
                    }

                    Plugin.LogInfo($"[EntityEditor] [Generic] {className}: found {foundMethods.Count} target methods in class hierarchy");
                    foreach (var fm in foundMethods)
                        Plugin.LogInfo($"[EntityEditor] [Generic]   {fm.cls}.{fm.name}({fm.paramCount}p) depth={fm.d}");

                    // 尝试调用找到的方法（优先 0 参数的）
                    foreach (var fm in foundMethods.OrderBy(f => f.paramCount))
                    {
                        if (called) break;
                        try
                        {
                            IntPtr exception = IntPtr.Zero;
                            if (fm.paramCount == 0)
                            {
                                unsafe { Il2CppApi.RuntimeInvoke(fm.methodPtr, e.Ptr, null, ref exception); }
                            }
                            else
                            {
                                IntPtr[] argPtrs = new IntPtr[fm.paramCount];
                                IntPtr[] storage = new IntPtr[fm.paramCount];
                                for (int a = 0; a < (int)fm.paramCount; a++)
                                {
                                    IntPtr paramType = Il2CppApi.GetMethodParam(fm.methodPtr, (uint)a);
                                    string? tn = paramType != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(paramType)) : null;
                                    bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                    storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                }
                                unsafe
                                {
                                    fixed (IntPtr* storPtr = storage)
                                    {
                                        for (int a = 0; a < (int)fm.paramCount; a++)
                                            argPtrs[a] = (IntPtr)(&storPtr[a]);
                                        fixed (IntPtr* argsArr = argPtrs)
                                        {
                                            Il2CppApi.RuntimeInvoke(fm.methodPtr, e.Ptr, (void**)argsArr, ref exception);
                                        }
                                    }
                                }
                            }
                            if (exception != IntPtr.Zero)
                                Plugin.LogInfo($"[EntityEditor] {fm.name}() exception on {name}");
                            else
                            {
                                Plugin.LogInfo($"[EntityEditor] ✓ Called {fm.name}() on {name} (depth={fm.d})");
                                called = true;
                            }
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {fm.name}() CRASH: {ex.Message}"); }
                    }

                    // 3b: 如果自身类没有找到，列出所有方法（0-2参数）用于调试
                    if (!called)
                    {
                        Plugin.LogInfo($"[EntityEditor] [Generic] Listing ALL methods (0-2p) on {className} hierarchy for debugging:");
                        IntPtr debugCls = classPtr;
                        int debugDepth = 0;
                        int methodCount = 0;
                        while (debugCls != IntPtr.Zero && debugDepth < 10)
                        {
                            string dClsName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(debugCls)) ?? "?";
                            IntPtr dIter = IntPtr.Zero;
                            IntPtr dMth;
                            while ((dMth = Il2CppApi.ClassGetMethods(debugCls, ref dIter)) != IntPtr.Zero)
                            {
                                string? dName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(dMth));
                                if (dName == null) continue;
                                uint dPc = Il2CppApi.GetMethodParamCountRaw(dMth);
                                if (dPc <= 2)
                                {
                                    uint dIflags = 0;
                                    uint dFlags = Il2CppApi.MethodGetFlags(dMth, ref dIflags);
                                    bool dStatic = (dFlags & 0x10) != 0;
                                    Plugin.LogInfo($"[EntityEditor] [Generic]   {dClsName}.{dName}({dPc}p) static={dStatic} depth={debugDepth}");
                                    methodCount++;
                                    if (methodCount >= 80) break;
                                }
                            }
                            if (methodCount >= 80) break;
                            debugCls = Il2CppApi.GetParent(debugCls);
                            debugDepth++;
                        }
                        Plugin.LogInfo($"[EntityEditor] [Generic] Total: {methodCount} methods listed");

                        // 3c: StuffOnMap 专用：手动执行完整删除流程
                        if (!called && (className.Contains("StuffOnMap") || className.Contains("DropItem")))
                        {
                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Manual destroy (field enumeration)...");
                            IntPtr mapStuffHelper = FindMapStuffHelper();
                            bool dicRemoved = false;
                            if (mapStuffHelper != IntPtr.Zero)
                            {
                                IntPtr mshClass = Il2CppApi.GetClass(mapStuffHelper);
                                IntPtr mshAreaMap = ReadFieldSafe(mapStuffHelper, mshClass, "area_map");
                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] MapStuffHelper.area_map={mshAreaMap.ToInt64():X}");
                                if (mshAreaMap != IntPtr.Zero)
                                {
                                    IntPtr areaMapClass = Il2CppApi.GetClass(mshAreaMap);

                                    // 读取 guid
                                    int guid = 0;
                                    if (e.FieldMeta.TryGetValue("guid", out var guidFe))
                                        try { guid = ReadIl2CppInt(e.Ptr, guidFe.Offset); } catch { }
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] entity guid={guid}");

                                    // Step A: 从 stuff_on_map_dic 移除
                                    IntPtr dicPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_dic");
                                    if (dicPtr != IntPtr.Zero && guid != 0)
                                    {
                                        IntPtr dicClass = Il2CppApi.GetClass(dicPtr);
                                        IntPtr removeMth = Il2CppApi.GetMethodFromName(dicClass, "Remove", 1);
                                        if (removeMth != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                IntPtr exRm = IntPtr.Zero;
                                                unsafe
                                                {
                                                    int guidArg = guid;
                                                    IntPtr* rmArgs = stackalloc IntPtr[1];
                                                    rmArgs[0] = (IntPtr)(&guidArg);
                                                    Il2CppApi.RuntimeInvoke(removeMth, dicPtr, (void**)rmArgs, ref exRm);
                                                }
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic.Remove({guid}) ex={exRm != IntPtr.Zero}");
                                            }
                                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic.Remove failed: {ex.Message}"); }
                                        }
                                    }

                                    // Step B: 从 stuff_on_map_dic_by_pos 移除
                                    // 通过 IL2CPP 字段枚举找到 _pos_point 字段（在父类 Item3dClickable 上）
                                    IntPtr dicByPosPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_dic_by_pos");
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] stuff_on_map_dic_by_pos={dicByPosPtr.ToInt64():X}");
                                    if (dicByPosPtr != IntPtr.Zero)
                                    {
                                        int posPointOffset = -1;
                                        IntPtr posSearchCls = classPtr;
                                        int posSearchD = 0;
                                        while (posSearchCls != IntPtr.Zero && posSearchD < 10)
                                        {
                                            IntPtr fi = IntPtr.Zero;
                                            IntPtr f;
                                            while ((f = Il2CppApi.ClassGetFields(posSearchCls, ref fi)) != IntPtr.Zero)
                                            {
                                                string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                                                if (fn == "_pos_point")
                                                {
                                                    int offset = (int)Il2CppApi.FieldGetOffset(f);
                                                    if (offset >= 0x10 && offset < 0x10000)
                                                    {
                                                        posPointOffset = offset;
                                                        Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Found _pos_point at offset=0x{offset:X} on {Il2CppApi.PtrToString(Il2CppApi.ClassGetName(posSearchCls))}");
                                                    }
                                                    break;
                                                }
                                            }
                                            if (posPointOffset >= 0) break;
                                            posSearchCls = Il2CppApi.GetParent(posSearchCls);
                                            posSearchD++;
                                        }

                                        if (posPointOffset >= 0)
                                        {
                                            int px = 0, py = 0;
                                            unsafe
                                            {
                                                px = *(int*)(e.Ptr + posPointOffset);
                                                py = *(int*)(e.Ptr + posPointOffset + 4);
                                            }
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] _pos_point=({px},{py})");

                                            // MyListDic.Remove(Point, item) — 枚举找虚方法
                                            IntPtr dicByPosClass = Il2CppApi.GetClass(dicByPosPtr);
                                            IntPtr removeMth = IntPtr.Zero;
                                            {
                                                IntPtr rIter = IntPtr.Zero;
                                                IntPtr rM;
                                                while ((rM = Il2CppApi.ClassGetMethods(dicByPosClass, ref rIter)) != IntPtr.Zero)
                                                {
                                                    string? rName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(rM));
                                                    uint rPc = Il2CppApi.GetMethodParamCountRaw(rM);
                                                    if (rName == "Remove" && rPc == 2) { removeMth = rM; break; }
                                                }
                                            }
                                            if (removeMth != IntPtr.Zero)
                                            {
                                                try
                                                {
                                                    IntPtr exRm = IntPtr.Zero;
                                                    unsafe
                                                    {
                                                        int* pointData = stackalloc int[2];
                                                        pointData[0] = px;
                                                        pointData[1] = py;
                                                        IntPtr* rmArgs = stackalloc IntPtr[2];
                                                        rmArgs[0] = (IntPtr)pointData;
                                                        rmArgs[1] = e.Ptr;
                                                        Il2CppApi.RuntimeInvoke(removeMth, dicByPosPtr, (void**)rmArgs, ref exRm);
                                                    }
                                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic_by_pos.Remove(({px},{py}), entity) ex={exRm != IntPtr.Zero}");
                                                    dicRemoved = true;
                                                }
                                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic_by_pos.Remove failed: {ex.Message}"); }
                                            }
                                            else
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] MyListDic.Remove(2p) not found");
                                        }
                                        else
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] _pos_point field not found in class hierarchy");
                                    }

                                    // Step C: 从 stuff_on_map_list 移除
                                    IntPtr listPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_list");
                                    if (listPtr != IntPtr.Zero)
                                    {
                                        IntPtr listClass = Il2CppApi.GetClass(listPtr);
                                        IntPtr removeMth = Il2CppApi.GetMethodFromName(listClass, "Remove", 1);
                                        if (removeMth != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                IntPtr exRm = IntPtr.Zero;
                                                unsafe
                                                {
                                                    IntPtr* rmArgs = stackalloc IntPtr[1];
                                                    rmArgs[0] = e.Ptr;
                                                    Il2CppApi.RuntimeInvoke(removeMth, listPtr, (void**)rmArgs, ref exRm);
                                                }
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] list.Remove(entity) ex={exRm != IntPtr.Zero}");
                                            }
                                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] list.Remove failed: {ex.Message}"); }
                                        }
                                    }
                                }
                            }

                            // Step D: 设置 is_dead 标记
                            if (e.FieldMeta.TryGetValue("is_dead", out var isDeadFe))
                            {
                                try { WriteIl2CppInt(e.Ptr, isDeadFe.Offset, 1); Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Set is_dead=1"); } catch { }
                            }

                            // Step E: 隐藏实体 — 优先用 RecycleToCache，但如果字典移除可能失败则用 GameObject.SetActive(false)
                            // RecycleToCache 在状态不一致时可能触发 native crash，所以只在字典移除成功后调用
                            if (dicRemoved)
                            {
                                IntPtr rtCls = classPtr;
                                int rtD = 0;
                                while (rtCls != IntPtr.Zero && rtD < 10)
                                {
                                    IntPtr rtMth = Il2CppApi.GetMethodFromName(rtCls, "RecycleToCache", 0);
                                    if (rtMth != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr exRt = IntPtr.Zero;
                                            unsafe { Il2CppApi.RuntimeInvoke(rtMth, e.Ptr, null, ref exRt); }
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] RecycleToCache() ex={exRt != IntPtr.Zero}");
                                        }
                                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] RecycleToCache failed: {ex.Message}"); }
                                        break;
                                    }
                                    rtCls = Il2CppApi.GetParent(rtCls);
                                    rtD++;
                                }
                            }
                            else
                            {
                                // 字典移除可能失败，只做视觉隐藏
                                try
                                {
                                    e.GoRef.SetActive(false);
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] SetActive(false) — dic removal may have failed");
                                }
                                catch { }
                            }
                            called = true;
                        }

                        // 3d: 尝试通过其他管理器类删除
                        if (!called)
                            Plugin.LogInfo($"[EntityEditor] [Generic] No destroy method on {className}, trying manager classes...");
                        // 根据类名推断可能的管理器
                        string[] mgrCandidates;
                        if (className.Contains("Animal"))
                            mgrCandidates = new[] { "AnimalHelper", "NpcHelper", "AnimalManager", "AnimalCtrl" };
                        else if (className.Contains("Ship"))
                            mgrCandidates = new[] { "ShipHelper", "ShipManager", "VehicleHelper", "VehicleManager" };
                        else if (className.Contains("StuffOnMap") || className.Contains("DropItem"))
                            mgrCandidates = new[] { "MapStuffHelper", "StuffOnMapHelper", "DropItemHelper", "StuffOnMapManager", "MapItemHelper", "StuffManager", "StuffHelper", "ItemDropHelper" };
                        else
                            mgrCandidates = new[] { "NpcHelper", "EntityManager", "PrefabManager" };

                        foreach (var mgrName in mgrCandidates)
                        {
                            if (called) break;
                            IntPtr mgrClass = FindClassByName(mgrName);
                            if (mgrClass == IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] [Generic] Manager class {mgrName} not found");
                                continue;
                            }
                            IntPtr mgrInst = FindClassInstance(mgrClass);
                            if (mgrInst == IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] [Generic] Manager {mgrName} instance not found");
                                continue;
                            }
                            Plugin.LogInfo($"[EntityEditor] [Generic] Found manager {mgrName}, searching remove methods...");

                            // 枚举管理器的方法，查找包含 Remove/Despawn/Kill/Delete 的方法
                            string[] removeKeywords = { "Remove", "Despawn", "Delete", "Kill", "Destroy", "Clear", "Release", "Recycle" };
                            IntPtr mIter = IntPtr.Zero;
                            IntPtr mMth;
                            while ((mMth = Il2CppApi.ClassGetMethods(mgrClass, ref mIter)) != IntPtr.Zero)
                            {
                                string? mName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(mMth));
                                if (mName == null) continue;
                                bool matches = false;
                                foreach (var kw in removeKeywords)
                                {
                                    if (mName.Contains(kw, StringComparison.OrdinalIgnoreCase)) { matches = true; break; }
                                }
                                if (!matches) continue;

                                uint mPc = Il2CppApi.GetMethodParamCountRaw(mMth);
                                // 获取参数类型信息
                                string paramInfo = "";
                                for (int pi = 0; pi < (int)mPc; pi++)
                                {
                                    IntPtr pt = Il2CppApi.GetMethodParam(mMth, (uint)pi);
                                    string? ptn = pt != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(pt)) : "?";
                                    if (pi > 0) paramInfo += ", ";
                                    paramInfo += ptn;
                                }
                                // 检查是否是静态方法 (METHOD_ATTRIBUTE_STATIC = 0x10)
                                uint iflags = 0;
                                uint methodFlags = Il2CppApi.MethodGetFlags(mMth, ref iflags);
                                bool isStatic = (methodFlags & 0x10) != 0;
                                Plugin.LogInfo($"[EntityEditor] [Generic]   {mgrName}.{mName}({mPc}p) params=({paramInfo}) static={isStatic}");

                                if (mPc != 1) continue; // 只尝试 1 参数的（传入实体指针）

                                // 获取参数类型名，检查是否兼容
                                IntPtr paramType = Il2CppApi.GetMethodParam(mMth, 0);
                                string? paramTypeName = paramType != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(paramType)) : null;
                                Plugin.LogInfo($"[EntityEditor] [Generic]   param0 type={paramTypeName}, entity class={className}");

                                try
                                {
                                    IntPtr exception = IntPtr.Zero;
                                    if (isStatic)
                                    {
                                        // 静态方法：不传 this，只传参数
                                        IntPtr[] argPtrs = new IntPtr[1];
                                        IntPtr[] storage = new IntPtr[1];
                                        storage[0] = e.Ptr;
                                        unsafe
                                        {
                                            fixed (IntPtr* storPtr = storage)
                                            {
                                                argPtrs[0] = (IntPtr)(&storPtr[0]);
                                                fixed (IntPtr* argsArr = argPtrs)
                                                {
                                                    Il2CppApi.RuntimeInvoke(mMth, IntPtr.Zero, (void**)argsArr, ref exception);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // 实例方法
                                        IntPtr[] argPtrs = new IntPtr[1];
                                        IntPtr[] storage = new IntPtr[1];
                                        storage[0] = e.Ptr;
                                        unsafe
                                        {
                                            fixed (IntPtr* storPtr = storage)
                                            {
                                                argPtrs[0] = (IntPtr)(&storPtr[0]);
                                                fixed (IntPtr* argsArr = argPtrs)
                                                {
                                                    Il2CppApi.RuntimeInvoke(mMth, mgrInst, (void**)argsArr, ref exception);
                                                }
                                            }
                                        }
                                    }
                                    if (exception == IntPtr.Zero)
                                    {
                                        Plugin.LogInfo($"[EntityEditor] ✓ Called {mgrName}.{mName}(entity) on {name}");
                                        called = true;
                                    }
                                    else
                                        Plugin.LogInfo($"[EntityEditor] {mgrName}.{mName}(entity) IL2CPP exception (ptr={exception.ToInt64():X})");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mgrName}.{mName}(entity) CRASH: {ex.Message}"); }
                                if (called) break;
                            }
                        }
                    }
                }

                // === 第四优先：在其他管理类上搜索 Remove/Del 方法 ===
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
                                    IntPtr inst = FindClassInstance(mgrClass);
                                    if (inst != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr exception = IntPtr.Zero;
                                            IntPtr[] argPtrs = new IntPtr[pc];
                                            IntPtr[] storage = new IntPtr[pc];
                                            storage[0] = e.Ptr;
                                            for (int a = 1; a < pc; a++) storage[a] = IntPtr.Zero;
                                            unsafe
                                            {
                                                fixed (IntPtr* storPtr = storage)
                                                {
                                                    for (int a = 0; a < pc; a++)
                                                        argPtrs[a] = (IntPtr)(&storPtr[a]);
                                                    fixed (IntPtr* argsArr = argPtrs)
                                                    {
                                                        Il2CppApi.RuntimeInvoke(mth, inst, (void**)argsArr, ref exception);
                                                    }
                                                }
                                            }
                                            if (exception == IntPtr.Zero)
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

                // === 兜底：BeforeDestroy + 从管理器移除 + 隐藏视觉 + DestroyImmediate ===
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
                                    IntPtr exH = IntPtr.Zero;
                                    IntPtr[] hArgs = new IntPtr[1];
                                    IntPtr[] hStorage = new IntPtr[1];
                                    hStorage[0] = IntPtr.Zero; // false
                                    unsafe
                                    {
                                        fixed (IntPtr* hStor = hStorage)
                                        {
                                            hArgs[0] = (IntPtr)(&hStor[0]);
                                            fixed (IntPtr* hArgsArr = hArgs)
                                            {
                                                Il2CppApi.RuntimeInvoke(hMth, e.Ptr, (void**)hArgsArr, ref exH);
                                            }
                                        }
                                    }
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
                        e.GoRef.SetActive(false);
                        UnityEngine.Object.DestroyImmediate(e.GoRef);
                        Plugin.LogInfo($"[EntityEditor] DestroyImmediate GO: {name}");
                        called = true;
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] DestroyImmediate failed: {ex.Message}"); }
                }

                Plugin.LogInfo($"[EntityEditor] === DestroyEntity END: {name} called={called} ===");
                EntityScan.Entities.RemoveAt(i);
                return "ok";
            }
            return $"entity not found: ptrHash={ptrHash}";
        }
        catch (Exception ex) { return ex.Message; }
    }


    /// <summary>
    /// 判断类名是否属于 Facility 层级
    /// </summary>
    private static bool IsFacilityClass(string className)
    {
        return className.Contains("Facility") || className.Contains("FacilityAncientTomb")
            || className.Contains("FacilityForMonster") || className.Contains("FacilityBarracks")
            || className.Contains("FacilityCityWall") || className.Contains("FacilityHive")
            || className.Contains("FacilityMine") || className.Contains("FacilityQuarry");
    }


    /// <summary>
    /// 查找游戏管理类的实例（通过静态字段或 FindObjectOfType）
    /// </summary>
    private static IntPtr FindClassInstance(IntPtr classPtr)
    {
        try
        {
            string className = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(classPtr)) ?? "?";
            // 方法1：查找静态 Instance/s_instance 字段并读取值
            foreach (var fh in Il2CppApi.EnumerateFieldHandles(classPtr))
            {
                if (!Il2CppApi.IsInstanceFieldName(fh.Name)) continue;

                Plugin.LogInfo($"[EntityEditor] Found field {fh.Name} on {className}, isStatic={fh.IsStatic}, attrs=0x{Il2CppApi.FieldGetFlags(fh.Field):X}");
                if (!fh.IsStatic) continue;

                try
                {
                    IntPtr value = Il2CppApi.ReadStaticFieldValue(fh.Field);
                    Plugin.LogInfo($"[EntityEditor] Static field {fh.Name} value={value.ToInt64():X}");
                    if (value == IntPtr.Zero) continue;

                    // 验证这个指针是否是一个有效的 IL2CPP 对象
                    IntPtr objClass = Il2CppApi.GetClass(value);
                    if (objClass == IntPtr.Zero) continue;

                    string? objClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(objClass));
                    Plugin.LogInfo($"[EntityEditor] ✓ Got instance from {fh.Name}, class={objClassName}");
                    return value;
                }
                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Read static field {fh.Name} error: {ex.Message}"); }
            }
            // 方法2：通过 Resources.FindObjectsOfTypeAll 查找
            // 我们需要通过 IL2CPP 的类型系统来查找
            // 尝试直接调用 FindObjectOfType
            IntPtr findMethod = Il2CppApi.GetMethodFromName(classPtr, "get_Instance", 0);
            if (findMethod != IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Found get_Instance() on {className}");
                IntPtr exception = IntPtr.Zero;
                unsafe
                {
                    IntPtr result = (IntPtr)Il2CppApi.RuntimeInvoke(findMethod, IntPtr.Zero, null, ref exception);
                    if (exception == IntPtr.Zero && result != IntPtr.Zero)
                    {
                        Plugin.LogInfo($"[EntityEditor] get_Instance() returned valid object");
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
                            Plugin.LogInfo($"[EntityEditor] Found instance of {className} on GO: {go.name}");
                            return comp.Pointer;
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindClassInstance error: {ex.Message}"); }
        return IntPtr.Zero;
    }


    /// <summary>
    /// 调用虚方法（通过方法枚举），忽略异常
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


    /// <summary>
    /// 获取 Territory 实例指针
    /// 链路: Game.get_main_scene() -> area_map -> get_my_territory()
    /// </summary>
    private static IntPtr FindTerritory()
    {
        try
        {
            IntPtr gameClass = FindClassByName("Game");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            IntPtr getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero) break;
                }
            }
            if (getMainSceneMth == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try { unsafe { mainScene = (IntPtr)Il2CppApi.RuntimeInvoke(getMainSceneMth, IntPtr.Zero, null, ref exception); } }
            catch { }
            if (mainScene == IntPtr.Zero) return IntPtr.Zero;

            IntPtr mainSceneClass = Il2CppApi.GetClass(mainScene);
            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr areaMapClass = Il2CppApi.GetClass(areaMapPtr);
            IntPtr territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "my_territory");
            if (territoryPtr == IntPtr.Zero)
                territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "_my_territory");

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
                    try { exception = IntPtr.Zero; unsafe { territoryPtr = (IntPtr)Il2CppApi.RuntimeInvoke(getMyTerritoryMth, areaMapPtr, null, ref exception); } }
                    catch { }
                }
            }

            if (territoryPtr != IntPtr.Zero)
            {
                IntPtr tc = Il2CppApi.GetClass(territoryPtr);
                string? tn = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(tc));
                Plugin.LogInfo($"[EntityEditor] ✓ Got Territory, class={tn}");
            }
            return territoryPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }


    /// <summary>
    /// 通过 Territory 实例获取 BuildHelper 指针
    /// IDA: territory->fields.build_helper
    /// </summary>
    private static IntPtr FindBuildHelperFromTerritory()
    {
        try
        {
            // IDA 完整链路: Game.get_main_scene() -> main_scene.area_map -> AreaMap.get_my_territory() -> territory.build_helper
            Plugin.LogInfo($"[EntityEditor] Finding BuildHelper via Game chain...");

            // Step 1: Game.get_main_scene()
            IntPtr gameClass = FindClassByName("Game");
            Plugin.LogInfo($"[EntityEditor] Game class={gameClass.ToInt64():X}");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            // 列出 Game 类的静态方法
            IntPtr iter = IntPtr.Zero;
            IntPtr mth;
            while ((mth = Il2CppApi.ClassGetMethods(gameClass, ref iter)) != IntPtr.Zero)
            {
                string? mName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(mth));
                uint pc = Il2CppApi.GetMethodParamCountRaw(mth);
                if (mName != null && (mName.Contains("main_scene") || mName.Contains("MainScene") || mName.Contains("instance") || mName.Contains("Instance")))
                    Plugin.LogInfo($"[EntityEditor] Game.{mName}({pc}p)");
            }

            IntPtr getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero)
                    {
                        Plugin.LogInfo($"[EntityEditor] Found Game.{alt}()");
                        break;
                    }
                }
            }
            if (getMainSceneMth == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Game.get_main_scene() not found");
                return IntPtr.Zero;
            }

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try
            {
                unsafe { mainScene = (IntPtr)Il2CppApi.RuntimeInvoke(getMainSceneMth, IntPtr.Zero, null, ref exception); }
            }
            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] get_main_scene() CRASH: {ex.Message}"); }
            if (exception != IntPtr.Zero || mainScene == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] get_main_scene() failed, result={mainScene.ToInt64():X}, exception={exception != IntPtr.Zero}");
                return IntPtr.Zero;
            }
            Plugin.LogInfo($"[EntityEditor] main_scene={mainScene.ToInt64():X}");

            // Step 2: main_scene.fields.area_map (读偏移量，不直接解引用)
            IntPtr mainSceneClass = Il2CppApi.GetClass(mainScene);
            Plugin.LogInfo($"[EntityEditor] mainScene class={mainSceneClass.ToInt64():X}");

            // 列出 MainScene 的字段
            IntPtr fi2 = IntPtr.Zero;
            IntPtr f2;
            while ((f2 = Il2CppApi.ClassGetFields(mainSceneClass, ref fi2)) != IntPtr.Zero)
            {
                string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f2));
                int offset = (int)Il2CppApi.FieldGetOffset(f2);
                IntPtr ft = Il2CppApi.FieldGetType(f2);
                string? ftn = ft != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(ft)) : "?";
                if (fn != null && (fn.Contains("area") || fn.Contains("map") || fn.Contains("territory") || fn.Contains("build")))
                    Plugin.LogInfo($"[EntityEditor] MainScene.{fn} offset={offset} type={ftn}");
            }

            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            Plugin.LogInfo($"[EntityEditor] area_map={areaMapPtr.ToInt64():X}");
            if (areaMapPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] main_scene.area_map is null");
                return IntPtr.Zero;
            }

            // Step 3: AreaMap.get_my_territory()
            IntPtr areaMapClass = Il2CppApi.GetClass(areaMapPtr);
            Plugin.LogInfo($"[EntityEditor] areaMap class={areaMapClass.ToInt64():X}");

            IntPtr territoryPtr = IntPtr.Zero;
            // 先尝试读字段
            territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "my_territory");
            if (territoryPtr == IntPtr.Zero)
                territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "_my_territory");
            Plugin.LogInfo($"[EntityEditor] area_map.my_territory (field) = {territoryPtr.ToInt64():X}");

            // 如果字段读取失败，尝试方法
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
                    try
                    {
                        exception = IntPtr.Zero;
                        unsafe { territoryPtr = (IntPtr)Il2CppApi.RuntimeInvoke(getMyTerritoryMth, areaMapPtr, null, ref exception); }
                        Plugin.LogInfo($"[EntityEditor] get_my_territory() = {territoryPtr.ToInt64():X}, exception={exception != IntPtr.Zero}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] get_my_territory() CRASH: {ex.Message}"); }
                }
            }

            if (territoryPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Could not get Territory");
                return IntPtr.Zero;
            }

            // Step 4: territory.fields.build_helper
            IntPtr territoryClass = Il2CppApi.GetClass(territoryPtr);
            Plugin.LogInfo($"[EntityEditor] territory class={territoryClass.ToInt64():X}");

            IntPtr buildHelperPtr = ReadFieldSafe(territoryPtr, territoryClass, "build_helper");
            if (buildHelperPtr == IntPtr.Zero)
                buildHelperPtr = ReadFieldSafe(territoryPtr, territoryClass, "_build_helper");
            Plugin.LogInfo($"[EntityEditor] territory.build_helper = {buildHelperPtr.ToInt64():X}");

            if (buildHelperPtr != IntPtr.Zero)
            {
                IntPtr bhClass = Il2CppApi.GetClass(buildHelperPtr);
                string? bhClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(bhClass));
                Plugin.LogInfo($"[EntityEditor] ✓ Got BuildHelper, class={bhClassName}");
            }
            return buildHelperPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindBuildHelperFromTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }


    /// <summary>
    /// 通过 Game.get_main_scene() → area_map → map_stuff_helper 获取 MapStuffHelper 指针
    /// IDA: Game.get_main_scene() -> main_scene.area_map -> area_map.map_stuff_helper
    /// </summary>
    private static IntPtr FindMapStuffHelper()
    {
        try
        {
            Plugin.LogInfo($"[EntityEditor] Finding MapStuffHelper via Game chain...");

            // Step 1: Game.get_main_scene()
            IntPtr gameClass = FindClassByName("Game");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            IntPtr getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppApi.GetMethodFromName(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero) break;
                }
            }
            if (getMainSceneMth == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try
            {
                unsafe { mainScene = (IntPtr)Il2CppApi.RuntimeInvoke(getMainSceneMth, IntPtr.Zero, null, ref exception); }
            }
            catch { return IntPtr.Zero; }
            if (mainScene == IntPtr.Zero) return IntPtr.Zero;

            // Step 2: main_scene.fields.area_map
            IntPtr mainSceneClass = Il2CppApi.GetClass(mainScene);
            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            // Step 3: area_map.fields.map_stuff_helper
            IntPtr areaMapClass = Il2CppApi.GetClass(areaMapPtr);
            IntPtr mapStuffHelperPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "map_stuff_helper");
            if (mapStuffHelperPtr != IntPtr.Zero)
            {
                IntPtr mshClass = Il2CppApi.GetClass(mapStuffHelperPtr);
                string? mshClassName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(mshClass));
                Plugin.LogInfo($"[EntityEditor] ✓ Got MapStuffHelper, class={mshClassName}");
            }
            else
                Plugin.LogInfo($"[EntityEditor] area_map.map_stuff_helper is null");
            return mapStuffHelperPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindMapStuffHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }


    /// <summary>
    /// 从 IL2CPP List 中按指针匹配移除元素，返回是否成功
    /// </summary>
    private static bool RemoveFromListByPtr(IntPtr listPtr, IntPtr targetPtr)
    {
        try
        {
            IntPtr listClass = Il2CppApi.GetClass(listPtr);
            int size = ReadIntFieldSafe(listPtr, listClass, "_size", 0);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _size={size}, target={targetPtr.ToInt64():X}");

            if (size <= 0) return false;

            // 读取 _items 数组
            IntPtr itemsPtr = ReadFieldSafe(listPtr, listClass, "_items");
            if (itemsPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _items is null");
                return false;
            }

            // IL2CPP 数组: 对象头 0x10, 然后是元素指针
            // _items 是 System.Object[]，每个元素是 IntPtr 大小
            IntPtr itemsClass = Il2CppApi.GetClass(itemsPtr);
            int arrLen = ReadIntFieldSafe(itemsPtr, itemsClass, "_length", 0);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _items._length={arrLen}");

            int matchIdx = -1;
            unsafe
            {
                // 数组数据从 offset 0x20 开始（对象头 0x10 + 数组头 0x10）
                IntPtr arrData = itemsPtr + 0x20;
                for (int i = 0; i < size && i < arrLen; i++)
                {
                    IntPtr elem = *(IntPtr*)(arrData + i * IntPtr.Size);
                    if (elem == targetPtr)
                    {
                        matchIdx = i;
                        break;
                    }
                }
            }

            if (matchIdx < 0)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: target not found in list");
                return false;
            }

            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: found at index={matchIdx}, calling RemoveAt...");

            // 调用 RemoveAt(index)
            IntPtr removeAtMth = Il2CppApi.GetMethodFromName(listClass, "RemoveAt", 1);
            if (removeAtMth == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: RemoveAt method not found");
                return false;
            }

            IntPtr exRemove = IntPtr.Zero;
            unsafe
            {
                int idx = matchIdx;
                IntPtr* args = stackalloc IntPtr[1];
                args[0] = (IntPtr)(&idx);
                Il2CppApi.RuntimeInvoke(removeAtMth, listPtr, (void**)args, ref exRemove);
            }

            int newSize = ReadIntFieldSafe(listPtr, listClass, "_size", -1);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: RemoveAt({matchIdx}) done, _size now={newSize}, ex={exRemove != IntPtr.Zero}");
            return newSize == size - 1;
        }
        catch (Exception ex)
        {
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr error: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// 从游戏管理器（PrefabManager 等）中移除实体引用，防止 OnGameExit 时空引用
    /// </summary>
    private static void RemoveFromManagers(IntPtr entityPtr, IntPtr classPtr, string className)
    {
        try
        {
            // 查找 PrefabManager 类
            string[] managerNames = { "PrefabManager", "EntityManager", "NpcManager", "AnimalManager", "VehicleManager", "DropItemManager" };
            foreach (var mgrName in managerNames)
            {
                IntPtr mgrClass = FindClassByName(mgrName);
                if (mgrClass == IntPtr.Zero) continue;

                // 获取管理器实例
                IntPtr mgrInst = FindClassInstance(mgrClass);
                if (mgrInst == IntPtr.Zero) continue;

                Plugin.LogInfo($"[EntityEditor] Found manager {mgrName}, scanning for entity references...");

                // 遍历管理器的所有字段，查找 List/Dictionary/数组
                IntPtr fi = IntPtr.Zero;
                IntPtr f;
                while ((f = Il2CppApi.ClassGetFields(mgrClass, ref fi)) != IntPtr.Zero)
                {
                    string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                    IntPtr ft = Il2CppApi.FieldGetType(f);
                    string? ftn = ft != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(ft)) : null;
                    if (fn == null || ftn == null) continue;

                    int offset = (int)Il2CppApi.FieldGetOffset(f);
                    if (offset < 0x10 || offset > 0x10000) continue;

                    // 检查是否是 List 或 Dictionary 类型
                    bool isList = ftn.Contains("List<");
                    bool isDict = ftn.Contains("Dictionary<");

                    if (isList)
                    {
                        unsafe
                        {
                            IntPtr listPtr = *(IntPtr*)(mgrInst + offset);
                            if (listPtr == IntPtr.Zero) continue;

                            // 尝试调用 List.Remove(entity)
                            IntPtr listClass = Il2CppApi.GetClass(listPtr);
                            IntPtr removeMth = Il2CppApi.GetMethodFromName(listClass, "Remove", 1);
                            if (removeMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr ex = IntPtr.Zero;
                                    IntPtr[] args = new IntPtr[1];
                                    IntPtr[] stor = new IntPtr[1];
                                    stor[0] = entityPtr;
                                    fixed (IntPtr* sp = stor)
                                    {
                                        args[0] = (IntPtr)(&sp[0]);
                                        fixed (IntPtr* ap = args)
                                        {
                                            Il2CppApi.RuntimeInvoke(removeMth, listPtr, (void**)ap, ref ex);
                                        }
                                    }
                                    Plugin.LogInfo($"[EntityEditor] Removed from {mgrName}.{fn} (List), ex={ex != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Remove from {mgrName}.{fn} failed: {ex.Message}"); }
                            }

                            // 也尝试 RemoveAll(Predicate)
                            IntPtr removeAllMth = Il2CppApi.GetMethodFromName(listClass, "RemoveAll", 1);
                            if (removeAllMth != IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] {mgrName}.{fn} has RemoveAll(1p)");
                            }
                        }
                    }
                    else if (isDict)
                    {
                        unsafe
                        {
                            IntPtr dictPtr = *(IntPtr*)(mgrInst + offset);
                            if (dictPtr == IntPtr.Zero) continue;

                            // 尝试读取实体的 guid/stuff_id 作为 key 来移除
                            IntPtr dictClass = Il2CppApi.GetClass(dictPtr);

                            // 尝试调用 ContainsKey + Remove
                            IntPtr containsKeyMth = Il2CppApi.GetMethodFromName(dictClass, "ContainsKey", 1);
                            IntPtr removeMth = Il2CppApi.GetMethodFromName(dictClass, "Remove", 1);

                            if (containsKeyMth != IntPtr.Zero && removeMth != IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] {mgrName}.{fn} is Dictionary, has ContainsKey+Remove");
                            }
                        }
                    }
                }
            }

            // 特殊处理：查找并清理所有包含该实体引用的 List
            // 遍历场景中所有 GameObject 的组件，查找引用了该实体的字段
            CleanupReferencesInScene(entityPtr, className);
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] RemoveFromManagers error: {ex.Message}"); }
    }


    /// <summary>
    /// 遍历场景中的管理器组件，清理对已销毁实体的引用
    /// </summary>
    private static void CleanupReferencesInScene(IntPtr entityPtr, string className)
    {
        try
        {
            // 查找 PrefabManager 组件
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                try
                {
                    var comps = go.GetComponents<Component>();
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        string cn = comp.GetIl2CppType().Name;

                        // 只处理管理器类
                        if (!cn.Contains("Manager") && !cn.Contains("Controller") && !cn.Contains("Helper"))
                            continue;

                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        IntPtr compClass = Il2CppApi.GetClass(compPtr);

                        // 查找并清理 List 字段
                        IntPtr fi = IntPtr.Zero;
                        IntPtr f;
                        while ((f = Il2CppApi.ClassGetFields(compClass, ref fi)) != IntPtr.Zero)
                        {
                            string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                            IntPtr ft = Il2CppApi.FieldGetType(f);
                            string? ftn = ft != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(ft)) : null;
                            if (fn == null || ftn == null) continue;

                            int offset = (int)Il2CppApi.FieldGetOffset(f);
                            if (offset < 0x10 || offset > 0x10000) continue;

                            // 检查是否是 List 类型且包含实体类型名
                            if (ftn.Contains("List<") && (ftn.Contains(className) || ftn.Contains("Entity") || ftn.Contains("Npc") || ftn.Contains("Animal")))
                            {
                                unsafe
                                {
                                    IntPtr listPtr = *(IntPtr*)(compPtr + offset);
                                    if (listPtr == IntPtr.Zero) continue;

                                    IntPtr listClass = Il2CppApi.GetClass(listPtr);
                                    IntPtr removeMth = Il2CppApi.GetMethodFromName(listClass, "Remove", 1);
                                    if (removeMth != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr ex = IntPtr.Zero;
                                            IntPtr[] args = new IntPtr[1];
                                            IntPtr[] stor = new IntPtr[1];
                                            stor[0] = entityPtr;
                                            fixed (IntPtr* sp = stor)
                                            {
                                                args[0] = (IntPtr)(&sp[0]);
                                                fixed (IntPtr* ap = args)
                                                {
                                                    Il2CppApi.RuntimeInvoke(removeMth, listPtr, (void**)ap, ref ex);
                                                }
                                            }
                                            Plugin.LogInfo($"[EntityEditor] Cleaned {cn}.{fn} (List), ex={ex != IntPtr.Zero}");
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] CleanupReferencesInScene error: {ex.Message}"); }
    }
}
