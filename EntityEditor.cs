using System;
using System.Collections.Generic;
using System.Linq;
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

    // soldier_type_id → 名称
    private static readonly Dictionary<int, string> SoldierTypeNames = new()
    {
        { 0, "市民" }, { 202999, "市民" },
        { 202501, "刀盾兵" }, { 202502, "巨盾兵" },
        { 202401, "剑士" }, { 202405, "长枪兵" }, { 202403, "钝器兵" },
        { 202303, "投石兵" }, { 202301, "弓箭兵" }, { 202302, "弩箭兵" }, { 202304, "火枪手" },
        { 202601, "弓骑兵" },
        { 202101, "骑士" }, { 202105, "皇家骑士" }, { 202103, "猪骑兵" }, { 202102, "狼骑兵" }, { 202104, "领主" },
        { 202201, "马战车" }, { 202203, "猪战车" }, { 202202, "狼战车" },
        { 202407, "图腾兵" }, { 202402, "大刀兵" }, { 202503, "后勤兵" }, { 202999, "民兵" },
        { 202883, "赏金猎人" }, { 202884, "刺客" },
        { 202899, "雇佣刀盾兵" }, { 202898, "雇佣巨盾兵" }, { 202897, "雇佣剑士" },
        { 202896, "雇佣长枪兵" }, { 202895, "雇佣钝器兵" }, { 202894, "雇佣投石兵" },
        { 202893, "雇佣弓箭兵" }, { 202892, "雇佣弩箭兵" }, { 202891, "雇佣弓骑兵" },
        { 202890, "雇佣马骑兵" }, { 202889, "雇佣猪骑兵" }, { 202888, "雇佣狼骑兵" },
        { 202887, "雇佣马战车" }, { 202886, "雇佣猪战车" }, { 202885, "雇佣狼战车" },
        { 202701, "红精灵法师" }, { 202702, "蓝精灵法师" }, { 202703, "绿精灵法师" }, { 202704, "三眼法师" },
        { 202404, "狼战士" },
    };

    internal static string GetSoldierTypeName(int id) => SoldierTypeNames.TryGetValue(id, out var name) ? name : "";

    internal class EditorEntity
    {
        public string GoName = "";
        public string ClassName = "";
        public string NpcName = "";
        public int HometownKingdomId;
        public int TerritoryKingdomId;
        public string StuffNameWithIdIndex = "";
        public int SoldierTypeId;
        public IntPtr Ptr;
        public int PtrHash;
        public int Guid;
        public int StuffId;
        public GameObject? GoRef;
        public Component? CompRef;
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
                        bool isNpc = className.Contains("Npc") && !className.Contains("NpcHelper") && !className.Contains("NpcTask") && !className.Contains("NpcFinder");

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

                        // 读取 stuff_name_with_id_index（Facility 类的显示名称）
                        // 尝试调用 GetFacilityNameWithIdIndex() 方法触发懒加载并获取结果
                        string stuffNameWithIdIndex = "";
                        try
                        {
                            // 先尝试直接读字段（可能已被缓存）
                            if (fieldMap.TryGetValue("stuff_name_with_id_index", out var snFe) && snFe.IsString)
                                stuffNameWithIdIndex = ReadIl2CppString(compPtr, snFe.Offset) ?? "";
                            // 如果字段为空，尝试调用方法触发计算
                            if (string.IsNullOrEmpty(stuffNameWithIdIndex))
                            {
                                IntPtr fnCls = compClass;
                                int fnD = 0;
                                while (fnCls != IntPtr.Zero && fnD < 10)
                                {
                                    IntPtr fnIter = IntPtr.Zero;
                                    IntPtr fnM;
                                    while ((fnM = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(fnCls, ref fnIter)) != IntPtr.Zero)
                                    {
                                        string? fnName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(fnM));
                                        if (fnName == "GetFacilityNameWithIdIndex" && Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(fnM) == 0)
                                        {
                                            IntPtr exFn = IntPtr.Zero;
                                            try
                                            {
                                                IntPtr fnResult = IntPtr.Zero;
                                                unsafe { fnResult = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(fnM, compPtr, null, ref exFn); }
                                                if (fnResult != IntPtr.Zero)
                                                {
                                                    // 读取返回的 IL2CPP string
                                                    unsafe
                                                    {
                                                        IntPtr charsPtr = fnResult + 0x14;
                                                        stuffNameWithIdIndex = Marshal.PtrToStringUni(charsPtr) ?? "";
                                                    }
                                                }
                                            }
                                            catch { }
                                            break;
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(stuffNameWithIdIndex)) break;
                                    fnCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(fnCls);
                                    fnD++;
                                }
                            }
                        }
                        catch { }

                        // 读取 soldier_type_id
                        int soldierTypeId = 0;
                        if (fieldMap.TryGetValue("soldier_type_id", out var stFe))
                            try { soldierTypeId = ReadIl2CppInt(compPtr, stFe.Offset); } catch { }

                        seenPtrHash.Add(ptrHash);

                        var entity = new EditorEntity
                        {
                            GoName = go.name,
                            ClassName = className,
                            NpcName = npcName,
                            HometownKingdomId = hometownKingdomId,
                            StuffNameWithIdIndex = stuffNameWithIdIndex,
                            SoldierTypeId = soldierTypeId,
                            Ptr = compPtr,
                            PtrHash = ptrHash,
                            Guid = guid,
                            StuffId = stuffId,
                            GoRef = go,
                            CompRef = comp,
                            FieldMeta = fieldMap
                        };

                        // 读取实体所属 territory 的 kingdom_id
                        try
                        {
                            IntPtr territoryPtr = ReadFieldSafe(compPtr, compClass, "territory");
                            if (territoryPtr == IntPtr.Zero)
                            {
                                // 递归搜索父类
                                IntPtr tCls = compClass;
                                int tD = 0;
                                while (tCls != IntPtr.Zero && tD < 10)
                                {
                                    IntPtr fi = IntPtr.Zero;
                                    IntPtr f;
                                    while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(tCls, ref fi)) != IntPtr.Zero)
                                    {
                                        string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                                        if (fn == "territory" || fn == "_territory")
                                        {
                                            int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                                            if (offset >= 0x10 && offset < 0x10000)
                                                unsafe { territoryPtr = *(IntPtr*)(compPtr + offset); }
                                            break;
                                        }
                                    }
                                    if (territoryPtr != IntPtr.Zero) break;
                                    tCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(tCls);
                                    tD++;
                                }
                            }
                            if (territoryPtr != IntPtr.Zero)
                            {
                                IntPtr tClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(territoryPtr);
                                entity.TerritoryKingdomId = ReadIntFieldSafe(territoryPtr, tClass, "kingdom_id", 0);
                            }
                        }
                        catch { }

                        // NPC 的 hometownKingdomId 优先，否则用 territory 的 kingdom_id
                        if (entity.HometownKingdomId == 0 && entity.TerritoryKingdomId != 0)
                            entity.HometownKingdomId = entity.TerritoryKingdomId;

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
            sb.Append($"\"stuffNameWithIdIndex\":\"{Escape(e.StuffNameWithIdIndex)}\",");
            sb.Append($"\"soldierTypeId\":{e.SoldierTypeId},");
            sb.Append($"\"soldierTypeName\":\"{Escape(GetSoldierTypeName(e.SoldierTypeId))}\",");
            sb.Append($"\"hometownKingdomId\":{e.HometownKingdomId},");
            sb.Append($"\"territoryKingdomId\":{e.TerritoryKingdomId},");
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
    /// 获取实体的世界坐标 JSON
    /// </summary>
    internal static string GetEntityPositionJson(int ptrHash)
    {
        foreach (var e in _entities)
        {
            if (e.PtrHash != ptrHash) continue;
            if (e.GoRef == null) return "{\"error\":\"no GameObject\"}";
            try
            {
                var pos = e.GoRef.transform.position;
                return $"{{\"x\":{pos.x},\"y\":{pos.y}}}";
            }
            catch (Exception ex) { return $"{{\"error\":\"{Escape(ex.Message)}\"}}"; }
        }
        return "{\"error\":\"entity not found\"}";
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
    /// 列出实体组件类的所有方法（调试用）
    /// </summary>
    internal static string ListMethods(int ptrHash)
    {
        foreach (var e in _entities)
        {
            if (e.PtrHash != ptrHash) continue;
            try
            {
                var sb = new System.Text.StringBuilder();
                IntPtr cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(e.Ptr);
                sb.AppendLine($"ptr={e.Ptr} classPtr={cls}");
                if (cls == IntPtr.Zero) return "classPtr is null";
                string? cn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(cls));
                sb.AppendLine($"className={cn}");
                int depth = 0;
                while (cls != IntPtr.Zero && depth < 10)
                {
                    string? className = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(cls));
                    sb.AppendLine($"=== {className} (depth={depth}) ===");
                    IntPtr iter = IntPtr.Zero;
                    int count = 0;
                    IntPtr m;
                    while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(cls, ref iter)) != IntPtr.Zero)
                    {
                        string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m));
                        if (mName != null) { sb.AppendLine($"  {mName}"); count++; }
                    }
                    sb.AppendLine($"  (total: {count})");
                    cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls);
                    depth++;
                }
                return sb.ToString();
            }
            catch (Exception ex) { return ex.Message; }
        }
        return "not found";
    }

    /// <summary>
    /// 消除实体：调用游戏的 LeaveMapAndDestroy / DestroyGo / OnDead
    /// </summary>
    internal static string DestroyEntity(int ptrHash)
    {
        try
        {
            Plugin.LogInfo($"[EntityEditor] DestroyEntity called, ptrHash={ptrHash}, entities.Count={_entities.Count}");
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.PtrHash != ptrHash) continue;

                Plugin.LogInfo($"[EntityEditor] Matched entity: {e.GoName} class={e.ClassName} ptrHash={e.PtrHash}");

                if (e.GoRef == null)
                    return "GameObject reference lost (rescan needed)";

                string name = e.GoRef.name;
                IntPtr classPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(e.Ptr);
                string className = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(classPtr)) ?? "?";
                Plugin.LogInfo($"[EntityEditor] === DestroyEntity START: {name} class={className} ptrHash={ptrHash} ===");

                bool called = false;

                // === 第一优先：NPC 类型用 LeaveMapAndDestroy ===
                // LeaveMapAndDestroy 是虚方法，il2cpp_class_get_method_from_name 搜不到
                // 需要用 il2cpp_class_get_methods 枚举
                if (className.Contains("Npc") && !className.Contains("NpcHelper"))
                {
                    string[] targetNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead", "LeaveMap", "OnLeaveMap" };
                    IntPtr searchCls = classPtr;
                    int depth = 0;
                    while (searchCls != IntPtr.Zero && depth < 15 && !called)
                    {
                        string clsName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(searchCls)) ?? "?";
                        IntPtr iter = IntPtr.Zero;
                        IntPtr mth;
                        while ((mth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(searchCls, ref iter)) != IntPtr.Zero)
                        {
                            string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(mth));
                            if (mName == null) continue;
                            foreach (var target in targetNames)
                            {
                                if (mName == target)
                                {
                                    uint pCount = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(mth);
                                    Plugin.LogInfo($"[EntityEditor] [NPC] Found {mName}({pCount}p) at depth={depth} cls={clsName}");
                                    try
                                    {
                                        IntPtr exception = IntPtr.Zero;
                                        if (pCount == 0)
                                        {
                                            unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mth, e.Ptr, null, ref exception); }
                                        }
                                        else
                                        {
                                            // 有参数的方法，传默认值
                                            IntPtr[] argPtrs = new IntPtr[pCount];
                                            IntPtr[] storage = new IntPtr[pCount];
                                            for (int a = 0; a < (int)pCount; a++)
                                            {
                                                IntPtr paramType = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(mth, (uint)a);
                                                string? tn = paramType != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(paramType)) : null;
                                                bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                                storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                            }
                                            unsafe
                                            {
                                                fixed (IntPtr* storPtr = storage)
                                                {
                                                    for (int a = 0; a < (int)pCount; a++)
                                                        argPtrs[a] = (IntPtr)(&storPtr[a]);
                                                    fixed (IntPtr* argsArr = argPtrs)
                                                    {
                                                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mth, e.Ptr, (void**)argsArr, ref exception);
                                                    }
                                                }
                                            }
                                        }
                                        if (exception != IntPtr.Zero)
                                            Plugin.LogInfo($"[EntityEditor] {mName}() exception on {name}");
                                        else
                                        {
                                            Plugin.LogInfo($"[EntityEditor] ✓ Called {mName}() on {name} (depth={depth})");
                                            called = true;
                                            break;
                                        }
                                    }
                                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mName}() CRASH: {ex.Message}"); }
                                }
                            }
                            if (called) break;
                        }
                        searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                        depth++;
                    }
                }

                // === 第二优先：Facility 类型用 BuildHelper.RemoveFacility ===
                // IDA 伪代码显示 BuildHelper__Dismantle 的核心流程是:
                //   BuildHelper__RemoveFacility(this, f)
                //   f->vtable._85_AfterDismantle(f, WagonOutPos, rotation, is_destroy, method)
                if (!called && IsFacilityClass(className))
                {
                    Plugin.LogInfo($"[EntityEditor] [Facility] Looking for BuildHelper via Territory...");
                    // IDA: territory->fields.build_helper -> BuildHelper__RemoveFacility(buildHelper, facility)
                    // build_helper 不是单例，是 Territory 的实例字段
                    IntPtr buildHelperInst = FindBuildHelperFromTerritory();
                    Plugin.LogInfo($"[EntityEditor] BuildHelper instance={buildHelperInst.ToInt64():X}");
                    if (buildHelperInst != IntPtr.Zero)
                    {
                        // 手动执行 Dismantle 的关键步骤（避免直接调用 Dismantle 导致崩溃）
                        // IDA 流程: Facility.CloseWindow -> SetJobCount(0) -> RemoveFacility -> AfterDismantle
                        IntPtr bhClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(buildHelperInst);

                        // Step 1: Facility.CloseWindow(f)
                        {
                            IntPtr closeWindowMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(classPtr, "CloseWindow", 0);
                            if (closeWindowMth == IntPtr.Zero)
                            {
                                IntPtr searchCls = classPtr;
                                int d = 0;
                                while (searchCls != IntPtr.Zero && d < 10 && closeWindowMth == IntPtr.Zero)
                                {
                                    closeWindowMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchCls, "CloseWindow", 0);
                                    searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                                    d++;
                                }
                            }
                            if (closeWindowMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr ex = IntPtr.Zero;
                                    unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(closeWindowMth, e.Ptr, null, ref ex); }
                                    Plugin.LogInfo($"[EntityEditor] Facility.CloseWindow() done, ex={ex != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] CloseWindow failed: {ex.Message}"); }
                            }
                        }

                        // Step 2: 尝试 Facility.Dismantle(6p) — 之前测试过不崩溃
                        {
                            IntPtr disMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(classPtr, "Dismantle", 6);
                            if (disMth == IntPtr.Zero)
                            {
                                IntPtr searchCls = classPtr;
                                int d = 0;
                                while (searchCls != IntPtr.Zero && d < 10 && disMth == IntPtr.Zero)
                                {
                                    disMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchCls, "Dismantle", 6);
                                    searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                                    d++;
                                }
                            }
                            if (disMth != IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] Found Facility.Dismantle(6p), calling...");
                                try
                                {
                                    IntPtr exception = IntPtr.Zero;
                                    int pc = 6;
                                    IntPtr[] argPtrs = new IntPtr[pc];
                                    IntPtr[] storage = new IntPtr[pc];
                                    for (int a = 0; a < pc; a++)
                                    {
                                        IntPtr paramType = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(disMth, (uint)a);
                                        string? tn = paramType != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(paramType)) : null;
                                        bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                        storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                    }
                                    unsafe
                                    {
                                        fixed (IntPtr* storPtr = storage)
                                        {
                                            for (int a = 0; a < pc; a++)
                                                argPtrs[a] = (IntPtr)(&storPtr[a]);
                                            fixed (IntPtr* argsArr = argPtrs)
                                            {
                                                Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(disMth, e.Ptr, (void**)argsArr, ref exception);
                                            }
                                        }
                                    }
                                    Plugin.LogInfo($"[EntityEditor] Facility.Dismantle(6p) done, exception={exception != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Facility.Dismantle(6p) failed: {ex.Message}"); }
                            }
                        }

                        // Step 3: 从 BuildHelper 的 facility 列表中移除
                        // 遍历 BuildHelper 的所有 List<Facility> 字段，移除目标 Facility
                        {
                            IntPtr fi = IntPtr.Zero;
                            IntPtr f;
                            while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(bhClass, ref fi)) != IntPtr.Zero)
                            {
                                string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                                IntPtr ft = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(f);
                                string? ftn = ft != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(ft)) : null;
                                if (fn == null || ftn == null) continue;
                                if (!ftn.Contains("List<Facility") && !ftn.Contains("List<Facility")) continue;

                                int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                                IntPtr listPtr;
                                unsafe { listPtr = *(IntPtr*)(buildHelperInst + offset); }
                                if (listPtr == IntPtr.Zero) continue;

                                // 尝试调用 List.Remove(item)
                                IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(listPtr);
                                IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "Remove", 1);
                                if (removeMth != IntPtr.Zero)
                                {
                                    try
                                    {
                                        IntPtr ex = IntPtr.Zero;
                                        IntPtr[] args = new IntPtr[1];
                                        IntPtr[] stor = new IntPtr[1];
                                        stor[0] = e.Ptr;
                                        unsafe
                                        {
                                            fixed (IntPtr* sp = stor)
                                            {
                                                args[0] = (IntPtr)(&sp[0]);
                                                fixed (IntPtr* ap = args)
                                                {
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, listPtr, (void**)ap, ref ex);
                                                }
                                            }
                                        }
                                        Plugin.LogInfo($"[EntityEditor] BH.{fn}.Remove(facility) done, ex={ex != IntPtr.Zero}");
                                    }
                                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] BH.{fn}.Remove failed: {ex.Message}"); }
                                }
                            }
                        }

                        // Step 4: RecycleMySp 清理渲染资源
                        {
                            IntPtr rCls = classPtr;
                            int rDepth = 0;
                            while (rCls != IntPtr.Zero && rDepth < 10)
                            {
                                IntPtr rMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(rCls, "RecycleMySp", 0);
                                if (rMth != IntPtr.Zero)
                                {
                                    try
                                    {
                                        IntPtr ex = IntPtr.Zero;
                                        unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(rMth, e.Ptr, null, ref ex); }
                                        Plugin.LogInfo($"[EntityEditor] RecycleMySp() done, ex={ex != IntPtr.Zero}");
                                    }
                                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] RecycleMySp failed: {ex.Message}"); }
                                    break;
                                }
                                rCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(rCls);
                                rDepth++;
                            }
                        }

                        // Step 5: 隐藏 + 销毁 GameObject
                        try
                        {
                            e.GoRef.SetActive(false);
                            UnityEngine.Object.DestroyImmediate(e.GoRef);
                            Plugin.LogInfo($"[EntityEditor] DestroyImmediate done");
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] DestroyImmediate failed: {ex.Message}"); }

                        Plugin.LogInfo($"[EntityEditor] Manual dismantle steps completed");
                        called = true;
                    }
                        else
                            Plugin.LogInfo($"[EntityEditor] Could not get BuildHelper instance");
                    }




                // === Ship专用处理 ===
                // 从船对象自身读取所属 territory（可能是敌方 territory），而非玩家 territory
                if (!called && (className.Contains("Ship") || className.Contains("BattleUnit") || className.Contains("Soldier")))
                {
                    Plugin.LogInfo($"[EntityEditor] [Ship] Ship destroy start...");
                    bool shipDestroyed = false;

                    int guid = 0;
                    if (e.FieldMeta.TryGetValue("guid", out var guidFe))
                        try { guid = ReadIl2CppInt(e.Ptr, guidFe.Offset); } catch { }
                    Plugin.LogInfo($"[EntityEditor] [Ship] entity guid={guid}");

                    // 从船对象自身读取 territory 字段（Ship 继承自 MyMonoBehaviour，有 territory 引用）
                    IntPtr shipTerritoryPtr = ReadFieldSafe(e.Ptr, classPtr, "territory");
                    if (shipTerritoryPtr == IntPtr.Zero)
                    {
                        // 递归搜索父类找 territory 字段
                        IntPtr searchCls = classPtr;
                        int depth = 0;
                        while (searchCls != IntPtr.Zero && depth < 10)
                        {
                            IntPtr fi = IntPtr.Zero;
                            IntPtr f;
                            while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(searchCls, ref fi)) != IntPtr.Zero)
                            {
                                string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                                if (fn == "territory" || fn == "_territory")
                                {
                                    int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                                    if (offset >= 0x10 && offset < 0x10000)
                                        unsafe { shipTerritoryPtr = *(IntPtr*)(e.Ptr + offset); }
                                    if (shipTerritoryPtr != IntPtr.Zero) break;
                                }
                            }
                            if (shipTerritoryPtr != IntPtr.Zero) break;
                            searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                            depth++;
                        }
                    }

                    if (shipTerritoryPtr != IntPtr.Zero)
                    {
                        IntPtr shipTerritoryClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(shipTerritoryPtr);
                        string? territoryName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(shipTerritoryClass));
                        Plugin.LogInfo($"[EntityEditor] [Ship] Ship's territory class={territoryName}, ptr={shipTerritoryPtr.ToInt64():X}");

                        // 从船的所属 territory 读取 ship_list 和 ship_dic
                        IntPtr shipListPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_list");
                        IntPtr shipDicPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_dic");
                        Plugin.LogInfo($"[EntityEditor] [Ship] ship_list={shipListPtr.ToInt64():X}, ship_dic={shipDicPtr.ToInt64():X}");

                        // 按指针从 ship_list 中找到并移除
                        if (shipListPtr != IntPtr.Zero)
                        {
                            Plugin.LogInfo($"[EntityEditor] [Ship] Removing from ship_list by pointer match...");
                            shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                            Plugin.LogInfo($"[EntityEditor] [Ship] RemoveFromListByPtr result={shipDestroyed}");
                        }

                        // 从 ship_dic 按 guid 移除
                        if (shipDicPtr != IntPtr.Zero && guid != 0)
                        {
                            IntPtr dicClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(shipDicPtr);
                            IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dicClass, "Remove", 1);
                            if (removeMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exRm = IntPtr.Zero;
                                    unsafe
                                    {
                                        int guidArg = guid;
                                        IntPtr* rmArgs = stackalloc IntPtr[1];
                                        rmArgs[0] = (IntPtr)(&guidArg);
                                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, shipDicPtr, (void**)rmArgs, ref exRm);
                                    }
                                    Plugin.LogInfo($"[EntityEditor] [Ship] ship_dic.Remove({guid}) ex={exRm != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [Ship] ship_dic.Remove failed: {ex.Message}"); }
                            }
                        }

                        // 调用 ShipHelper.DestroyShip 做完整清理
                        IntPtr shipHelperPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_helper");
                        if (shipHelperPtr != IntPtr.Zero)
                        {
                            IntPtr shClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(shipHelperPtr);
                            IntPtr destroyShipMth = IntPtr.Zero;
                            {
                                IntPtr shIter = IntPtr.Zero;
                                IntPtr shM;
                                while ((shM = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(shClass, ref shIter)) != IntPtr.Zero)
                                {
                                    string? shName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(shM));
                                    if (shName == "DestroyShip") { destroyShipMth = shM; break; }
                                }
                            }
                            if (destroyShipMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exDestroy = IntPtr.Zero;
                                    unsafe
                                    {
                                        int boolArg = 0;
                                        IntPtr* destroyArgs = stackalloc IntPtr[2];
                                        destroyArgs[0] = e.Ptr;
                                        destroyArgs[1] = (IntPtr)(&boolArg);
                                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(destroyShipMth, shipHelperPtr, (void**)destroyArgs, ref exDestroy);
                                    }
                                    Plugin.LogInfo($"[EntityEditor] [Ship] DestroyShip call ex={exDestroy != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [Ship] DestroyShip failed: {ex.Message}"); }
                            }
                            else
                            {
                                CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                                CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                                CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                            }
                        }
                        else
                        {
                            CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                            CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                            CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                        }

                        // 最终验证
                        if (shipListPtr != IntPtr.Zero)
                        {
                            IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(shipListPtr);
                            int finalSize = ReadIntFieldSafe(shipListPtr, listClass, "_size", -1);
                            Plugin.LogInfo($"[EntityEditor] [Ship] Final: ship_list._size={finalSize}");
                        }
                    }
                    else
                    {
                        Plugin.LogInfo($"[EntityEditor] [Ship] Could not find territory field on ship, trying FindTerritory fallback...");
                        // 回退到 FindTerritory
                        IntPtr territoryPtr = FindTerritory();
                        if (territoryPtr != IntPtr.Zero)
                        {
                            IntPtr tc = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(territoryPtr);
                            IntPtr shipListPtr = ReadFieldSafe(territoryPtr, tc, "ship_list");
                            if (shipListPtr != IntPtr.Zero)
                                shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                        }
                        CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                    }

                    // 兜底清理
                    if (!shipDestroyed)
                    {
                        Plugin.LogInfo($"[EntityEditor] [Ship] Fallback cleanup...");
                        CallVoidMethod(classPtr, e.Ptr, "UnIndexUnitByPos", 0);
                        CallVoidMethod(classPtr, e.Ptr, "BeforeDestroy", 0);
                        // is_dead
                        {
                            IntPtr idCls = classPtr;
                            int idD = 0;
                            while (idCls != IntPtr.Zero && idD < 10)
                            {
                                IntPtr fi = IntPtr.Zero;
                                IntPtr f;
                                while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(idCls, ref fi)) != IntPtr.Zero)
                                {
                                    string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                                    if (fn == "is_dead")
                                    {
                                        int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                                        if (offset >= 0x10 && offset < 0x10000)
                                            unsafe { *(int*)(e.Ptr + offset) = 1; }
                                        break;
                                    }
                                }
                                idCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(idCls);
                                idD++;
                            }
                        }
                        try { e.GoRef.SetActive(false); } catch { }
                    }
                    called = true;
                }
                // === 第三优先：通用销毁方法（非NPC/非Facility） ===
                // 动物、船、掉落物等：先在自身类链搜索虚方法，再搜索管理器类
                if (!called && !className.Contains("Npc") && !IsFacilityClass(className))
                {
                    // 3a: 在实体自身的类继承链中搜索销毁相关虚方法
                    string[] targetNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead", "LeaveMap", "OnLeaveMap", "Despawn", "RecycleSelf", "Dead", "Die", "OnDie", "OnDestroy", "BeforeRecycle", "Leave", "DeadOnBattle", "BeSeckill" };
                    IntPtr searchCls = classPtr;
                    int depth = 0;
                    // 先记录找到的方法名用于日志
                    var foundMethods = new List<(string name, uint paramCount, IntPtr methodPtr, int d, string cls)>();
                    while (searchCls != IntPtr.Zero && depth < 15)
                    {
                        string clsName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(searchCls)) ?? "?";
                        IntPtr iter = IntPtr.Zero;
                        IntPtr mth;
                        while ((mth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(searchCls, ref iter)) != IntPtr.Zero)
                        {
                            string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(mth));
                            if (mName == null) continue;
                            uint pc = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(mth);
                            foreach (var target in targetNames)
                            {
                                if (mName == target)
                                {
                                    foundMethods.Add((mName, pc, mth, depth, clsName));
                                    break;
                                }
                            }
                        }
                        searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                        depth++;
                    }

                    Plugin.LogInfo($"[EntityEditor] [Generic] {className}: found {foundMethods.Count} target methods in class hierarchy");
                    foreach (var fm in foundMethods)
                        Plugin.LogInfo($"[EntityEditor] [Generic]   {fm.cls}.{fm.name}({fm.paramCount}p) depth={fm.d}");

                    // 尝试调用找到的方法（优先 0 参数的）
                    foreach (var fm in foundMethods.OrderBy(f => f.paramCount))
                    {
                        if (called) break;
                        try
                        {
                            IntPtr exception = IntPtr.Zero;
                            if (fm.paramCount == 0)
                            {
                                unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(fm.methodPtr, e.Ptr, null, ref exception); }
                            }
                            else
                            {
                                IntPtr[] argPtrs = new IntPtr[fm.paramCount];
                                IntPtr[] storage = new IntPtr[fm.paramCount];
                                for (int a = 0; a < (int)fm.paramCount; a++)
                                {
                                    IntPtr paramType = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(fm.methodPtr, (uint)a);
                                    string? tn = paramType != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(paramType)) : null;
                                    bool isBool = tn != null && (tn == "System.Boolean" || tn == "bool");
                                    storage[a] = isBool ? (IntPtr)1 : IntPtr.Zero;
                                }
                                unsafe
                                {
                                    fixed (IntPtr* storPtr = storage)
                                    {
                                        for (int a = 0; a < (int)fm.paramCount; a++)
                                            argPtrs[a] = (IntPtr)(&storPtr[a]);
                                        fixed (IntPtr* argsArr = argPtrs)
                                        {
                                            Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(fm.methodPtr, e.Ptr, (void**)argsArr, ref exception);
                                        }
                                    }
                                }
                            }
                            if (exception != IntPtr.Zero)
                                Plugin.LogInfo($"[EntityEditor] {fm.name}() exception on {name}");
                            else
                            {
                                Plugin.LogInfo($"[EntityEditor] ✓ Called {fm.name}() on {name} (depth={fm.d})");
                                called = true;
                            }
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {fm.name}() CRASH: {ex.Message}"); }
                    }

                    // 3b: 如果自身类没有找到，列出所有方法（0-2参数）用于调试
                    if (!called)
                    {
                        Plugin.LogInfo($"[EntityEditor] [Generic] Listing ALL methods (0-2p) on {className} hierarchy for debugging:");
                        IntPtr debugCls = classPtr;
                        int debugDepth = 0;
                        int methodCount = 0;
                        while (debugCls != IntPtr.Zero && debugDepth < 10)
                        {
                            string dClsName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(debugCls)) ?? "?";
                            IntPtr dIter = IntPtr.Zero;
                            IntPtr dMth;
                            while ((dMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(debugCls, ref dIter)) != IntPtr.Zero)
                            {
                                string? dName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(dMth));
                                if (dName == null) continue;
                                uint dPc = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(dMth);
                                if (dPc <= 2)
                                {
                                    uint dIflags = 0;
                                    uint dFlags = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_flags(dMth, ref dIflags);
                                    bool dStatic = (dFlags & 0x10) != 0;
                                    Plugin.LogInfo($"[EntityEditor] [Generic]   {dClsName}.{dName}({dPc}p) static={dStatic} depth={debugDepth}");
                                    methodCount++;
                                    if (methodCount >= 80) break;
                                }
                            }
                            if (methodCount >= 80) break;
                            debugCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(debugCls);
                            debugDepth++;
                        }
                        Plugin.LogInfo($"[EntityEditor] [Generic] Total: {methodCount} methods listed");

                        // 3c: StuffOnMap 专用：手动执行完整删除流程
                        if (!called && (className.Contains("StuffOnMap") || className.Contains("DropItem")))
                        {
                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Manual destroy (field enumeration)...");
                            IntPtr mapStuffHelper = FindMapStuffHelper();
                            bool dicRemoved = false;
                            if (mapStuffHelper != IntPtr.Zero)
                            {
                                IntPtr mshClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mapStuffHelper);
                                IntPtr mshAreaMap = ReadFieldSafe(mapStuffHelper, mshClass, "area_map");
                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] MapStuffHelper.area_map={mshAreaMap.ToInt64():X}");
                                if (mshAreaMap != IntPtr.Zero)
                                {
                                    IntPtr areaMapClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mshAreaMap);

                                    // 读取 guid
                                    int guid = 0;
                                    if (e.FieldMeta.TryGetValue("guid", out var guidFe))
                                        try { guid = ReadIl2CppInt(e.Ptr, guidFe.Offset); } catch { }
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] entity guid={guid}");

                                    // Step A: 从 stuff_on_map_dic 移除
                                    IntPtr dicPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_dic");
                                    if (dicPtr != IntPtr.Zero && guid != 0)
                                    {
                                        IntPtr dicClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(dicPtr);
                                        IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dicClass, "Remove", 1);
                                        if (removeMth != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                IntPtr exRm = IntPtr.Zero;
                                                unsafe
                                                {
                                                    int guidArg = guid;
                                                    IntPtr* rmArgs = stackalloc IntPtr[1];
                                                    rmArgs[0] = (IntPtr)(&guidArg);
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, dicPtr, (void**)rmArgs, ref exRm);
                                                }
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic.Remove({guid}) ex={exRm != IntPtr.Zero}");
                                            }
                                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic.Remove failed: {ex.Message}"); }
                                        }
                                    }

                                    // Step B: 从 stuff_on_map_dic_by_pos 移除
                                    // 通过 IL2CPP 字段枚举找到 _pos_point 字段（在父类 Item3dClickable 上）
                                    IntPtr dicByPosPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_dic_by_pos");
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] stuff_on_map_dic_by_pos={dicByPosPtr.ToInt64():X}");
                                    if (dicByPosPtr != IntPtr.Zero)
                                    {
                                        int posPointOffset = -1;
                                        IntPtr posSearchCls = classPtr;
                                        int posSearchD = 0;
                                        while (posSearchCls != IntPtr.Zero && posSearchD < 10)
                                        {
                                            IntPtr fi = IntPtr.Zero;
                                            IntPtr f;
                                            while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(posSearchCls, ref fi)) != IntPtr.Zero)
                                            {
                                                string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                                                if (fn == "_pos_point")
                                                {
                                                    int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                                                    if (offset >= 0x10 && offset < 0x10000)
                                                    {
                                                        posPointOffset = offset;
                                                        Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Found _pos_point at offset=0x{offset:X} on {Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(posSearchCls))}");
                                                    }
                                                    break;
                                                }
                                            }
                                            if (posPointOffset >= 0) break;
                                            posSearchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(posSearchCls);
                                            posSearchD++;
                                        }

                                        if (posPointOffset >= 0)
                                        {
                                            int px = 0, py = 0;
                                            unsafe
                                            {
                                                px = *(int*)(e.Ptr + posPointOffset);
                                                py = *(int*)(e.Ptr + posPointOffset + 4);
                                            }
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] _pos_point=({px},{py})");

                                            // MyListDic.Remove(Point, item) — 枚举找虚方法
                                            IntPtr dicByPosClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(dicByPosPtr);
                                            IntPtr removeMth = IntPtr.Zero;
                                            {
                                                IntPtr rIter = IntPtr.Zero;
                                                IntPtr rM;
                                                while ((rM = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(dicByPosClass, ref rIter)) != IntPtr.Zero)
                                                {
                                                    string? rName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(rM));
                                                    uint rPc = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(rM);
                                                    if (rName == "Remove" && rPc == 2) { removeMth = rM; break; }
                                                }
                                            }
                                            if (removeMth != IntPtr.Zero)
                                            {
                                                try
                                                {
                                                    IntPtr exRm = IntPtr.Zero;
                                                    unsafe
                                                    {
                                                        int* pointData = stackalloc int[2];
                                                        pointData[0] = px;
                                                        pointData[1] = py;
                                                        IntPtr* rmArgs = stackalloc IntPtr[2];
                                                        rmArgs[0] = (IntPtr)pointData;
                                                        rmArgs[1] = e.Ptr;
                                                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, dicByPosPtr, (void**)rmArgs, ref exRm);
                                                    }
                                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic_by_pos.Remove(({px},{py}), entity) ex={exRm != IntPtr.Zero}");
                                                    dicRemoved = true;
                                                }
                                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] dic_by_pos.Remove failed: {ex.Message}"); }
                                            }
                                            else
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] MyListDic.Remove(2p) not found");
                                        }
                                        else
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] _pos_point field not found in class hierarchy");
                                    }

                                    // Step C: 从 stuff_on_map_list 移除
                                    IntPtr listPtr = ReadFieldSafe(mshAreaMap, areaMapClass, "stuff_on_map_list");
                                    if (listPtr != IntPtr.Zero)
                                    {
                                        IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(listPtr);
                                        IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "Remove", 1);
                                        if (removeMth != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                IntPtr exRm = IntPtr.Zero;
                                                unsafe
                                                {
                                                    IntPtr* rmArgs = stackalloc IntPtr[1];
                                                    rmArgs[0] = e.Ptr;
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, listPtr, (void**)rmArgs, ref exRm);
                                                }
                                                Plugin.LogInfo($"[EntityEditor] [StuffOnMap] list.Remove(entity) ex={exRm != IntPtr.Zero}");
                                            }
                                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] list.Remove failed: {ex.Message}"); }
                                        }
                                    }
                                }
                            }

                            // Step D: 设置 is_dead 标记
                            if (e.FieldMeta.TryGetValue("is_dead", out var isDeadFe))
                            {
                                try { WriteIl2CppInt(e.Ptr, isDeadFe.Offset, 1); Plugin.LogInfo($"[EntityEditor] [StuffOnMap] Set is_dead=1"); } catch { }
                            }

                            // Step E: 隐藏实体 — 优先用 RecycleToCache，但如果字典移除可能失败则用 GameObject.SetActive(false)
                            // RecycleToCache 在状态不一致时可能触发 native crash，所以只在字典移除成功后调用
                            if (dicRemoved)
                            {
                                IntPtr rtCls = classPtr;
                                int rtD = 0;
                                while (rtCls != IntPtr.Zero && rtD < 10)
                                {
                                    IntPtr rtMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(rtCls, "RecycleToCache", 0);
                                    if (rtMth != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr exRt = IntPtr.Zero;
                                            unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(rtMth, e.Ptr, null, ref exRt); }
                                            Plugin.LogInfo($"[EntityEditor] [StuffOnMap] RecycleToCache() ex={exRt != IntPtr.Zero}");
                                        }
                                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [StuffOnMap] RecycleToCache failed: {ex.Message}"); }
                                        break;
                                    }
                                    rtCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(rtCls);
                                    rtD++;
                                }
                            }
                            else
                            {
                                // 字典移除可能失败，只做视觉隐藏
                                try
                                {
                                    e.GoRef.SetActive(false);
                                    Plugin.LogInfo($"[EntityEditor] [StuffOnMap] SetActive(false) — dic removal may have failed");
                                }
                                catch { }
                            }
                            called = true;
                        }

                        // 3d: 尝试通过其他管理器类删除
                        if (!called)
                            Plugin.LogInfo($"[EntityEditor] [Generic] No destroy method on {className}, trying manager classes...");
                        // 根据类名推断可能的管理器
                        string[] mgrCandidates;
                        if (className.Contains("Animal"))
                            mgrCandidates = new[] { "AnimalHelper", "NpcHelper", "AnimalManager", "AnimalCtrl" };
                        else if (className.Contains("Ship"))
                            mgrCandidates = new[] { "ShipHelper", "ShipManager", "VehicleHelper", "VehicleManager" };
                        else if (className.Contains("StuffOnMap") || className.Contains("DropItem"))
                            mgrCandidates = new[] { "MapStuffHelper", "StuffOnMapHelper", "DropItemHelper", "StuffOnMapManager", "MapItemHelper", "StuffManager", "StuffHelper", "ItemDropHelper" };
                        else
                            mgrCandidates = new[] { "NpcHelper", "EntityManager", "PrefabManager" };

                        foreach (var mgrName in mgrCandidates)
                        {
                            if (called) break;
                            IntPtr mgrClass = FindClassByName(mgrName);
                            if (mgrClass == IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] [Generic] Manager class {mgrName} not found");
                                continue;
                            }
                            IntPtr mgrInst = FindClassInstance(mgrClass);
                            if (mgrInst == IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] [Generic] Manager {mgrName} instance not found");
                                continue;
                            }
                            Plugin.LogInfo($"[EntityEditor] [Generic] Found manager {mgrName}, searching remove methods...");

                            // 枚举管理器的方法，查找包含 Remove/Despawn/Kill/Delete 的方法
                            string[] removeKeywords = { "Remove", "Despawn", "Delete", "Kill", "Destroy", "Clear", "Release", "Recycle" };
                            IntPtr mIter = IntPtr.Zero;
                            IntPtr mMth;
                            while ((mMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(mgrClass, ref mIter)) != IntPtr.Zero)
                            {
                                string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(mMth));
                                if (mName == null) continue;
                                bool matches = false;
                                foreach (var kw in removeKeywords)
                                {
                                    if (mName.Contains(kw, StringComparison.OrdinalIgnoreCase)) { matches = true; break; }
                                }
                                if (!matches) continue;

                                uint mPc = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(mMth);
                                // 获取参数类型信息
                                string paramInfo = "";
                                for (int pi = 0; pi < (int)mPc; pi++)
                                {
                                    IntPtr pt = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(mMth, (uint)pi);
                                    string? ptn = pt != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(pt)) : "?";
                                    if (pi > 0) paramInfo += ", ";
                                    paramInfo += ptn;
                                }
                                // 检查是否是静态方法 (METHOD_ATTRIBUTE_STATIC = 0x10)
                                uint iflags = 0;
                                uint methodFlags = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_flags(mMth, ref iflags);
                                bool isStatic = (methodFlags & 0x10) != 0;
                                Plugin.LogInfo($"[EntityEditor] [Generic]   {mgrName}.{mName}({mPc}p) params=({paramInfo}) static={isStatic}");

                                if (mPc != 1) continue; // 只尝试 1 参数的（传入实体指针）

                                // 获取参数类型名，检查是否兼容
                                IntPtr paramType = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param(mMth, 0);
                                string? paramTypeName = paramType != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(paramType)) : null;
                                Plugin.LogInfo($"[EntityEditor] [Generic]   param0 type={paramTypeName}, entity class={className}");

                                try
                                {
                                    IntPtr exception = IntPtr.Zero;
                                    if (isStatic)
                                    {
                                        // 静态方法：不传 this，只传参数
                                        IntPtr[] argPtrs = new IntPtr[1];
                                        IntPtr[] storage = new IntPtr[1];
                                        storage[0] = e.Ptr;
                                        unsafe
                                        {
                                            fixed (IntPtr* storPtr = storage)
                                            {
                                                argPtrs[0] = (IntPtr)(&storPtr[0]);
                                                fixed (IntPtr* argsArr = argPtrs)
                                                {
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mMth, IntPtr.Zero, (void**)argsArr, ref exception);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // 实例方法
                                        IntPtr[] argPtrs = new IntPtr[1];
                                        IntPtr[] storage = new IntPtr[1];
                                        storage[0] = e.Ptr;
                                        unsafe
                                        {
                                            fixed (IntPtr* storPtr = storage)
                                            {
                                                argPtrs[0] = (IntPtr)(&storPtr[0]);
                                                fixed (IntPtr* argsArr = argPtrs)
                                                {
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mMth, mgrInst, (void**)argsArr, ref exception);
                                                }
                                            }
                                        }
                                    }
                                    if (exception == IntPtr.Zero)
                                    {
                                        Plugin.LogInfo($"[EntityEditor] ✓ Called {mgrName}.{mName}(entity) on {name}");
                                        called = true;
                                    }
                                    else
                                        Plugin.LogInfo($"[EntityEditor] {mgrName}.{mName}(entity) IL2CPP exception (ptr={exception.ToInt64():X})");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mgrName}.{mName}(entity) CRASH: {ex.Message}"); }
                                if (called) break;
                            }
                        }
                    }
                }

                // === 第四优先：在其他管理类上搜索 Remove/Del 方法 ===
                if (!called && IsFacilityClass(className))
                {
                    Plugin.LogInfo($"[EntityEditor] [Facility] Trying other manager classes...");
                    string[] managerClassNames = { "Territory", "AreaMap", "FacilityManager", "FacilityCtrl", "GameCtrl", "MainScene" };
                    foreach (var mgrName in managerClassNames)
                    {
                        if (called) break;
                        IntPtr mgrClass = FindClassByName(mgrName);
                        if (mgrClass == IntPtr.Zero) continue;
                        string[] removeNames = { "RemoveFacility", "DelFacility", "DestroyFacility" };
                        foreach (var methodName in removeNames)
                        {
                            if (called) break;
                            for (int pc = 1; pc <= 3; pc++)
                            {
                                IntPtr mth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(mgrClass, methodName, pc);
                                if (mth != IntPtr.Zero)
                                {
                                    IntPtr inst = FindClassInstance(mgrClass);
                                    if (inst != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr exception = IntPtr.Zero;
                                            IntPtr[] argPtrs = new IntPtr[pc];
                                            IntPtr[] storage = new IntPtr[pc];
                                            storage[0] = e.Ptr;
                                            for (int a = 1; a < pc; a++) storage[a] = IntPtr.Zero;
                                            unsafe
                                            {
                                                fixed (IntPtr* storPtr = storage)
                                                {
                                                    for (int a = 0; a < pc; a++)
                                                        argPtrs[a] = (IntPtr)(&storPtr[a]);
                                                    fixed (IntPtr* argsArr = argPtrs)
                                                    {
                                                        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mth, inst, (void**)argsArr, ref exception);
                                                    }
                                                }
                                            }
                                            if (exception == IntPtr.Zero)
                                            {
                                                Plugin.LogInfo($"[EntityEditor] ✓ Called {mgrName}.{methodName}({pc}p)");
                                                called = true;
                                            }
                                        }
                                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {mgrName}.{methodName}({pc}p) CRASH: {ex.Message}"); }
                                    }
                                }
                            }
                        }
                    }
                }

                // === 兜底：BeforeDestroy + 从管理器移除 + 隐藏视觉 + DestroyImmediate ===
                if (!called)
                {
                    Plugin.LogInfo($"[EntityEditor] No game destroy method worked, trying fallback...");

                    // Step 1: BeforeDestroy 清理
                    {
                        IntPtr bdCls = classPtr;
                        int bdDepth = 0;
                        while (bdCls != IntPtr.Zero && bdDepth < 15)
                        {
                            IntPtr bdMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(bdCls, "BeforeDestroy", 0);
                            if (bdMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exBd = IntPtr.Zero;
                                    unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(bdMth, e.Ptr, null, ref exBd); }
                                    Plugin.LogInfo($"[EntityEditor] BeforeDestroy() at depth={bdDepth}");
                                }
                                catch { }
                                break;
                            }
                            bdCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(bdCls);
                            bdDepth++;
                        }
                    }

                    // Step 2: 从 PrefabManager 等管理器中移除引用
                    RemoveFromManagers(e.Ptr, classPtr, className);

                    // Step 3: 隐藏视觉
                    string[] hideNames = { "SetBodyViewVisible", "SetBodyLogicVisible" };
                    foreach (var hideName in hideNames)
                    {
                        IntPtr hCls = classPtr;
                        int hDepth = 0;
                        while (hCls != IntPtr.Zero && hDepth < 15)
                        {
                            IntPtr hMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(hCls, hideName, 1);
                            if (hMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exH = IntPtr.Zero;
                                    IntPtr[] hArgs = new IntPtr[1];
                                    IntPtr[] hStorage = new IntPtr[1];
                                    hStorage[0] = IntPtr.Zero; // false
                                    unsafe
                                    {
                                        fixed (IntPtr* hStor = hStorage)
                                        {
                                            hArgs[0] = (IntPtr)(&hStor[0]);
                                            fixed (IntPtr* hArgsArr = hArgs)
                                            {
                                                Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(hMth, e.Ptr, (void**)hArgsArr, ref exH);
                                            }
                                        }
                                    }
                                    Plugin.LogInfo($"[EntityEditor] {hideName}(false) at depth={hDepth}");
                                }
                                catch { }
                                break;
                            }
                            hCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(hCls);
                            hDepth++;
                        }
                    }

                    // Step 4: RecycleMySp 清理渲染资源
                    IntPtr cls2 = classPtr;
                    int d2 = 0;
                    while (cls2 != IntPtr.Zero && d2 < 15)
                    {
                        IntPtr mp = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(cls2, "RecycleMySp", 0);
                        if (mp != IntPtr.Zero)
                        {
                            try
                            {
                                IntPtr ex2 = IntPtr.Zero;
                                unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(mp, e.Ptr, null, ref ex2); }
                            }
                            catch { }
                            break;
                        }
                        cls2 = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls2);
                        d2++;
                    }

                    // Step 5: 隐藏 + 销毁 GameObject
                    try
                    {
                        e.GoRef.SetActive(false);
                        UnityEngine.Object.DestroyImmediate(e.GoRef);
                        Plugin.LogInfo($"[EntityEditor] DestroyImmediate GO: {name}");
                        called = true;
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] DestroyImmediate failed: {ex.Message}"); }
                }

                Plugin.LogInfo($"[EntityEditor] === DestroyEntity END: {name} called={called} ===");
                _entities.RemoveAt(i);
                return "ok";
            }
            return $"entity not found: ptrHash={ptrHash}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>
    /// 判断类名是否属于 Facility 层级
    /// </summary>
    private static bool IsFacilityClass(string className)
    {
        return className.Contains("Facility") || className.Contains("FacilityAncientTomb")
            || className.Contains("FacilityForMonster") || className.Contains("FacilityBarracks")
            || className.Contains("FacilityCityWall") || className.Contains("FacilityHive")
            || className.Contains("FacilityMine") || className.Contains("FacilityQuarry");
    }

    /// <summary>
    /// 通过类名在所有已加载的 IL2CPP 程序集中查找类
    /// </summary>
    private static unsafe IntPtr FindClassByName(string className)
    {
        try
        {
            IntPtr domain = Il2CppInterop.Runtime.IL2CPP.il2cpp_domain_get();
            uint count = 0;
            IntPtr* assemblies = Il2CppInterop.Runtime.IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
            for (uint i = 0; i < count; i++)
            {
                IntPtr assembly = assemblies[i];
                IntPtr image = Il2CppInterop.Runtime.IL2CPP.il2cpp_assembly_get_image(assembly);
                IntPtr cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_from_name(image, "", className);
                if (cls != IntPtr.Zero) return cls;
                string[] namespaces = { "Il2Cpp", "Il2CppScripts", "Game", "" };
                foreach (var ns in namespaces)
                {
                    cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_from_name(image, ns, className);
                    if (cls != IntPtr.Zero) return cls;
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 查找游戏管理类的实例（通过静态字段或 FindObjectOfType）
    /// </summary>
    private static IntPtr FindClassInstance(IntPtr classPtr)
    {
        try
        {
            string className = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(classPtr)) ?? "?";
            // 方法1：查找静态 Instance/s_instance 字段并读取值
            IntPtr iter = IntPtr.Zero;
            IntPtr field;
            while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(classPtr, ref iter)) != IntPtr.Zero)
            {
                string? fieldName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                if (fieldName == null) continue;
                if (fieldName == "s_instance" || fieldName == "_instance" || fieldName == "Instance" || fieldName == "instance")
                {
                    // 检查是否是静态字段
                    int attrs = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_flags(field);
                    bool isStatic = (attrs & 0x10) != 0; // FIELD_ATTRIBUTE_STATIC = 0x10
                    Plugin.LogInfo($"[EntityEditor] Found field {fieldName} on {className}, isStatic={isStatic}, attrs=0x{attrs:X}");

                    if (isStatic)
                    {
                        try
                        {
                            IntPtr value = IntPtr.Zero;
                            unsafe
                            {
                                Il2CppInterop.Runtime.IL2CPP.il2cpp_field_static_get_value(field, &value);
                            }
                            Plugin.LogInfo($"[EntityEditor] Static field {fieldName} value={value.ToInt64():X}");
                            if (value != IntPtr.Zero)
                            {
                                // 验证这个指针是否是一个有效的 IL2CPP 对象
                                IntPtr objClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(value);
                                if (objClass != IntPtr.Zero)
                                {
                                    string? objClassName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(objClass));
                                    Plugin.LogInfo($"[EntityEditor] ✓ Got instance from {fieldName}, class={objClassName}");
                                    return value;
                                }
                            }
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Read static field {fieldName} error: {ex.Message}"); }
                    }
                }
            }
            // 方法2：通过 Resources.FindObjectsOfTypeAll 查找
            // 我们需要通过 IL2CPP 的类型系统来查找
            // 尝试直接调用 FindObjectOfType
            IntPtr findMethod = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(classPtr, "get_Instance", 0);
            if (findMethod != IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Found get_Instance() on {className}");
                IntPtr exception = IntPtr.Zero;
                unsafe
                {
                    IntPtr result = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(findMethod, IntPtr.Zero, null, ref exception);
                    if (exception == IntPtr.Zero && result != IntPtr.Zero)
                    {
                        Plugin.LogInfo($"[EntityEditor] get_Instance() returned valid object");
                        return result;
                    }
                }
            }
            // 方法3：遍历场景中的 GameObject 查找组件
            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            foreach (var go in allGOs)
            {
                try
                {
                    var comps = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        IntPtr compClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(comp.Pointer);
                        if (compClass == classPtr)
                        {
                            Plugin.LogInfo($"[EntityEditor] Found instance of {className} on GO: {go.name}");
                            return comp.Pointer;
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindClassInstance error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 调用虚方法（通过方法枚举），忽略异常
    /// </summary>
    private static void CallVoidMethod(IntPtr classPtr, IntPtr objPtr, string methodName, int maxDepth)
    {
        IntPtr cls = classPtr;
        int d = 0;
        while (cls != IntPtr.Zero && d <= maxDepth)
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr m;
            while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(cls, ref iter)) != IntPtr.Zero)
            {
                string? name = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m));
                if (name == methodName)
                {
                    try
                    {
                        IntPtr ex = IntPtr.Zero;
                        unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(m, objPtr, null, ref ex); }
                        Plugin.LogInfo($"[EntityEditor] {methodName}() ex={ex != IntPtr.Zero}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {methodName}() failed: {ex.Message}"); }
                    return;
                }
            }
            cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls);
            d++;
        }
        Plugin.LogInfo($"[EntityEditor] {methodName}() not found");
    }

    /// <summary>
    /// 获取 Territory 实例指针
    /// 链路: Game.get_main_scene() -> area_map -> get_my_territory()
    /// </summary>
    private static IntPtr FindTerritory()
    {
        try
        {
            IntPtr gameClass = FindClassByName("Game");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            IntPtr getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero) break;
                }
            }
            if (getMainSceneMth == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try { unsafe { mainScene = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(getMainSceneMth, IntPtr.Zero, null, ref exception); } }
            catch { }
            if (mainScene == IntPtr.Zero) return IntPtr.Zero;

            IntPtr mainSceneClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mainScene);
            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr areaMapClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(areaMapPtr);
            IntPtr territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "my_territory");
            if (territoryPtr == IntPtr.Zero)
                territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "_my_territory");

            if (territoryPtr == IntPtr.Zero)
            {
                IntPtr getMyTerritoryMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(areaMapClass, "get_my_territory", 0);
                if (getMyTerritoryMth == IntPtr.Zero)
                {
                    string[] altNames = { "GetMyTerritory", "get_MyTerritory", "my_territory" };
                    foreach (var alt in altNames)
                    {
                        getMyTerritoryMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(areaMapClass, alt, 0);
                        if (getMyTerritoryMth != IntPtr.Zero) break;
                    }
                }
                if (getMyTerritoryMth != IntPtr.Zero)
                {
                    try { exception = IntPtr.Zero; unsafe { territoryPtr = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(getMyTerritoryMth, areaMapPtr, null, ref exception); } }
                    catch { }
                }
            }

            if (territoryPtr != IntPtr.Zero)
            {
                IntPtr tc = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(territoryPtr);
                string? tn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(tc));
                Plugin.LogInfo($"[EntityEditor] ✓ Got Territory, class={tn}");
            }
            return territoryPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 通过 Territory 实例获取 BuildHelper 指针
    /// IDA: territory->fields.build_helper
    /// </summary>
    private static IntPtr FindBuildHelperFromTerritory()
    {
        try
        {
            // IDA 完整链路: Game.get_main_scene() -> main_scene.area_map -> AreaMap.get_my_territory() -> territory.build_helper
            Plugin.LogInfo($"[EntityEditor] Finding BuildHelper via Game chain...");

            // Step 1: Game.get_main_scene()
            IntPtr gameClass = FindClassByName("Game");
            Plugin.LogInfo($"[EntityEditor] Game class={gameClass.ToInt64():X}");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            // 列出 Game 类的静态方法
            IntPtr iter = IntPtr.Zero;
            IntPtr mth;
            while ((mth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(gameClass, ref iter)) != IntPtr.Zero)
            {
                string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(mth));
                uint pc = Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_param_count(mth);
                if (mName != null && (mName.Contains("main_scene") || mName.Contains("MainScene") || mName.Contains("instance") || mName.Contains("Instance")))
                    Plugin.LogInfo($"[EntityEditor] Game.{mName}({pc}p)");
            }

            IntPtr getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero)
                    {
                        Plugin.LogInfo($"[EntityEditor] Found Game.{alt}()");
                        break;
                    }
                }
            }
            if (getMainSceneMth == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Game.get_main_scene() not found");
                return IntPtr.Zero;
            }

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try
            {
                unsafe { mainScene = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(getMainSceneMth, IntPtr.Zero, null, ref exception); }
            }
            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] get_main_scene() CRASH: {ex.Message}"); }
            if (exception != IntPtr.Zero || mainScene == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] get_main_scene() failed, result={mainScene.ToInt64():X}, exception={exception != IntPtr.Zero}");
                return IntPtr.Zero;
            }
            Plugin.LogInfo($"[EntityEditor] main_scene={mainScene.ToInt64():X}");

            // Step 2: main_scene.fields.area_map (读偏移量，不直接解引用)
            IntPtr mainSceneClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mainScene);
            Plugin.LogInfo($"[EntityEditor] mainScene class={mainSceneClass.ToInt64():X}");

            // 列出 MainScene 的字段
            IntPtr fi2 = IntPtr.Zero;
            IntPtr f2;
            while ((f2 = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(mainSceneClass, ref fi2)) != IntPtr.Zero)
            {
                string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f2));
                int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f2);
                IntPtr ft = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(f2);
                string? ftn = ft != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(ft)) : "?";
                if (fn != null && (fn.Contains("area") || fn.Contains("map") || fn.Contains("territory") || fn.Contains("build")))
                    Plugin.LogInfo($"[EntityEditor] MainScene.{fn} offset={offset} type={ftn}");
            }

            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            Plugin.LogInfo($"[EntityEditor] area_map={areaMapPtr.ToInt64():X}");
            if (areaMapPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] main_scene.area_map is null");
                return IntPtr.Zero;
            }

            // Step 3: AreaMap.get_my_territory()
            IntPtr areaMapClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(areaMapPtr);
            Plugin.LogInfo($"[EntityEditor] areaMap class={areaMapClass.ToInt64():X}");

            IntPtr territoryPtr = IntPtr.Zero;
            // 先尝试读字段
            territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "my_territory");
            if (territoryPtr == IntPtr.Zero)
                territoryPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "_my_territory");
            Plugin.LogInfo($"[EntityEditor] area_map.my_territory (field) = {territoryPtr.ToInt64():X}");

            // 如果字段读取失败，尝试方法
            if (territoryPtr == IntPtr.Zero)
            {
                IntPtr getMyTerritoryMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(areaMapClass, "get_my_territory", 0);
                if (getMyTerritoryMth == IntPtr.Zero)
                {
                    string[] altNames = { "GetMyTerritory", "get_MyTerritory", "my_territory" };
                    foreach (var alt in altNames)
                    {
                        getMyTerritoryMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(areaMapClass, alt, 0);
                        if (getMyTerritoryMth != IntPtr.Zero) break;
                    }
                }
                if (getMyTerritoryMth != IntPtr.Zero)
                {
                    try
                    {
                        exception = IntPtr.Zero;
                        unsafe { territoryPtr = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(getMyTerritoryMth, areaMapPtr, null, ref exception); }
                        Plugin.LogInfo($"[EntityEditor] get_my_territory() = {territoryPtr.ToInt64():X}, exception={exception != IntPtr.Zero}");
                    }
                    catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] get_my_territory() CRASH: {ex.Message}"); }
                }
            }

            if (territoryPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] Could not get Territory");
                return IntPtr.Zero;
            }

            // Step 4: territory.fields.build_helper
            IntPtr territoryClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(territoryPtr);
            Plugin.LogInfo($"[EntityEditor] territory class={territoryClass.ToInt64():X}");

            IntPtr buildHelperPtr = ReadFieldSafe(territoryPtr, territoryClass, "build_helper");
            if (buildHelperPtr == IntPtr.Zero)
                buildHelperPtr = ReadFieldSafe(territoryPtr, territoryClass, "_build_helper");
            Plugin.LogInfo($"[EntityEditor] territory.build_helper = {buildHelperPtr.ToInt64():X}");

            if (buildHelperPtr != IntPtr.Zero)
            {
                IntPtr bhClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(buildHelperPtr);
                string? bhClassName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(bhClass));
                Plugin.LogInfo($"[EntityEditor] ✓ Got BuildHelper, class={bhClassName}");
            }
            return buildHelperPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindBuildHelperFromTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 通过 Game.get_main_scene() → area_map → map_stuff_helper 获取 MapStuffHelper 指针
    /// IDA: Game.get_main_scene() -> main_scene.area_map -> area_map.map_stuff_helper
    /// </summary>
    private static IntPtr FindMapStuffHelper()
    {
        try
        {
            Plugin.LogInfo($"[EntityEditor] Finding MapStuffHelper via Game chain...");

            // Step 1: Game.get_main_scene()
            IntPtr gameClass = FindClassByName("Game");
            if (gameClass == IntPtr.Zero) return IntPtr.Zero;

            IntPtr getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, "get_main_scene", 0);
            if (getMainSceneMth == IntPtr.Zero)
            {
                string[] altNames = { "GetMainScene", "get_MainScene", "main_scene", "get_instance", "get_Instance" };
                foreach (var alt in altNames)
                {
                    getMainSceneMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(gameClass, alt, 0);
                    if (getMainSceneMth != IntPtr.Zero) break;
                }
            }
            if (getMainSceneMth == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            IntPtr mainScene = IntPtr.Zero;
            try
            {
                unsafe { mainScene = (IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(getMainSceneMth, IntPtr.Zero, null, ref exception); }
            }
            catch { return IntPtr.Zero; }
            if (mainScene == IntPtr.Zero) return IntPtr.Zero;

            // Step 2: main_scene.fields.area_map
            IntPtr mainSceneClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mainScene);
            IntPtr areaMapPtr = ReadFieldSafe(mainScene, mainSceneClass, "area_map");
            if (areaMapPtr == IntPtr.Zero) return IntPtr.Zero;

            // Step 3: area_map.fields.map_stuff_helper
            IntPtr areaMapClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(areaMapPtr);
            IntPtr mapStuffHelperPtr = ReadFieldSafe(areaMapPtr, areaMapClass, "map_stuff_helper");
            if (mapStuffHelperPtr != IntPtr.Zero)
            {
                IntPtr mshClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(mapStuffHelperPtr);
                string? mshClassName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(mshClass));
                Plugin.LogInfo($"[EntityEditor] ✓ Got MapStuffHelper, class={mshClassName}");
            }
            else
                Plugin.LogInfo($"[EntityEditor] area_map.map_stuff_helper is null");
            return mapStuffHelperPtr;
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] FindMapStuffHelper error: {ex.Message}"); }
        return IntPtr.Zero;
    }

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
    private static int ReadIntFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName, int defaultVal = 0)
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

    /// <summary>
    /// 从 IL2CPP List 中按指针匹配移除元素，返回是否成功
    /// </summary>
    private static bool RemoveFromListByPtr(IntPtr listPtr, IntPtr targetPtr)
    {
        try
        {
            IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(listPtr);
            int size = ReadIntFieldSafe(listPtr, listClass, "_size", 0);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _size={size}, target={targetPtr.ToInt64():X}");

            if (size <= 0) return false;

            // 读取 _items 数组
            IntPtr itemsPtr = ReadFieldSafe(listPtr, listClass, "_items");
            if (itemsPtr == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _items is null");
                return false;
            }

            // IL2CPP 数组: 对象头 0x10, 然后是元素指针
            // _items 是 System.Object[]，每个元素是 IntPtr 大小
            IntPtr itemsClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(itemsPtr);
            int arrLen = ReadIntFieldSafe(itemsPtr, itemsClass, "_length", 0);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: _items._length={arrLen}");

            int matchIdx = -1;
            unsafe
            {
                // 数组数据从 offset 0x20 开始（对象头 0x10 + 数组头 0x10）
                IntPtr arrData = itemsPtr + 0x20;
                for (int i = 0; i < size && i < arrLen; i++)
                {
                    IntPtr elem = *(IntPtr*)(arrData + i * IntPtr.Size);
                    if (elem == targetPtr)
                    {
                        matchIdx = i;
                        break;
                    }
                }
            }

            if (matchIdx < 0)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: target not found in list");
                return false;
            }

            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: found at index={matchIdx}, calling RemoveAt...");

            // 调用 RemoveAt(index)
            IntPtr removeAtMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "RemoveAt", 1);
            if (removeAtMth == IntPtr.Zero)
            {
                Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: RemoveAt method not found");
                return false;
            }

            IntPtr exRemove = IntPtr.Zero;
            unsafe
            {
                int idx = matchIdx;
                IntPtr* args = stackalloc IntPtr[1];
                args[0] = (IntPtr)(&idx);
                Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeAtMth, listPtr, (void**)args, ref exRemove);
            }

            int newSize = ReadIntFieldSafe(listPtr, listClass, "_size", -1);
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr: RemoveAt({matchIdx}) done, _size now={newSize}, ex={exRemove != IntPtr.Zero}");
            return newSize == size - 1;
        }
        catch (Exception ex)
        {
            Plugin.LogInfo($"[EntityEditor] RemoveFromListByPtr error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 在对象上读取指定名称的指针字段（含父类搜索）
    /// </summary>
    private static IntPtr ReadFieldRecursive(IntPtr objPtr, IntPtr classPtr, string fieldName)
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
                    unsafe
                    {
                        IntPtr* ptr = (IntPtr*)(objPtr + offset);
                        return *ptr;
                    }
                }
            }
            searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
            depth++;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 从游戏管理器（PrefabManager 等）中移除实体引用，防止 OnGameExit 时空引用
    /// </summary>
    private static void RemoveFromManagers(IntPtr entityPtr, IntPtr classPtr, string className)
    {
        try
        {
            // 查找 PrefabManager 类
            string[] managerNames = { "PrefabManager", "EntityManager", "NpcManager", "AnimalManager", "VehicleManager", "DropItemManager" };
            foreach (var mgrName in managerNames)
            {
                IntPtr mgrClass = FindClassByName(mgrName);
                if (mgrClass == IntPtr.Zero) continue;

                // 获取管理器实例
                IntPtr mgrInst = FindClassInstance(mgrClass);
                if (mgrInst == IntPtr.Zero) continue;

                Plugin.LogInfo($"[EntityEditor] Found manager {mgrName}, scanning for entity references...");

                // 遍历管理器的所有字段，查找 List/Dictionary/数组
                IntPtr fi = IntPtr.Zero;
                IntPtr f;
                while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(mgrClass, ref fi)) != IntPtr.Zero)
                {
                    string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                    IntPtr ft = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(f);
                    string? ftn = ft != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(ft)) : null;
                    if (fn == null || ftn == null) continue;

                    int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                    if (offset < 0x10 || offset > 0x10000) continue;

                    // 检查是否是 List 或 Dictionary 类型
                    bool isList = ftn.Contains("List<");
                    bool isDict = ftn.Contains("Dictionary<");

                    if (isList)
                    {
                        unsafe
                        {
                            IntPtr listPtr = *(IntPtr*)(mgrInst + offset);
                            if (listPtr == IntPtr.Zero) continue;

                            // 尝试调用 List.Remove(entity)
                            IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(listPtr);
                            IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "Remove", 1);
                            if (removeMth != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr ex = IntPtr.Zero;
                                    IntPtr[] args = new IntPtr[1];
                                    IntPtr[] stor = new IntPtr[1];
                                    stor[0] = entityPtr;
                                    fixed (IntPtr* sp = stor)
                                    {
                                        args[0] = (IntPtr)(&sp[0]);
                                        fixed (IntPtr* ap = args)
                                        {
                                            Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, listPtr, (void**)ap, ref ex);
                                        }
                                    }
                                    Plugin.LogInfo($"[EntityEditor] Removed from {mgrName}.{fn} (List), ex={ex != IntPtr.Zero}");
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] Remove from {mgrName}.{fn} failed: {ex.Message}"); }
                            }

                            // 也尝试 RemoveAll(Predicate)
                            IntPtr removeAllMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "RemoveAll", 1);
                            if (removeAllMth != IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] {mgrName}.{fn} has RemoveAll(1p)");
                            }
                        }
                    }
                    else if (isDict)
                    {
                        unsafe
                        {
                            IntPtr dictPtr = *(IntPtr*)(mgrInst + offset);
                            if (dictPtr == IntPtr.Zero) continue;

                            // 尝试读取实体的 guid/stuff_id 作为 key 来移除
                            IntPtr dictClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(dictPtr);

                            // 尝试调用 ContainsKey + Remove
                            IntPtr containsKeyMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dictClass, "ContainsKey", 1);
                            IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(dictClass, "Remove", 1);

                            if (containsKeyMth != IntPtr.Zero && removeMth != IntPtr.Zero)
                            {
                                Plugin.LogInfo($"[EntityEditor] {mgrName}.{fn} is Dictionary, has ContainsKey+Remove");
                            }
                        }
                    }
                }
            }

            // 特殊处理：查找并清理所有包含该实体引用的 List
            // 遍历场景中所有 GameObject 的组件，查找引用了该实体的字段
            CleanupReferencesInScene(entityPtr, className);
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] RemoveFromManagers error: {ex.Message}"); }
    }

    /// <summary>
    /// 遍历场景中的管理器组件，清理对已销毁实体的引用
    /// </summary>
    private static void CleanupReferencesInScene(IntPtr entityPtr, string className)
    {
        try
        {
            // 查找 PrefabManager 组件
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                try
                {
                    var comps = go.GetComponents<Component>();
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        string cn = comp.GetIl2CppType().Name;

                        // 只处理管理器类
                        if (!cn.Contains("Manager") && !cn.Contains("Controller") && !cn.Contains("Helper"))
                            continue;

                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        IntPtr compClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(compPtr);

                        // 查找并清理 List 字段
                        IntPtr fi = IntPtr.Zero;
                        IntPtr f;
                        while ((f = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(compClass, ref fi)) != IntPtr.Zero)
                        {
                            string? fn = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(f));
                            IntPtr ft = Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_type(f);
                            string? ftn = ft != IntPtr.Zero ? Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_type_get_name(ft)) : null;
                            if (fn == null || ftn == null) continue;

                            int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(f);
                            if (offset < 0x10 || offset > 0x10000) continue;

                            // 检查是否是 List 类型且包含实体类型名
                            if (ftn.Contains("List<") && (ftn.Contains(className) || ftn.Contains("Entity") || ftn.Contains("Npc") || ftn.Contains("Animal")))
                            {
                                unsafe
                                {
                                    IntPtr listPtr = *(IntPtr*)(compPtr + offset);
                                    if (listPtr == IntPtr.Zero) continue;

                                    IntPtr listClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(listPtr);
                                    IntPtr removeMth = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(listClass, "Remove", 1);
                                    if (removeMth != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            IntPtr ex = IntPtr.Zero;
                                            IntPtr[] args = new IntPtr[1];
                                            IntPtr[] stor = new IntPtr[1];
                                            stor[0] = entityPtr;
                                            fixed (IntPtr* sp = stor)
                                            {
                                                args[0] = (IntPtr)(&sp[0]);
                                                fixed (IntPtr* ap = args)
                                                {
                                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(removeMth, listPtr, (void**)ap, ref ex);
                                                }
                                            }
                                            Plugin.LogInfo($"[EntityEditor] Cleaned {cn}.{fn} (List), ex={ex != IntPtr.Zero}");
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] CleanupReferencesInScene error: {ex.Message}"); }
    }

    /// <summary>
    /// 从 Territory 实例中读取 build_helper 字段
    /// </summary>
    private static IntPtr ReadBuildHelperFromTerritory(IntPtr territoryPtr, IntPtr territoryClass)
    {
        try
        {
            // 在 Territory 类（含父类）中查找 build_helper 字段
            IntPtr searchCls = territoryClass;
            int depth = 0;
            while (searchCls != IntPtr.Zero && depth < 10)
            {
                IntPtr fi = IntPtr.Zero;
                IntPtr field;
                while ((field = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_fields(searchCls, ref fi)) != IntPtr.Zero)
                {
                    string? fieldName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_name(field));
                    if (fieldName == "build_helper")
                    {
                        int offset = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(field);
                        string clsNm = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(searchCls)) ?? "?";
                        Plugin.LogInfo($"[EntityEditor] Found build_helper field on {clsNm} offset={offset}");
                        unsafe
                        {
                            IntPtr* ptr = (IntPtr*)(territoryPtr + offset);
                            IntPtr buildHelper = *ptr;
                            Plugin.LogInfo($"[EntityEditor] build_helper value={buildHelper.ToInt64():X}");
                            return buildHelper;
                        }
                    }
                }
                searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                depth++;
            }
            Plugin.LogInfo($"[EntityEditor] build_helper field not found on Territory");
        }
        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] ReadBuildHelperFromTerritory error: {ex.Message}"); }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 遍历所有 GameObject 查找 NpcHelper 组件，尝试移除指定 NPC
    /// </summary>
    private static void RemoveFromNpcHelper(IntPtr npcPtr, string className)
    {
        var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allGOs)
        {
            try
            {
                var comps = go.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    string cn = comp.GetIl2CppType().Name;
                    if (cn != "NpcHelper") continue;

                    IntPtr compPtr = GetIl2CppPtr(comp);
                    if (compPtr == IntPtr.Zero) continue;

                    IntPtr compClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(compPtr);

                    // 列出 NpcHelper 的所有方法
                    IntPtr iter = IntPtr.Zero;
                    IntPtr m;
                    Plugin.LogInfo($"[EntityEditor] Found NpcHelper on {go.name}, listing methods:");
                    while ((m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_methods(compClass, ref iter)) != IntPtr.Zero)
                    {
                        string? mName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(m));
                        if (mName != null && (mName.Contains("Remove") || mName.Contains("Delete") || mName.Contains("Despawn") || mName.Contains("Kill") || mName.Contains("Npc") || mName.Contains("npc")))
                            Plugin.LogInfo($"[EntityEditor]   NpcHelper.{mName}");
                    }

                    // 尝试调用 RemoveNpc / Remove / Delete 等方法
                    string[] removeNames = { "RemoveNpc", "Remove", "Delete", "Despawn", "Kill", "RemoveEntity", "RemoveById" };
                    foreach (var methodName in removeNames)
                    {
                        IntPtr methodPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(compClass, methodName, 1);
                        if (methodPtr != IntPtr.Zero)
                        {
                            Plugin.LogInfo($"[EntityEditor] Trying NpcHelper.{methodName}(ptr)");
                            try
                            {
                                IntPtr exception = IntPtr.Zero;
                                unsafe
                                {
                                    void* argPtr = (void*)npcPtr;
                                    void** args = &argPtr;
                                    Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, compPtr, args, ref exception);
                                }
                                Plugin.LogInfo($"[EntityEditor] Called NpcHelper.{methodName} OK");
                                return;
                            }
                            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] NpcHelper.{methodName} failed: {ex.Message}"); }
                        }
                    }
                    return;
                }
            }
            catch { }
        }
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
