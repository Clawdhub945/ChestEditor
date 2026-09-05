using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ChestEditor.Interop;

/// <summary>IL2CPP 字段元数据（偏移 + 类型分类）</summary>
internal sealed class Il2CppField
{
    public int Offset;
    public string TypeName = "";
    public bool IsFloat;
    public bool IsString;
    public bool IsPointer;
}

/// <summary>
/// IL2CPP 原生 API 的唯一切入口：指针提取、类名、字段枚举（沿父类链、带缓存）。
/// 全项目禁止再私拷这些原语。
/// </summary>
internal static class Il2CppApi
{
    private static readonly HashSet<string> FloatTypeNames = new() { "System.Single", "float" };
    private static readonly HashSet<string> StringTypeNames = new() { "System.String", "String", "Il2CppSystem.String" };
    private static readonly HashSet<string> IntTypeNames = new()
    {
        "System.Int32", "int", "System.Int64", "long", "System.Boolean", "bool",
        "System.Byte", "byte", "System.Int16", "short", "System.UInt32",
        "System.UInt64", "System.UInt16", "System.SByte", "System.IntPtr"
    };

    private static readonly ConcurrentDictionary<IntPtr, Dictionary<string, Il2CppField>> _classFieldCache = new();

    /// <summary>清空字段布局缓存（游戏热重载/重扫场景时调用）</summary>
    internal static void ClearClassFieldCache() => _classFieldCache.Clear();

    /// <summary>从 IL2CPP 代理对象提取原生指针</summary>
    internal static IntPtr GetIl2CppPtr(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return (IntPtr)prop.GetValue(obj)!;
        }
        catch { }
        return IntPtr.Zero;
    }

    internal static string? PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringAnsi(ptr); } catch { return null; }
    }

    internal static IntPtr GetClass(IntPtr objPtr) => objPtr == IntPtr.Zero ? IntPtr.Zero : Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(objPtr);

    internal static string? GetClassName(IntPtr classPtr) => PtrToString(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(classPtr));

    internal static IntPtr GetParent(IntPtr classPtr) => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(classPtr);

    /// <summary>枚举单个类的实例字段（不含父类）</summary>
    internal static List<(string Name, int Offset, string TypeName)> EnumerateFields(IntPtr classPtr)
    {
        var fields = new List<(string, int, string)>();
        if (classPtr == IntPtr.Zero) return fields;
        try
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr field;
            while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(classPtr, ref iter)) != IntPtr.Zero)
            {
                string? name = PtrToString(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                if (string.IsNullOrEmpty(name)) continue;

                int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);

                string typeName = "";
                try
                {
                    IntPtr typePtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(field);
                    if (typePtr != IntPtr.Zero)
                        typeName = PtrToString(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(typePtr)) ?? "";
                }
                catch { }

                fields.Add((name, offset, typeName));
            }
        }
        catch { }
        return fields;
    }

    /// <summary>
    /// 沿父类链收集字段并按类指针缓存（depth &lt; 15，环形防护）。
    /// force=true 时忽略缓存强制重扫（游戏热重载后字段布局可能变化）。
    /// </summary>
    internal static Dictionary<string, Il2CppField> GetClassFieldsCached(IntPtr compClass, string className, bool force = false)
    {
        if (!force && _classFieldCache.TryGetValue(compClass, out var cached))
            return cached;

        var fieldMap = new Dictionary<string, Il2CppField>();
        var seen = new HashSet<long>();
        IntPtr cls = compClass;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < 15)
        {
            if (!seen.Add(cls.ToInt64())) break;

            foreach (var (name, offset, typeName) in EnumerateFields(cls))
            {
                if (offset > 0 && !fieldMap.ContainsKey(name))
                {
                    bool isFloat = FloatTypeNames.Contains(typeName);
                    bool isString = StringTypeNames.Contains(typeName);
                    bool isInt = IntTypeNames.Contains(typeName);
                    fieldMap[name] = new Il2CppField
                    {
                        Offset = offset,
                        TypeName = typeName,
                        IsFloat = isFloat,
                        IsString = isString,
                        IsPointer = !isFloat && !isString && !isInt
                    };
                }
            }

            try { cls = GetParent(cls); } catch { break; }
            depth++;
        }

        _classFieldCache[compClass] = fieldMap;
        if (fieldMap.Count == 0)
            Plugin.LogInfo($"[Il2CppApi] 类 {className} 无字段 (depth={depth})");
        return fieldMap;
    }

    /// <summary>安全读取对象的指针字段（含父类搜索，校验偏移范围）</summary>

    /// <summary>
    /// 安全读取对象的指针字段（含父类搜索），使用 il2cpp_field_get_offset
    /// </summary>
    internal static IntPtr ReadFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName)
    {
        try
        {
            IntPtr searchCls = classPtr;
            int depth = 0;
            while (searchCls != IntPtr.Zero && depth < 10)
            {
                IntPtr fi = IntPtr.Zero;
                IntPtr field;
                while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(searchCls, ref fi)) != IntPtr.Zero)
                {
                    string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                    if (fn == fieldName)
                    {
                        int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);
                        // 验证偏移量合理（对象头至少 0x10 字节）
                        if (offset < 0x10 || offset > 0x10000)
                        {
                            Plugin.LogInfo($"[EntityEditor] ReadFieldSafe: {fieldName} offset={offset} seems invalid, skipping");
                            return IntPtr.Zero;
                        }
                        unsafe
                        {
                            IntPtr value = *(IntPtr*)(objPtr + offset);
                            return value;
                        }
                    }
                }
                searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                depth++;
            }
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] ReadFieldSafe({fieldName}) error: {ex.Message}"); }
        return IntPtr.Zero;
    }


    /// <summary>
    /// 安全读取对象的 int 字段（含父类搜索）
    /// </summary>
    internal static int ReadIntFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName, int defaultVal = 0)
    {
        try
        {
            IntPtr searchCls = classPtr;
            int depth = 0;
            while (searchCls != IntPtr.Zero && depth < 10)
            {
                IntPtr fi = IntPtr.Zero;
                IntPtr field;
                while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(searchCls, ref fi)) != IntPtr.Zero)
                {
                    string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                    if (fn == fieldName)
                    {
                        int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);
                        if (offset < 0x10 || offset > 0x10000) return defaultVal;
                        unsafe { return *(int*)(objPtr + offset); }
                    }
                }
                searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                depth++;
            }
        }
        catch { }
        return defaultVal;
    }
}
