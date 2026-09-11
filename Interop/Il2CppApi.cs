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

/// <summary>IL2CPP 方法元数据（名称 + 方法指针 + 参数个数）</summary>
internal readonly record struct Il2CppMethodInfo(string Name, IntPtr Method, int ParamCount);

/// <summary>
/// IL2CPP 原生 API 的唯一切入口：指针提取、类名、字段/方法枚举（沿父类链、带缓存）。
/// 全项目禁止再私拷这些原语，业务层不得直接调用 Il2CppInterop.Runtime.IL2CPP.il2cpp_*。
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

    // ==================== 指针 / 字符串 / 类 ====================

    /// <summary>从 IL2CPP 代理对象提取原生指针</summary>
    internal static IntPtr GetIl2CppPtr(object? obj)
    {
        if (obj == null) return IntPtr.Zero;
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

    /// <summary>类名查找时依次尝试的候选命名空间（原 EntityDestroyer 内联的猜测列表）</summary>
    private static readonly string[] CandidateNamespaces = { "Il2Cpp", "Il2CppScripts", "Game", "" };

    /// <summary>在所有已加载的 IL2CPP 程序集中按类名查找类（依次尝试候选命名空间）</summary>
    internal static unsafe IntPtr FindClassByName(string className)
    {
        try
        {
            IntPtr domain = DomainGet();
            uint count = 0;
            IntPtr* assemblies = DomainGetAssemblies(domain, ref count);
            for (uint i = 0; i < count; i++)
            {
                IntPtr image = AssemblyGetImage(assemblies[i]);
                IntPtr cls = ClassFromName(image, "", className);
                if (cls != IntPtr.Zero) return cls;
                foreach (var ns in CandidateNamespaces)
                {
                    cls = ClassFromName(image, ns, className);
                    if (cls != IntPtr.Zero) return cls;
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    // ==================== 字段枚举 / 查找 ====================

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
    /// 沿父类链按名查找字段偏移（环形防护）。找到返回 true 并输出 offset。
    /// </summary>
    internal static bool TryFindFieldOffset(IntPtr classPtr, string fieldName, out int offset, int maxDepth = 10)
    {
        offset = -1;
        var seen = new HashSet<long>();
        IntPtr cls = classPtr;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < maxDepth)
        {
            if (!seen.Add(cls.ToInt64())) break; // 环形防护
            foreach (var (name, off, _) in EnumerateFields(cls))
            {
                if (name == fieldName) { offset = off; return true; }
            }
            try { cls = GetParent(cls); } catch { break; }
            depth++;
        }
        return false;
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

    // ==================== 字段读取（安全） ====================

    /// <summary>安全读取对象的指针字段（含父类搜索，校验偏移范围）</summary>
    internal static IntPtr ReadFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName)
    {
        if (!TryFindFieldOffset(classPtr, fieldName, out int offset)) return IntPtr.Zero;
        if (offset < 0x10 || offset > 0x10000)
        {
            Plugin.LogInfo($"[Il2CppApi] ReadFieldSafe: {fieldName} offset={offset} 非法，跳过");
            return IntPtr.Zero;
        }
        try { return Il2CppMemory.ReadIl2CppPointer(objPtr, offset); }
        catch (Exception ex) { Plugin.LogInfo($"[Il2CppApi] ReadFieldSafe({fieldName}) error: {ex.Message}"); return IntPtr.Zero; }
    }

    /// <summary>按多个候选字段名依次读取指针字段（第一个命中的有效偏移生效）</summary>
    internal static IntPtr ReadPointerFieldAny(IntPtr objPtr, IntPtr classPtr, params string[] fieldNames)
    {
        foreach (var name in fieldNames)
        {
            IntPtr v = ReadFieldSafe(objPtr, classPtr, name);
            if (v != IntPtr.Zero) return v;
        }
        return IntPtr.Zero;
    }

    /// <summary>安全读取对象的 int 字段（含父类搜索）</summary>
    internal static int ReadIntFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName, int defaultVal = 0)
    {
        if (!TryFindFieldOffset(classPtr, fieldName, out int offset)) return defaultVal;
        if (offset < 0x10 || offset > 0x10000) return defaultVal;
        try { return Il2CppMemory.ReadIl2CppInt(objPtr, offset); }
        catch { return defaultVal; }
    }

    // ==================== 方法枚举 / 查找 ====================

    /// <summary>枚举单个类的实例方法（不含父类）</summary>
    internal static List<Il2CppMethodInfo> EnumerateMethods(IntPtr classPtr)
    {
        var list = new List<Il2CppMethodInfo>();
        if (classPtr == IntPtr.Zero) return list;
        try
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr m;
            while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(classPtr, ref iter)) != IntPtr.Zero)
            {
                string? name = PtrToString(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m));
                if (string.IsNullOrEmpty(name)) continue;
                int pc = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(m);
                list.Add(new Il2CppMethodInfo(name, m, pc));
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// 沿父类链收集字段（按名去重、仅取 offset&gt;0、depth&lt;maxDepth、环形防护）。
    /// 替代各业务类里重复的 "while 父类 → EnumerateFields" 循环。
    /// </summary>
    internal static List<(string Name, int Offset)> CollectFieldsInHierarchy(IntPtr classPtr, int maxDepth = 10)
    {
        var list = new List<(string Name, int Offset)>();
        var seen = new HashSet<long>();
        IntPtr cls = classPtr;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < maxDepth)
        {
            if (!seen.Add(cls.ToInt64())) break;
            foreach (var (name, offset, _) in EnumerateFields(cls))
            {
                if (offset > 0 && !list.Any(x => x.Name == name))
                    list.Add((name, offset));
            }
            try { cls = GetParent(cls); } catch { break; }
            depth++;
        }
        return list;
    }

    // ==================== 字段句柄 / 静态字段 ====================

    private const int FieldAttributeStatic = 0x10;

    /// <summary>字段句柄（名称 + 句柄指针 + 偏移 + 是否静态）</summary>
    internal readonly record struct Il2CppFieldHandle(string Name, IntPtr Field, int Offset, bool IsStatic);

    /// <summary>枚举单个类的字段句柄（含 offset 与 static 标记，不含父类）</summary>
    internal static List<Il2CppFieldHandle> EnumerateFieldHandles(IntPtr classPtr)
    {
        var list = new List<Il2CppFieldHandle>();
        if (classPtr == IntPtr.Zero) return list;
        try
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr field;
            while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(classPtr, ref iter)) != IntPtr.Zero)
            {
                string? name = PtrToString(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                if (string.IsNullOrEmpty(name)) continue;
                int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);
                bool isStatic = (Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_flags(field) & FieldAttributeStatic) != 0;
                list.Add(new Il2CppFieldHandle(name, field, offset, isStatic));
            }
        }
        catch { }
        return list;
    }

    /// <summary>单例字段命名惯例（原 EntityDestroyer 内联的候选名）</summary>
    internal static bool IsInstanceFieldName(string name)
        => name is "s_instance" or "_instance" or "Instance" or "instance";

    /// <summary>读取静态字段的值（按指针宽度）</summary>
    internal static unsafe IntPtr ReadStaticFieldValue(IntPtr field)
    {
        IntPtr value = IntPtr.Zero;
        try { Il2CppInterop.Runtime.IL2CPP.il2cpp_field_static_get_value(field, &value); } catch { }
        return value;
    }

    // ==================== 原生 API 门面（业务层唯一允许的访问点） ====================
    // 约定：业务代码不得出现 Il2CppInterop.Runtime.IL2CPP.* 的直接调用，
    // 一律经由下列封装（保持与原 API 同签名，便于机械替换、零行为变化）。

    internal static IntPtr GetMethodFromName(IntPtr cls, string name, int paramCount)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(cls, name, paramCount);

    internal static IntPtr ClassGetName(IntPtr classPtr)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(classPtr);

    internal static IntPtr TypeGetName(IntPtr typePtr)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(typePtr);

    internal static IntPtr MethodGetName(IntPtr methodPtr)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(methodPtr);

    internal static IntPtr FieldGetName(IntPtr fieldPtr)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(fieldPtr);

    internal static IntPtr ClassGetMethods(IntPtr cls, ref IntPtr iter)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(cls, ref iter);

    internal static IntPtr ClassGetFields(IntPtr cls, ref IntPtr iter)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(cls, ref iter);

    internal static IntPtr FieldGetType(IntPtr field)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(field);

    internal static int FieldGetOffset(IntPtr field)
        => (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);

    internal static int FieldGetFlags(IntPtr field)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_flags(field);

    internal static unsafe void FieldStaticGetValue(IntPtr field, void* value)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_field_static_get_value(field, value);

    internal static unsafe IntPtr RuntimeInvoke(IntPtr method, IntPtr objPtr, void** args, ref IntPtr exception)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(method, objPtr, args, ref exception);

    internal static uint GetMethodParamCountRaw(IntPtr method)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(method);

    internal static IntPtr GetMethodParam(IntPtr method, uint index)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(method, index);

    internal static uint MethodGetFlags(IntPtr method, ref uint iflags)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_flags(method, ref iflags);

    internal static IntPtr DomainGet()
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_domain_get();

    internal static unsafe IntPtr* DomainGetAssemblies(IntPtr domain, ref uint count)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);

    internal static IntPtr AssemblyGetImage(IntPtr assembly)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_assembly_get_image(assembly);

    internal static IntPtr ClassFromName(IntPtr image, string ns, string className)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_class_from_name(image, ns, className);

    /// <summary>
    /// 分配未初始化的 IL2CPP 对象（等价 C# new，字段全 0）。调用后必须再 Invoke 其 .ctor。
    /// </summary>
    internal static IntPtr ObjectNew(IntPtr klass)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_object_new(klass);

    /// <summary>Il2CppTypeEnum：0x12=CLASS, 0x15=GENERICINST, 0x14=ARRAY, 0x1d=SZARRAY</summary>
    internal static int TypeGetType(IntPtr type)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_type(type);

    /// <summary>
    /// 托管字符串 → IL2CPP 字符串对象（生命周期归 il2cpp GC，无需手动释放）。
    /// 供 Invoke 的 string 参数封送与静态字段写入（SaveLoadPatches 同款机制，这里收进门面）。
    /// ⚠ 必须配合 <see cref="GcHandleNew"/> 使用：新字符串只被 .NET 托管数组（参数槽）引用，
    ///   而 il2cpp 的 Boehm GC **不扫描 .NET 托管堆** —— 被调方法内部一旦分配触发 GC，
    ///   这个字符串就会被回收，游戏若把它存进字段（如 Npc.npc_name）就留下悬垂指针，
    ///   之后任何访问（UI 悬停显示名字）都是裸 AV。
    /// </summary>
    internal static IntPtr StringToIl2Cpp(string s)
        => Il2CppInterop.Runtime.IL2CPP.ManagedStringToIl2Cpp(s);

    /// <summary>
    /// 给 IL2CPP 对象登记 GC 根（il2cpp 侧可见），返回 handle；用完必须 GcHandleFree。
    /// 用于跨 runtime_invoke 边界保住临时对象（字符串参数等）不被 il2cpp GC 回收。
    /// </summary>
    internal static IntPtr GcHandleNew(IntPtr obj, bool pinned)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_new(obj, pinned);

    /// <summary>释放 GcHandleNew 登记的 GC 根。</summary>
    internal static void GcHandleFree(IntPtr handle)
        => Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_free(handle);
}
