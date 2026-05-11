using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// 统一实体编辑器 - 合并实体扫描(含stuff_id)和NPC查找(类名含Npc)，精简字段显示
/// </summary>
internal static class EntityEditor
{
    private static readonly List<EditorEntity> _entities = new();

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

    // NPC 类名关键词
    private static readonly string[] NpcClassKeywords = { "Npc" };

    internal class EditorEntity
    {
        public string GoName = "";
        public string ClassName = "";
        public string NpcName = "";
        public int HometownKingdomId;
        public IntPtr Ptr;
        public int PtrHash;
        public int Guid;
        public int StuffId;
        public Dictionary<string, FieldEntry> FieldMeta = new();
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
    /// 统一扫描：实体扫描(stuff_id) + NPC查找(类名含Npc)，按ptrHash去重
    /// </summary>
    internal static void ScanAll()
    {
        _entities.Clear();
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[EntityEditor] 开始统一扫描...");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.LogInfo($"[EntityEditor] 共 {allGOs.Length} 个 GameObject");

            var seenPtrHash = new HashSet<int>();
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

                        int ptrHash = compPtr.GetHashCode();
                        if (seenPtrHash.Contains(ptrHash)) continue;

                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { compPtr })!; }
                        catch { continue; }
                        if (compClass == IntPtr.Zero) continue;

                        string className = comp.GetIl2CppType().Name;
                        var fieldMap = GetOrCacheClassFields(compClass, className);

                        // 判断是否匹配：有 stuff_id 字段 且 stuffId>0,guid>0  OR  类名含 Npc
                        bool hasStuffId = fieldMap.ContainsKey("stuff_id");
                        bool isNpc = className == "Npc";

                        if (!hasStuffId && !isNpc) continue;

                        // 读取基础字段
                        int guid = 0, stuffId = 0, hometownKingdomId = 0;
                        string npcName = "";

                        if (hasStuffId)
                        {
                            try { stuffId = ReadIl2CppInt(compPtr, fieldMap["stuff_id"].Offset); } catch { }
                            if (stuffId <= 0 && !isNpc) continue; // stuff_id 为 0 且不是 NPC，跳过
                        }

                        if (fieldMap.TryGetValue("guid", out var guidFe))
                            try { guid = ReadIl2CppInt(compPtr, guidFe.Offset); } catch { }
                        if (guid <= 0 && !isNpc) continue; // 无 guid 且不是 NPC，跳过

                        if (isNpc)
                        {
                            if (fieldMap.TryGetValue("npc_name", out var nameFe) && nameFe.IsString)
                                try { npcName = ReadIl2CppString(compPtr, nameFe.Offset) ?? ""; } catch { }
                            if (fieldMap.TryGetValue("hometown_kingdom_id", out var hkFe))
                                try { hometownKingdomId = ReadIl2CppInt(compPtr, hkFe.Offset); } catch { }
                        }

                        seenPtrHash.Add(ptrHash);

                        var entity = new EditorEntity
                        {
                            GoName = go.name,
                            ClassName = className,
                            NpcName = npcName,
                            HometownKingdomId = hometownKingdomId,
                            Ptr = compPtr,
                            PtrHash = ptrHash,
                            Guid = guid,
                            StuffId = stuffId,
                            FieldMeta = fieldMap
                        };

