using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 箱子/背包：设施扫描、物品增删、计划库存、筛选、定位、JSON 构建
/// </summary>
internal static partial class ChestService
{
    internal static string GetFiltersJson() => JsonBuilder.Build(w =>
    {
        w.WriteStartArray();
        foreach (var kvp in _filterItems)
        {
            w.WriteStartObject();
            w.WriteNumber("stuffId", kvp.Key);
            w.WriteString("name", kvp.Value.Name);
            w.WriteBoolean("enabled", kvp.Value.Enabled);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });


    /// <summary>
    /// 读取设施的计划库存字典。
    /// 返回 <c>null</c> 表示该设施**不支持**计划库存功能（无 GetStuffPlanDic 方法，如普通箱子）；
    /// 返回列表（可能为空）表示支持该功能。
    /// </summary>
    internal static List<KeyValuePair<int, int>>? ReadStuffPlanDic(object facility)
    {
        try
        {
            if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase) return null;
            IntPtr objPtr = GetIl2CppPtr(facility);
            if (objPtr == IntPtr.Zero) return null;
            var methodPtr = FindIl2CppMethod(facility, "GetStuffPlanDic");
            if (methodPtr == IntPtr.Zero) return null;    // 无此方法 = 该设施不支持计划库存

            var result = new List<KeyValuePair<int, int>>();
            IntPtr dictPtr = Invoke(methodPtr, objPtr);
            if (dictPtr == IntPtr.Zero) return result;    // 支持功能，但当前计划字典为空

            var dict = new Il2CppSystem.Collections.Generic.Dictionary<int, int>(dictPtr);
            var enumerator = dict.GetEnumerator();
            while (enumerator.MoveNext())
                result.Add(new KeyValuePair<int, int>(enumerator.Current.Key, enumerator.Current.Value));
            return result;
        }
        catch { return null; }
    }


    internal static void Il2CppDictSetItem(IntPtr dictPtr, int key, int value)
    {
        IntPtr dictClass = Il2CppApi.GetClass(dictPtr);
        Plugin.LogInfo($"[Plan] dictClass={Il2CppApi.GetClassName(dictClass) ?? "?"}, dictPtr={dictPtr}");

        foreach (var m in EnumerateMethods(dictClass))
            if (m.Name.Contains("Item") || m.Name.Contains("Remove") || m.Name.Contains("Add") || m.Name.Contains("Set"))
                Plugin.LogInfo($"[Plan] 方法: {m.Name}");

        IntPtr setItemMethod = FindMethodInHierarchy(dictClass, "set_Item", 2);
        Plugin.LogInfo($"[Plan] set_Item ptr={setItemMethod}");
        if (setItemMethod == IntPtr.Zero) return;

        InvokeVoid(setItemMethod, dictPtr, key, value);
        Plugin.LogInfo($"[Plan] set_Item({key},{value}) 已调用");
    }


    internal static void Il2CppDictRemove(IntPtr dictPtr, int key)
    {
        IntPtr dictClass = Il2CppApi.GetClass(dictPtr);
        IntPtr removeMethod = FindMethodInHierarchy(dictClass, "Remove", 1);
        Plugin.LogInfo($"[Plan] Remove ptr={removeMethod}");
        if (removeMethod == IntPtr.Zero) return;

        InvokeVoid(removeMethod, dictPtr, key);
        Plugin.LogInfo($"[Plan] Remove({key}) 已调用");
    }


    internal static void SetStuffPlanValue(object facility, int itemId, int count)
    {
        try
        {
            if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase) return;
            IntPtr objPtr = GetIl2CppPtr(facility);
            if (objPtr == IntPtr.Zero) return;
            var methodPtr = FindIl2CppMethod(facility, "GetStuffPlanDic");
            if (methodPtr == IntPtr.Zero) return;

            IntPtr dictPtr = Invoke(methodPtr, objPtr);
            if (dictPtr == IntPtr.Zero) return;

            if (count <= 0)
                Il2CppDictRemove(dictPtr, itemId);
            else
                Il2CppDictSetItem(dictPtr, itemId, count);
        }
        catch (Exception ex) { Plugin.LogError($"SetStuffPlanValue 出错: {ex.Message}"); }
    }
    // ====== 筛选 ======

    internal static void ToggleFilter(int stuffId)
    {
        if (_filterItems.ContainsKey(stuffId))
        {
            var (name, enabled) = _filterItems[stuffId];
            _filterItems[stuffId] = (name, !enabled);
        }
        RefreshChestList();
    }

    internal static void SetAllFilters(bool enabled)
    {
        foreach (var k in _filterItems.Keys.ToList())
            _filterItems[k] = (_filterItems[k].Name, enabled);
        RefreshChestList();
    }

    // ====== 计划库存 ======

    internal static void SetPlanStock(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;
        SetStuffPlanValue(_chests[chestIndex].Facility, stuffId, count);
        RefreshChestList();
    }
}
