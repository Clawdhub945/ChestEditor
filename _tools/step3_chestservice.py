import sys
sys.path.insert(0, '_tools')
from splitter import split_members, pick_sig, write_file

cm = split_members('ChestEditorComponent.cs', 'partial class ChestEditorComponent')
im = split_members('Il2CppHelper.cs', 'static class Il2CppHelper')

USINGS_GAME = '''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChestEditor.Core.JsonUtil;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

'''

used_c = set()
used_i = set()

def take(members, pat, used, sig=True):
    sel = pick_sig(members, pat) if sig else pick(members, pat)
    assert len(sel) == 1, (pat, len(sel))
    text, a, b = sel[0]
    used.add((a, b))
    return text

# ===== 箱子模型 =====
item_info = take(cm, r'internal struct ItemInfo', used_c)
chest_info = take(cm, r'internal struct ChestInfo', used_c)

# ===== 状态字段 =====
filter_items = take(cm, r'private static readonly Dictionary<int, \(string Name, bool Enabled\)> _filterItems', used_c)
all_items_field = take(cm, r'private static List<KeyValuePair<int, string>>\? _allItems', used_c)
bag_methods = take(cm, r'private static MethodInfo\? _addStuffMethod', used_c)
chests_field = '''    private static readonly List<ChestInfo> _chests = new();
    internal static IReadOnlyList<ChestInfo> Chests => _chests;'''

# ===== 方法 =====
cache_bag_methods = take(cm, r'private static void CacheBagMethods\(object bag\)', used_c)
remove_update = take(cm, r'internal void RemoveAndUpdate\(int chestIndex, int stuffId, int count\)', used_c)
add_update = take(cm, r'internal void AddAndUpdate\(int chestIndex, int stuffId, int count\)', used_c)
update_chest_items = take(cm, r'internal void UpdateChestItems\(int chestIndex\)', used_c)
refresh_chest_list = take(cm, r'internal void RefreshChestList\(\)', used_c)
read_items_from_bag = take(cm, r'private List<ItemInfo> ReadItemsFromBag\(object facility\)', used_c)
read_capacity = take(cm, r'private void ReadCapacityFromBag\(', used_c)
read_facility_pos = take(cm, r'private void ReadFacilityPos\(', used_c)
locate_facility = take(cm, r'private void LocateFacility\(float targetX, float targetY\)', used_c)
fallback_locate = take(cm, r'private static void FallbackLocate\(float targetX, float targetY\)', used_c)
get_filters_json = take(cm, r'internal static string GetFiltersJson\(\)', used_c)

# ===== 计划库存（来自 Il2CppHelper） =====
read_plan_dic = take(im, r'internal static List<KeyValuePair<int, int>>\? ReadStuffPlanDic\(', used_i)
dict_set = take(im, r'internal static void Il2CppDictSetItem\(', used_i)
dict_remove = take(im, r'internal static void Il2CppDictRemove\(', used_i)
set_plan_value = take(im, r'internal static void SetStuffPlanValue\(', used_i)

# ===== 文本修补 =====
def fix(text):
    t = text
    t = t.replace('internal void RemoveAndUpdate', 'internal static void RemoveAndUpdate')
    t = t.replace('internal void AddAndUpdate', 'internal static void AddAndUpdate')
    t = t.replace('internal void UpdateChestItems', 'internal static void UpdateChestItems')
    t = t.replace('internal void RefreshChestList', 'internal static void RefreshChestList')
    t = t.replace('private List<ItemInfo> ReadItemsFromBag', 'private static List<ItemInfo> ReadItemsFromBag')
    t = t.replace('private void ReadCapacityFromBag', 'private static void ReadCapacityFromBag')
    t = t.replace('private void ReadFacilityPos', 'private static void ReadFacilityPos')
    t = t.replace('private void LocateFacility', 'internal static void LocateFacility')
    t = t.replace('Il2CppHelper.ReadStuffPlanDic(', 'ReadStuffPlanDic(')
    t = t.replace('Il2CppHelper.ClearDebuggedTypes();\n', '')
    # 移除调试块（DebugStuffPlanDic 调用，3行）
    t = t.replace('''                // 调试：检查 stuff_plan_dic 是否存在（只检查 _filterItems 中的）
                if (_filterItems.ContainsKey(stuffId))
                    Il2CppHelper.DebugStuffPlanDic(facility, stuffId,
                        _filterItems[stuffId].Name);

''', '')
    # 移除 _collapsedChests（IMGUI 遗留）
    t = t.replace('        _collapsedChests.Clear();\n', '')
    t = t.replace('''
        // 默认全部收起
        foreach (var c in _chests)
            _collapsedChests.Add(c.Guid);
''', '')
    return t

