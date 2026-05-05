using System;
using System.Collections.Generic;
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
