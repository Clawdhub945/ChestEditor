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
internal static class ChestService
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


    internal static void LocateFacility(float targetX, float targetY)
    {
        try
        {
            var mainScene = GameContext.GetMainScene();
            if (mainScene == null) { Plugin.LogInfo("[Locate] mainScene is null"); FallbackLocate(targetX, targetY); return; }

            // 获取 camera_helper
            var cameraHelper = GetProp(mainScene, "camera_helper");
            if (cameraHelper == null) { Plugin.LogInfo("[Locate] cameraHelper is null"); FallbackLocate(targetX, targetY); return; }

            // 调用 CameraSetTo(float, float, bool)
            var chType = cameraHelper.GetType();
            var cameraSetTo = chType.GetMethod("CameraSetTo",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(float), typeof(float), typeof(bool) }, null);
            if (cameraSetTo != null)
            {
                cameraSetTo.Invoke(cameraHelper, new object[] { targetX, targetY, true });
                Plugin.LogInfo($"[Locate] CameraSetTo({targetX}, {targetY}) via reflection done");
            }
            else
            {
                Plugin.LogInfo("[Locate] CameraSetTo(float,float,bool) not found, trying IL2CPP");
                // 回退: IL2CPP 方式
                IntPtr chPtr = GetIl2CppPtr(cameraHelper);
                if (chPtr == IntPtr.Zero) { FallbackLocate(targetX, targetY); return; }
                IntPtr chClass = Il2CppApi.GetClass(chPtr);

                // 尝试 CameraSetTo 3参数版本
                IntPtr csMth = FindMethodInHierarchy(chClass, "CameraSetTo", 3);
                if (csMth != IntPtr.Zero)
                {
                    InvokeVoid(csMth, chPtr, targetX, targetY, true);
                    Plugin.LogInfo("[Locate] CameraSetTo IL2CPP 已调用");
                }
                else
                {
                    // 直接设置 camera_con.position
                    IntPtr cameraConPtr = ReadFieldSafe(chPtr, chClass, "camera_con");
                    if (cameraConPtr != IntPtr.Zero)
                    {
                        var transform = GetProp(cameraHelper, "camera_con");
                        if (transform != null)
                        {
                            var tType = transform.GetType();
                            var setPos = tType.GetMethod("set_position",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                null, new[] { typeof(UnityEngine.Vector3) }, null);
                            if (setPos != null)
                            {
                                setPos.Invoke(transform, new object[] { new UnityEngine.Vector3(targetX, targetY, 0) });
                                Plugin.LogInfo($"[Locate] Transform.set_position via reflection done");
                            }
                        }
                    }
                    else
                        FallbackLocate(targetX, targetY);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogInfo($"[Locate] error: {ex.Message}");
            FallbackLocate(targetX, targetY);
        }
    }



    private static void FallbackLocate(float targetX, float targetY)
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

    // ====== 定位 ======

    internal static string LocateChest(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count)
            return JsonBuilder.Error("chest not found");
        var c = _chests[chestIndex];
        LocateFacility(c.PosX, c.PosY);
        return JsonBuilder.Object(w => { w.WriteBoolean("ok", true); w.WriteNumber("posX", JsonBuilder.Safe(c.PosX)); w.WriteNumber("posY", JsonBuilder.Safe(c.PosY)); });
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
        _itemsJson = JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var kvp in _allItems)
            {
                w.WriteStartObject();
                w.WriteNumber("stuffId", kvp.Key);
                w.WriteString("name", kvp.Value);
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
