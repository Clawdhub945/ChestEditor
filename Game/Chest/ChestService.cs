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
    internal struct ItemInfo { public int StuffId; public int Count; }

    // PlanStock 为 null 表示该设施不支持计划库存功能（无 GetStuffPlanDic，如普通箱子）；
    // 非 null（可能为空列表）表示支持 —— 前端据此决定是否显示计划库存 UI。
    internal struct ChestInfo { public int Guid; public int StuffId; public string Name; public List<ItemInfo> Items; public int MaxCap; public int UsedCap; public float PosX; public float PosY; public object Facility; public List<ItemInfo>? PlanStock; }

    private static readonly List<ChestInfo> _chests = new();
    internal static IReadOnlyList<ChestInfo> Chests => _chests;


    // 箱子设施筛选表：数据见 Data/chest_filters.json
    // 注：运行时会就地切换 enabled，所以取的是数据表的一份独立副本，而非共享缓存。
    private static readonly Dictionary<int, (string Name, bool Enabled)> _filterItems = DataTables.ChestFilters();


    private static List<KeyValuePair<int, string>>? _allItems;


    // 缓存方法
    private static MethodInfo? _addStuffMethod;
    private static MethodInfo? _removeStuffMethod;
    private static MethodInfo? _addStuffNoNotifyMethod;
    private static bool _methodCached;

    // ====== 物品操作方法 ======

    private static void CacheBagMethods(object bag)
    {
        if (_methodCached) return;
        _methodCached = true;

        var bagType = bag.GetType();
        var methods = bagType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // 找 AddStuffWithoutNotify(int, int)
        foreach (var m in methods)
        {
            if (m.Name == "AddStuffWithoutNotify")
            {
                var p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int))
                {
                    _addStuffNoNotifyMethod = m;

                    break;
                }
            }
        }

        // 找 AddStuff(int, int, bool) 作为备选
        if (_addStuffNoNotifyMethod == null)
        {
            foreach (var m in methods)
            {
                if (m.Name == "AddStuff")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                    {
                        _addStuffMethod = m;

                        break;
                    }
                }
            }
        }

        // 找 RemoveStuff(int, int, bool)
        foreach (var m in methods)
        {
            if (m.Name == "RemoveStuff")
            {
                var p = m.GetParameters();
                if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                {
                    _removeStuffMethod = m;

                    break;
                }
            }
        }
    }


    internal static void RemoveAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = GetProp(chest.Facility, "bag");
            if (bag == null)
            {
                Plugin.LogError("bag 为 null");
                return;
            }

            CacheBagMethods(bag);

            if (_removeStuffMethod != null)
            {
                _removeStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"删除成功: {DataTables.ItemName(stuffId)}({stuffId}) x{count}");

                // 只更新当前箱子的物品数据
                UpdateChestItems(chestIndex);
            }
            else
            {
                Plugin.LogError("RemoveStuff 方法未找到");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"RemoveAndUpdate 异常: {ex.Message}");
        }
    }


    internal static void AddAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = GetProp(chest.Facility, "bag");
            if (bag == null)
            {
                Plugin.LogError("bag 为 null");
                return;
            }

            CacheBagMethods(bag);

            // 优先用 AddStuffWithoutNotify 避免回调卡死
            if (_addStuffNoNotifyMethod != null)
            {
                _addStuffNoNotifyMethod.Invoke(bag, new object[] { stuffId, count });
                Plugin.LogInfo($"添加成功: {DataTables.ItemName(stuffId)}({stuffId}) x{count}");
            }
            else if (_addStuffMethod != null)
            {
                _addStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"添加成功: {DataTables.ItemName(stuffId)}({stuffId}) x{count}");
            }
            else
            {
                Plugin.LogError("AddStuff 方法未找到");
                return;
            }

            // 只更新当前箱子的物品数据
            UpdateChestItems(chestIndex);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"AddAndUpdate 异常: {ex.Message}");
        }
    }


    internal static void UpdateChestItems(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        var newItems = ReadItemsFromBag(chest.Facility);

        int maxCap = 0, usedCap = 0;
        ReadCapacityFromBag(chest.Facility, newItems, ref maxCap, ref usedCap);

        _chests[chestIndex] = new ChestInfo
        {
            Guid = chest.Guid,
            StuffId = chest.StuffId,
            Name = chest.Name,
            Items = newItems,
            MaxCap = maxCap,
            UsedCap = usedCap,
            PosX = chest.PosX,
            PosY = chest.PosY,
            Facility = chest.Facility,
            PlanStock = chest.PlanStock
        };
    }
}
