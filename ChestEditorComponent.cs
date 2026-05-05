using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ChestEditor;

public partial class ChestEditorComponent : MonoBehaviour
{
    internal static ChestEditorComponent? Instance;

    // HTTP 服务器写请求队列
    internal struct WriteRequest
    {
        public int ChestIndex; // >=0:操作箱子, -1:刷新, -2:筛选切换, -3:筛选全选, -4:设置计划库存
        public int StuffId;
        public int Count;
        public bool IsAdd;
        public int ExtraIndex; // 计划库存操作时存储目标箱子索引
        public string? ResultJson;
        public System.Threading.ManualResetEventSlim Signal;
    }
    internal readonly ConcurrentQueue<WriteRequest> WriteQueue = new();

    // JSON 缓存（主线程写，HTTP 线程读，volatile 保证可见性）
    internal volatile string ChestsJson = "[]";
    internal volatile string ItemsJson = "[]";

    private List<ChestInfo> _chests = new();
    private HashSet<int> _collapsedChests = new();

    // 筛选
    private static readonly Dictionary<int, (string Name, bool Enabled)> _filterItems = new()
    {
        { 103001, ("大箱子", true) },
        { 103002, ("料堆", true) },
        { 103003, ("货架", true) },
        { 106005, ("王座", true) },
        { 103004, ("交易台", false) },
        { 103005, ("通商口岸", false) },
        { 103008, ("周转箱", false) },
        { 103009, ("冰窖", false) },
        { 103012, ("马车站", false) },
        { 104002, ("讲台", false) },
        { 104004, ("布道台", false) },
        { 104007, ("诊断台", false) },
        { 104016, ("餐桌", false) },
        { 104019, ("接待台", false) },
        { 104020, ("宴会桌", false) },
        { 104026, ("幸运轮盘", false) },
        { 105026, ("码头", false) },
        { 105031, ("资源搜集点", false) },
        { 106001, ("军营", false) },
        { 106002, ("牢房", false) },
        { 106004, ("野营", false) },
        { 106008, ("训练场", false) },
        { 107004, ("强盗营地", false) },
        { 107005, ("蛮族军营", false) },
        { 108012, ("永恒圣殿", false) },
        { 109005, ("国库", false) },
        { 111002, ("泰坦之手", false) },
        { 105003, ("牧场", false) },
        { 103013, ("喂食器", false) },
        { 108008, ("蚁穴", false) },
        { 108011, ("红蚁穴", false) },
        { 111003, ("光明祭坛", false) },
        { 111004, ("黑暗祭坛", false) },
        { 111005, ("永恒圣殿", false) },
    };

    internal struct ItemInfo { public int StuffId; public int Count; }
    internal struct ChestInfo { public int Guid; public int StuffId; public string Name; public List<ItemInfo> Items; public int MaxCap; public int UsedCap; public float PosX; public float PosY; public object Facility; public List<ItemInfo> PlanStock; }

    private static List<KeyValuePair<int, string>>? _allItems;

    // 缓存方法
    private static MethodInfo? _addStuffMethod;
    private static MethodInfo? _removeStuffMethod;
    private static MethodInfo? _addStuffNoNotifyMethod;
    private static bool _methodCached;

    public ChestEditorComponent(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    internal List<ChestInfo> GetChests() => _chests;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            _showWindow = !_showWindow;
            if (_showWindow) RefreshChestList();
        }

        // 每帧更新 JSON 缓存
        UpdateJsonCache();

