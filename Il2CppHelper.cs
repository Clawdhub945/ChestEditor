using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChestEditor;

internal static class Il2CppHelper
{
    internal const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    internal static object? GetProp(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var prop = t.GetProperty(name, BF);
                if (prop != null) return prop.GetValue(obj);
                t = t.BaseType;
            }
        }
        catch { }
        return null;
    }

    internal static int GetInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(name, BF);
                if (field != null) return (int)(field.GetValue(obj) ?? 0);
                var prop = t.GetProperty(name, BF);
                if (prop != null) return (int)(prop.GetValue(obj) ?? 0);
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    internal static int GetGuid(object obj)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                foreach (var name in new[] { "Guid", "guid" })
                {
                    var prop = t.GetProperty(name, BF);
                    if (prop != null) { var g = prop.GetGetMethod(); if (g != null) return (int)(g.Invoke(obj, null) ?? 0); }
                }
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    internal static float GetFloat(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(name, BF);
                if (field != null) return Convert.ToSingle(field.GetValue(obj) ?? 0);
                var prop = t.GetProperty(name, BF);
                if (prop != null) return Convert.ToSingle(prop.GetValue(obj) ?? 0);
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    internal static IntPtr FindIl2CppMethod(object facility, string methodName)
    {
        if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return IntPtr.Zero;
        IntPtr objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(il2cppObj);
        IntPtr realClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(objPtr);
        IntPtr searchClass = realClass;
        while (searchClass != IntPtr.Zero)
        {
            var m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchClass, methodName, 0);
            if (m != IntPtr.Zero) return m;
            searchClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchClass);
        }
        return IntPtr.Zero;
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
            var w = GetGameW();
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

    internal static object? GetGameW()
    {
        try
        {
            // Game.get_w() 是静态方法
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return null;

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return null;

            var getW = gameType.GetMethod("get_w", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (getW != null)
                return getW.Invoke(null, null);

            // 备选：找静态字段 w 或 _w 或 Ins
            foreach (var name in new[] { "w", "_w", "Ins", "instance" })
            {
                var field = gameType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    var val = field.GetValue(null);
                    if (val != null) return val;
                }
                var prop = gameType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null)
                {
                    var getter = prop.GetGetMethod(true);
                    if (getter != null) return getter.Invoke(null, null);
                }
            }
        }
        catch { }
        return null;
    }

    // ====== 龙系统诊断 ======
    private static bool _dragonDiagDone = false;
    internal static void DiagnoseDragonSystem()
    {
        if (_dragonDiagDone) return;

        try
        {
            var w = GetGameW();
            if (w == null) return; // 等存档加载

            _dragonDiagDone = true;

            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            Type[]? csharpTypes = null;
            try { csharpTypes = csharpAsm?.GetTypes(); } catch { }
            if (csharpTypes == null) return;
            var wType = w.GetType();
            Plugin.LogInfo($"[DragonDiag] Game.w 类型: {wType.FullName}");

            // 列出 dragon 相关字段/属性
            var t = wType;
            while (t != null && t != typeof(object))
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.Name.Contains("dragon") && !field.Name.Contains("Dragon")) continue;
                    try
                    {
                        var val = field.GetValue(w);
                        string valStr = val == null ? "null" : $"{val.GetType().FullName}";
                        Plugin.LogInfo($"[DragonDiag] 字段 {field.Name} : {field.FieldType.Name} = {valStr}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[DragonDiag] 字段 {field.Name} : 读取失败 {ex.Message}"); }
                }
                foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!prop.Name.Contains("dragon") && !prop.Name.Contains("Dragon")) continue;
                    try
                    {
                        var getter = prop.GetGetMethod(true);
                        if (getter == null) continue;
                        var val = getter.Invoke(w, null);
                        string valStr = val == null ? "null" : $"{val.GetType().FullName}";
                        Plugin.LogInfo($"[DragonDiag] 属性 {prop.Name} : {prop.PropertyType.Name} = {valStr}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[DragonDiag] 属性 {prop.Name} : 读取失败 {ex.Message}"); }
                }
                t = t.BaseType;
            }

            // 列出 dragon 相关方法
            Plugin.LogInfo("[DragonDiag] === Game.w 上 dragon 相关方法 ===");
            foreach (var m in wType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name.Contains("ragon") || m.Name.Contains("Dragon"))
                {
                    var p = m.GetParameters();
                    var pStr = string.Join(", ", p.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                    Plugin.LogInfo($"[DragonDiag] 方法 {m.Name}({pStr})");
                }
            }

            // 尝试读取 dragon_soul_list
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null)
            {
                Plugin.LogInfo("[DragonDiag] dragon_soul_list 为 null");
            }
            else
            {
                var slType = soulList.GetType();
                Plugin.LogInfo($"[DragonDiag] dragon_soul_list 类型: {slType.FullName}");

                // 尝试获取 Count
                int count = 0;
                var countProp = slType.GetProperty("Count", BF);
                if (countProp != null)
                    count = Convert.ToInt32(countProp.GetValue(soulList) ?? 0);
                Plugin.LogInfo($"[DragonDiag] dragon_soul_list.Count = {count}");

                // 列出 dragon_soul_list 的方法
                Plugin.LogInfo("[DragonDiag] === dragon_soul_list 方法 ===");
                foreach (var m in slType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var p = m.GetParameters();
                    var pStr = string.Join(", ", p.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                    Plugin.LogInfo($"[DragonDiag]   {m.Name}({pStr})");
                }

                // 如果有元素，检查第一个 DragonSoul 的结构
                if (count > 0)
                {
                    var getItem = slType.GetMethod("get_Item", BF);
                    if (getItem != null)
                    {
                        var first = getItem.Invoke(soulList, new object[] { 0 });
                        if (first != null)
                        {
                            var soulType = first.GetType();
                            Plugin.LogInfo($"[DragonDiag] DragonSoul 类型: {soulType.FullName}");
                            var st = soulType;
                            while (st != null && st != typeof(object))
                            {
                                foreach (var f in st.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                                {
                                    try
                                    {
                                        var v = f.GetValue(first);
                                        Plugin.LogInfo($"[DragonDiag]   {f.Name} = {v}");
                                    }
                                    catch (Exception ex) { Plugin.LogInfo($"[DragonDiag]   {f.Name} : err {ex.Message}"); }
                                }
                                st = st.BaseType;
                            }
                            // DragonSoul 方法
                            Plugin.LogInfo("[DragonDiag] === DragonSoul 方法 ===");
                            foreach (var m in soulType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                var p = m.GetParameters();
                                var pStr = string.Join(", ", p.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                                Plugin.LogInfo($"[DragonDiag]   {m.Name}({pStr})");
                            }
                        }
                    }
                }
            }

            // 检查 dragon_control_count / dragon_control_level
            int controlCount = GetInt(w, "dragon_control_count");
            int controlLevel = GetInt(w, "dragon_control_level");
            Plugin.LogInfo($"[DragonDiag] dragon_control_count={controlCount}, dragon_control_level={controlLevel}");

            // 检查 AddDragonSoul 方法参数
            var addDragonMethod = wType.GetMethod("AddDragonSoul", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (addDragonMethod != null)
            {
                var p = addDragonMethod.GetParameters();
                Plugin.LogInfo($"[DragonDiag] AddDragonSoul 参数: {string.Join(", ", p.Select(x => $"{x.ParameterType.FullName} {x.Name}"))}");
                // 检查返回类型
                Plugin.LogInfo($"[DragonDiag] AddDragonSoul 返回类型: {addDragonMethod.ReturnType.FullName}");

                // 检查 monster_nature_list 的元素类型
                if (p.Length >= 2)
                {
                    var listType = p[1].ParameterType;
                    Plugin.LogInfo($"[DragonDiag] nature_list 参数类型: {listType.FullName}");
                    if (listType.IsGenericType)
                    {
                        var args = listType.GetGenericArguments();
                        foreach (var a in args)
                            Plugin.LogInfo($"[DragonDiag] nature_list 泛型参数: {a.FullName}");
                    }
                }
            }

            // 尝试查看 DragonNatures 枚举或数据
            try
            {
                var dragonNatureType = csharpTypes.FirstOrDefault(t => t.Name == "DragonNature" || t.Name.Contains("DragonNature"));
                if (dragonNatureType != null)
                {
                    Plugin.LogInfo($"[DragonDiag] DragonNature 类型: {dragonNatureType.FullName}");
                    foreach (var f in dragonNatureType.GetFields(BindingFlags.Static | BindingFlags.Public))
                        Plugin.LogInfo($"[DragonDiag]   静态字段 {f.Name} = {f.GetValue(null)}");
                }
                else
                {
                    Plugin.LogInfo("[DragonDiag] 未找到 DragonNature 类型");
                }
            }
            catch { }

            // 搜索所有包含 "Nature" 的枚举
            try
            {
                foreach (var et in csharpTypes)
                {
                    try
                    {
                        if (et.IsEnum && et.Name.Contains("ature") && et.Name.Contains("Dragon"))
                        {
                            Plugin.LogInfo($"[DragonDiag] 枚举 {et.FullName}:");
                            foreach (var name in Enum.GetNames(et))
                                Plugin.LogInfo($"[DragonDiag]   {name} = {(int)Enum.Parse(et, name)}");
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 搜索所有包含 Monster 的枚举
            try
            {
                foreach (var et in csharpTypes)
                {
                    try
                    {
                        if (et.IsEnum && et.Name.Contains("Monster"))
                        {
                            var names = Enum.GetNames(et);
                            Plugin.LogInfo($"[DragonDiag] 枚举 {et.FullName} ({names.Length} 个值):");
                            foreach (var name in names.Take(10))
                                Plugin.LogInfo($"[DragonDiag]   {name} = {(int)Enum.Parse(et, name)}");
                            if (names.Length > 10) Plugin.LogInfo($"[DragonDiag]   ... 共 {names.Length} 个");
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 搜索 DialogChooseDragon 和 DialogCreateMonster 的方法
            try
            {
                foreach (var typeName in new[] { "DialogChooseDragon", "DialogCreateMonster" })
                {
                    var dialogType = csharpTypes.FirstOrDefault(t => t.Name == typeName);
                    if (dialogType != null)
                    {
                        Plugin.LogInfo($"[DragonDiag] === {typeName} 方法 ===");
                        foreach (var m in dialogType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            var p = m.GetParameters();
                            var pStr = string.Join(", ", p.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                            Plugin.LogInfo($"[DragonDiag]   {m.Name}({pStr}) -> {m.ReturnType.Name}");
                        }
                    }
                }
            }
            catch { }

            // 搜索所有包含 "MonsterType" 或 "CreatureType" 的枚举
            try
            {
                foreach (var et in csharpTypes)
                {
                    try
                    {
                        if (et.IsEnum && (et.Name == "MonsterType" || et.Name == "CreatureType" || et.Name == "StuffType"))
                        {
                            var names = Enum.GetNames(et);
                            Plugin.LogInfo($"[DragonDiag] 枚举 {et.FullName} ({names.Length} 个值):");
                            foreach (var name in names.Where(n => n.Contains("ragon") || n.Contains("Dragon")))
                                Plugin.LogInfo($"[DragonDiag]   {name} = {(int)Enum.Parse(et, name)}");
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        catch (Exception ex) { Plugin.LogError($"[DragonDiag] 异常: {ex.Message}"); }
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
                foreach (var kvp in ItemNames.GetAllItems())
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
                foreach (var kvp in ItemNames.GetAllItems())
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
                foreach (var kvp in ItemNames.GetAllItems())
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
            var w = GetGameW();
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
            Plugin.LogInfo($"龙素材设置: {ItemNames.GetName(stuffId)}({stuffId}) -> {newCount}");
        }
        catch (Exception ex) { Plugin.LogError($"SetDragonItemQuantity 出错: {ex.Message}"); }
    }

    // ====== 召唤龙 ======
    private static MethodInfo? _addDragonSoulMethod;

    internal static readonly (string Name, string ChineseName, int BaseId)[] DragonTypes = new[]
    {
        ("MonsterDragon1Rock",        "岩龙",     201401),
        ("MonsterDragon2Molten",      "熔岩龙",   201411),
        ("MonsterDragon3Ironthorn",   "铁棘龙",   201421),
        ("MonsterDragon4Void",        "虚空龙",   201431),
        ("MonsterDragon5Fire",        "火龙",     201441),
        ("MonsterDragon6Thunder",     "电龙",     201451),
        ("MonsterDragon7Wind",        "疾风龙",   201461),
        ("MonsterDragon8Nether",      "幽冥龙",   201471),
        ("MonsterDragon9Ice",         "冰龙",     201481),
        ("MonsterDragon10Poison",     "毒龙",     201491),
        ("MonsterDragon11Storm",      "风暴龙",   201501),
        ("MonsterDragon12Mirage",     "幻心龙",   201511),
        ("MonsterDragon13SacredShield","圣盾龙",  201521),
        ("MonsterDragon14Arcane",     "奥术龙",   201531),
        ("MonsterDragon15Tidal",      "潮汐龙",   201541),
        ("MonsterDragon16AncientTree", "远古树龙", 201551),
    };

    internal static readonly (int Id, string Name)[] DragonNatures = new[]
    {
        (1, "坚韧"), (2, "洞察"), (3, "再生"), (4, "反震"), (5, "迅捷"), (6, "穿透"),
        (7, "蓄能"), (8, "破防"), (9, "护盾"), (10, "警觉"), (11, "刚毅"), (12, "沉稳"),
        (13, "狂怒"), (14, "重创"), (15, "牵制"), (16, "反制"), (17, "冷血"), (18, "血誓"),
    };

    internal static string GetDragonTypesJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (int i = 0; i < DragonTypes.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var (name, cn, baseId) = DragonTypes[i];
            sb.Append($"{{\"name\":\"{name}\",\"cn\":\"{cn}\",\"baseId\":{baseId}}}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    internal static string GetDragonNaturesJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (int i = 0; i < DragonNatures.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"id\":{DragonNatures[i].Id},\"name\":\"{DragonNatures[i].Name}\"}}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    internal static string SummonDragon(int dragonStuffId, int[]? natureIds = null)
    {
        try
        {
            var w = GetGameW();
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

    internal static void SetInt(object obj, string name, int value)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(name, BF);
                if (field != null) { field.SetValue(obj, value); return; }
                var prop = t.GetProperty(name, BF);
                if (prop != null) { prop.SetValue(obj, value); return; }
                t = t.BaseType;
            }
        }
        catch { }
    }

    // ====== 龙魂列表读取 ======
    private static bool _dragonSoulTypeDumped;

    internal static List<Dictionary<string, object?>>? ReadDragonSouls()
    {
        try
        {
            var w = GetGameW();
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

            // 第一次 dump DragonSoul 类型结构
            var first = getItem.Invoke(soulList, new object[] { 0 });
            if (first != null && !_dragonSoulTypeDumped)
            {
                _dragonSoulTypeDumped = true;
                var soulType = first.GetType();
                Plugin.LogInfo($"[DragonSoul] 类型: {soulType.FullName}");
                var st = soulType;
                while (st != null && st != typeof(object))
                {
                    foreach (var f in st.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        try { Plugin.LogInfo($"[DragonSoul]   字段 {f.Name} ({f.FieldType.Name}) = {f.GetValue(first)}"); }
                        catch { }
                    }
                    foreach (var p in st.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        try
                        {
                            var getter = p.GetGetMethod(true);
                            if (getter != null) Plugin.LogInfo($"[DragonSoul]   属性 {p.Name} ({p.PropertyType.Name}) = {getter.Invoke(first, null)}");
                        }
                        catch { }
                    }
                    st = st.BaseType;
                }
                // DragonSoul 方法
                Plugin.LogInfo("[DragonSoul] === 方法 ===");
                foreach (var m in soulType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var pars = m.GetParameters();
                    var pStr = string.Join(", ", pars.Select(x => $"{x.ParameterType.Name} {x.Name}"));
                    Plugin.LogInfo($"[DragonSoul]   {m.Name}({pStr}) -> {m.ReturnType.Name}");
                }
            }

            // 读取所有龙魂数据
            var result = new List<Dictionary<string, object?>>();
            for (int i = 0; i < count; i++)
            {
                var soul = getItem.Invoke(soulList, new object[] { i });
                if (soul == null) continue;
                var dict = new Dictionary<string, object?>();
                var soulType = soul.GetType();
                var st2 = soulType;
                while (st2 != null && st2 != typeof(object))
                {
                    foreach (var f in st2.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        try { dict[f.Name] = f.GetValue(soul); } catch { }
                    }
                    foreach (var p in st2.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        try
                        {
                            var getter = p.GetGetMethod(true);
                            if (getter != null) dict[p.Name] = getter.Invoke(soul, null);
                        }
                        catch { }
                    }
                    st2 = st2.BaseType;
                }
                result.Add(dict);
            }
            return result;
        }
        catch { return null; }
    }

    internal static string GetDragonSoulsJson()
    {
        var souls = ReadDragonSouls();
        if (souls == null) return "[]";
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (int i = 0; i < souls.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('{');
            bool first = true;
            foreach (var kv in souls[i])
            {
                if (!first) sb.Append(',');
                first = false;
                string valStr;
                if (kv.Value == null) valStr = "null";
                else if (kv.Value is bool b) valStr = b ? "true" : "false";
                else if (kv.Value is int or float or double or long)
                    valStr = kv.Value.ToString()!;
                else if (kv.Value is string s)
                    valStr = $"\"{s.Replace("\"", "'")}\"";
                else
                    valStr = $"\"{kv.Value.GetType().Name}\"";
                sb.Append($"\"{kv.Key}\":{valStr}");
            }
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // 修改龙魂属性
    internal static string SetDragonSoulProperty(int soulIndex, string property, int value)
    {
        try
        {
            var w = GetGameW();
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

            // 用属性 setter 设置值
            var soulType = soul.GetType();
            var setter = soulType.GetMethod($"set_{property}", BF);
            if (setter == null) return $"属性 {property} setter 未找到";

            setter.Invoke(soul, new object[] { value });
            Plugin.LogInfo($"[DragonSoul] 设置 soul[{soulIndex}].{property} = {value}");
            return "ok";
        }
        catch (Exception ex) { return ex.Message; }
    }

    // 查找地图上的龙对象
    internal static void SearchMapDragons()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) { Plugin.LogInfo("[MapDragon] Assembly-CSharp 未找到"); return; }
            var msType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "MonsterStatus");
            if (msType == null) { Plugin.LogInfo("[MapDragon] MonsterStatus 类型未找到"); return; }

            // 用非泛型 FindObjectsOfType(Type)
            MethodInfo? findMethod = null;
            foreach (var m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (m.Name == "FindObjectsOfType" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(Type))
                { findMethod = m; break; }
            }
            if (findMethod == null) { Plugin.LogInfo("[MapDragon] FindObjectsOfType(Type) 未找到"); return; }
            var objects = findMethod.Invoke(null, new object[] { msType }) as Array;
            if (objects == null || objects.Length == 0)
            {
                Plugin.LogInfo("[MapDragon] 场景中未找到 MonsterStatus 对象");
                return;
            }

            Plugin.LogInfo($"[MapDragon] 找到 {objects.Length} 个 MonsterStatus 对象");
            int dragonCount = 0;
            foreach (var obj in objects)
            {
                try
                {
                    if (obj == null) continue;
                    var guidProp = obj.GetType().GetProperty("dragon_soul_guid", BF);
                    if (guidProp != null)
                    {
                        var guidVal = guidProp.GetValue(obj);
                        if (guidVal != null && guidVal.ToString() != "0" && !string.IsNullOrEmpty(guidVal?.ToString()))
                        {
                            dragonCount++;
                            Plugin.LogInfo($"[MapDragon] 龙对象 #{dragonCount}: guid={guidVal}");
                            var t = obj.GetType();
                            while (t != null && t != typeof(object))
                            {
                                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                                {
                                    try { Plugin.LogInfo($"[MapDragon]   {f.Name} ({f.FieldType.Name}) = {f.GetValue(obj)}"); } catch { }
                                }
                                t = t.BaseType;
                            }
                        }
                    }
                }
                catch { }
            }
            Plugin.LogInfo($"[MapDragon] 共找到 {dragonCount} 条龙");
        }
        catch (Exception ex) { Plugin.LogError($"[MapDragon] 搜索异常: {ex.Message}"); }
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

    private static HashSet<int> _debuggedTypes = new();
    internal static void ClearDebuggedTypes() => _debuggedTypes.Clear();
    internal static void DebugStuffPlanDic(object facility, int stuffId, string name) { }
}
