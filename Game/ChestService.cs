using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static ChestEditor.Core.JsonUtil;
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

    internal struct ChestInfo { public int Guid; public int StuffId; public string Name; public List<ItemInfo> Items; public int MaxCap; public int UsedCap; public float PosX; public float PosY; public object Facility; public List<ItemInfo> PlanStock; }

    private static readonly List<ChestInfo> _chests = new();
    internal static IReadOnlyList<ChestInfo> Chests => _chests;


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


    internal static void UpdateChestItems(int chestIndex)
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
                    name = ItemNames.GetName(stuffId);

                var items = ReadItemsFromBag(facility);

                int maxCap = 0, usedCap = 0;
                ReadCapacityFromBag(facility, ref maxCap, ref usedCap);

                float px = 0, py = 0;
                ReadFacilityPos(facility, ref px, ref py);

                var planDic = ReadStuffPlanDic(facility);
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



        }
        catch (Exception ex)
        {

        }
    }


    private static List<ItemInfo> ReadItemsFromBag(object facility)
    {
        var items = new List<ItemInfo>();

        object? bag = GetProp(facility, "bag");
        if (bag == null) return items;

        // 获取 BagDic 对象
        object? bagDic = GetProp(bag, "dic");

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

                                var ge = valType.GetMethod("GetEnumerator", BF);
                                if (ge != null)
                                {
                                    var en = ge.Invoke(val, null);
                                    var mn = en.GetType().GetMethod("MoveNext", BF);
                                    var cr = en.GetType().GetProperty("Current", BF);

                                    while ((bool)(mn.Invoke(en, null) ?? false))
                                    {
                                        var entry = cr.GetValue(en);
                                        if (entry == null) continue;

                                        int key = GetInt(entry, "Key");
                                        int v = GetInt(entry, "Value");
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
                    var countProp = resultType.GetProperty("Count", BF);
                    if (countProp != null)
                    {
                        int count = (int)(countProp.GetValue(result) ?? 0);

                        // 遍历每一项
                        var getItem = resultType.GetMethod("get_Item", BF);
                        if (getItem != null)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    var entry = getItem.Invoke(result, new object[] { i });
                                    if (entry == null) continue;
                                    int key = GetInt(entry, "stuff_id") != 0 ? GetInt(entry, "stuff_id") : GetInt(entry, "Key") != 0 ? GetInt(entry, "Key") : GetInt(entry, "StuffId");
                                    int val = GetInt(entry, "count") != 0 ? GetInt(entry, "count") : GetInt(entry, "Value") != 0 ? GetInt(entry, "Value") : GetInt(entry, "Count");
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


    private static void ReadCapacityFromBag(object facility, ref int maxCap, ref int usedCap)
    {
        try
        {
            object? bag = GetProp(facility, "bag");
            if (bag == null) return;

            maxCap = GetInt(bag, "limit");
            if (maxCap == 0) maxCap = GetInt(bag, "Limit");

            // 计算已用容量
            var items = ReadItemsFromBag(facility);
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
            // 通过 C# 反射获取 Game.get_main_scene()
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) { Plugin.LogInfo("[Locate] Assembly-CSharp not found"); FallbackLocate(targetX, targetY); return; }

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) { Plugin.LogInfo("[Locate] Game type not found"); FallbackLocate(targetX, targetY); return; }

            var getMainScene = gameType.GetMethod("get_main_scene", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (getMainScene == null) { Plugin.LogInfo("[Locate] get_main_scene not found"); FallbackLocate(targetX, targetY); return; }

            var mainScene = getMainScene.Invoke(null, null);
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
                IntPtr chClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(chPtr);

                // 尝试 CameraSetTo 3参数版本
                IntPtr csMth = IntPtr.Zero;
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr m;
                    while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(chClass, ref iter)) != IntPtr.Zero)
                    {
                        string? mName = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m));
                        if (mName == "CameraSetTo" && Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(m) == 3)
                        { csMth = m; break; }
                    }
                }
                if (csMth != IntPtr.Zero)
                {
                    IntPtr ex = IntPtr.Zero;
                    unsafe
                    {
                        int boolTrue = 1;
                        IntPtr* args = stackalloc IntPtr[3];
                        args[0] = (IntPtr)(&targetX);
                        args[1] = (IntPtr)(&targetY);
                        args[2] = (IntPtr)(&boolTrue);
                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(csMth, chPtr, (void**)args, ref ex);
                    }
                    Plugin.LogInfo($"[Locate] CameraSetTo IL2CPP ex={ex != IntPtr.Zero}");
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


    internal static List<KeyValuePair<int, int>>? ReadStuffPlanDic(object facility)
    {
        try
        {
            if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return null;
            IntPtr objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(il2cppObj);
            var methodPtr = FindIl2CppMethod(facility, "GetStuffPlanDic");
            if (methodPtr == IntPtr.Zero) return null;

            IntPtr dictPtr;
            unsafe
            {
                IntPtr exception = IntPtr.Zero;
                void** args = null;
                dictPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, objPtr, args, ref exception);
            }
            if (dictPtr == IntPtr.Zero) return null;

            var dict = new Il2CppSystem.Collections.Generic.Dictionary<int, int>(dictPtr);
            var result = new List<KeyValuePair<int, int>>();
            var enumerator = dict.GetEnumerator();
            while (enumerator.MoveNext())
                result.Add(new KeyValuePair<int, int>(enumerator.Current.Key, enumerator.Current.Value));
            return result;
        }
        catch { return null; }
    }


    internal static void Il2CppDictSetItem(IntPtr dictPtr, int key, int value)
    {
        IntPtr dictClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(dictPtr);
        string className = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(
            Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(dictClass)) ?? "?";
        Plugin.LogInfo($"[Plan] dictClass={className}, dictPtr={dictPtr}");

        IntPtr iter = IntPtr.Zero;
        IntPtr m;
        while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(dictClass, ref iter)) != IntPtr.Zero)
        {
            string mName = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(
                Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m)) ?? "?";
            if (mName.Contains("Item") || mName.Contains("Remove") || mName.Contains("Add") || mName.Contains("Set"))
                Plugin.LogInfo($"[Plan] 方法: {mName}");
        }

        IntPtr setItemMethod = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dictClass, "set_Item", 2);
        Plugin.LogInfo($"[Plan] set_Item ptr={setItemMethod}");
        if (setItemMethod == IntPtr.Zero) return;

        unsafe
        {
            int k = key, v = value;
            void** args = stackalloc void*[2];
            args[0] = &k;
            args[1] = &v;
            IntPtr exception = IntPtr.Zero;
            Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(setItemMethod, dictPtr, args, ref exception);
            Plugin.LogInfo($"[Plan] set_Item({key},{value}) exception={exception}");
        }
    }


    internal static void Il2CppDictRemove(IntPtr dictPtr, int key)
    {
        IntPtr dictClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(dictPtr);
        IntPtr removeMethod = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dictClass, "Remove", 1);
        Plugin.LogInfo($"[Plan] Remove ptr={removeMethod}");
        if (removeMethod == IntPtr.Zero) return;

        unsafe
        {
            int k = key;
            void** args = stackalloc void*[1];
            args[0] = &k;
            IntPtr exception = IntPtr.Zero;
            Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMethod, dictPtr, args, ref exception);
            Plugin.LogInfo($"[Plan] Remove({key}) exception={exception}");
        }
    }


    internal static void SetStuffPlanValue(object facility, int itemId, int count)
    {
        try
        {
            if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return;
            IntPtr objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(il2cppObj);
            var methodPtr = FindIl2CppMethod(facility, "GetStuffPlanDic");
            if (methodPtr == IntPtr.Zero) return;

            IntPtr dictPtr;
            unsafe
            {
                IntPtr exception = IntPtr.Zero;
                void** args = null;
                dictPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, objPtr, args, ref exception);
            }
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
            return "{\"error\":\"chest not found\"}";
        var c = _chests[chestIndex];
        LocateFacility(c.PosX, c.PosY);
        return $"{{\"ok\":true,\"posX\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"posY\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
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
            sb.Append($"{{\"stuffId\":{kvp.Key},\"name\":\"{Escape(kvp.Value)}\"}}");
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
            return "{\"error\":\"chest not found\"}";
        var sb = new System.Text.StringBuilder();
        AppendChestJson(sb, chestIndex, _chests[chestIndex]);
        return sb.ToString();
    }

    private static void AppendChestJson(System.Text.StringBuilder sb, int index, ChestInfo c)
    {
        sb.Append($"{{\"index\":{index},\"guid\":{c.Guid},\"stuffId\":{c.StuffId},\"name\":\"{Escape(c.Name)}\",\"maxCap\":{c.MaxCap},\"usedCap\":{c.UsedCap},\"posX\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"posY\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"items\":[");
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

}
