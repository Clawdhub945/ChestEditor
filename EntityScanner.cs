using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// 通用实体扫描器 - 扫描所有含 stuff_id 字段的实体，支持 CRUD
/// </summary>
internal static class EntityScanner
{
    // 扫描结果缓存
    private static readonly List<EntityInfo> _entities = new();

    // IL2CPP API 缓存
    private static bool _apiCached;
    private static MethodInfo? _il2cpp_get_class;
    private static MethodInfo? _il2cpp_field_get_offset;
    private static MethodInfo? _il2cpp_class_get_fields;
    private static MethodInfo? _il2cpp_class_get_parent;
    private static MethodInfo? _il2cpp_field_get_name;
    private static MethodInfo? _il2cpp_field_get_type;
    private static MethodInfo? _il2cpp_type_get_name;

    // 组件类字段偏移缓存: className -> { fieldName -> (offset, typeName, isFloat) }
    private static readonly Dictionary<string, Dictionary<string, FieldInfo>> _classFieldCache = new();

    // 浮点类型名集合
    private static readonly HashSet<string> FloatTypeNames = new() { "System.Single", "float" };

    internal class EntityInfo
    {
        public string GoName = "";
        public string ClassName = "";
        public IntPtr Ptr;
        public int Guid;
        public int StuffId;
        public Dictionary<string, FieldInfo> Fields = new();
    }

    internal class FieldInfo
    {
        public int Offset;
        public string TypeName = "";
        public bool IsFloat;
        public int IntVal;
        public float FloatVal;
    }

    private static void CacheIl2CppApi()
    {
        if (_apiCached) return;
        _apiCached = true;

        var il2cppType = typeof(Il2CppInterop.Runtime.IL2CPP);
        _il2cpp_get_class = il2cppType.GetMethod("il2cpp_object_get_class",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_offset = il2cppType.GetMethod("il2cpp_field_get_offset",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_class_get_fields = il2cppType.GetMethod("il2cpp_class_get_fields",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_class_get_parent = il2cppType.GetMethod("il2cpp_class_get_parent",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_name = il2cppType.GetMethod("il2cpp_field_get_name",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_type = il2cppType.GetMethod("il2cpp_field_get_type",
            BindingFlags.Static | BindingFlags.Public);
        _il2cpp_type_get_name = il2cppType.GetMethod("il2cpp_type_get_name",
            BindingFlags.Static | BindingFlags.Public);
    }

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

    private static string? PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringAnsi(ptr); } catch { return null; }
    }

    private static List<(string Name, int Offset, string TypeName)> GetIl2CppFields(IntPtr classPtr)
    {
        var fields = new List<(string, int, string)>();
        try
        {
            if (_il2cpp_class_get_fields == null || _il2cpp_field_get_name == null || _il2cpp_field_get_offset == null)
                return fields;

            IntPtr iter = IntPtr.Zero;
            object[] args = new object[] { classPtr, iter };

            while (true)
            {
                object? result = _il2cpp_class_get_fields.Invoke(null, args);
                if (result == null) break;
                IntPtr field = (IntPtr)result;
                if (field == IntPtr.Zero) break;

                IntPtr namePtr = (IntPtr)_il2cpp_field_get_name.Invoke(null, new object[] { field })!;
                string? name = PtrToString(namePtr);
                if (string.IsNullOrEmpty(name))
                {
                    args[1] = field;
                    continue;
                }

                object offsetResult = _il2cpp_field_get_offset.Invoke(null, new object[] { field })!;
                int offset = offsetResult is uint u ? (int)u : Convert.ToInt32(offsetResult);

                string typeName = "";
                if (_il2cpp_field_get_type != null && _il2cpp_type_get_name != null)
                {
                    try
                    {
                        IntPtr typePtr = (IntPtr)_il2cpp_field_get_type.Invoke(null, new object[] { field })!;
                        if (typePtr != IntPtr.Zero)
                            typeName = PtrToString((IntPtr)_il2cpp_type_get_name.Invoke(null, new object[] { typePtr })!) ?? "";
                    }
                    catch { }
                }

                fields.Add((name, offset, typeName));
                args[1] = field;
            }
        }
        catch { }
        return fields;
    }

    /// <summary>
    /// 获取或缓存组件类的全部字段（递归父类）
    /// </summary>
    private static Dictionary<string, FieldInfo> GetOrCacheClassFields(IntPtr compClass, string className)
    {
        if (_classFieldCache.TryGetValue(className, out var cached))
            return cached;

        var fieldMap = new Dictionary<string, FieldInfo>();
        var seen = new HashSet<int>();
        IntPtr cls = compClass;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < 15)
        {
            int addr = cls.GetHashCode();
            if (seen.Contains(addr)) break;
            seen.Add(addr);

            foreach (var (name, offset, typeName) in GetIl2CppFields(cls))
            {
                if (offset > 0 && !fieldMap.ContainsKey(name))
                {
                    fieldMap[name] = new FieldInfo
                    {
                        Offset = offset,
                        TypeName = typeName,
                        IsFloat = FloatTypeNames.Contains(typeName)
                    };
                }
            }

            try { cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls); } catch { break; }
            depth++;
        }

        _classFieldCache[className] = fieldMap;
        return fieldMap;
    }

    /// <summary>
    /// 扫描所有含 stuff_id 字段的实体
    /// </summary>
    internal static void ScanEntities()
    {
        _entities.Clear();
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[EntityScanner] 开始扫描...");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.LogInfo($"[EntityScanner] 共 {allGOs.Length} 个 GameObject");

            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { compPtr })!; }
                        catch { continue; }
                        if (compClass == IntPtr.Zero) continue;

