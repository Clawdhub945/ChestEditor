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
internal static partial class DragonService
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