        // 处理 HTTP 写请求
        ProcessWriteRequests();
    }

    private void UpdateJsonCache()
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < _chests.Count; i++)
            {
                var c = _chests[i];
                if (i > 0) sb.Append(',');
                sb.Append($"{{\"index\":{i},\"guid\":{c.Guid},\"stuffId\":{c.StuffId},\"name\":\"{Escape(c.Name)}\",\"maxCap\":{c.MaxCap},\"usedCap\":{c.UsedCap},\"posX\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"posY\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"items\":[");
                for (int j = 0; j < c.Items.Count; j++)
                {
                    var item = c.Items[j];
                    if (j > 0) sb.Append(',');
                    sb.Append($"{{\"stuffId\":{item.StuffId},\"name\":\"{Escape(ItemNames.GetName(item.StuffId))}\",\"count\":{item.Count}}}");
                }
                sb.Append("],\"planStock\":[");
                if (c.PlanStock != null)
                {
                    for (int j = 0; j < c.PlanStock.Count; j++)
                    {
                        if (j > 0) sb.Append(',');
                        var ps = c.PlanStock[j];
                        sb.Append($"{{\"stuffId\":{ps.StuffId},\"name\":\"{Escape(ItemNames.GetName(ps.StuffId))}\",\"count\":{ps.Count}}}");
                    }
                }
                sb.Append("]}");
            }
            sb.Append(']');
            ChestsJson = sb.ToString();

            // 物品列表（用于添加物品下拉）
            if (_allItems == null)
                _allItems = ItemNames.GetAllItems().ToList();
            if (_allItems != null)
            {
                var sb2 = new StringBuilder();
                sb2.Append('[');
                bool first = true;
                foreach (var kvp in _allItems)
                {
                    if (!first) sb2.Append(',');
                    first = false;
                    sb2.Append($"{{\"stuffId\":{kvp.Key},\"name\":\"{Escape(kvp.Value)}\"}}");
                }
                sb2.Append(']');
                ItemsJson = sb2.ToString();
            }
        }
        catch { }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    internal static string GetFiltersJson()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var kvp in _filterItems)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append($"{{\"stuffId\":{kvp.Key},\"name\":\"{Escape(kvp.Value.Name)}\",\"enabled\":{(kvp.Value.Enabled ? "true" : "false")}}}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private void ProcessWriteRequests()
    {
        while (WriteQueue.TryDequeue(out var req))
        {
            try
            {
                // ChestIndex == -1 表示刷新操作, -2=筛选切换, -3=全选/清空筛选
                if (req.ChestIndex == -1)
                {
                    RefreshChestList();
                    req.ResultJson = "{\"ok\":true}";
                }
                else if (req.ChestIndex == -2)
                {
                    if (_filterItems.ContainsKey(req.StuffId))
                    {
                        var (name, enabled) = _filterItems[req.StuffId];
                        _filterItems[req.StuffId] = (name, !enabled);
                    }
                    RefreshChestList();
                    req.ResultJson = "{\"ok\":true}";
                }
                else if (req.ChestIndex == -3)
                {
                    foreach (var k in _filterItems.Keys.ToList())
                        _filterItems[k] = (_filterItems[k].Name, req.IsAdd);
                    RefreshChestList();
                    req.ResultJson = "{\"ok\":true}";
                }
                else if (req.ChestIndex == -4)
                {
                    // 设置计划库存: ExtraIndex=箱子索引, StuffId=物品ID, Count=新数量
                    if (req.ExtraIndex >= 0 && req.ExtraIndex < _chests.Count)
                    {
                        Il2CppHelper.SetStuffPlanValue(_chests[req.ExtraIndex].Facility, req.StuffId, req.Count);
                        RefreshChestList();
                    }
                    req.ResultJson = "{\"ok\":true}";
                }
                else if (req.ChestIndex == -5)
                {
                    // 定位设施: ExtraIndex=箱子索引
                    if (req.ExtraIndex >= 0 && req.ExtraIndex < _chests.Count)
                    {
                        var c = _chests[req.ExtraIndex];
                        LocateFacility(c.PosX, c.PosY);
                        req.ResultJson = $"{{\"ok\":true,\"posX\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"posY\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
                    }
                    else
                    {
                        req.ResultJson = "{\"error\":\"chest not found\"}";
                    }
                }
                else if (req.IsAdd)
                {
                    AddAndUpdate(req.ChestIndex, req.StuffId, req.Count);
                }
                else
                {
                    RemoveAndUpdate(req.ChestIndex, req.StuffId, req.Count);
                }

                // 返回更新后的箱子 JSON（非刷新操作时）
                if (req.ChestIndex >= 0 && req.ChestIndex < _chests.Count)
                {
                    var c = _chests[req.ChestIndex];
                    var sb = new StringBuilder();
                    sb.Append("{\"index\":").Append(req.ChestIndex);
                    sb.Append(",\"guid\":").Append(c.Guid);
                    sb.Append(",\"stuffId\":").Append(c.StuffId);
                    sb.Append(",\"name\":\"").Append(Escape(c.Name)).Append('"');
                    sb.Append(",\"maxCap\":").Append(c.MaxCap);
                    sb.Append(",\"usedCap\":").Append(c.UsedCap);
                    sb.Append(",\"items\":[");
                    for (int j = 0; j < c.Items.Count; j++)
                    {
                        var item = c.Items[j];
                        if (j > 0) sb.Append(',');
                        sb.Append($"{{\"stuffId\":{item.StuffId},\"name\":\"{Escape(ItemNames.GetName(item.StuffId))}\",\"count\":{item.Count}}}");
                    }
                    sb.Append("],\"planStock\":[");
                    if (c.PlanStock != null)
                    {
                        for (int j = 0; j < c.PlanStock.Count; j++)
                        {
                            if (j > 0) sb.Append(',');
                            var ps = c.PlanStock[j];
                            sb.Append($"{{\"stuffId\":{ps.StuffId},\"name\":\"{Escape(ItemNames.GetName(ps.StuffId))}\",\"count\":{ps.Count}}}");
                        }
                    }
                    sb.Append("]}");
                    req.ResultJson = sb.ToString();
                }
                else if (req.ChestIndex >= 0)
                {
                    req.ResultJson = "{\"error\":\"chest not found\"}";
                }
            }
            catch (Exception ex)
            {
                req.ResultJson = $"{{\"error\":\"{Escape(ex.Message)}\"}}";
            }
            finally
            {
                req.Signal.Set();
            }
        }
    }

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

    internal void RemoveAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = Il2CppHelper.GetProp(chest.Facility, "bag");
            if (bag == null)
            {
                Plugin.LogError("bag 为 null");
                return;
            }

            CacheBagMethods(bag);

            if (_removeStuffMethod != null)
            {
                _removeStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"删除成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");

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

    internal void AddAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = Il2CppHelper.GetProp(chest.Facility, "bag");
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
                Plugin.LogInfo($"添加成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");
            }
            else if (_addStuffMethod != null)
            {
                _addStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"添加成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");
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

    internal void UpdateChestItems(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        var newItems = ReadItemsFromBag(chest.Facility);

        int maxCap = 0, usedCap = 0;
        ReadCapacityFromBag(chest.Facility, ref maxCap, ref usedCap);

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

    // ====== 核心逻辑 ======

    internal void RefreshChestList()
    {
        _chests.Clear();
        _collapsedChests.Clear();
        Il2CppHelper.ClearDebuggedTypes();

        try
        {
            var territory = SaveLoadPatches.CachedTerritory;
            if (territory == null)
            {

                return;
            }

            object? facilityDic = Il2CppHelper.GetProp(territory, "facility_dic");
            if (facilityDic == null) return;

            var values = Il2CppHelper.GetProp(facilityDic, "Values");
            if (values == null) return;

            var getEnum = values.GetType().GetMethod("GetEnumerator", Il2CppHelper.BF);
            if (getEnum == null) return;
            var enumerator = getEnum.Invoke(values, null);
            var moveNext = enumerator.GetType().GetMethod("MoveNext", Il2CppHelper.BF);
            var current = enumerator.GetType().GetProperty("Current", Il2CppHelper.BF);

            while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
            {
                var facility = current.GetValue(enumerator);
                if (facility == null) continue;

                int stuffId = Il2CppHelper.GetInt(facility, "stuff_id");

                // 调试：检查 stuff_plan_dic 是否存在（只检查 _filterItems 中的）
                if (_filterItems.ContainsKey(stuffId))
                    Il2CppHelper.DebugStuffPlanDic(facility, stuffId,
                        _filterItems[stuffId].Name);

                bool show = _filterItems.ContainsKey(stuffId) && _filterItems[stuffId].Enabled;
                if (!show) continue;

                int guid = Il2CppHelper.GetGuid(facility);

                string name = "";
                string logPrefix = Il2CppHelper.GetProp(facility, "LOG_PREFIX")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(logPrefix))
                {
                    var parts = logPrefix.Split(' ');
                    if (parts.Length >= 2) name = parts[1];
                }
                if (string.IsNullOrEmpty(name))
                    name = Il2CppHelper.GetProp(facility, "stuff_name_with_id_index")?.ToString() ?? "";
                if (string.IsNullOrEmpty(name))
                    name = ItemNames.GetName(stuffId);

                var items = ReadItemsFromBag(facility);

                int maxCap = 0, usedCap = 0;
                ReadCapacityFromBag(facility, ref maxCap, ref usedCap);

                float px = 0, py = 0;
                ReadFacilityPos(facility, ref px, ref py);

                var planDic = Il2CppHelper.ReadStuffPlanDic(facility);
                var planStock = new List<ItemInfo>();
                if (planDic != null)
                    foreach (var kv in planDic)
                        planStock.Add(new ItemInfo { StuffId = kv.Key, Count = kv.Value });

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

            // 默认全部收起
            foreach (var c in _chests)
                _collapsedChests.Add(c.Guid);


        }
        catch (Exception ex)
        {

        }
    }

    private List<ItemInfo> ReadItemsFromBag(object facility)
    {
        var items = new List<ItemInfo>();

        object? bag = Il2CppHelper.GetProp(facility, "bag");
        if (bag == null) return items;

        // 获取 BagDic 对象
        object? bagDic = Il2CppHelper.GetProp(bag, "dic");

        // 1. 找到 bag.GetStuffCount(int, Dictionary, Dictionary) 方法
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

        // 2. 从 BagDic 中提取两个 Dictionary<int,int> 参数
        object? dict1 = null, dict2 = null;
        if (bagDic != null)
        {
            var dicType = bagDic.GetType();
            var dicFields = new List<object>();
            // 遍历 BagDic 的所有字段，找到 Dictionary<int,int> 类型的
            var t = dicType;
            while (t != null && t != typeof(object))
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(bagDic);
                        if (val == null) continue;
                        var valType = val.GetType();
                        if (valType.IsGenericType)
                        {
                            var args = valType.GetGenericArguments();
                            if (args.Length == 2 && args[0] == typeof(int) && args[1] == typeof(int))
                            {

                                dicFields.Add(val);
                            }
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }

            if (dicFields.Count >= 2)
            {
                dict1 = dicFields[0];
                dict2 = dicFields[1];
            }
            else if (dicFields.Count == 1)
            {
                dict1 = dicFields[0];
            }
        }

        // 3. 用 GetStuffCount 逐个检查物品，传入实际字典
        if (getStuffCountMethod != null)
        {
            var possibleIds = new List<int>();
            foreach (var kvp in ItemNames.GetAllItems())
                possibleIds.Add(kvp.Key);



            try
            {
                foreach (int stuffId in possibleIds)
                {
                    try
                    {
                        var result = getStuffCountMethod.Invoke(bag, new object[] { stuffId, dict1, dict2 });
                        int count = Convert.ToInt32(result ?? 0);
                        if (count > 0)
                            items.Add(new ItemInfo { StuffId = stuffId, Count = count });
                    }
                    catch { }
                }

                if (items.Count > 0)
                {

                    return items;
                }
                else
                {

                }
            }
            catch
            {

            }
        }

        // 4. 备选：直接遍历 BagDic 的 Dictionary<int,int> 字段
        if (bagDic != null)
        {

            var t = bagDic.GetType();
            while (t != null && t != typeof(object))
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(bagDic);
                        if (val == null) continue;

                        var valType = val.GetType();
                        if (valType.IsGenericType)
                        {
                            var args = valType.GetGenericArguments();
                            if (args.Length == 2 && args[0] == typeof(int) && args[1] == typeof(int))
                            {

                                var ge = valType.GetMethod("GetEnumerator", Il2CppHelper.BF);
                                if (ge != null)
                                {
                                    var en = ge.Invoke(val, null);
                                    var mn = en.GetType().GetMethod("MoveNext", Il2CppHelper.BF);
                                    var cr = en.GetType().GetProperty("Current", Il2CppHelper.BF);

                                    while ((bool)(mn.Invoke(en, null) ?? false))
                                    {
                                        var entry = cr.GetValue(en);
                                        if (entry == null) continue;

                                        int key = Il2CppHelper.GetInt(entry, "Key");
                                        int v = Il2CppHelper.GetInt(entry, "Value");
                                        if (v > 0)
                                            items.Add(new ItemInfo { StuffId = key, Count = v });
                                    }

                                    if (items.Count > 0)
                                    {

                                        return items;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                t = t.BaseType;
            }
        }

        // 5. 备选：尝试 bag.GetAllStuff()
        try
        {
            var getAllStuff = bag.GetType().GetMethod("GetAllStuff", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getAllStuff != null)
            {

                var result = getAllStuff.Invoke(bag, null);
                if (result != null)
                {
                    // 尝试遍历返回的集合
                    var resultType = result.GetType();
                    var countProp = resultType.GetProperty("Count", Il2CppHelper.BF);
                    if (countProp != null)
                    {
                        int count = (int)(countProp.GetValue(result) ?? 0);

                        // 遍历每一项
                        var getItem = resultType.GetMethod("get_Item", Il2CppHelper.BF);
                        if (getItem != null)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    var entry = getItem.Invoke(result, new object[] { i });
                                    if (entry == null) continue;
                                    int key = Il2CppHelper.GetInt(entry, "stuff_id") != 0 ? Il2CppHelper.GetInt(entry, "stuff_id") : Il2CppHelper.GetInt(entry, "Key") != 0 ? Il2CppHelper.GetInt(entry, "Key") : Il2CppHelper.GetInt(entry, "StuffId");
                                    int val = Il2CppHelper.GetInt(entry, "count") != 0 ? Il2CppHelper.GetInt(entry, "count") : Il2CppHelper.GetInt(entry, "Value") != 0 ? Il2CppHelper.GetInt(entry, "Value") : Il2CppHelper.GetInt(entry, "Count");
                                    if (key > 0 && val > 0)
                                        items.Add(new ItemInfo { StuffId = key, Count = val });
                                }
                                catch { }
                            }
                        }
                    }

                    if (items.Count > 0)
                    {

                        return items;
                    }
                }
            }
        }
        catch
        {

        }


        return items;
    }

    private static int GetListCount(object list)
    {
        try
        {
            var countProp = list.GetType().GetProperty("Count", Il2CppHelper.BF);
            if (countProp != null) return (int)(countProp.GetValue(list) ?? 0);
        }
        catch { }
        return 0;
    }

    private static int GetListItem(object list, int index)
    {
        try
        {
            var getItem = list.GetType().GetMethod("get_Item", Il2CppHelper.BF);
            if (getItem != null) return (int)(getItem.Invoke(list, new object[] { index }) ?? 0);
        }
        catch { }
        return 0;
    }

    private void ReadCapacityFromBag(object facility, ref int maxCap, ref int usedCap)
    {
        try
        {
            object? bag = Il2CppHelper.GetProp(facility, "bag");
            if (bag == null) return;

            maxCap = Il2CppHelper.GetInt(bag, "limit");
            if (maxCap == 0) maxCap = Il2CppHelper.GetInt(bag, "Limit");

            // 计算已用容量
            var items = ReadItemsFromBag(facility);
            usedCap = items.Sum(x => x.Count);
        }
        catch { }
    }

    private void ReadFacilityPos(object facility, ref float px, ref float py)
    {
        try
        {
            string[] xNames = { "pos_x", "posX", "PosX", "x", "X", "tile_x", "tileX", "TileX", "grid_x", "gridX" };
            string[] yNames = { "pos_y", "posY", "PosY", "y", "Y", "tile_y", "tileY", "TileY", "grid_y", "gridY" };

            foreach (var n in xNames)
            {
                float v = Il2CppHelper.GetFloat(facility, n);
                if (v != 0) { px = v; break; }
            }
            foreach (var n in yNames)
            {
                float v = Il2CppHelper.GetFloat(facility, n);
                if (v != 0) { py = v; break; }
            }

            if (px == 0 && py == 0)
            {
                var pos = Il2CppHelper.GetProp(facility, "position") ?? Il2CppHelper.GetProp(facility, "Position") ?? Il2CppHelper.GetProp(facility, "pos");
                if (pos != null)
                {
                    px = Il2CppHelper.GetFloat(pos, "x");
                    py = Il2CppHelper.GetFloat(pos, "y");
                }
            }
        }
        catch { }
    }

    private void LocateFacility(float targetX, float targetY)
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) return;
            var pos = cam.transform.position;
            pos.x = targetX;
            pos.y = targetY;
            cam.transform.position = pos;
        }
        catch { }
    }
}
