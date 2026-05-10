using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// NPC 实体扫描器 - 探索和编辑地图上的 NPC 数据
/// </summary>
internal static class NpcEntityScanner
{
    // 可能的 NPC GO 名称关键词（探索阶段用）
    private static readonly string[] NpcKeywords = {
        "npc", "character", "unit", "soldier", "worker", "enemy", "mob",
        "merchant", "trader", "guard", "villager", "creature", "animal",
        "monster", "boss", "ally", "friendly", "hostile", "human"
    };

    // 重点组件名（有战斗属性的）
    private static readonly string[] CombatComponentNames = {
        "MonsterBody", "Monster5", "Monster", "NpcBody", "Npc", "Character",
        "Unit", "Soldier", "Creature", "Animal", "Boss", "Enemy",
        "MonsterHelper", "MonsterBase", "MonsterData", "MonsterStat"
    };

    // NPC 战斗属性字段（来自 BattleUnit 和 Monster）
    private static readonly string[] _npcStatFields = {
        "stuff_id", "guid", "hp", "hp_total", "atk_min", "atk_max",
        "magic_atk_min", "magic_atk_max", "speed", "atk_range", "atk_cd"
    };
    private static readonly HashSet<string> _npcFloatFields = new() {
        "hp", "hp_total", "atk_min", "atk_max", "magic_atk_min", "magic_atk_max",
        "speed", "atk_range", "atk_cd"
    };

    // 字段偏移缓存
    private static readonly Dictionary<string, int> _npcFieldOffsets = new();
    private static bool _npcOffsetsCached;

    // IL2CPP API 缓存
    private static bool _apiCached;
    private static MethodInfo? _il2cpp_get_class;
    private static MethodInfo? _il2cpp_field_get_offset;
    private static MethodInfo? _il2cpp_class_get_fields;
    private static MethodInfo? _il2cpp_class_get_parent;
    private static MethodInfo? _il2cpp_field_get_name;
    private static MethodInfo? _il2cpp_field_get_type;
    private static MethodInfo? _il2cpp_type_get_name;

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

            // il2cpp_class_get_fields(klass, IntPtr& iter) - iter 是 ref IntPtr
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
                if (string.IsNullOrEmpty(name))
                {
                    args[1] = field;
                    continue;
                }

                // il2cpp_field_get_offset 返回 UInt32
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

