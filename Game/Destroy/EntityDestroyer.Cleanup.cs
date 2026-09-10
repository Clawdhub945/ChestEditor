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
    // ===== 清理辅助：从 IL2CPP 容器 / 场景组件中移除对已销毁实体的引用 =====

    private static bool RemoveFromListByPtr(IntPtr listPtr, IntPtr targetPtr)
    {
        try
        {
            IntPtr listClass = Il2CppApi.GetClass(listPtr);
            int size = ReadIntFieldSafe(listPtr, listClass, "_size", 0);
            Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: _size={size}, target={targetPtr.ToInt64():X}");

            if (size <= 0) return false;

            // 读取 _items 数组
            IntPtr itemsPtr = ReadFieldSafe(listPtr, listClass, "_items");
            if (itemsPtr == IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: _items is null");
                return false;
            }

            // IL2CPP 数组: 对象头 0x10, 然后是元素指针
            // _items 是 System.Object[]，每个元素是 IntPtr 大小
            IntPtr itemsClass = Il2CppApi.GetClass(itemsPtr);
            int arrLen = ReadIntFieldSafe(itemsPtr, itemsClass, "_length", 0);
            Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: _items._length={arrLen}");

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
                Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: target not found in list");
                return false;
            }

            Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: found at index={matchIdx}, calling RemoveAt...");

            // 调用 RemoveAt(index)
            IntPtr removeAtMth = Il2CppApi.GetMethodFromName(listClass, "RemoveAt", 1);
            if (removeAtMth == IntPtr.Zero)
            {
                Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: RemoveAt method not found");
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
            Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr: RemoveAt({matchIdx}) done, _size now={newSize}, ex={exRemove != IntPtr.Zero}");
            return newSize == size - 1;
        }
        catch (Exception ex)
        {
            Plugin.LogVerbose($"[EntityEditor] RemoveFromListByPtr error: {ex.Message}");
            return false;
        }
    }


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
                IntPtr mgrInst = GameChainLocator.FindClassInstance(mgrClass);
                if (mgrInst == IntPtr.Zero) continue;

                Plugin.LogVerbose($"[EntityEditor] Found manager {mgrName}, scanning for entity references...");

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
                                    Plugin.LogVerbose($"[EntityEditor] Removed from {mgrName}.{fn} (List), ex={ex != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] Remove from {mgrName}.{fn} failed: {ex.Message}"); }
                            }

                            // 也尝试 RemoveAll(Predicate)
                            IntPtr removeAllMth = Il2CppApi.GetMethodFromName(listClass, "RemoveAll", 1);
                            if (removeAllMth != IntPtr.Zero)
                            {
                                Plugin.LogVerbose($"[EntityEditor] {mgrName}.{fn} has RemoveAll(1p)");
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
                                Plugin.LogVerbose($"[EntityEditor] {mgrName}.{fn} is Dictionary, has ContainsKey+Remove");
                            }
                        }
                    }
                }
            }

            // 特殊处理：查找并清理所有包含该实体引用的 List
            // 遍历场景中所有 GameObject 的组件，查找引用了该实体的字段
            CleanupReferencesInScene(entityPtr, className);
        }
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] RemoveFromManagers error: {ex.Message}"); }
    }


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
                                            Plugin.LogVerbose($"[EntityEditor] Cleaned {cn}.{fn} (List), ex={ex != IntPtr.Zero}");
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
        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] CleanupReferencesInScene error: {ex.Message}"); }
    }
}
