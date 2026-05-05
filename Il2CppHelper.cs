using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChestEditor;

internal static class Il2CppHelper
{
    internal const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    // IL2CPP 原生 API 缓存
    private static MethodInfo? _il2cpp_get_class;
    private static MethodInfo? _il2cpp_class_get_name;
    private static MethodInfo? _il2cpp_class_get_fields;
    private static MethodInfo? _il2cpp_field_get_name;
    private static MethodInfo? _il2cpp_field_get_offset;
    private static MethodInfo? _il2cpp_field_get_type;
    private static MethodInfo? _il2cpp_type_get_type;
    private static bool _il2cppApiCached;

    private static void CacheIl2CppApi()
    {
        if (_il2cppApiCached) return;
        _il2cppApiCached = true;
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Il2CppInterop.Runtime");
        if (asm == null) { Plugin.LogInfo("[Il2CppApi] Il2CppInterop.Runtime 未找到"); return; }
        var t = asm.GetTypes().FirstOrDefault(x => x.Name == "IL2CPP");
        if (t == null) { Plugin.LogInfo("[Il2CppApi] IL2CPP 类未找到"); return; }
        _il2cpp_get_class = t.GetMethod("il2cpp_object_get_class", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_class_get_name = t.GetMethod("il2cpp_class_get_name", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_class_get_fields = t.GetMethod("il2cpp_class_get_fields", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_field_get_name = t.GetMethod("il2cpp_field_get_name", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_field_get_offset = t.GetMethod("il2cpp_field_get_offset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_field_get_type = t.GetMethod("il2cpp_field_get_type", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _il2cpp_type_get_type = t.GetMethod("il2cpp_type_get_type", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Plugin.LogInfo($"[Il2CppApi] get_class={_il2cpp_get_class != null}, class_get_name={_il2cpp_class_get_name != null}, " +
            $"class_get_fields={_il2cpp_class_get_fields != null}, field_get_name={_il2cpp_field_get_name != null}, " +
            $"field_get_offset={_il2cpp_field_get_offset != null}");
    }

    // 从 Il2CppObjectBase 获取原生指针
    private static IntPtr GetIl2CppPtr(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return (IntPtr)prop.GetValue(obj)!;
        }
        catch { }
        return IntPtr.Zero;
    }

    // 将 IL2CPP 返回的 char* (IntPtr) 转为 string
    private static unsafe string? PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        try { return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr); } catch { return null; }
    }

    // 获取 IL2CPP 对象的真实类名
    private static string? GetIl2CppClassName(IntPtr ptr)
    {
        try
        {
            CacheIl2CppApi();
            if (_il2cpp_get_class == null || _il2cpp_class_get_name == null) return null;
            IntPtr classPtr = (IntPtr)_il2cpp_get_class.Invoke(null, new object[] { ptr })!;
            if (classPtr == IntPtr.Zero) return null;
            // il2cpp_class_get_name 返回 char* (IntPtr), 不是 string
            IntPtr namePtr = (IntPtr)_il2cpp_class_get_name.Invoke(null, new object[] { classPtr })!;
            return PtrToString(namePtr);
        }
        catch { return null; }
    }

    // 获取 IL2CPP 类的所有字段名和偏移
    private static List<(string Name, int Offset)> GetIl2CppFields(IntPtr classPtr)
    {
        var fields = new List<(string, int)>();
        try
        {
            CacheIl2CppApi();
            if (_il2cpp_class_get_fields == null || _il2cpp_field_get_name == null || _il2cpp_field_get_offset == null)
                return fields;

            // il2cpp_class_get_fields(klass, IntPtr& iter) - iter 是 ref IntPtr
            // 使用 IntPtr[] 数组来模拟 ref 传递
            IntPtr iter = IntPtr.Zero;
            object[] args = new object[] { classPtr, iter };

            while (true)
            {
                object? result = _il2cpp_class_get_fields.Invoke(null, args);
                if (result == null) break;
                IntPtr field = (IntPtr)result;
                if (field == IntPtr.Zero) break;

                // 读取字段名 (返回 char*)
                IntPtr namePtr = (IntPtr)_il2cpp_field_get_name.Invoke(null, new object[] { field })!;
                string? name = PtrToString(namePtr);

                // il2cpp_field_get_offset 返回 UInt32
                object offsetResult = _il2cpp_field_get_offset.Invoke(null, new object[] { field })!;
                int offset = offsetResult is uint u ? (int)u : Convert.ToInt32(offsetResult);

                if (name != null) fields.Add((name, offset));

                // 更新 iter 为当前 field 指针，用于下一次迭代
                args[1] = field;
            }
        }
        catch { }
        return fields;
    }

    // 从 IL2CPP 对象指针读取 int 字段
    private static unsafe int ReadIl2CppInt(IntPtr objPtr, int offset)
    {
        try { return *(int*)(objPtr + offset); } catch { return 0; }
    }

    // 从 IL2CPP 对象指针读取 string 字段
    private static unsafe string? ReadIl2CppString(IntPtr objPtr, int offset)
    {
        try
        {
            IntPtr strPtr = *(IntPtr*)(objPtr + offset);
            if (strPtr == IntPtr.Zero) return null;
            // IL2CPP string 对象布局: [klass(IntPtr), monitor(IntPtr), length(int), chars...]
            // 但实际布局取决于平台。在 64 位上: 前 8 字节是 klass, 接下来 4 字节是 length
            int len = *(int*)(strPtr + IntPtr.Size);
            if (len <= 0 || len > 10000) return null;
            // UTF-16 chars 从 IntPtr.Size + 4 开始（无 padding 在 64 位上）
            char* chars = (char*)(strPtr + IntPtr.Size + 4);
            return new string(chars, 0, len);
        }
        catch { return null; }
    }

    // 从 IL2CPP 对象指针读取 List<int> 的内容
    private static unsafe List<int>? ReadIl2CppIntList(IntPtr objPtr, int offset)
    {
        try
        {
            IntPtr listPtr = *(IntPtr*)(objPtr + offset);
            if (listPtr == IntPtr.Zero) return null;

            CacheIl2CppApi();
            if (_il2cpp_get_class == null) return null;

            IntPtr classPtr = (IntPtr)_il2cpp_get_class.Invoke(null, new object[] { listPtr })!;
            if (classPtr == IntPtr.Zero) return null;

            // 遍历字段找 _size 和 _items
            int size = -1;
            int itemsOffset = -1;
            var fields = GetIl2CppFields(classPtr);
            foreach (var (name, off) in fields)
            {
                if (name == "_size") size = ReadIl2CppInt(listPtr, off);
                else if (name == "_items") itemsOffset = off;
            }

            if (size <= 0 || itemsOffset < 0) return null;

            // _items 是 T[] 数组
            IntPtr itemsPtr = *(IntPtr*)(listPtr + itemsOffset);
            if (itemsPtr == IntPtr.Zero) return null;

            // 数组对象布局: [klass(IntPtr), monitor(IntPtr), max_length(int), length(int), data...]
            // 但实际上 IL2CPP 数组: [Il2CppObject(2*IntPtr), bounds(Il2CppArrayBounds), max_length(int), data...]
            // Il2CppObject = klass(8) + monitor(8), bounds = length(4) + lower_bound(4)
            // 所以数据从 offset = 2*IntPtr.Size + 8 开始
            int dataStart = 2 * IntPtr.Size + 8;
            var result = new List<int>();
            for (int i = 0; i < size; i++)
            {
                int val = *(int*)(itemsPtr + dataStart + i * 4);
                result.Add(val);
            }
            return result;
        }
        catch { return null; }
    }

    // 写入 IL2CPP 对象的 int 字段
    private static unsafe void WriteIl2CppInt(IntPtr objPtr, int offset, int value)
    {
        try { *(int*)(objPtr + offset) = value; } catch { }
    }

    // 读取 IL2CPP 对象的所有字段
    internal static Dictionary<string, object?> ReadIl2CppFields(object obj)
    {
        var dict = new Dictionary<string, object?>();
        try
        {
            IntPtr ptr = GetIl2CppPtr(obj);
            if (ptr == IntPtr.Zero) return dict;

            CacheIl2CppApi();
            if (_il2cpp_get_class == null) return dict;

            IntPtr classPtr = (IntPtr)_il2cpp_get_class.Invoke(null, new object[] { ptr })!;
            if (classPtr == IntPtr.Zero) return dict;

            var fields = GetIl2CppFields(classPtr);
            foreach (var (name, offset) in fields)
            {
                try
                {
                    // 简单启发：偏移大的是引用类型（string 等），偏移小且对齐的是值类型
                    // 对于 string 类型字段，尝试读取为 string
                    // 对于 int 类型字段，尝试读取为 int
                    // 先尝试 int，如果值看起来像指针则尝试 string
                    int intVal = ReadIl2CppInt(ptr, offset);
                    // IL2CPP 对象的字段从 IntPtr.Size 开始（前 IntPtr.Size 是类指针）
                    // 所以实际偏移需要加上 IntPtr.Size... 等等，offset 已经是相对于对象起始的偏移了
                    dict[name] = intVal;
                }
                catch { }
            }
        }
        catch { }
        return dict;
    }

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
        // 存档未加载时不要调用 Game.get_w()，否则 IL2CPP 会 AccessViolation 崩溃
        if (SaveLoadPatches.CachedTerritory == null) return null;

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

    // ====== 龙系统诊断 (已禁用) ======
    internal static void DiagnoseDragonSystem() { }

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

            // 读取第一个元素以获取字段布局
            var first = getItem.Invoke(soulList, new object[] { 0 });
            if (first == null) return null;

            IntPtr firstPtr = GetIl2CppPtr(first);
            if (firstPtr == IntPtr.Zero) return null;

            CacheIl2CppApi();
            IntPtr classPtr = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { firstPtr })!;
            var fields = GetIl2CppFields(classPtr);

