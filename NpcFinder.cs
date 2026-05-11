using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// NPC 查找器 - 按组件类名查找 NPC 实体（Npc/NpcBody/NpcDeadBody）
/// </summary>
internal static class NpcFinder
{
    // 扫描结果缓存
    private static readonly List<NpcInfo> _npcs = new();

    // 匹配的组件类名关键词
    private static readonly string[] NpcClassKeywords = { "Npc" };

    // IL2CPP API 缓存
    private static bool _apiCached;
    private static MethodInfo? _il2cpp_get_class;
    private static MethodInfo? _il2cpp_field_get_offset;
    private static MethodInfo? _il2cpp_class_get_fields;
    private static MethodInfo? _il2cpp_class_get_parent;
    private static MethodInfo? _il2cpp_field_get_name;
    private static MethodInfo? _il2cpp_field_get_type;
    private static MethodInfo? _il2cpp_type_get_name;

    // 组件类字段偏移缓存
    private static readonly Dictionary<string, Dictionary<string, FieldEntry>> _classFieldCache = new();

    private static readonly HashSet<string> FloatTypeNames = new() { "System.Single", "float" };
    private static readonly HashSet<string> StringTypeNames = new() { "System.String", "String", "Il2CppSystem.String" };
    private static readonly HashSet<string> IntTypeNames = new() { "System.Int32", "int", "System.Int64", "long", "System.Boolean", "bool", "System.Byte", "byte", "System.Int16", "short", "System.UInt32", "System.UInt64", "System.UInt16", "System.SByte", "System.IntPtr" };

    internal class NpcInfo
    {
        public string GoName = "";
        public string ClassName = "";
        public string NpcName = "";
        public int HometownKingdomId;
        public IntPtr Ptr;
        public int PtrHash;
        public int Guid;
        public int StuffId;
        public Dictionary<string, FieldEntry> FieldMeta = new(); // offset/type only, no values
    }

    internal class FieldEntry
    {
        public int Offset;
        public string TypeName = "";
        public bool IsFloat;
        public bool IsString;
        public bool IsPointer;
    }

