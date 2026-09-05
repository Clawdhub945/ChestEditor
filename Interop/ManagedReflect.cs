using System.Collections.Concurrent;
using System.Reflection;

namespace ChestEditor.Interop;

/// <summary>
/// 对 IL2CPP 互操作包装对象（托管代理）做 C# 反射读写的统一入口，带 MemberInfo 缓存。
/// </summary>
internal static class ManagedReflect
{
    internal const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    private static readonly ConcurrentDictionary<(Type, string), FieldInfo?> _fieldCache = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propCache = new();

    private static FieldInfo? FindField(Type type, string name) =>
        _fieldCache.GetOrAdd((type, name), static key =>
        {
            var t = (Type?)key.Item1;
            while (t != null && t != typeof(object))
            {
                var f = t.GetField(key.Item2, BF);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        });

    private static PropertyInfo? FindProp(Type type, string name) =>
        _propCache.GetOrAdd((type, name), static key =>
        {
            var t = (Type?)key.Item1;
            while (t != null && t != typeof(object))
            {
                var p = t.GetProperty(key.Item2, BF);
                if (p != null) return p;
                t = t.BaseType;
            }
            return null;
        });

    internal static object? GetProp(object obj, string name)
    {
        try { return FindProp(obj.GetType(), name)?.GetValue(obj); }
        catch { return null; }
    }

    internal static int GetInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var field = FindField(t, name);
            if (field != null) return (int)(field.GetValue(obj) ?? 0);
            var prop = FindProp(t, name);
            if (prop != null) return (int)(prop.GetValue(obj) ?? 0);
        }
        catch { }
        return 0;
    }

    internal static float GetFloat(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var field = FindField(t, name);
            if (field != null) return Convert.ToSingle(field.GetValue(obj) ?? 0);
            var prop = FindProp(t, name);
            if (prop != null) return Convert.ToSingle(prop.GetValue(obj) ?? 0);
        }
        catch { }
        return 0;
    }

    internal static bool GetBool(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var prop = FindProp(t, name);
            if (prop != null) return Convert.ToBoolean(prop.GetValue(obj));
            var field = FindField(t, name);
            if (field != null) return Convert.ToBoolean(field.GetValue(obj));
        }
        catch { }
        return false;
    }

    internal static void SetInt(object obj, string name, int value)
    {
        try
        {
            var t = obj.GetType();
            var field = FindField(t, name);
            if (field != null) { field.SetValue(obj, value); return; }
            FindProp(t, name)?.SetValue(obj, value);
        }
        catch { }
    }

    /// <summary>读取 Guid/guid 属性（优先 Guid）</summary>
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
}