                        string className = comp.GetIl2CppType().Name;

                        // 获取该组件类的全部字段
                        var fieldMap = GetOrCacheClassFields(compClass, className);

                        // 检查是否有 stuff_id 字段
                        if (!fieldMap.TryGetValue("stuff_id", out var stuffIdField)) continue;

                        // 读取 stuff_id
                        int stuffId = 0;
                        try { stuffId = ReadIl2CppInt(compPtr, stuffIdField.Offset); } catch { }

                        // 跳过 stuff_id 为 0 的模板
                        if (stuffId <= 0) continue;

                        // 读取 guid
                        int guid = 0;
                        if (fieldMap.TryGetValue("guid", out var guidField))
                            try { guid = ReadIl2CppInt(compPtr, guidField.Offset); } catch { }

                        // 跳过无 guid 的模板
                        if (guid <= 0) continue;

                        // 读取所有字段值
                        var fields = new Dictionary<string, FieldInfo>();
                        foreach (var kv in fieldMap)
                        {
                            var fi = new FieldInfo
                            {
                                Offset = kv.Value.Offset,
                                TypeName = kv.Value.TypeName,
                                IsFloat = kv.Value.IsFloat
                            };
                            try
                            {
                                if (fi.IsFloat)
                                    fi.FloatVal = ReadIl2CppFloat(compPtr, fi.Offset);
                                else
                                    fi.IntVal = ReadIl2CppInt(compPtr, fi.Offset);
                            }
                            catch { }
                            fields[kv.Key] = fi;
                        }

                        var entity = new EntityInfo
                        {
                            GoName = go.name,
                            ClassName = className,
                            Ptr = compPtr,
                            Guid = guid,
                            StuffId = stuffId,
                            Fields = fields
                        };

                        _entities.Add(entity);
                        found++;
                        break; // 每个 GO 只取第一个含 stuff_id 的组件
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[EntityScanner] 完成, 找到 {found} 个实体");

            // 按 stuff_id 排序
            _entities.Sort((a, b) => a.StuffId.CompareTo(b.StuffId));