    private static void CacheIl2CppApi()
    {
        if (_apiCached) return;
        _apiCached = true;

        var il2cppType = typeof(Il2CppInterop.Runtime.IL2CPP);
        _il2cpp_get_class = il2cppType.GetMethod("il2cpp_object_get_class", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_offset = il2cppType.GetMethod("il2cpp_field_get_offset", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_class_get_fields = il2cppType.GetMethod("il2cpp_class_get_fields", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_class_get_parent = il2cppType.GetMethod("il2cpp_class_get_parent", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_name = il2cppType.GetMethod("il2cpp_field_get_name", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_field_get_type = il2cppType.GetMethod("il2cpp_field_get_type", BindingFlags.Static | BindingFlags.Public);
        _il2cpp_type_get_name = il2cppType.GetMethod("il2cpp_type_get_name", BindingFlags.Static | BindingFlags.Public);
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

    private static Dictionary<string, FieldEntry> GetOrCacheClassFields(IntPtr compClass, string className)
    {
        if (_classFieldCache.TryGetValue(className, out var cached))
            return cached;

        var fieldMap = new Dictionary<string, FieldEntry>();
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
                    fieldMap[name] = new FieldEntry
                    {
                        Offset = offset,
                        TypeName = typeName,
                        IsFloat = FloatTypeNames.Contains(typeName),
                        IsString = StringTypeNames.Contains(typeName),
                        IsPointer = !FloatTypeNames.Contains(typeName) && !StringTypeNames.Contains(typeName) && !IntTypeNames.Contains(typeName)
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
    /// 扫描所有含 Npc 关键词的组件实体
    /// </summary>
    internal static void ScanNpcs()
    {
        _npcs.Clear();
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[NpcFinder] 开始扫描...");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.LogInfo($"[NpcFinder] 共 {allGOs.Length} 个 GameObject");

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

                        // 检查类名是否包含 Npc 关键词
                        bool isNpc = false;
                        foreach (var kw in NpcClassKeywords)
                        {
                            if (className.Contains(kw)) { isNpc = true; break; }
                        }
                        if (!isNpc) continue;

                        // 获取全部字段元数据（不读值）
                        var fieldMap = GetOrCacheClassFields(compClass, className);

                        // 只读 guid、stuff_id、npc_name、hometown_kingdom_id
                        int guid = 0, stuffId = 0, hometownKingdomId = 0;
                        string npcName = "";
                        if (fieldMap.TryGetValue("guid", out var guidFe))
                            try { guid = ReadIl2CppInt(compPtr, guidFe.Offset); } catch { }
                        if (fieldMap.TryGetValue("stuff_id", out var sidFe))
                            try { stuffId = ReadIl2CppInt(compPtr, sidFe.Offset); } catch { }
                        if (fieldMap.TryGetValue("npc_name", out var nameFe) && nameFe.IsString)
                            try { npcName = ReadIl2CppString(compPtr, nameFe.Offset) ?? ""; } catch { }
                        if (fieldMap.TryGetValue("hometown_kingdom_id", out var hkFe))
                        {
                            try { hometownKingdomId = ReadIl2CppInt(compPtr, hkFe.Offset); } catch { }
                            if (found < 5)
                                Plugin.LogInfo($"[NpcFinder]   hometown_kingdom_id offset={hkFe.Offset} value={hometownKingdomId} className={className}");
                        }
                        else if (found < 5)
                        {
                            Plugin.LogInfo($"[NpcFinder]   hometown_kingdom_id NOT FOUND in fieldMap (className={className}, fields={string.Join(",", fieldMap.Keys)})");
                        }

                        var npc = new NpcInfo
                        {
                            GoName = go.name,
                            ClassName = className,
                            NpcName = npcName,
                            HometownKingdomId = hometownKingdomId,
                            Ptr = compPtr,
                            PtrHash = compPtr.GetHashCode(),
                            Guid = guid,
                            StuffId = stuffId,
                            FieldMeta = fieldMap
                        };

                        _npcs.Add(npc);
                        found++;
                        break; // 每个 GO 只取第一个 Npc 组件
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[NpcFinder] 完成, 找到 {found} 个 NPC 实体");

            for (int i = 0; i < Math.Min(20, _npcs.Count); i++)
            {
                var e = _npcs[i];
                Plugin.LogInfo($"[NpcFinder]   {e.GoName} [{e.ClassName}] guid={e.Guid} stuffId={e.StuffId} hometownKingdomId={e.HometownKingdomId} hasHkField={e.FieldMeta.ContainsKey("hometown_kingdom_id")} fields={e.FieldMeta.Count}");
            }
        }
        catch (Exception ex) { Plugin.LogError($"[NpcFinder] 异常: {ex.Message}\n{ex.StackTrace}"); }
    }

    internal static string GetNpcsJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var e in _npcs)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('{');
            sb.Append($"\"goName\":\"{Escape(e.GoName)}\",");
            sb.Append($"\"className\":\"{Escape(e.ClassName)}\",");
            sb.Append($"\"npcName\":\"{Escape(e.NpcName)}\",");
            sb.Append($"\"hometownKingdomId\":{e.HometownKingdomId},");
            sb.Append($"\"ptrHash\":{e.PtrHash},");
            sb.Append($"\"guid\":{e.Guid},");
            sb.Append($"\"stuffId\":{e.StuffId},");
            sb.Append($"\"fieldCount\":{e.FieldMeta.Count}");
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// 按需读取单个实体的全部字段值
    /// </summary>
    internal static string GetNpcFieldsJson(int ptrHash)
    {
        foreach (var e in _npcs)
        {
            if (e.PtrHash != ptrHash) continue;

            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in e.FieldMeta)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"\"{Escape(kv.Key)}\":{{");
                sb.Append($"\"isFloat\":{(kv.Value.IsFloat ? "true" : "false")},");
                sb.Append($"\"isString\":{(kv.Value.IsString ? "true" : "false")},");
                sb.Append($"\"isPointer\":{(kv.Value.IsPointer ? "true" : "false")},");
                sb.Append($"\"typeName\":\"{Escape(kv.Value.TypeName)}\",");
                sb.Append("\"value\":");
                try
                {
                    if (kv.Value.IsPointer)
                    {
                        sb.Append("\"object\"");
                    }
                    else if (kv.Value.IsString)
                    {
                        string? sv = ReadIl2CppString(e.Ptr, kv.Value.Offset);
                        sb.Append($"\"{Escape(sv ?? "")}\"");
                    }
                    else if (kv.Value.IsFloat)
                    {
                        float v = ReadIl2CppFloat(e.Ptr, kv.Value.Offset);
                        sb.Append(v.ToString("G"));
                    }
                    else
                    {
                        int v = ReadIl2CppInt(e.Ptr, kv.Value.Offset);
                        sb.Append(v);
                    }
                }
                catch { sb.Append(kv.Value.IsPointer ? "\"object\"" : (kv.Value.IsString ? "\"\"" : "0")); }
                sb.Append("}");
            }
            sb.Append('}');
            return sb.ToString();
        }
        return "{\"error\":\"not found\"}";
    }

    internal static string SetNpcField(int ptrHash, string fieldName, float value)
    {
        try
        {
            foreach (var e in _npcs)
            {
                if (e.PtrHash != ptrHash) continue;
                if (!e.FieldMeta.TryGetValue(fieldName, out var fe))
                    return $"unknown field: {fieldName}";

                if (fe.IsFloat)
                    WriteIl2CppFloat(e.Ptr, fe.Offset, value);
                else
                    WriteIl2CppInt(e.Ptr, fe.Offset, (int)value);

                Plugin.LogInfo($"[NpcFinder] SetNpcField ptrHash={ptrHash}, {fieldName}={value}");
                return "ok";
            }
            return $"npc not found: ptrHash={ptrHash}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("<", "\\u003c").Replace(">", "\\u003e");

    private static unsafe int ReadIl2CppInt(IntPtr objPtr, int offset) => *(int*)(objPtr + offset);
    private static unsafe float ReadIl2CppFloat(IntPtr objPtr, int offset) => *(float*)(objPtr + offset);
    private static unsafe void WriteIl2CppInt(IntPtr objPtr, int offset, int value) => *(int*)(objPtr + offset) = value;
    private static unsafe void WriteIl2CppFloat(IntPtr objPtr, int offset, float value) => *(float*)(objPtr + offset) = value;

    private static unsafe string? ReadIl2CppString(IntPtr objPtr, int offset)
    {
        try
        {
            IntPtr strPtr = *(IntPtr*)(objPtr + offset);
            if (strPtr == IntPtr.Zero) return null;
            // IL2CPP string: chars start at offset 0x14 (20 bytes) from the string object pointer
            IntPtr charsPtr = strPtr + 0x14;
            return Marshal.PtrToStringUni(charsPtr);
        }
        catch { return null; }
    }
}