                // 更新 iter 为当前 field 指针，用于下一次迭代
                args[1] = field;
            }
        }
        catch { }
        return fields;
    }

    /// <summary>
    /// 探索函数：扫描所有 GO，打印可能的 NPC 实体及其组件字段
    /// 输出到 BepInEx console log
    /// </summary>
    internal static void ExploreNpcEntities()
    {
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[NpcEntityScanner] ========== 开始扫描 NPC 实体 ==========");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.LogInfo($"[NpcEntityScanner] 共 {allGOs.Length} 个 GameObject");

            var matchedGOs = new List<(GameObject go, string keyword)>();

            // 第一轮：按关键词过滤
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name.ToLower();
                    foreach (var kw in NpcKeywords)
                    {
                        if (goName.Contains(kw))
                        {
                            matchedGOs.Add((go, kw));
                            break;
                        }
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[NpcEntityScanner] 关键词匹配到 {matchedGOs.Count} 个 GO");

            // 打印匹配的 GO 名称和组件
            int printed = 0;
            foreach (var (go, keyword) in matchedGOs)
            {
                if (printed >= 50)
                {
                    Plugin.LogInfo($"[NpcEntityScanner] 达到输出上限 50，停止");
                    break;
                }
                printed++;

                try
                {
                    Plugin.LogInfo($"[NpcEntityScanner] --- GO: {go.name} (keyword: {keyword}) ---");

                    var components = go.GetComponents<Component>();
                    Plugin.LogInfo($"[NpcEntityScanner]   组件数: {components.Length}");

                    foreach (var comp in components)
                    {
                        if (comp == null) continue;

                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { compPtr })!; }
                        catch { continue; }
                        if (compClass == IntPtr.Zero) continue;

                        // 获取类名
                        string className = comp.GetIl2CppType().Name;
                        Plugin.LogInfo($"[NpcEntityScanner]   组件: {className} (classPtr=0x{compClass.ToInt64():X})");

                        // 检查是否是重点组件
                        bool isCombatComponent = false;
                        foreach (var ccn in CombatComponentNames)
                        {
                            if (className.Contains(ccn)) { isCombatComponent = true; break; }
                        }

                        // 枚举字段（重点组件打印全部字段）
                        var fields = GetIl2CppFields(compClass);
                        Plugin.LogInfo($"[NpcEntityScanner]     字段数: {fields.Count}");
                        int fieldCount = 0;
                        int maxFields = isCombatComponent ? 100 : 10;
                        foreach (var (name, offset, typeName) in fields)
                        {
                            if (fieldCount >= maxFields)
                            {
                                Plugin.LogInfo($"[NpcEntityScanner]     ... 还有更多字段");
                                break;
                            }
                            Plugin.LogInfo($"[NpcEntityScanner]     {name} (offset={offset}, type={typeName})");
                            fieldCount++;
                        }

                        // 重点组件遍历类层级找战斗属性
                        if (isCombatComponent)
                        {
                            Plugin.LogInfo($"[NpcEntityScanner]     重点组件，遍历类层级...");
                            IntPtr cls = compClass;
                            int depth = 0;
                            while (cls != IntPtr.Zero && depth < 10)
                            {
                                try
                                {
                                    string parentClassName = Marshal.PtrToStringAnsi((IntPtr)Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name(cls)) ?? "unknown";
                                    var parentFields = GetIl2CppFields(cls);
                                    if (parentFields.Count > 0)
                                    {
                                        Plugin.LogInfo($"[NpcEntityScanner]       层级 {depth} ({parentClassName}): {parentFields.Count} 个字段");
                                        foreach (var (name, offset, typeName) in parentFields)
                                        {
                                            // 只打印可能有战斗属性的字段
                                            if (name.Contains("hp") || name.Contains("atk") || name.Contains("attack") ||
                                                name.Contains("speed") || name.Contains("damage") || name.Contains("defense") ||
                                                name.Contains("guid") || name.Contains("stuff") || name.Contains("id") ||
                                                name.Contains("level") || name.Contains("hp_total") || name.Contains("name"))
                                            {
                                                Plugin.LogInfo($"[NpcEntityScanner]         *** {name} (offset={offset}, type={typeName}) ***");
                                            }
                                        }
                                    }
                                }
                                catch { }
                                try { cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls); } catch { break; }
                                depth++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogError($"[NpcEntityScanner] 处理 GO 异常: {ex.Message}");
                }
            }

            // 第二轮：扫描所有有 hp_total 字段的非龙组件
            Plugin.LogInfo("[NpcEntityScanner] ========== 扫描所有含 hp_total 的非龙组件 ==========");
            int combatFound = 0;
            foreach (var go in allGOs)
            {
                if (combatFound >= 30) break;
                try
                {
                    string goName = go.name.ToLower();
                    if (goName.Contains("dragon")) continue;

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

                        // 检查是否有 hp_total 字段
                        var fields = GetIl2CppFields(compClass);
                        bool hasHpTotal = false;
                        foreach (var f in fields)
                        {
                            if (f.Name == "hp_total") { hasHpTotal = true; break; }
                        }

                        if (hasHpTotal)
                        {
                            combatFound++;
                            string className = comp.GetIl2CppType().Name;
                            Plugin.LogInfo($"[NpcEntityScanner] *** 战斗组件: GO={go.name}, 类={className} ***");

                            // 打印所有字段
                            foreach (var (name, offset, typeName) in fields)
                            {
                                Plugin.LogInfo($"[NpcEntityScanner]     {name} (offset={offset}, type={typeName})");
                            }
                        }
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[NpcEntityScanner] ========== 扫描完成，找到 {combatFound} 个非龙战斗组件 ==========");
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[NpcEntityScanner] 异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描所有有战斗属性的实体（不限于关键词匹配）
    /// </summary>
    internal static void ScanAllCombatEntities()
    {
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[NpcEntityScanner] ========== 扫描所有战斗实体 ==========");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            var combatEntities = new Dictionary<string, List<string>>();

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

                        var fields = GetIl2CppFields(compClass);
                        bool hasCombatField = false;
                        foreach (var f in fields)
                        {
                            if (f.Name == "hp_total" || f.Name == "atk_min" || f.Name == "atk_max")
                            {
                                hasCombatField = true;
                                break;
                            }
                        }

                        if (hasCombatField)
                        {
                            string className = comp.GetIl2CppType().Name;
                            if (!combatEntities.ContainsKey(go.name))
                                combatEntities[go.name] = new List<string>();
                            if (!combatEntities[go.name].Contains(className))
                                combatEntities[go.name].Add(className);
                        }
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[NpcEntityScanner] 找到 {combatEntities.Count} 个有战斗属性的 GO:");
            foreach (var (goName, classNames) in combatEntities)
            {
                Plugin.LogInfo($"[NpcEntityScanner]   {goName}: [{string.Join(", ", classNames)}]");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[NpcEntityScanner] 异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 缓存 NPC 战斗属性字段偏移（遍历 Monster -> BattleUnit 类层级）
    /// </summary>
    private static void CacheNpcFieldOffsets(IntPtr compPtr, IntPtr compClass)
    {
        if (_npcOffsetsCached) return;
        _npcOffsetsCached = true;

        var seen = new HashSet<int>();
        IntPtr cls = compClass;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < 10)
        {
            int clsAddr = cls.GetHashCode();
            if (seen.Contains(clsAddr)) break;
            seen.Add(clsAddr);

            var fields = GetIl2CppFields(cls);
            foreach (var (name, offset, typeName) in fields)
            {
                if (offset > 0 && !_npcFieldOffsets.ContainsKey(name))
                    _npcFieldOffsets[name] = offset;
            }

            try { cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls); } catch { break; }
            depth++;
        }

        Plugin.LogInfo($"[NpcEntityScanner] 缓存了 {_npcFieldOffsets.Count} 个字段偏移");
    }

    /// <summary>
    /// 读取所有 NPC 实体（Monster 类型）
    /// </summary>
    internal static List<Dictionary<string, object>> ReadNpcEntities()
    {
        var result = new List<Dictionary<string, object>>();
        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[ReadNpcEntities] 开始扫描...");
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.LogInfo($"[ReadNpcEntities] 共 {allGOs.Length} 个 GO");

            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    string goNameLower = goName.ToLower();
                    // 排除 egg 和 bullet
                    if (goNameLower.Contains("egg") || goNameLower.Contains("bullet")) continue;

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
                        // 只处理 Monster 或其子类（排除 MonsterBody）
                        if (!className.Contains("Monster") || className == "MonsterBody") continue;

                        Plugin.LogInfo($"[ReadNpcEntities] 找到 Monster 组件: {className} in {goName}");

                        // 缓存字段偏移
                        CacheNpcFieldOffsets(compPtr, compClass);
                        Plugin.LogInfo($"[ReadNpcEntities] 缓存后字段数: {_npcFieldOffsets.Count}, 包含guid: {_npcFieldOffsets.ContainsKey("guid")}");

                        // 检查是否有 guid 字段
                        if (!_npcFieldOffsets.ContainsKey("guid"))
                        {
                            Plugin.LogInfo($"[ReadNpcEntities] 无 guid 字段，跳过");
                            continue;
                        }

                        int guid = 0, stuffId = 0;
                        try { guid = ReadIl2CppInt(compPtr, _npcFieldOffsets["guid"]); } catch { }
                        try { stuffId = ReadIl2CppInt(compPtr, _npcFieldOffsets["stuff_id"]); } catch { }

                        // 过滤无效实体
                        if (guid <= 0) continue;

                        var dict = new Dictionary<string, object> { ["goName"] = goName };
                        foreach (var fname in _npcStatFields)
                        {
                            if (!_npcFieldOffsets.TryGetValue(fname, out int off)) continue;
                            try
                            {
                                if (_npcFloatFields.Contains(fname))
                                    dict[fname] = ReadIl2CppFloat(compPtr, off);
                                else
                                    dict[fname] = ReadIl2CppInt(compPtr, off);
                            }
                            catch { }
                        }
                        result.Add(dict);
                        break; // 每个 GO 只取第一个 Monster 组件
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogError($"[ReadNpcEntities] 异常: {ex.Message}"); }

        Plugin.LogInfo($"[ReadNpcEntities] 完成, 找到 {result.Count} 个实体");
        foreach (var d in result)
        {
            int sid = d.TryGetValue("stuff_id", out var sv) ? Convert.ToInt32(sv) : 0;
            int g = d.TryGetValue("guid", out var gv) ? Convert.ToInt32(gv) : 0;
            float hp = d.TryGetValue("hp", out var hv) ? Convert.ToSingle(hv) : 0;
            Plugin.LogInfo($"[ReadNpcEntities]   {d["goName"]} stuffId={sid} guid={g} hp={hp}");
        }
        return result;
    }

    /// <summary>
    /// 获取 NPC 实体 JSON
    /// </summary>
    internal static string GetNpcEntitiesJson()
    {
        var entities = ReadNpcEntities();
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var d in entities)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('{');
            bool f2 = true;
            foreach (var kv in d)
            {
                if (!f2) sb.Append(',');
                f2 = false;
                if (kv.Value is float fv)
                    sb.Append($"\"{kv.Key}\":{fv:G}");
                else if (kv.Value is string sv)
                    sb.Append($"\"{kv.Key}\":\"{sv}\"");
                else
                    sb.Append($"\"{kv.Key}\":{kv.Value}");
            }
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// 设置 NPC 实体属性
    /// </summary>
    internal static string SetNpcEntityField(int guid, string fieldName, float value)
    {
        try
        {
            CacheIl2CppApi();
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    if (!goName.ToLower().Contains("monster")) continue;

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
                        if (!className.Contains("Monster")) continue;

                        // 确保字段偏移已缓存
                        if (!_npcOffsetsCached)
                            CacheNpcFieldOffsets(compPtr, compClass);

                        if (!_npcFieldOffsets.ContainsKey("guid")) continue;

                        int entityGuid = 0;
                        try { entityGuid = ReadIl2CppInt(compPtr, _npcFieldOffsets["guid"]); } catch { }
                        if (entityGuid != guid) continue;

                        if (!_npcFieldOffsets.TryGetValue(fieldName, out int offset))
                            return $"unknown field: {fieldName}";

                        // 写入值
                        if (_npcFloatFields.Contains(fieldName))
                            WriteIl2CppFloat(compPtr, offset, value);
                        else
                            WriteIl2CppInt(compPtr, offset, (int)value);

                        Plugin.LogInfo($"[SetNpcEntityField] guid={guid}, {fieldName}={value}");
                        return "ok";
                    }
                }
                catch { }
            }
            return $"entity not found: guid={guid}";
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ===== 阵营扫描 =====

    private static List<Dictionary<string, object>> _factionEntities = new();
    private static string _factionFieldName = "";
    private static List<string> _allDiscoveredFields = new();

    /// <summary>
    /// 按阵营扫描所有战斗实体（通过 owner_facility_guid 关联建筑组件类型判断阵营）
    /// </summary>
    internal static void ScanByFaction()
    {
        _factionEntities.Clear();
        _allDiscoveredFields.Clear();
        _factionFieldName = "owner_facility_guid";

        try
        {
            CacheIl2CppApi();
            Plugin.LogInfo("[ScanByFaction] 开始扫描...");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();

            // 临时缓存：每个组件类的字段偏移
            var classFieldCache = new Dictionary<string, Dictionary<string, (int offset, string typeName)>>();

            // ===== 第一遍：扫描所有建筑 GO，建立 facility guid -> 阵营 映射 =====
            // FacilityBarracks = 玩家建筑 (阵营1)
            // FacilityEnemyBarracks = 敌方建筑 (阵营2)
            // FacilityBigTree = 怪物巢穴 (阵营2，中立怪物)
            var facilityFactionMap = new Dictionary<int, int>(); // facility guid -> faction

            foreach (var go in allGOs)
            {
                try
                {
                    var components = go.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        string cn = comp.GetIl2CppType().Name;

                        int faction = -1;
                        if (cn == "FacilityBarracks") faction = 1;
                        else if (cn == "FacilityEnemyBarracks") faction = 2;
                        else if (cn == "FacilityBigTree") faction = 2; // 树巢怪物 = 敌方
                        else continue;

                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;

                        // 获取字段缓存
                        if (!classFieldCache.TryGetValue(cn, out var fieldMap))
                        {
                            IntPtr compClass = (IntPtr)_il2cpp_get_class!.Invoke(null, new object[] { compPtr })!;
                            fieldMap = new Dictionary<string, (int, string)>();
                            var seen = new HashSet<int>();
                            IntPtr cls = compClass;
                            int depth = 0;
                            while (cls != IntPtr.Zero && depth < 10)
                            {
                                int a = cls.GetHashCode();
                                if (seen.Contains(a)) break;
                                seen.Add(a);
                                foreach (var (n, o, t) in GetIl2CppFields(cls))
                                {
                                    if (o > 0 && !fieldMap.ContainsKey(n)) fieldMap[n] = (o, t);
                                }
                                try { cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls); } catch { break; }
                                depth++;
                            }
                            classFieldCache[cn] = fieldMap;
                        }

                        // 读取建筑 guid
                        if (fieldMap.ContainsKey("guid"))
                        {
                            int facGuid = 0;
                            try { facGuid = ReadIl2CppInt(compPtr, fieldMap["guid"].Item1); } catch { }
                            if (facGuid > 0 && !facilityFactionMap.ContainsKey(facGuid))
                            {
                                facilityFactionMap[facGuid] = faction;
                            }
                        }
                        break; // 每个 GO 只取第一个建筑组件
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[ScanByFaction] 建筑扫描完成: {facilityFactionMap.Count} 个建筑映射");

            // ===== 第二遍：扫描所有 Monster 实体，通过 owner_facility_guid 查找阵营 =====
            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    string goNameLower = goName.ToLower();
                    if (goNameLower.Contains("egg") || goNameLower.Contains("bullet")) continue;

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

                        // 获取字段缓存
                        if (!classFieldCache.TryGetValue(className, out var fieldMap))
                        {
                            fieldMap = new Dictionary<string, (int, string)>();
                            var seen = new HashSet<int>();
                            IntPtr cls = compClass;
                            int depth = 0;
                            while (cls != IntPtr.Zero && depth < 10)
                            {
                                int clsAddr = cls.GetHashCode();
                                if (seen.Contains(clsAddr)) break;
                                seen.Add(clsAddr);
                                var fields = GetIl2CppFields(cls);
                                foreach (var (name, offset, typeName) in fields)
                                {
                                    if (offset > 0 && !fieldMap.ContainsKey(name))
                                        fieldMap[name] = (offset, typeName);
                                }
                                try { cls = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_parent(cls); } catch { break; }
                                depth++;
                            }
                            classFieldCache[className] = fieldMap;
                        }

                        // 检查是否有 hp_total 字段（BattleUnit 特征）和 owner_facility_guid
                        if (!fieldMap.ContainsKey("hp_total")) continue;
                        if (!fieldMap.ContainsKey("guid")) continue;
                        // 排除非 Monster 类（如 Npc 模板也有 hp_total）
                        if (!className.Contains("Monster") || className.Contains("Npc")) continue;

                        // 读取 guid
                        int guid = 0;
                        try { guid = ReadIl2CppInt(compPtr, fieldMap["guid"].offset); } catch { }
                        if (guid <= 0) continue;

                        // 首次发现战斗实体时，记录所有字段名
                        if (_allDiscoveredFields.Count == 0)
                        {
                            _allDiscoveredFields.AddRange(fieldMap.Keys);
                            _allDiscoveredFields.Sort();
                        }

                        // 读取实体数据
                        var dict = new Dictionary<string, object> { ["goName"] = goName, ["className"] = className };
                        int stuffId = 0;
                        try { stuffId = ReadIl2CppInt(compPtr, fieldMap["stuff_id"].offset); } catch { }
                        dict["stuff_id"] = stuffId;
                        dict["guid"] = guid;

                        foreach (var fname in _npcStatFields)
                        {
                            if (fname == "stuff_id" || fname == "guid") continue;
                            if (!fieldMap.TryGetValue(fname, out var fi)) continue;
                            try
                            {
                                if (_npcFloatFields.Contains(fname))
                                    dict[fname] = ReadIl2CppFloat(compPtr, fi.offset);
                                else
                                    dict[fname] = ReadIl2CppInt(compPtr, fi.offset);
                            }
                            catch { }
                        }

                        // 通过 owner_facility_guid 查找阵营
                        int faction = -1;
                        if (fieldMap.ContainsKey("owner_facility_guid"))
                        {
                            int ownerFacGuid = 0;
                            try { ownerFacGuid = ReadIl2CppInt(compPtr, fieldMap["owner_facility_guid"].Item1); } catch { }
                            dict["owner_facility_guid"] = ownerFacGuid;
                            if (ownerFacGuid > 0 && facilityFactionMap.TryGetValue(ownerFacGuid, out int facFaction))
                                faction = facFaction;
                        }
                        dict["faction"] = faction;

                        _factionEntities.Add(dict);
                        found++;
                        break; // 每个 GO 只取第一个战斗组件
                    }
                }
                catch { }
            }

            Plugin.LogInfo($"[ScanByFaction] 完成: {found} 个实体");

            var groups = new Dictionary<int, int>();
            foreach (var e in _factionEntities)
            {
                int f = Convert.ToInt32(e["faction"]);
                groups.TryGetValue(f, out int c);
                groups[f] = c + 1;
            }
            foreach (var kv in groups)
                Plugin.LogInfo($"[ScanByFaction] 阵营{kv.Key}: {kv.Value}个");

        }
        catch (Exception ex) { Plugin.LogError($"[ScanByFaction] 异常: {ex.Message}\n{ex.StackTrace}"); }
    }

    /// <summary>
    /// 获取阵营扫描结果 JSON
    /// </summary>
    internal static string GetFactionEntitiesJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"factionField\":\"").Append(_factionFieldName ?? "").Append("\",");
        sb.Append("\"allFields\":[");
        for (int i = 0; i < _allDiscoveredFields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(_allDiscoveredFields[i]).Append('"');
        }
        sb.Append("],\"entities\":[");
        bool first = true;
        foreach (var d in _factionEntities)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('{');
            bool f2 = true;
            foreach (var kv in d)
            {
                if (!f2) sb.Append(',');
                f2 = false;
                sb.Append('"').Append(kv.Key).Append("\":");
                if (kv.Value is float fv)
                    sb.Append(fv.ToString("G"));
                else if (kv.Value is string sv)
                    sb.Append('"').Append(sv).Append('"');
                else
                    sb.Append(kv.Value);
            }
            sb.Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }

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
