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
                            GoRef = go,
                            CompRef = comp,
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
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.PtrHash != ptrHash) continue;

                if (e.GoRef == null)
                    return "GameObject reference lost (rescan needed)";

                string name = e.GoRef.name;
                IntPtr classPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(e.Ptr);
                string className = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(classPtr)) ?? "?";
                Plugin.LogInfo($"[EntityEditor] === DestroyEntity START: {name} class={className} ptrHash={ptrHash} ===");

                bool called = false;

                // === 第一优先：NPC 类型用 LeaveMapAndDestroy ===
                if (className.Contains("Npc") && !className.Contains("NpcHelper"))
                {
                    string[] zeroParamNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead" };
                    foreach (var methodName in zeroParamNames)
                    {
                        if (called) break;
                        IntPtr searchCls = classPtr;
                        int depth = 0;
                        while (searchCls != IntPtr.Zero && depth < 15 && !called)
                        {
                            string clsName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(searchCls)) ?? "?";
                            IntPtr methodPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchCls, methodName, 0);
                            Plugin.LogInfo($"[EntityEditor] [NPC] {methodName} depth={depth} cls={clsName} found={methodPtr != IntPtr.Zero}");
                            if (methodPtr != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exception = IntPtr.Zero;
                                    unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, e.Ptr, null, ref exception); }
                                    if (exception != IntPtr.Zero)
                                        Plugin.LogInfo($"[EntityEditor] {methodName}() exception on {name}");
                                    else
                                    {
                                        Plugin.LogInfo($"[EntityEditor] ✓ Called {methodName}() on {name}");
                                        called = true;
                                    }
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {methodName}() CRASH: {ex.Message}"); }
                            }
                            searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                            depth++;
                        }
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

                        // Step 4: 隐藏 + 禁用 GameObject
                        try
                        {
                            e.GoRef.SetActive(false);
                            Plugin.LogInfo($"[EntityEditor] SetActive(false) done");
                        }
                        catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] SetActive failed: {ex.Message}"); }

                        Plugin.LogInfo($"[EntityEditor] Manual dismantle steps completed");
                        called = true;
                    }
                        else
                            Plugin.LogInfo($"[EntityEditor] Could not get BuildHelper instance");
                    }

                // === 第三优先：通用 0 参数销毁方法（非NPC/非Facility） ===
                if (!called && !className.Contains("Npc") && !IsFacilityClass(className))
                {
                    string[] zeroParamNames = { "LeaveMapAndDestroy", "LeaveMapAndDestroyWithFamily", "OnDead" };
                    foreach (var methodName in zeroParamNames)
                    {
                        if (called) break;
                        IntPtr searchCls = classPtr;
                        int depth = 0;
                        while (searchCls != IntPtr.Zero && depth < 15 && !called)
                        {
                            string clsName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(searchCls)) ?? "?";
                            IntPtr methodPtr = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(searchCls, methodName, 0);
                            if (methodPtr != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr exception = IntPtr.Zero;
                                    unsafe { Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(methodPtr, e.Ptr, null, ref exception); }
                                    if (exception == IntPtr.Zero)
                                    {
                                        Plugin.LogInfo($"[EntityEditor] ✓ Called {methodName}() on {name}");
                                        called = true;
                                    }
                                }
                                catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] {methodName}() CRASH: {ex.Message}"); }
                            }
                            searchCls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(searchCls);
                            depth++;
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

                // === 兜底：BeforeDestroy + 隐藏视觉 + DestroyImmediate ===
                if (!called)
                {
                    Plugin.LogInfo($"[EntityEditor] No game destroy method worked, trying fallback...");
                    // BeforeDestroy 清理
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
                    // 隐藏视觉
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
                    // RecycleMySp
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
                    // DestroyImmediate
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
    /// 安全读取对象的指针字段（含父类搜索），使用 il2cpp_field_get_offset
    /// </summary>
    private static IntPtr ReadFieldSafe(IntPtr objPtr, IntPtr classPtr, string fieldName)
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