            // 过滤掉静态字段 (offset=0 且不是第一个字段) 和已知常量
            var instanceFields = fields.Where(f =>
                f.Offset > 0 &&
                f.Name != "HEAD" && f.Name != "SHIELD" && f.Name != "CLAW" && f.Name != "CLOUD"
            ).ToList();

            // 获取字段类型信息
            var fieldTypes = new Dictionary<string, int>(); // 0=int, 1=string, 2=list
            if (_il2cpp_field_get_type != null && _il2cpp_type_get_type != null && _il2cpp_class_get_fields != null)
            {
                object[] args = new object[] { classPtr, IntPtr.Zero };
                int fi = 0;
                while (fi < fields.Count)
                {
                    object? fieldResult = _il2cpp_class_get_fields.Invoke(null, args);
                    if (fieldResult == null) break;
                    IntPtr fieldPtr = (IntPtr)fieldResult;
                    if (fieldPtr == IntPtr.Zero) break;

                    IntPtr typePtr = (IntPtr)_il2cpp_field_get_type.Invoke(null, new object[] { fieldPtr })!;
                    if (typePtr != IntPtr.Zero)
                    {
                        int typeEnum = (int)_il2cpp_type_get_type.Invoke(null, new object[] { typePtr })!;
                        // typeEnum: 14=string, 8=i4(int), 9=u4, 1=void*, etc.
                        // 对于引用类型 (class/interface), typeEnum 可能是其他值
                        string fn = fields[fi].Name;
                        if (typeEnum == 14) fieldTypes[fn] = 1; // string
                        else if (fn == "nature_list") fieldTypes[fn] = 2; // List<int>, 已知
                        else fieldTypes[fn] = 0; // int/其他值类型
                    }
                    args[1] = fieldPtr;
                    fi++;
                }
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
                foreach (var (name, offset) in instanceFields)
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
                else if (kv.Value is List<int> intList)
                    valStr = $"[{string.Join(",", intList)}]";
                else
                    valStr = $"\"{kv.Value.GetType().Name}\"";
                sb.Append($"\"{kv.Key}\":{valStr}");
            }
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // 修改龙魂属性 (通过 IL2CPP 原生字段写入)
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