methods = [cache_bag_methods, remove_update, add_update, update_chest_items,
           refresh_chest_list, read_items_from_bag, read_capacity, read_facility_pos,
           locate_facility, fallback_locate, get_filters_json,
           read_plan_dic, dict_set, dict_remove, set_plan_value]
methods_text = '\n\n'.join(fix(t) for t in methods)

new_members = '''
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

    // ====== 定位 ======

    internal static string LocateChest(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count)
            return "{\\"error\\":\\"chest not found\\"}";
        var c = _chests[chestIndex];
        LocateFacility(c.PosX, c.PosY);
        return $"{{\\"ok\\":true,\\"posX\\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\\"posY\\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
    }

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
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (int i = 0; i < _chests.Count; i++)
        {
            if (i > 0) sb.Append(',');
            AppendChestJson(sb, i, _chests[i]);
        }
        sb.Append(']');
        _chestsJson = sb.ToString();
        _chestsJsonAt = System.Environment.TickCount64;
        return _chestsJson;
    }

    internal static string GetItemsJson()
    {
        if (System.Environment.TickCount64 - _itemsJsonAt < 500) return _itemsJson;
        if (_allItems == null)
            _allItems = ItemNames.GetAllItems().ToList();
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var kvp in _allItems)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append($"{{\\"stuffId\\":{kvp.Key},\\"name\\":\\"{Escape(kvp.Value)}\\"}}");
        }
        sb.Append(']');
        _itemsJson = sb.ToString();
        _itemsJsonAt = System.Environment.TickCount64;
        return _itemsJson;
    }

    /// <summary>单个箱子的 JSON（增删物品后返回最新状态）</summary>
    internal static string BuildChestJson(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count)
            return "{\\"error\\":\\"chest not found\\"}";
        var sb = new System.Text.StringBuilder();
        AppendChestJson(sb, chestIndex, _chests[chestIndex]);
        return sb.ToString();
    }

    private static void AppendChestJson(System.Text.StringBuilder sb, int index, ChestInfo c)
    {
        sb.Append($"{{\\"index\\":{index},\\"guid\\":{c.Guid},\\"stuffId\\":{c.StuffId},\\"name\\":\\"{Escape(c.Name)}\\",\\"maxCap\\":{c.MaxCap},\\"usedCap\\":{c.UsedCap},\\"posX\\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\\"posY\\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\\"items\\":[");
        for (int j = 0; j < c.Items.Count; j++)
        {
            var item = c.Items[j];
            if (j > 0) sb.Append(',');
            sb.Append($"{{\\"stuffId\\":{item.StuffId},\\"name\\":\\"{Escape(ItemNames.GetName(item.StuffId))}\\",\\"count\\":{item.Count}}}");
        }
        sb.Append("],\\"planStock\\":[");
        if (c.PlanStock != null)
        {
            for (int j = 0; j < c.PlanStock.Count; j++)
            {
                if (j > 0) sb.Append(',');
                var ps = c.PlanStock[j];
                sb.Append($"{{\\"stuffId\\":{ps.StuffId},\\"name\\":\\"{Escape(ItemNames.GetName(ps.StuffId))}\\",\\"count\\":{ps.Count}}}");
            }
        }
        sb.Append("]}");
    }
'''

write_file('Game/ChestService.cs', USINGS_GAME + '''/// <summary>
/// 箱子/背包：设施扫描、物品增删、计划库存、筛选、定位、JSON 构建
/// </summary>
internal static class ChestService
{
''' + '\n\n'.join([item_info, chest_info, chests_field, filter_items, all_items_field, bag_methods]) +
    '\n' + methods_text + new_members + '''
}
''')

leftover_c = [(a, b, t.split(chr(10))[0][:60]) for (t, a, b) in cm if (a, b) not in used_c]
leftover_i = [(a, b, t.split(chr(10))[0][:60]) for (t, a, b) in im if (a, b) not in used_i]
print('Component 未搬走成员:')
for x in leftover_c: print('  ', x)
print('Il2CppHelper 未搬走成员:')
for x in leftover_i: print('  ', x)