            // 打印前 20 个
            for (int i = 0; i < Math.Min(20, _entities.Count); i++)
            {
                var e = _entities[i];
                Plugin.LogInfo($"[EntityScanner]   {e.GoName} [{e.ClassName}] stuffId={e.StuffId} guid={e.Guid} fields={e.Fields.Count}");
            }
        }
        catch (Exception ex) { Plugin.LogError($"[EntityScanner] 异常: {ex.Message}\n{ex.StackTrace}"); }
    }

    /// <summary>
    /// 获取实体扫描结果 JSON
    /// </summary>
    internal static string GetEntitiesJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var e in _entities)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('{');
            sb.Append($"\"goName\":\"{Escape(e.GoName)}\",");
            sb.Append($"\"className\":\"{Escape(e.ClassName)}\",");
            sb.Append($"\"guid\":{e.Guid},");
            sb.Append($"\"stuffId\":{e.StuffId},");
            sb.Append($"\"name\":\"{Escape(StuffIdNames.GetName(e.StuffId))}\",");
            sb.Append("\"fields\":{");
            bool f2 = true;
            foreach (var kv in e.Fields)
            {
                if (!f2) sb.Append(',');
                f2 = false;
                sb.Append($"\"{Escape(kv.Key)}\":");
                if (kv.Value.IsFloat)
                    sb.Append(kv.Value.FloatVal.ToString("G"));
                else
                    sb.Append(kv.Value.IntVal);
            }
            sb.Append("}}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// 设置实体字段值
    /// </summary>
    internal static string SetEntityField(int guid, string fieldName, float value)
    {
        try
        {
            // 先在缓存中查找
            foreach (var e in _entities)
            {
                if (e.Guid != guid) continue;
                if (!e.Fields.TryGetValue(fieldName, out var fi))
                    return $"unknown field: {fieldName}";

                // 写入值
                if (fi.IsFloat)
                    WriteIl2CppFloat(e.Ptr, fi.Offset, value);
                else
                    WriteIl2CppInt(e.Ptr, fi.Offset, (int)value);

                // 更新缓存
                if (fi.IsFloat)
                    fi.FloatVal = value;
                else
                    fi.IntVal = (int)value;

                Plugin.LogInfo($"[EntityScanner] SetEntityField guid={guid}, {fieldName}={value}");
                return "ok";
            }

            // 缓存中没有，重新扫描查找
            CacheIl2CppApi();
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { compPtr })!; }
                        catch { continue; }
                        if (compClass == IntPtr.Zero) continue;

                        string className = comp.GetIl2CppType().Name;
                        var fieldMap = GetOrCacheClassFields(compClass, className);

                        if (!fieldMap.TryGetValue("guid", out var guidField)) continue;

                        int entityGuid = 0;
                        try { entityGuid = ReadIl2CppInt(compPtr, guidField.Offset); } catch { }
                        if (entityGuid != guid) continue;

                        if (!fieldMap.TryGetValue(fieldName, out var targetField))
                            return $"unknown field: {fieldName}";

                        if (targetField.IsFloat)
                            WriteIl2CppFloat(compPtr, targetField.Offset, value);
                        else
                            WriteIl2CppInt(compPtr, targetField.Offset, (int)value);

                        Plugin.LogInfo($"[EntityScanner] SetEntityField (rescan) guid={guid}, {fieldName}={value}");
                        return "ok";
                    }
                }
                catch { }
            }

            return $"entity not found: guid={guid}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("<", "\\u003c").Replace(">", "\\u003e");

    private static unsafe int ReadIl2CppInt(IntPtr objPtr, int offset)
    {
        try { return *(int*)(objPtr + offset); } catch { return 0; }
    }

    private static unsafe float ReadIl2CppFloat(IntPtr objPtr, int offset)
    {
        try { return *(float*)(objPtr + offset); } catch { return 0; }
    }

    private static unsafe void WriteIl2CppInt(IntPtr objPtr, int offset, int value)
    {
        *(int*)(objPtr + offset) = value;
    }

    private static unsafe void WriteIl2CppFloat(IntPtr objPtr, int offset, float value)
    {
        *(float*)(objPtr + offset) = value;
    }
}
