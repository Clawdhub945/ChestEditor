using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChestEditor.Core;

/// <summary>
/// 游戏数据表。数据由 <c>_tools/generate_data_tables.py</c> 从官方 json_data 生成，
/// 作为嵌入资源（ChestEditor.Data.*.json）随程序集打包，首次访问时解析并缓存。
///
/// 取代原先分散硬编码在 ItemCatalog / EntityScan / ChestService / DragonService 中的数据表。
/// </summary>
internal static class DataTables
{
    private const string ResourcePrefix = "ChestEditor.Data.";
    private static readonly object Lock = new();

    /// <summary>物品行：官方 stuff_id / 显示名 / stuff_type</summary>
    internal readonly struct ItemRow
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public int StuffType { get; init; }
    }

    private static Dictionary<int, ItemRow>? _items;
    private static Dictionary<int, string>? _soldierTypes;
    private static (string Name, string Cn, int BaseId)[]? _dragonTypes;
    private static (int Id, string Name)[]? _dragonNatures;

    // ==================== 资源解析 ====================

    private static JsonDocument? TryOpen(string file)
    {
        var text = EmbeddedResources.GetText(ResourcePrefix + file);
        if (string.IsNullOrEmpty(text))
        {
            Plugin.LogError($"[DataTables] 嵌入资源缺失: {ResourcePrefix}{file}");
            return null;
        }
        try { return JsonDocument.Parse(text); }
        catch (System.Exception ex)
        {
            Plugin.LogError($"[DataTables] 解析 {file} 失败: {ex.Message}");
            return null;
        }
    }

    // ==================== 物品表 ====================

    private static Dictionary<int, ItemRow> Items
    {
        get
        {
            lock (Lock)
            {
                if (_items != null) return _items;
                var map = new Dictionary<int, ItemRow>();
                using var doc = TryOpen("items.json");
                if (doc != null)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                    {
                        int id = row[0].GetInt32();
                        map[id] = new ItemRow
                        {
                            Id = id,
                            Name = row[1].GetString() ?? "",
                            StuffType = row[2].GetInt32()
                        };
                    }
                }
                Plugin.LogInfo($"[DataTables] items 载入 {map.Count} 条");
                return _items = map;
            }
        }
    }

    /// <summary>物品显示名（未知 id 返回空串）</summary>
    internal static string ItemName(int stuffId) => Items.TryGetValue(stuffId, out var r) ? r.Name : "";

    internal static bool TryGetItem(int stuffId, out ItemRow row) => Items.TryGetValue(stuffId, out row);

    /// <summary>全部物品（按 stuff_id 升序）</summary>
    internal static List<KeyValuePair<int, string>> AllItems()
    {
        var list = new List<KeyValuePair<int, string>>(Items.Count);
        foreach (var kv in Items.OrderBy(x => x.Key))
            list.Add(new KeyValuePair<int, string>(kv.Key, kv.Value.Name));
        return list;
    }

    // ==================== 士兵类型表 ====================

    private static Dictionary<int, string> SoldierTypes
    {
        get
        {
            lock (Lock)
            {
                if (_soldierTypes != null) return _soldierTypes;
                var map = new Dictionary<int, string>();
                using var doc = TryOpen("soldier_types.json");
                if (doc != null)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                        map[row[0].GetInt32()] = row[1].GetString() ?? "";
                }
                return _soldierTypes = map;
            }
        }
    }

    /// <summary>士兵类型名（未知 id 返回空串）</summary>
    internal static string SoldierTypeName(int soldierTypeId)
        => SoldierTypes.TryGetValue(soldierTypeId, out var n) ? n : "";

    // ==================== 箱子筛选表 ====================

    /// <summary>
    /// 返回箱子设施筛选表的【新实例】（调用方会就地修改 enabled 状态，故不能共享缓存）。
    /// </summary>
    internal static Dictionary<int, (string Name, bool Enabled)> ChestFilters()
    {
        var map = new Dictionary<int, (string Name, bool Enabled)>();
        using var doc = TryOpen("chest_filters.json");
        if (doc != null)
        {
            foreach (var row in doc.RootElement.EnumerateArray())
                map[row[0].GetInt32()] = (row[1].GetString() ?? "", row[2].GetInt32() != 0);
        }
        return map;
    }

    // ==================== 龙表 ====================

    internal static (string Name, string Cn, int BaseId)[] DragonTypes
    {
        get
        {
            lock (Lock)
            {
                if (_dragonTypes != null) return _dragonTypes;
                var list = new List<(string, string, int)>();
                using var doc = TryOpen("dragon_types.json");
                if (doc != null)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                        list.Add((row[0].GetString() ?? "", row[1].GetString() ?? "", row[2].GetInt32()));
                }
                return _dragonTypes = list.ToArray();
            }
        }
    }

    internal static (int Id, string Name)[] DragonNatures
    {
        get
        {
            lock (Lock)
            {
                if (_dragonNatures != null) return _dragonNatures;
                var list = new List<(int, string)>();
                using var doc = TryOpen("dragon_natures.json");
                if (doc != null)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                        list.Add((row[0].GetInt32(), row[1].GetString() ?? ""));
                }
                return _dragonNatures = list.ToArray();
            }
        }
    }
}
