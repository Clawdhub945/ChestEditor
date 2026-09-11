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
        public int SubType { get; init; }
    }

    private static Dictionary<int, ItemRow>? _items;
    private static Dictionary<int, string>? _soldierTypes;
    private static (string Name, string Cn, int BaseId)[]? _dragonTypes;
    private static (int Id, string Name)[]? _dragonNatures;
    private static (int Id, string Name)[]? _animals;

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
                            StuffType = row[2].GetInt32(),
                            SubType = row[3].GetInt32()
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

    /// <summary>物品 stuff_type（未知 id 返回 0）。语义：1=建筑 2=生物/士兵/龙 3=食物
    /// 4=材料/装备 5=活体动物 6=资源/药 7=种子 8=尸体 9=技术/信仰。</summary>
    internal static int ItemType(int stuffId) => Items.TryGetValue(stuffId, out var r) ? r.StuffType : 0;

    /// <summary>物品官方子类型（stuff_type_name.json 的类，未知 id 返回 0）。
    /// 例：405=具体武器 602=药物 801=死亡的动物 806=商人 812=种族 815=龙专属物品。</summary>
    internal static int ItemSubType(int stuffId) => Items.TryGetValue(stuffId, out var r) ? r.SubType : 0;

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

    // ==================== 召唤小人/士兵选项表 ====================

    private static string? _spawnTablesJson;

    /// <summary>
    /// spawn_tables.json 原文（{races, soldierTypes, weapons, armors, shields}），
    /// 由 _tools/generate_data_tables.py 生成，前端下拉直接消费，原样透传。
    /// </summary>
    internal static string SpawnTablesJson()
    {
        lock (Lock)
        {
            if (_spawnTablesJson == null)
            {
                using var doc = TryOpen("spawn_tables.json");
                _spawnTablesJson = doc?.RootElement.GetRawText() ?? "{}";
            }
            return _spawnTablesJson;
        }
    }

    private static Dictionary<int, int>? _soldierTypeRaces;

    /// <summary>
    /// 兵种 → 种族（spawn_tables.json soldierTypes 第 6 位 = 官方 race_id_limit，
    /// 即 SoldierHelper.CreateSoldier 的 race_id 参数同源；骑士=0、精灵系=7）。
    /// 未知兵种返回 0。
    /// </summary>
    internal static int SoldierTypeRace(int soldierTypeId)
    {
        lock (Lock)
        {
            if (_soldierTypeRaces == null)
            {
                _soldierTypeRaces = new Dictionary<int, int>();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(SpawnTablesJson());
                    if (doc.RootElement.TryGetProperty("soldierTypes", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var row in arr.EnumerateArray())
                        {
                            var items = row.EnumerateArray().ToList();
                            if (items.Count >= 6 && items[5].ValueKind == System.Text.Json.JsonValueKind.Number)
                                _soldierTypeRaces[items[0].GetInt32()] = items[5].GetInt32();
                        }
                    }
                }
                catch (Exception ex) { Plugin.LogError($"[DataTables] SoldierTypeRace 解析失败: {ex.Message}"); }
            }
            return _soldierTypeRaces.TryGetValue(soldierTypeId, out var race) ? race : 0;
        }
    }

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

    // ==================== 动物表 ====================

    /// <summary>可召唤的家畜（animal_type=1）。⚠ 野生动物（type=2，鹿类）实测召唤后会被
    /// 游戏的野生生态立即回收（实体表从不出现），水生（type=0）不上陆地——都排除。</summary>
    internal static (int Id, string Name)[] Animals
    {
        get
        {
            lock (Lock)
            {
                if (_animals != null) return _animals;
                var list = new List<(int, string)>();
                using var doc = TryOpen("animals.json");
                if (doc != null)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                        if (row[2].GetInt32() == 1)
                            list.Add((row[0].GetInt32(), row[1].GetString() ?? ""));
                }
                Plugin.LogInfo($"[DataTables] animals 载入 {list.Count} 种家畜");
                return _animals = list.ToArray();
            }
        }
    }
}
