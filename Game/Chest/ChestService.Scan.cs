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
    // ====== 核心逻辑 ======

    internal static void RefreshChestList()
    {
        _chests.Clear();
        
        try
        {
            var territory = SaveLoadPatches.CachedTerritory;
            if (territory == null)
            {

                return;
            }

            object? facilityDic = GetProp(territory, "facility_dic");
            if (facilityDic == null) return;

            var values = GetProp(facilityDic, "Values");
            if (values == null) return;

            var getEnum = values.GetType().GetMethod("GetEnumerator", BF);
            if (getEnum == null) return;
            var enumerator = getEnum.Invoke(values, null);
            var moveNext = enumerator.GetType().GetMethod("MoveNext", BF);
            var current = enumerator.GetType().GetProperty("Current", BF);

            while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
            {
                var facility = current.GetValue(enumerator);
                if (facility == null) continue;

                int stuffId = GetInt(facility, "stuff_id");

                bool show = _filterItems.ContainsKey(stuffId) && _filterItems[stuffId].Enabled;
                if (!show) continue;

                int guid = GetGuid(facility);

                string name = "";
                string logPrefix = GetProp(facility, "LOG_PREFIX")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(logPrefix))
                {
                    var parts = logPrefix.Split(' ');
                    if (parts.Length >= 2) name = parts[1];
                }
                if (string.IsNullOrEmpty(name))
                    name = GetProp(facility, "stuff_name_with_id_index")?.ToString() ?? "";
                if (string.IsNullOrEmpty(name))
                    name = DataTables.ItemName(stuffId);

                var items = ReadItemsFromBag(facility);

                int maxCap = 0, usedCap = 0;
                ReadCapacityFromBag(facility, items, ref maxCap, ref usedCap);

                float px = 0, py = 0;
                ReadFacilityPos(facility, ref px, ref py);

                // 计划库存仅部分设施支持（游戏里为 FacilityWagonStation 驿站类）。
                // ReadStuffPlanDic 返回 null = 该设施没有 GetStuffPlanDic -> 保持 null，
                // 前端因此不显示"添加计划 / 计划"行。
                var planDic = ReadStuffPlanDic(facility);
                List<ItemInfo>? planStock = null;
                if (planDic != null)
                {
                    planStock = new List<ItemInfo>();
                    foreach (var kv in planDic)
                        planStock.Add(new ItemInfo { StuffId = kv.Key, Count = kv.Value });
                }

                _chests.Add(new ChestInfo
                {
                    Guid = guid,
                    StuffId = stuffId,
                    Name = name,
                    Items = items,
                    MaxCap = maxCap,
                    UsedCap = usedCap,
                    PosX = px,
                    PosY = py,
                    Facility = facility,
                    PlanStock = planStock
                });
            }

            int planCount = 0;
            foreach (var ch in _chests) if (ch.PlanStock != null) planCount++;
            Plugin.LogInfo($"[Chest] 刷新完成: {_chests.Count} 个容器, {planCount} 个支持计划库存");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Chest] RefreshChestList 异常: {ex.Message}");
        }
    }


    private static List<ItemInfo> ReadItemsFromBag(object facility)
    {
        var items = new List<ItemInfo>();

        object? bag = GetProp(facility, "bag");
        if (bag == null) return items;

        // 首选：bag.dic 是 BagDic，反编译确认其直接继承 Dictionary<int,int>，
        // 直接枚举全部物品（O(物品种类)），替代旧的 620 次 GetStuffCount 反射调用
        object? bagDic = GetProp(bag, "dic");
        if (bagDic is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppDic)
        {
            try
            {
                var d = il2cppDic.TryCast<Il2CppSystem.Collections.Generic.Dictionary<int, int>>();
                if (d != null)
                {
                    var enumerator = d.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var kv = enumerator.Current;
                        if (kv.Value > 0)
                            items.Add(new ItemInfo { StuffId = kv.Key, Count = kv.Value });
                    }
                    return items;
                }
            }
            catch { }
        }

        // 兜底：bag.GetStuffCount(stuffId, excludeDic1, excludeDic2) 逐个查询可入箱物品
        //（签名来自反编译 Bag__GetStuffCount；exclude 传 null 即原始数量）
        try
        {
            MethodInfo? getStuffCountMethod = null;
            foreach (var m in bag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == "GetStuffCount")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(int))
                    {
                        getStuffCountMethod = m;
                        break;
                    }
                }
            }
            if (getStuffCountMethod != null)
            {
                foreach (var kvp in DataTables.AllItems())
                {
                    try
                    {
                        var result = getStuffCountMethod.Invoke(bag, new object?[] { kvp.Key, null, null });
                        int count = Convert.ToInt32(result ?? 0);
                        if (count > 0)
                            items.Add(new ItemInfo { StuffId = kvp.Key, Count = count });
                    }
                    catch { }
                }
            }
        }
        catch { }

        return items;
    }


    private static void ReadCapacityFromBag(object facility, List<ItemInfo>? precomputedItems, ref int maxCap, ref int usedCap)
    {
        try
        {
            object? bag = GetProp(facility, "bag");
            if (bag == null) return;

            // Bag.limit 字段真实存在（反编译 Bag__IsBagFullByCount）；
            // 0 表示不限量（普通箱子），>0 表示按数量限量（码头/熔炉等）
            maxCap = GetInt(bag, "limit");

            // 计算已用容量
            var items = precomputedItems ?? ReadItemsFromBag(facility);
            usedCap = items.Sum(x => x.Count);
        }
        catch { }
    }


    private static void ReadFacilityPos(object facility, ref float px, ref float py)
    {
        try
        {
            string[] xNames = { "pos_x", "posX", "PosX", "x", "X", "tile_x", "tileX", "TileX", "grid_x", "gridX" };
            string[] yNames = { "pos_y", "posY", "PosY", "y", "Y", "tile_y", "tileY", "TileY", "grid_y", "gridY" };

            foreach (var n in xNames)
            {
                float v = GetFloat(facility, n);
                if (v != 0) { px = v; break; }
            }
            foreach (var n in yNames)
            {
                float v = GetFloat(facility, n);
                if (v != 0) { py = v; break; }
            }

            if (px == 0 && py == 0)
            {
                var pos = GetProp(facility, "position") ?? GetProp(facility, "Position") ?? GetProp(facility, "pos");
                if (pos != null)
                {
                    px = GetFloat(pos, "x");
                    py = GetFloat(pos, "y");
                }
            }
        }
        catch { }
    }
}
