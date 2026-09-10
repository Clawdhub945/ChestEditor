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
    /// <summary>通用策略：非 NPC/Facility 实体（动物/掉落物等），类链虚方法 → StuffOnMap 手动流程 → 管理器关键字搜索</summary>
    private static bool TryDestroyGeneric(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
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
                    IntPtr mapStuffHelper = GameChainLocator.GetMapStuffHelper();
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
                            e.GoRef!.SetActive(false);
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
                    IntPtr mgrInst = GameChainLocator.FindClassInstance(mgrClass);
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
        return called;
    }
}
