using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>龙系统：素材背包、龙魂、召唤、龙实体扫描与属性修改</summary>
internal static class DragonService
{

    // ====== 龙素材背包 (Game.w.dragon_stuff_bag) ======

    // 本地缓存：IL2CPP 读取 StuffCount 始终返回 0，所以用本地缓存跟踪数量
    private static Dictionary<int, int>? _dragonItemCache;

    private static bool _dragonCacheInitialized;


    private static void EnsureDragonCache()
    {
        if (_dragonCacheInitialized) return;
        _dragonCacheInitialized = true;
        _dragonItemCache = new Dictionary<int, int>();
        // 初始化 5 种龙素材为 0
        foreach (int sid in new[] { 815001, 815002, 815003, 815004, 815005 })
            _dragonItemCache[sid] = 0;

        // 尝试从游戏读取初始值（如果读不到就是 0）
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return;
            object? dragonBag = GetProp(w, "dragon_stuff_bag");
            if (dragonBag == null) return;

            // 尝试用 ReadBagContents 的各种策略读取
            var contents = ReadBagContents(dragonBag);
            if (contents != null && contents.Count > 0)
            {
                foreach (var kv in contents)
                {
                    if (_dragonItemCache.ContainsKey(kv.Key))
                        _dragonItemCache[kv.Key] = kv.Value;
                }
                Plugin.LogInfo($"[Dragon] 初始读取到 {contents.Count} 种龙素材");
            }
        }
        catch { }
    }


    internal static Dictionary<int, int> GetDragonItemCache()
    {
        EnsureDragonCache();
        return _dragonItemCache!;
    }


    private static void UpdateDragonCache(int stuffId, int newCount)
    {
        EnsureDragonCache();
        if (_dragonItemCache!.ContainsKey(stuffId))
            _dragonItemCache[stuffId] = Math.Max(0, newCount);
    }


    /// <summary>
    /// 直接从游戏 dragon_stuff_bag 读取所有龙素材（每次调用都重新读取）
    /// </summary>
    private static MethodInfo? _dragonStuffCountMethod;


    internal static List<KeyValuePair<int, int>> ReadDragonBagLive()
    {
        var result = new List<KeyValuePair<int, int>>();
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return result;
            object? dragonBag = GetProp(w, "dragon_stuff_bag");
            if (dragonBag == null) return result;

            if (_dragonStuffCountMethod == null)
            {
                var methods = dragonBag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    if (m.Name == "StuffCount")
                    {
                        var p = m.GetParameters();
                        if (p.Length == 1 && p[0].ParameterType == typeof(int))
                        {
                            _dragonStuffCountMethod = m;
                            break;
                        }
                    }
                }
            }

            if (_dragonStuffCountMethod == null) return result;

            for (int sid = 815001; sid <= 815100; sid++)
            {
                try
                {
                    var res = _dragonStuffCountMethod.Invoke(dragonBag, new object[] { sid });
                    int count = Convert.ToInt32(res ?? 0);
                    if (count > 0)
                        result.Add(new KeyValuePair<int, int>(sid, count));
                }
                catch { }
            }
        }
        catch { }
        return result;
    }


    internal static List<KeyValuePair<int, int>>? ReadDragonStuffBag()
    {
        try
        {
            var cache = GetDragonItemCache();
            var result = new List<KeyValuePair<int, int>>();
            foreach (var kv in cache)
            {
                if (kv.Value > 0)
                    result.Add(new KeyValuePair<int, int>(kv.Key, kv.Value));
            }
            return result;
        }
        catch { return null; }
    }


    private static bool _dragonMethodsLogged;

    private static bool _dragonDicLogged;


    internal static List<KeyValuePair<int, int>> ReadBagContents(object bag)
    {
        var result = new List<KeyValuePair<int, int>>();
        try
        {
            var allMethods = bag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 只打印一次方法签名
            if (!_dragonMethodsLogged)
            {
                _dragonMethodsLogged = true;
                foreach (var m in allMethods)
                {
                    if (m.Name.Contains("StuffCount") || m.Name.Contains("stuffCount"))
                    {
                        var p = m.GetParameters();
                        var pTypes = string.Join(", ", p.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                        Plugin.LogInfo($"[Dragon] Bag.{m.Name}({pTypes})");
                    }
                }
            }

            // 策略0：找 StuffCount(int, int) 2参数版本（游戏代码用的版本）
            MethodInfo? twoParamMethod = null;
            foreach (var m in allMethods)
            {
                if (m.Name == "StuffCount" || m.Name == "GetStuffCount")
                {
                    var p = m.GetParameters();
                    if (p.Length == 2 && p[0].ParameterType == typeof(int))
                    {
                        twoParamMethod = m;
                        break;
                    }
                }
            }
            if (twoParamMethod != null)
            {
                foreach (var kvp in DataTables.AllItems())
                {
                    try
                    {
                        var res = twoParamMethod.Invoke(bag, new object[] { kvp.Key, 0 });
                        int count = Convert.ToInt32(res ?? 0);
                        if (count > 0)
                            result.Add(new KeyValuePair<int, int>(kvp.Key, count));
                    }
                    catch { }
                }
                if (result.Count > 0) return result;
            }

            // 策略0b：找 StuffCount(int) 单参数版本
            MethodInfo? singleParamMethod = null;
            foreach (var m in allMethods)
            {
                if (m.Name == "StuffCount" || m.Name == "GetStuffCount")
                {
                    var p = m.GetParameters();
                    if (p.Length == 1 && p[0].ParameterType == typeof(int))
                    {
                        singleParamMethod = m;
                        break;
                    }
                }
            }
            if (singleParamMethod != null)
            {
                foreach (var kvp in DataTables.AllItems())
                {
                    try
                    {
                        var res = singleParamMethod.Invoke(bag, new object[] { kvp.Key });
                        int count = Convert.ToInt32(res ?? 0);
                        if (count > 0)
                            result.Add(new KeyValuePair<int, int>(kvp.Key, count));
                    }
                    catch { }
                }
                if (result.Count > 0) return result;
            }

            // 策略1：找 GetStuffCount(int, dict, dict) 方法
            object? bagDic = GetProp(bag, "dic");
            // 打印一次 BagDic 字段
            if (!_dragonDicLogged && bagDic != null)
            {
                _dragonDicLogged = true;
                var dicT = bagDic.GetType();
                Plugin.LogInfo($"[Dragon] BagDic 类型: {dicT.FullName}");
                while (dicT != null && dicT != typeof(object))
                {
                    foreach (var df in dicT.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        try
                        {
                            var dv = df.GetValue(bagDic);
                            Plugin.LogInfo($"[Dragon] BagDic.{df.Name} : {df.FieldType.Name} = {(dv != null ? dv.GetType().FullName : "null")}");
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[Dragon] BagDic.{df.Name} : 读取失败 {ex.Message}"); }
                    }
                    dicT = dicT.BaseType;
                }
            }
            MethodInfo? getStuffCountMethod = null;
            foreach (var m in allMethods)
            {
                if (m.Name == "GetStuffCount" || m.Name == "StuffCount")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(int))
                    {
                        getStuffCountMethod = m;
                        break;
                    }
                }
            }

            // 提取字典参数
            object? dict1 = null, dict2 = null;
            if (bagDic != null)
            {
                var dicType = bagDic.GetType();
                var t = dicType;
                var dicFields = new List<object>();
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
                                    dicFields.Add(val);
                            }
                        }
                        catch { }
                    }
                    t = t.BaseType;
                }
                if (dicFields.Count >= 2) { dict1 = dicFields[0]; dict2 = dicFields[1]; }
                else if (dicFields.Count == 1) { dict1 = dicFields[0]; }
            }

            // 用 GetStuffCount 遍历所有已知物品
            if (getStuffCountMethod != null)
            {
                foreach (var kvp in DataTables.AllItems())
                {
                    try
                    {
                        var res = getStuffCountMethod.Invoke(bag, new object[] { kvp.Key, dict1, dict2 });
                        int count = Convert.ToInt32(res ?? 0);
                        if (count > 0)
                            result.Add(new KeyValuePair<int, int>(kvp.Key, count));
                    }
                    catch { }
                }
                if (result.Count > 0) return result;
            }

            // 策略2：直接遍历 bag 上所有 Dictionary<int,int> 类型的字段/属性
            var bagType = bag.GetType();
            var bagT = bagType;
            while (bagT != null && bagT != typeof(object))
            {
                foreach (var field in bagT.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(bag);
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
                                            result.Add(new KeyValuePair<int, int>(key, v));
                                    }
                                    if (result.Count > 0) return result;
                                }
                            }
                        }
                    }
                    catch { }
                }
                bagT = bagT.BaseType;
            }

            // 策略3：从 bag.dic 中遍历 Dictionary<int,int>
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
                                                result.Add(new KeyValuePair<int, int>(key, v));
                                        }
                                        if (result.Count > 0) return result;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    t = t.BaseType;
                }
            }

            // 策略4：bag.GetAllStuff()
            var getAllStuff = bag.GetType().GetMethod("GetAllStuff", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getAllStuff != null)
            {
                var res = getAllStuff.Invoke(bag, null);
                if (res != null)
                {
                    var resultType = res.GetType();
                    int count = (int)(resultType.GetProperty("Count", BF)?.GetValue(res) ?? 0);
                    var getItem = resultType.GetMethod("get_Item", BF);
                    if (getItem != null)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                var entry = getItem.Invoke(res, new object[] { i });
                                if (entry == null) continue;
                                int key = GetInt(entry, "stuff_id") != 0 ? GetInt(entry, "stuff_id") : GetInt(entry, "Key");
                                int val = GetInt(entry, "count") != 0 ? GetInt(entry, "count") : GetInt(entry, "Value");
                                if (key > 0 && val > 0)
                                    result.Add(new KeyValuePair<int, int>(key, val));
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch { }
        return result;
    }


    internal static void SetDragonItemQuantity(int stuffId, int newCount)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) { Plugin.LogError("Game.w 为 null"); return; }

            object? dragonBag = GetProp(w, "dragon_stuff_bag");
            if (dragonBag == null) { Plugin.LogError("dragon_stuff_bag 为 null"); return; }

            // 缓存方法
            CacheBagOps(dragonBag);

            // 先用大数删除确保清空（RemoveStuff 多删不会报错，只会删到 0）
            if (_dragonRemoveMethod != null)
                _dragonRemoveMethod.Invoke(dragonBag, new object[] { stuffId, 999999, false });

            // 再添加新数量
            if (newCount > 0)
            {
                if (_dragonAddNoNotifyMethod != null)
                    _dragonAddNoNotifyMethod.Invoke(dragonBag, new object[] { stuffId, newCount });
                else if (_dragonAddMethod != null)
                    _dragonAddMethod.Invoke(dragonBag, new object[] { stuffId, newCount, false });
            }

            // 更新本地缓存
            UpdateDragonCache(stuffId, newCount);
            Plugin.LogInfo($"龙素材设置: {DataTables.ItemName(stuffId)}({stuffId}) -> {newCount}");
        }
        catch (Exception ex) { Plugin.LogError($"SetDragonItemQuantity 出错: {ex.Message}"); }
    }


    // ====== 召唤龙 ======
    private static MethodInfo? _addDragonSoulMethod;


    /// <summary>龙类型表（数据见 Data/dragon_types.json）</summary>
    internal static readonly (string Name, string ChineseName, int BaseId)[] DragonTypes = DataTables.DragonTypes;


    /// <summary>龙天性表（数据见 Data/dragon_natures.json，源自官方 dragon_nature.json）</summary>
    internal static readonly (int Id, string Name)[] DragonNatures = DataTables.DragonNatures;


    internal static string GetDragonTypesJson() => JsonBuilder.Build(w =>
    {
        w.WriteStartArray();
        foreach (var (name, cn, baseId) in DragonTypes)
        {
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("cn", cn);
            w.WriteNumber("baseId", baseId);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });


    internal static string GetDragonNaturesJson() => JsonBuilder.Build(w =>
    {
        w.WriteStartArray();
        foreach (var (id, name) in DragonNatures)
        {
            w.WriteStartObject();
            w.WriteNumber("id", id);
            w.WriteString("name", name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });


    /// <summary>
    /// 把反射读取到的任意值写成 JSON 值。
    /// 旧实现用字符串拼接：字符串不转义、数字 ToString 未指定 InvariantCulture，
    /// 现在统一交给 Utf8JsonWriter。
    /// </summary>
    private static void WriteJsonValue(Utf8JsonWriter w, object? value)
    {
        switch (value)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case int i: w.WriteNumberValue(i); break;
            case long l: w.WriteNumberValue(l); break;
            case float f: w.WriteNumberValue(JsonBuilder.Safe(f)); break;
            case double d: w.WriteNumberValue(double.IsFinite(d) ? d : 0d); break;
            case string s: w.WriteStringValue(s); break;
            case List<int> ints:
                w.WriteStartArray();
                foreach (var n in ints) w.WriteNumberValue(n);
                w.WriteEndArray();
                break;
            default: w.WriteStringValue(value.GetType().Name); break;
        }
    }


    internal static string SummonDragon(int dragonStuffId, int[]? natureIds = null)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "Game.w 为 null";

            // 确保驭龙数量上限足够
            int controlCount = GetInt(w, "dragon_control_count");
            var soulList = GetProp(w, "dragon_soul_list");
            int currentSouls = 0;
            if (soulList != null)
            {
                var countProp = soulList.GetType().GetProperty("Count", BF);
                if (countProp != null)
                    currentSouls = Convert.ToInt32(countProp.GetValue(soulList) ?? 0);
            }
            if (currentSouls >= controlCount)
            {
                // 提升驭龙上限
                SetInt(w, "dragon_control_count", currentSouls + 1);
                Plugin.LogInfo($"[Dragon] 驭龙上限提升: {controlCount} -> {currentSouls + 1}");
            }

            // 找 AddDragonSoul 方法
            if (_addDragonSoulMethod == null)
            {
                _addDragonSoulMethod = w.GetType().GetMethod("AddDragonSoul",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (_addDragonSoulMethod == null)
                return "AddDragonSoul 方法未找到";

            var parameters = _addDragonSoulMethod.GetParameters();

            // 构造 nature_list 参数
            object? natureList = null;
            if (parameters.Length >= 2)
            {
                var listType = parameters[1].ParameterType;
                natureList = Activator.CreateInstance(listType);
                // 添加选中的 nature
                if (natureIds != null && natureIds.Length > 0)
                {
                    var addMethod = listType.GetMethod("Add", BF);
                    if (addMethod != null)
                    {
                        foreach (int nid in natureIds)
                        {
                            try { addMethod.Invoke(natureList, new object[] { nid }); }
                            catch { }
                        }
                    }
                }
                Plugin.LogInfo($"[Dragon] 创建 nature_list: {listType.FullName}");
            }

            // 调用 AddDragonSoul
            object? result;
            if (parameters.Length >= 2)
                result = _addDragonSoulMethod.Invoke(w, new object[] { dragonStuffId, natureList! });
            else if (parameters.Length == 1)
                result = _addDragonSoulMethod.Invoke(w, new object[] { dragonStuffId });
            else
                return "AddDragonSoul 参数数量异常";

            Plugin.LogInfo($"[Dragon] AddDragonSoul 返回: {result}");
            return result?.ToString() ?? "ok";
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Dragon] SummonDragon 出错: {ex}");
            return ex.Message;
        }
    }



    // ====== 龙魂列表读取 ======
    internal static List<Dictionary<string, object?>>? ReadDragonSouls()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return null;
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return null;

            var slType = soulList.GetType();
            int count = 0;
            var countProp = slType.GetProperty("Count", BF);
            if (countProp != null) count = Convert.ToInt32(countProp.GetValue(soulList) ?? 0);
            if (count == 0) return new List<Dictionary<string, object?>>();

            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null) return null;

            // 读取第一个元素以获取字段布局
            var first = getItem.Invoke(soulList, new object[] { 0 });
            if (first == null) return null;

            IntPtr firstPtr = GetIl2CppPtr(first);
            if (firstPtr == IntPtr.Zero) return null;

            IntPtr classPtr = Il2CppApi.GetClass(firstPtr);
            var fields = Il2CppApi.EnumerateFields(classPtr);

            // 过滤掉静态字段 (offset=0 且不是第一个字段) 和已知常量
            var instanceFields = fields.Where(f =>
                f.Offset > 0 &&
                f.Name != "HEAD" && f.Name != "SHIELD" && f.Name != "CLAW" && f.Name != "CLOUD"
            ).ToList();

            // 获取字段类型信息（0=int, 1=string, 2=list）
            var fieldTypes = new Dictionary<string, int>();
            foreach (var f in fields)
            {
                if (f.TypeName.Contains("String")) fieldTypes[f.Name] = 1;
                else if (f.Name == "nature_list") fieldTypes[f.Name] = 2;
                else fieldTypes[f.Name] = 0;
            }

            // 读取所有龙魂数据
            var result = new List<Dictionary<string, object?>>();
            for (int i = 0; i < count; i++)
            {
                var soul = getItem.Invoke(soulList, new object[] { i });
                if (soul == null) continue;
                IntPtr ptr = GetIl2CppPtr(soul);
                if (ptr == IntPtr.Zero) continue;

                var dict = new Dictionary<string, object?>();
                foreach (var (name, offset, _) in instanceFields)
                {
                    try
                    {
                        int ft = fieldTypes.TryGetValue(name, out int v) ? v : 0;
                        if (ft == 1)
                            dict[name] = ReadIl2CppString(ptr, offset);
                        else if (ft == 2)
                        {
                            var list = ReadIl2CppIntList(ptr, offset);
                            dict[name] = list;
                        }
                        else
                            dict[name] = ReadIl2CppInt(ptr, offset);
                    }
                    catch { }
                }
                result.Add(dict);
            }
            return result;
        }
        catch { return null; }
    }


    internal static string GetDragonSoulsJson()
    {
        ApplySoulModifications(); // 读档自动恢复：直接遍历龙魂列表，无场景扫描，零卡顿
        var souls = ReadDragonSouls();
        if (souls == null) return "[]";
        return JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var soul in souls)
            {
                w.WriteStartObject();
                foreach (var kv in soul)
                {
                    w.WritePropertyName(kv.Key);
                    WriteJsonValue(w, kv.Value);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }


    // 修改龙魂属性 (通过 IL2CPP 原生字段写入)
    internal static string SetDragonSoulProperty(int soulIndex, string property, int value)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "Game.w 为 null";
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return "dragon_soul_list 为 null";

            var slType = soulList.GetType();
            int count = Convert.ToInt32(slType.GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
            if (soulIndex < 0 || soulIndex >= count) return "索引越界";

            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null) return "get_Item 未找到";

            var soul = getItem.Invoke(soulList, new object[] { soulIndex });
            if (soul == null) return "龙魂为空";

            IntPtr ptr = GetIl2CppPtr(soul);
            if (ptr == IntPtr.Zero) return "无法获取原生指针";

            IntPtr classPtr = Il2CppApi.GetClass(ptr);
            var fields = Il2CppApi.EnumerateFields(classPtr);
            var field = fields.FirstOrDefault(f => f.Name == property);
            if (field.Name == null) return $"字段 {property} 未找到";
            if (field.Offset <= 0) return $"字段 {property} 是静态字段，不可修改";

            // 强化五项服务端同样钳制到 0-50（防止绕过前端直调 API）
            if (IsSoulStrengthenProp(property))
                value = Math.Clamp(value, 0, SoulStrengthenMax);

            WriteIl2CppInt(ptr, field.Offset, value);
            Plugin.LogInfo($"[DragonSoul] 设置 soul[{soulIndex}].{property} = {value}");
            RecordSoulModification(soul, property, value);
            return "ok";
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ====== 龙魂强化持久化 ======
    // 龙魂带跨读档稳定的 string guid，按 "dragon:{guid}:{field}" 记录；
    // 每次读取龙魂列表（含 3 秒轮询）前自动恢复，无场景扫描，不卡顿。

    internal const int SoulStrengthenMax = 50;

    private static readonly HashSet<string> _soulStrengthenProps = new() { "head", "claw", "shield", "cloud", "potentiality" };

    private static bool IsSoulStrengthenProp(string p) => _soulStrengthenProps.Contains(p);

    private static void RecordSoulModification(object soul, string property, int value)
    {
        try
        {
            var guid = GetProp(soul, "guid")?.ToString();
            if (!string.IsNullOrEmpty(guid))
                ModificationStore.RecordRaw($"dragon:{guid}:{property}", value);
        }
        catch { }
    }

    /// <summary>
    /// 游戏侧恢复入口（不依赖网页轮询）：龙魂强化立即恢复。
    /// 返回 true=完成（无记录或已应用）；false=游戏世界/龙魂列表未就绪，稍后重试。
    /// </summary>
    internal static bool TryRestoreSoulModifications()
    {
        if (ModificationStore.GetByPrefix("dragon:").Count == 0) return true;
        var w = GameContext.GetGame();
        if (w == null) return false;
        object? soulList = GetProp(w, "dragon_soul_list");
        if (soulList == null) return false;
        int count = Convert.ToInt32(soulList.GetType().GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
        if (count == 0) return false;
        ApplySoulModifications();
        return true;
    }

    /// <summary>
    /// 游戏侧恢复入口：扫描地图实体并内联应用 dragone: 记录（需要一次场景扫描）。
    /// </summary>
    internal static void RestoreEntityModificationsViaScan(UnityEngine.GameObject[]? source = null) => ReadDragonEntities(source);

    private static void ApplySoulModifications()
    {
        try
        {
            var pending = ModificationStore.GetByPrefix("dragon:");
            if (pending.Count == 0) return;

            var w = GameContext.GetGame();
            if (w == null) return;
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return;

            var slType = soulList.GetType();
            int count = Convert.ToInt32(slType.GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null || count == 0) return;

            // guid -> (field -> value)
            var byGuid = new Dictionary<string, List<KeyValuePair<string, float>>>();
            foreach (var kv in pending)
            {
                int a = kv.Key.IndexOf(':') + 1;
                int b = kv.Key.IndexOf(':', a);
                if (b <= a) continue;
                var guid = kv.Key.Substring(a, b - a);
                var field = kv.Key.Substring(b + 1);
                if (!byGuid.TryGetValue(guid, out var list2)) { list2 = new List<KeyValuePair<string, float>>(); byGuid[guid] = list2; }
                list2.Add(new KeyValuePair<string, float>(field, kv.Value));
            }

            IntPtr? classPtrCache = null;
            var fields = new List<(string Name, int Offset, string TypeName)>();
            for (int i = 0; i < count; i++)
            {
                var soul = getItem.Invoke(soulList, new object[] { i });
                if (soul == null) continue;
                var guid = GetProp(soul, "guid")?.ToString();
                if (string.IsNullOrEmpty(guid) || !byGuid.TryGetValue(guid!, out var mods)) continue;

                IntPtr ptr = GetIl2CppPtr(soul);
                if (ptr == IntPtr.Zero) continue;
                IntPtr classPtr = Il2CppApi.GetClass(ptr);
                if (classPtr != classPtrCache)
                {
                    fields = Il2CppApi.EnumerateFields(classPtr);
                    classPtrCache = classPtr;
                }
                foreach (var m in mods)
                {
                    int target = (int)m.Value;
                    if (IsSoulStrengthenProp(m.Key)) target = Math.Clamp(target, 0, SoulStrengthenMax);
                    var f = fields.FirstOrDefault(x => x.Name == m.Key);
                    if (f.Name == null || f.Offset <= 0) continue;
                    int cur = ReadIl2CppInt(ptr, f.Offset);
                    if (cur != target)
                    {
                        WriteIl2CppInt(ptr, f.Offset, target);
                        Plugin.LogInfo($"[DragonService] 恢复龙魂 {guid}.{m.Key} = {target}");
                    }
                }
            }
        }
        catch (Exception ex) { Plugin.LogError($"[DragonService] 恢复龙魂强化失败: {ex.Message}"); }
    }


    /// <summary>
    /// 搜索地图上的龙实体 GameObject
    /// </summary>

    // 龙实体战斗属性字段偏移缓存
    private static readonly Dictionary<string, int> _dragonFieldOffsets = new();

    private static bool _dragonOffsetsCached;


    private static void CacheDragonFieldOffsets(IntPtr compPtr, IntPtr compClass)
    {
        if (_dragonOffsetsCached) return;
        _dragonOffsetsCached = true;
        foreach (var (name, offset) in Il2CppApi.CollectFieldsInHierarchy(compClass))
        {
            if (!_dragonFieldOffsets.ContainsKey(name))
                _dragonFieldOffsets[name] = offset;
        }
    }


    private static readonly string[] _dragonStatFields = { "stuff_id", "guid", "hp", "hp_total", "atk_min", "atk_max", "magic_atk_min", "magic_atk_max", "speed", "power", "atk_range", "atk_cd", "view_range", "create_days",
        "train_increase_hp", "train_increase_atk", "train_increase_ability_power", "train_increase_phys_res", "train_increase_magic_res" };

    private static readonly HashSet<string> _dragonFloatFields = new() { "hp", "hp_total", "atk_min", "atk_max", "magic_atk_min", "magic_atk_max", "speed", "power", "atk_range", "atk_cd", "view_range" };


    internal static List<Dictionary<string, object>> ReadDragonEntities(UnityEngine.GameObject[]? source = null)
    {
        var result = new List<Dictionary<string, object>>();
        try
        {
            var allGOs = source ?? UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    if (!goName.ToLower().Contains("dragon")) continue;
                    found++;
                    if (found > 30) break;

                    var components = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;
                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                        if (compClass == IntPtr.Zero) continue;

                        // 检查是否有 hp_total 字段（战斗组件）
                        if (!_dragonOffsetsCached)
                        {
                            var tmpFields = Il2CppApi.CollectFieldsInHierarchy(compClass);
                            if (!tmpFields.Any(x => x.Name == "hp_total")) continue;
                            foreach (var (n, o) in tmpFields) { if (!_dragonFieldOffsets.ContainsKey(n)) _dragonFieldOffsets[n] = o; }
                            _dragonOffsetsCached = true;
                        }

                        if (!_dragonFieldOffsets.ContainsKey("hp_total")) continue;

                        int guid = 0, stuffId = 0;
                        try { guid = ReadIl2CppInt(compPtr, _dragonFieldOffsets["guid"]); } catch { }
                        try { stuffId = ReadIl2CppInt(compPtr, _dragonFieldOffsets["stuff_id"]); } catch { }
                        // 只保留真正的龙实体：stuffId 在 201401-2015510 范围，guid > 0
                        if (guid <= 0 || stuffId < 201401 || stuffId > 2015510) continue;

                        var dict = new Dictionary<string, object> { ["goName"] = goName };
                        foreach (var fname in _dragonStatFields)
                        {
                            if (!_dragonFieldOffsets.TryGetValue(fname, out int off)) continue;
                            try
                            {
                                if (_dragonFloatFields.Contains(fname))
                                    dict[fname] = ReadIl2CppFloat(compPtr, off);
                                else
                                    dict[fname] = ReadIl2CppInt(compPtr, off);
                            }
                            catch { }
                        }

                        // 应用持久化的实体属性修改（按 stuff_id 匹配，直接写内存并同步显示值）
                        var pendingEnt = ModificationStore.GetByPrefix("dragone:" + stuffId + ":");
                        foreach (var kv in pendingEnt)
                        {
                            int c = kv.Key.LastIndexOf(':');
                            if (c < 0) continue;
                            var fname2 = kv.Key.Substring(c + 1);
                            if (!_dragonFieldOffsets.TryGetValue(fname2, out int off2)) continue;
                            if (_dragonFloatFields.Contains(fname2))
                            {
                                WriteIl2CppFloat(compPtr, off2, kv.Value);
                                dict[fname2] = kv.Value;
                            }
                            else
                            {
                                WriteIl2CppInt(compPtr, off2, (int)kv.Value);
                                dict[fname2] = (int)kv.Value;
                            }
                        }
                        result.Add(dict);
                        break;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogError($"[ReadDragonEntities] 异常: {ex.Message}"); }
        Plugin.LogInfo($"[ReadDragonEntities] 完成, 找到 {result.Count} 个实体");
        return result;
    }


    internal static string GetDragonEntitiesJson()
    {
        var entities = ReadDragonEntities();
        return JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var d in entities)
            {
                w.WriteStartObject();
                foreach (var kv in d)
                {
                    w.WritePropertyName(kv.Key);
                    WriteJsonValue(w, kv.Value);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }


    internal static string SetDragonEntityField(int guid, string fieldName, float value)
    {
        try
        {
            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            int found = 0;
            bool guidMatched = false;
            string? unknownField = null;
            foreach (var go in allGOs)
            {
                try
                {
                    if (!go.name.ToLower().Contains("dragon")) continue;
                    found++;
                    if (found > 30) break;

                    var components = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;
                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                        if (compClass == IntPtr.Zero) continue;

                        // 与 ReadDragonEntities 一致：只认带 hp_total 的战斗组件补齐字段偏移，
                        // 避免非战斗组件污染缓存
                        if (!_dragonFieldOffsets.ContainsKey("hp_total"))
                        {
                            var tmpFields = Il2CppApi.CollectFieldsInHierarchy(compClass);
                            if (tmpFields.Any(x => x.Name == "hp_total"))
                                foreach (var (n, o) in tmpFields) { if (!_dragonFieldOffsets.ContainsKey(n)) _dragonFieldOffsets[n] = o; }
                        }

                        if (!_dragonFieldOffsets.TryGetValue("guid", out int guidOff)) continue;
                        int curGuid = ReadIl2CppInt(compPtr, guidOff);
                        if (curGuid != guid) continue;

                        guidMatched = true;
                        if (!_dragonFieldOffsets.TryGetValue(fieldName, out int offset))
                        {
                            // 该战斗组件没有此字段：记录并继续找同 GO 的其他组件（勿提前放弃）
                            unknownField = fieldName;
                            continue;
                        }

                        unsafe
                        {
                            if (_dragonFloatFields.Contains(fieldName))
                                *(float*)(compPtr + offset) = value;
                            else
                                *(int*)(compPtr + offset) = (int)value;
                        }

                        // 记录持久化：实体读档后重建、指针会变，按 stuff_id 匹配（与面板关联方式一致）
                        try
                        {
                            int stuffId = _dragonFieldOffsets.TryGetValue("stuff_id", out int sidOff)
                                ? ReadIl2CppInt(compPtr, sidOff) : 0;
                            if (stuffId > 0)
                                ModificationStore.RecordRaw($"dragone:{stuffId}:{fieldName}", value);
                        }
                        catch { }
                        return "ok";
                    }
                }
                catch { }
            }
            return unknownField != null ? $"unknown field: {unknownField}" : (guidMatched ? $"unknown field: {fieldName}" : "dragon not found");
        }
        catch (Exception ex) { return ex.Message; }
    }


    internal static void SearchMapDragonEntities()
    {
        try
        {

            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            Plugin.LogInfo($"[DragonEntity] 扫描 {allGOs.Length} 个 GameObject...");

            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    // 匹配包含 dragon (不区分大小写) 的 GO 名称
                    if (!goName.ToLower().Contains("dragon")) continue;

                    found++;

                    // 找到所有龙GO，读取战斗属性
                    {
                        var components = go.GetComponents<UnityEngine.Component>();
                        foreach (var comp in components)
                        {
                            if (comp == null) continue;
                            IntPtr compPtr = GetIl2CppPtr(comp);
                            if (compPtr == IntPtr.Zero) continue;
                            IntPtr compClass = IntPtr.Zero;
                            try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                            if (compClass == IntPtr.Zero) continue;

                            var allFields = Il2CppApi.CollectFieldsInHierarchy(compClass);

                            // 只输出有 hp_total 字段的组件（战斗组件）
                            if (!allFields.Any(x => x.Name == "hp_total")) continue;

                            int stuffId = 0, guid = 0;
                            float hp = 0, hpTotal = 0, atkMax = 0, mAtkMax = 0, speed = 0, power = 0;
                            foreach (var (name, offset) in allFields)
                            {
                                try
                                {
                                    switch (name)
                                    {
                                        case "stuff_id": stuffId = ReadIl2CppInt(compPtr, offset); break;
                                        case "guid": guid = ReadIl2CppInt(compPtr, offset); break;
                                        case "hp": hp = ReadIl2CppFloat(compPtr, offset); break;
                                        case "hp_total": hpTotal = ReadIl2CppFloat(compPtr, offset); break;
                                        case "atk_max": atkMax = ReadIl2CppFloat(compPtr, offset); break;
                                        case "magic_atk_max": mAtkMax = ReadIl2CppFloat(compPtr, offset); break;
                                        case "speed": speed = ReadIl2CppFloat(compPtr, offset); break;
                                        case "power": power = ReadIl2CppFloat(compPtr, offset); break;
                                    }
                                }
                                catch { }
                            }
                            Plugin.LogInfo($"[DragonEntity] {goName} guid={guid} stuffId={stuffId} HP={hp}/{hpTotal} ATK={atkMax} MATK={mAtkMax} SPD={speed} PWR={power}");
                            break; // 每个GO只取一个战斗组件
                        }
                    }

                    if (found >= 30) break;
                }
                catch { }
            }
            Plugin.LogInfo($"[DragonEntity] 共找到 {found} 个龙 GO");
        }
        catch (Exception ex) { Plugin.LogError($"[DragonEntity] 搜索异常: {ex.Message}"); }
    }


    private static MethodInfo? _dragonAddMethod;

    private static MethodInfo? _dragonRemoveMethod;

    private static MethodInfo? _dragonAddNoNotifyMethod;

    private static bool _dragonMethodsCached;


    private static void CacheBagOps(object bag)
    {
        if (_dragonMethodsCached) return;
        _dragonMethodsCached = true;

        var methods = bag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var m in methods)
        {
            if (m.Name == "AddStuffWithoutNotify")
            {
                var p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int))
                    _dragonAddNoNotifyMethod = m;
            }
            if (m.Name == "AddStuff" && _dragonAddMethod == null)
            {
                var p = m.GetParameters();
                if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                    _dragonAddMethod = m;
            }
            if (m.Name == "RemoveStuff")
            {
                var p = m.GetParameters();
                if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                    _dragonRemoveMethod = m;
            }
        }
    }

    internal static void InvalidateBagJsonCache()
    {
        _bagJsonAt = 0;
    }

    private static string _bagJson = "[]";
    private static long _bagJsonAt;

    /// <summary>合并实时读取与本地记账缓存，生成龙素材背包 JSON（带 500ms TTL 缓存）</summary>
    internal static string GetBagJson()
    {
        if (System.Environment.TickCount64 - _bagJsonAt < 500) return _bagJson;
        try
        {
            var liveItems = ReadDragonBagLive();
            var cache = GetDragonItemCache();
            var merged = new Dictionary<int, int>();
            foreach (var kv in liveItems)
                merged[kv.Key] = kv.Value;
            if (cache != null)
            {
                foreach (var kv in cache)
                {
                    if (kv.Value > 0 && !merged.ContainsKey(kv.Key))
                        merged[kv.Key] = kv.Value;
                }
            }

            _bagJson = JsonBuilder.Build(w =>
            {
                w.WriteStartArray();
                foreach (var kv in merged.OrderByDescending(x => x.Value))
                {
                    w.WriteStartObject();
                    w.WriteNumber("stuffId", kv.Key);
                    w.WriteString("name", DataTables.ItemName(kv.Key));
                    w.WriteNumber("count", kv.Value);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            });
            _bagJsonAt = System.Environment.TickCount64;
        }
        catch { _bagJson = "[]"; }
        return _bagJson;
    }
}