            IntPtr ptr = GetIl2CppPtr(soul);
            if (ptr == IntPtr.Zero) return "无法获取原生指针";

            CacheIl2CppApi();
            IntPtr classPtr = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { ptr })!;
            var fields = GetIl2CppFields(classPtr);
            var field = fields.FirstOrDefault(f => f.Name == property);
            if (field.Name == null) return $"字段 {property} 未找到";
            if (field.Offset <= 0) return $"字段 {property} 是静态字段，不可修改";

            WriteIl2CppInt(ptr, field.Offset, value);
            Plugin.LogInfo($"[DragonSoul] 设置 soul[{soulIndex}].{property} = {value}");
            return "ok";
        }
        catch (Exception ex) { return ex.Message; }
    }

    // 查找地图上的龙对象 - 通过 IL2CPP 原生字段读取
    internal static void SearchMapDragons()
    {
        try
        {
            var souls = ReadDragonSouls();
            if (souls == null || souls.Count == 0)
            {
                Plugin.LogInfo("[MapDragon] 未找到龙魂数据");
                return;
            }

            Plugin.LogInfo($"[MapDragon] 共 {souls.Count} 条龙魂:");
            for (int i = 0; i < souls.Count; i++)
            {
                var soul = souls[i];
                string guid = soul.TryGetValue("guid", out var g) ? (g?.ToString() ?? "") : "";
                int stuffId = soul.TryGetValue("stuff_id", out var sid) ? Convert.ToInt32(sid ?? 0) : 0;
                int isActive = soul.TryGetValue("is_active", out var ia) ? Convert.ToInt32(ia ?? 0) : 0;

                // 查找龙类型名
                string typeName = "未知";
                foreach (var (Name, ChineseName, BaseId) in DragonTypes)
                {
                    if (stuffId >= BaseId && stuffId < BaseId + 10)
                    {
                        int level = stuffId - BaseId + 1;
                        typeName = $"{ChineseName} Lv{level}";
                        break;
                    }
                }

                string natureStr = "";
                if (soul.TryGetValue("nature_list", out var nl) && nl is List<int> natures && natures.Count > 0)
                {
                    var natureNames = natures.Select(nid =>
                    {
                        var found = DragonNatures.FirstOrDefault(x => x.Id == nid);
                        return found.Id > 0 ? found.Name : $"#{nid}";
                    });
                    natureStr = $" [{string.Join(",", natureNames)}]";
                }

                // 读取属性值
                int head = soul.TryGetValue("head", out var hv) ? Convert.ToInt32(hv ?? 0) : 0;
                int claw = soul.TryGetValue("claw", out var cv) ? Convert.ToInt32(cv ?? 0) : 0;
                int shield = soul.TryGetValue("shield", out var sv) ? Convert.ToInt32(sv ?? 0) : 0;
                int cloud = soul.TryGetValue("cloud", out var clv) ? Convert.ToInt32(clv ?? 0) : 0;
                int pot = soul.TryGetValue("potentiality", out var pv) ? Convert.ToInt32(pv ?? 0) : 0;

                Plugin.LogInfo($"[MapDragon] #{i}: {typeName} (stuffId={stuffId}) active={isActive} guid=\"{guid}\"{natureStr} head={head} claw={claw} shield={shield} cloud={cloud} pot={pot}");
            }
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