                        _entities.Add(entity);
                        found++;
                        break; // 每个 GO 只取第一个匹配组件
                    }
                }
                catch { }
            }

            // 按 className 排序
            _entities.Sort((a, b) =>
            {
                int cmp = string.Compare(a.ClassName, b.ClassName, StringComparison.Ordinal);
                return cmp != 0 ? cmp : a.StuffId.CompareTo(b.StuffId);
            });

            Plugin.LogInfo($"[EntityEditor] 完成, 找到 {found} 个实体");
            for (int i = 0; i < Math.Min(20, _entities.Count); i++)
            {
                var e = _entities[i];
                Plugin.LogInfo($"[EntityEditor]   {e.GoName} [{e.ClassName}] guid={e.Guid} stuffId={e.StuffId} npcName={e.NpcName} fields={e.FieldMeta.Count}");
            }
        }
        catch (Exception ex) { Plugin.LogError($"[EntityEditor] 异常: {ex.Message}\n{ex.StackTrace}"); }
    }

    /// <summary>
    /// 返回实体列表 JSON（精简字段数）
    /// </summary>
    internal static string GetAllJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var e in _entities)
        {
            if (!first) sb.Append(',');
            first = false;

            // 计算精简字段数（排除pointer）
            int slimCount = 0;
            foreach (var kv in e.FieldMeta)
                if (!kv.Value.IsPointer) slimCount++;

            sb.Append('{');
            sb.Append($"\"goName\":\"{Escape(e.GoName)}\",");
            sb.Append($"\"className\":\"{Escape(e.ClassName)}\",");
            sb.Append($"\"npcName\":\"{Escape(e.NpcName)}\",");
            sb.Append($"\"hometownKingdomId\":{e.HometownKingdomId},");
            sb.Append($"\"ptrHash\":{e.PtrHash},");
            sb.Append($"\"guid\":{e.Guid},");
            sb.Append($"\"stuffId\":{e.StuffId},");
            sb.Append($"\"name\":\"{Escape(StuffIdNames.GetName(e.StuffId))}\",");
            sb.Append($"\"fieldCount\":{slimCount}");
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// 返回单个实体的精简字段值（排除pointer字段）
    /// </summary>
    internal static string GetFieldsJson(int ptrHash)
    {
        foreach (var e in _entities)
        {
            if (e.PtrHash != ptrHash) continue;

            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in e.FieldMeta)
            {
                if (kv.Value.IsPointer) continue; // 跳过pointer字段

                if (!first) sb.Append(',');
                first = false;
                sb.Append($"\"{Escape(kv.Key)}\":{{");
                sb.Append($"\"isFloat\":{(kv.Value.IsFloat ? "true" : "false")},");
                sb.Append($"\"isString\":{(kv.Value.IsString ? "true" : "false")},");
                sb.Append($"\"typeName\":\"{Escape(kv.Value.TypeName)}\",");
                sb.Append("\"value\":");
                try
                {
                    if (kv.Value.IsString)
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
                catch { sb.Append(kv.Value.IsString ? "\"\"" : "0"); }
                sb.Append("}");
            }
            sb.Append('}');
            return sb.ToString();
        }
        return "{\"error\":\"not found\"}";
    }

    /// <summary>
    /// 设置实体字段值
    /// </summary>
    internal static string SetField(int ptrHash, string fieldName, float value)
    {
        try
        {
            foreach (var e in _entities)
            {
                if (e.PtrHash != ptrHash) continue;
                if (!e.FieldMeta.TryGetValue(fieldName, out var fe))
                    return $"unknown field: {fieldName}";
                if (fe.IsPointer)
                    return "cannot edit pointer field";

                if (fe.IsFloat)
                    WriteIl2CppFloat(e.Ptr, fe.Offset, value);
                else
                    WriteIl2CppInt(e.Ptr, fe.Offset, (int)value);

                Plugin.LogInfo($"[EntityEditor] SetField ptrHash={ptrHash}, {fieldName}={value}");
                return "ok";
            }
            return $"entity not found: ptrHash={ptrHash}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>
    /// 消除实体（销毁对应的 GameObject）
    /// </summary>
    internal static string DestroyEntity(int ptrHash)
    {
        try
        {
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.PtrHash != ptrHash) continue;

                var obj = Il2CppInterop.Runtime.IL2CPP.PointerToObject(e.Ptr);
                if (obj == null) return "failed to resolve object";
                var unityObj = obj.TryCast<UnityEngine.Object>();
                if (unityObj == null) return "not a Unity object";
                string name = unityObj.name;
                UnityEngine.Object.Destroy(unityObj);
                _entities.RemoveAt(i);
                Plugin.LogInfo($"[EntityEditor] DestroyEntity ptrHash={ptrHash} name={name}");
                return "ok";
            }
            return $"entity not found: ptrHash={ptrHash}";
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
            IntPtr charsPtr = strPtr + 0x14;
            return Marshal.PtrToStringUni(charsPtr);
        }
        catch { return null; }
    }
}
