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

    internal static List<KeyValuePair<int, int>>? ReadDragonStuffBag()
    {
        try
        {
            var w = GetGameW();
            if (w == null) return null;

            object? dragonBag = GetProp(w, "dragon_stuff_bag");
            if (dragonBag == null) return null;

            return ReadBagContents(dragonBag);
        }
        catch { return null; }
    }

    internal static List<KeyValuePair<int, int>> ReadBagContents(object bag)
    {
        var result = new List<KeyValuePair<int, int>>();
        try
        {
            // 策略1：找 GetStuffCount(int, dict, dict) 方法
            object? bagDic = GetProp(bag, "dic");
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

            // 备选：直接遍历 BagDic 的 Dictionary<int,int>
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

            // 备选：bag.GetAllStuff()
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

            // 获取当前数量
            int currentCount = 0;
            var contents = ReadBagContents(dragonBag);
            foreach (var kv in contents)
            {
                if (kv.Key == stuffId) { currentCount = kv.Value; break; }
            }

            if (newCount <= 0)
            {
                // 删除全部
                if (currentCount > 0 && _dragonRemoveMethod != null)
                {
                    _dragonRemoveMethod.Invoke(dragonBag, new object[] { stuffId, currentCount, false });
                    Plugin.LogInfo($"龙素材删除: {ItemNames.GetName(stuffId)}({stuffId}) x{currentCount}");
                }
            }
            else if (newCount != currentCount)
            {
                // 先删除旧的
                if (currentCount > 0 && _dragonRemoveMethod != null)
                    _dragonRemoveMethod.Invoke(dragonBag, new object[] { stuffId, currentCount, false });

                // 再添加新的
                if (_dragonAddNoNotifyMethod != null)
                    _dragonAddNoNotifyMethod.Invoke(dragonBag, new object[] { stuffId, newCount });
                else if (_dragonAddMethod != null)
                    _dragonAddMethod.Invoke(dragonBag, new object[] { stuffId, newCount, false });

                Plugin.LogInfo($"龙素材设置: {ItemNames.GetName(stuffId)}({stuffId}) {currentCount} -> {newCount}");
            }
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

    private static HashSet<int> _debuggedTypes = new();
    internal static void ClearDebuggedTypes() => _debuggedTypes.Clear();
    internal static void DebugStuffPlanDic(object facility, int stuffId, string name)
    {
        if (!_debuggedTypes.Add(stuffId)) return;
        try
        {
            if (facility is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return;
            IntPtr objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(il2cppObj);
            IntPtr realClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(objPtr);

            IntPtr methodPtr = IntPtr.Zero;
            IntPtr searchClass = realClass;
            while (searchClass != IntPtr.Zero)
            {
                methodPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchClass, "GetStuffPlanDic", 0);
                if (methodPtr != IntPtr.Zero) break;
                searchClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchClass);
            }

            if (methodPtr == IntPtr.Zero) return;

            IntPtr dictPtr;
            unsafe
            {
                IntPtr exception = IntPtr.Zero;
                void** args = null;
                dictPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, objPtr, args, ref exception);
            }

            if (dictPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[调试] {name}(stuffId={stuffId}) stuff_plan_dic=null");
                return;
            }

            var dict = new Il2CppSystem.Collections.Generic.Dictionary<int, int>(dictPtr);
            Plugin.LogInfo($"[调试] {name}(stuffId={stuffId}) stuff_plan_dic 条目数={dict.Count}");
        }
        catch (Exception ex)
        {
            Plugin.LogInfo($"[调试] {name}(stuffId={stuffId}) 出错: {ex.Message}");
        }
    }
}
