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
    // ====== 永恒神殿（驯龙→龙素材面板） ======
    // 永恒圣殿两类：108012 国王的永恒圣殿 / 111005 永恒圣殿；名字含"永恒"兜底

    private static readonly int[] TempleStuffIds = { 108012, 111005 };

    internal static object? FindTempleFacility()
    {
        try
        {
            var territory = SaveLoadPatches.CachedTerritory;
            if (territory == null) return null;
            var facilityDic = GetProp(territory, "facility_dic");
            if (facilityDic == null) return null;
            var values = GetProp(facilityDic, "Values");
            if (values == null) return null;
            var getEnum = values.GetType().GetMethod("GetEnumerator", BF);
            if (getEnum == null) return null;
            var enumerator = getEnum.Invoke(values, null);
            var moveNext = enumerator.GetType().GetMethod("MoveNext", BF);
            var current = enumerator.GetType().GetProperty("Current", BF);
            while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
            {
                var facility = current.GetValue(enumerator);
                if (facility == null) continue;
                int stuffId = GetInt(facility, "stuff_id");
                if (TempleStuffIds.Contains(stuffId)) return facility;
                var nm = GetProp(facility, "stuff_name_with_id_index")?.ToString() ?? "";
                if (nm.Contains("永恒")) return facility;
            }
        }
        catch { }
        return null;
    }

    internal static string GetTempleJson()
    {
        try
        {
            var facility = FindTempleFacility();
            if (facility == null) return JsonBuilder.Object(w =>
            {
                w.WriteBoolean("found", false);
                w.WriteStartArray("items"); w.WriteEndArray();
                w.WriteStartArray("plan"); w.WriteEndArray();
            });

            var items = ReadItemsFromBag(facility);
            var plan = ReadStuffPlanDic(facility); // 计划库存（永恒圣殿自带的功能）
            return JsonBuilder.Object(w =>
            {
                w.WriteBoolean("found", true);
                w.WriteStartArray("items");
                foreach (var it in items)
                {
                    w.WriteStartObject();
                    w.WriteNumber("stuffId", it.StuffId);
                    w.WriteString("name", DataTables.ItemName(it.StuffId));
                    w.WriteNumber("count", it.Count);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("plan");
                if (plan != null)
                {
                    foreach (var kv in plan)
                    {
                        w.WriteStartObject();
                        w.WriteNumber("stuffId", kv.Key);
                        w.WriteNumber("count", kv.Value);
                        w.WriteEndObject();
                    }
                }
                w.WriteEndArray();
            });
        }
        catch (Exception ex) { return JsonBuilder.Error(ex); }
    }

    /// <summary>设置永恒神殿的计划库存数量（0 = 删除该计划）</summary>
    internal static string SetTemplePlan(int stuffId, int count)
    {
        try
        {
            var facility = FindTempleFacility();
            if (facility == null) return JsonBuilder.Error("temple not found");
            SetStuffPlanValue(facility, stuffId, count);
            Plugin.LogInfo($"[Temple] 设置计划库存 {DataTables.ItemName(stuffId)}({stuffId}) = {count}");
            return GetTempleJson();
        }
        catch (Exception ex) { return JsonBuilder.Error(ex); }
    }

    /// <summary>设置永恒神殿内某物品数量（清空原有数量后写入目标值）</summary>
    internal static string SetTempleItem(int stuffId, int count)
    {
        try
        {
            var facility = FindTempleFacility();
            if (facility == null) return JsonBuilder.Error("temple not found");
            object? bag = GetProp(facility, "bag");
            if (bag == null) return JsonBuilder.Error("bag is null");

            CacheBagMethods(bag);
            var current = ReadItemsFromBag(facility).FirstOrDefault(x => x.StuffId == stuffId);
            if (current.Count > 0 && _removeStuffMethod != null)
                _removeStuffMethod.Invoke(bag, new object[] { stuffId, current.Count, false });

            if (count > 0)
            {
                if (_addStuffNoNotifyMethod != null)
                    _addStuffNoNotifyMethod.Invoke(bag, new object[] { stuffId, count });
                else if (_addStuffMethod != null)
                    _addStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                else
                    return JsonBuilder.Error("AddStuff not found");
            }

            Plugin.LogInfo($"[Temple] 设置 {DataTables.ItemName(stuffId)}({stuffId}) = {count}");
            return GetTempleJson();
        }
        catch (Exception ex) { return JsonBuilder.Error(ex); }
    }
}
