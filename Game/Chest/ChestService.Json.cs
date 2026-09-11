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
    // ====== JSON 构建（带 500ms TTL 缓存，操作后 Invalidate） ======

    private static string _chestsJson = "[]";
    private static long _chestsJsonAt;
    private static string _itemsJson = "[]";
    private static long _itemsJsonAt;

    internal static void InvalidateCaches()
    {
        _chestsJsonAt = 0;
        _itemsJsonAt = 0;
    }

    internal static string GetChestsJson()
    {
        if (System.Environment.TickCount64 - _chestsJsonAt < 500) return _chestsJson;
        _chestsJson = JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            for (int i = 0; i < _chests.Count; i++)
                AppendChestJson(w, i, _chests[i]);
            w.WriteEndArray();
        });
        _chestsJsonAt = System.Environment.TickCount64;
        return _chestsJson;
    }

    internal static string GetItemsJson()
    {
        if (System.Environment.TickCount64 - _itemsJsonAt < 500) return _itemsJson;
        if (_allItems == null)
            _allItems = DataTables.AllItems().ToList();
        // 排除大类 1=建筑 2=生物/士兵/龙 5=活体动物 9=技术/信仰，其余输出**官方子类型**
        // （stuff_type_name.json 的类，如 405=具体武器 801=死亡的动物 812=种族），
        // 由前端按子类型白名单过滤：种族(812)/商人(806)/龙魂石(815)/未知(808) 等伪物品类不显示。
        _itemsJson = JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var kvp in _allItems)
            {
                int t = DataTables.ItemType(kvp.Key);
                if (t != 3 && t != 4 && t != 6 && t != 7 && t != 8) continue;
                w.WriteStartObject();
                w.WriteNumber("stuffId", kvp.Key);
                w.WriteString("name", kvp.Value);
                w.WriteNumber("subType", DataTables.ItemSubType(kvp.Key));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
        _itemsJsonAt = System.Environment.TickCount64;
        return _itemsJson;
    }

    /// <summary>单个箱子的 JSON（增删物品后返回最新状态）</summary>
    internal static string BuildChestJson(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count)
            return JsonBuilder.Error("chest not found");
        return JsonBuilder.Build(w => AppendChestJson(w, chestIndex, _chests[chestIndex]));
    }

    private static void AppendChestJson(Utf8JsonWriter w, int index, ChestInfo c)
    {
        w.WriteStartObject();
        w.WriteNumber("index", index);
        w.WriteNumber("guid", c.Guid);
        w.WriteNumber("stuffId", c.StuffId);
        w.WriteString("name", c.Name);
        w.WriteNumber("maxCap", c.MaxCap);
        w.WriteNumber("usedCap", c.UsedCap);
        w.WriteNumber("posX", JsonBuilder.Safe(c.PosX));
        w.WriteNumber("posY", JsonBuilder.Safe(c.PosY));

        w.WriteStartArray("items");
        foreach (var item in c.Items)
        {
            w.WriteStartObject();
            w.WriteNumber("stuffId", item.StuffId);
            w.WriteString("name", DataTables.ItemName(item.StuffId));
            w.WriteNumber("count", item.Count);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // 仅对支持计划库存的容器输出该字段（前端用 planStock != null 判断是否显示计划 UI）；
        // 不支持的容器不写此字段 -> 前端得到 undefined -> 隐藏计划库存。
        if (c.PlanStock != null)
        {
            w.WriteStartArray("planStock");
            foreach (var ps in c.PlanStock)
            {
                w.WriteStartObject();
                w.WriteNumber("stuffId", ps.StuffId);
                w.WriteString("name", DataTables.ItemName(ps.StuffId));
                w.WriteNumber("count", ps.Count);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        w.WriteEndObject();
    }
}
