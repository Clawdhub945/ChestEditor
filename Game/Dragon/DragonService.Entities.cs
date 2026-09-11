using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>龙系统：素材背包、龙魂、召唤、龙实体扫描与属性修改</summary>
internal static partial class DragonService
{
    /// <summary>
    /// 搜索地图上的龙实体 GameObject
    /// </summary>

    // 龙实体战斗属性字段偏移缓存
    private static readonly Dictionary<string, int> _dragonFieldOffsets = new();

    private static bool _dragonOffsetsCached;


    private static void CacheDragonFieldOffsets(IntPtr compPtr, IntPtr compClass)
    {
        if (_dragonOffsetsCached) return;
        _dragonOffsetsCached = true;
        foreach (var (name, offset) in Il2CppApi.CollectFieldsInHierarchy(compClass))
        {
            if (!_dragonFieldOffsets.ContainsKey(name))
                _dragonFieldOffsets[name] = offset;
        }
    }


    private static readonly string[] _dragonStatFields = { "stuff_id", "guid", "hp", "hp_total", "atk_min", "atk_max", "magic_atk_min", "magic_atk_max", "speed", "power", "atk_range", "atk_cd", "view_range", "create_days",
        "train_increase_hp", "train_increase_atk", "train_increase_ability_power", "train_increase_phys_res", "train_increase_magic_res" };

    private static readonly HashSet<string> _dragonFloatFields = new() { "hp", "hp_total", "atk_min", "atk_max", "magic_atk_min", "magic_atk_max", "speed", "power", "atk_range", "atk_cd", "view_range" };


    internal static List<Dictionary<string, object>> ReadDragonEntities(UnityEngine.GameObject[]? source = null)
    {
        var result = new List<Dictionary<string, object>>();
        try
        {
            var allGOs = source ?? UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    if (!goName.ToLower().Contains("dragon")) continue;
                    found++;
                    if (found > 30) break;

                    var components = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;
                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                        if (compClass == IntPtr.Zero) continue;

                        // 检查是否有 hp_total 字段（战斗组件）
                        if (!_dragonOffsetsCached)
                        {
                            var tmpFields = Il2CppApi.CollectFieldsInHierarchy(compClass);
                            if (!tmpFields.Any(x => x.Name == "hp_total")) continue;
                            foreach (var (n, o) in tmpFields) { if (!_dragonFieldOffsets.ContainsKey(n)) _dragonFieldOffsets[n] = o; }
                            _dragonOffsetsCached = true;
                        }

                        if (!_dragonFieldOffsets.ContainsKey("hp_total")) continue;

                        int guid = 0, stuffId = 0;
                        try { guid = ReadIl2CppInt(compPtr, _dragonFieldOffsets["guid"]); } catch { }
                        try { stuffId = ReadIl2CppInt(compPtr, _dragonFieldOffsets["stuff_id"]); } catch { }
                        // 只保留真正的龙实体：stuffId 在 201401-2015510 范围，guid > 0
                        if (guid <= 0 || stuffId < 201401 || stuffId > 2015510) continue;

                        var dict = new Dictionary<string, object> { ["goName"] = goName };
                        foreach (var fname in _dragonStatFields)
                        {
                            if (!_dragonFieldOffsets.TryGetValue(fname, out int off)) continue;
                            try
                            {
                                if (_dragonFloatFields.Contains(fname))
                                    dict[fname] = ReadIl2CppFloat(compPtr, off);
                                else
                                    dict[fname] = ReadIl2CppInt(compPtr, off);
                            }
                            catch { }
                        }

                        // 应用持久化的实体属性修改（按 stuff_id 匹配，直接写内存并同步显示值）
                        var pendingEnt = ModificationStore.GetByPrefix("dragone:" + stuffId + ":");
                        foreach (var kv in pendingEnt)
                        {
                            int c = kv.Key.LastIndexOf(':');
                            if (c < 0) continue;
                            var fname2 = kv.Key.Substring(c + 1);
                            if (!_dragonFieldOffsets.TryGetValue(fname2, out int off2)) continue;
                            if (_dragonFloatFields.Contains(fname2))
                            {
                                WriteIl2CppFloat(compPtr, off2, kv.Value);
                                dict[fname2] = kv.Value;
                            }
                            else
                            {
                                WriteIl2CppInt(compPtr, off2, (int)kv.Value);
                                dict[fname2] = (int)kv.Value;
                            }
                        }
                        result.Add(dict);
                        break;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Plugin.LogError($"[ReadDragonEntities] 异常: {ex.Message}"); }
        Plugin.LogInfo($"[ReadDragonEntities] 完成, 找到 {result.Count} 个实体");
        return result;
    }


    internal static string GetDragonEntitiesJson()
    {
        var entities = ReadDragonEntities();
        return JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var d in entities)
            {
                w.WriteStartObject();
                foreach (var kv in d)
                {
                    w.WritePropertyName(kv.Key);
                    WriteJsonValue(w, kv.Value);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }


    internal static string SetDragonEntityField(int guid, string fieldName, float value)
    {
        try
        {
            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            int found = 0;
            bool guidMatched = false;
            string? unknownField = null;
            foreach (var go in allGOs)
            {
                try
                {
                    if (!go.name.ToLower().Contains("dragon")) continue;
                    found++;
                    if (found > 30) break;

                    var components = go.GetComponents<UnityEngine.Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        IntPtr compPtr = GetIl2CppPtr(comp);
                        if (compPtr == IntPtr.Zero) continue;
                        IntPtr compClass = IntPtr.Zero;
                        try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                        if (compClass == IntPtr.Zero) continue;

                        // 与 ReadDragonEntities 一致：只认带 hp_total 的战斗组件补齐字段偏移，
                        // 避免非战斗组件污染缓存
                        if (!_dragonFieldOffsets.ContainsKey("hp_total"))
                        {
                            var tmpFields = Il2CppApi.CollectFieldsInHierarchy(compClass);
                            if (tmpFields.Any(x => x.Name == "hp_total"))
                                foreach (var (n, o) in tmpFields) { if (!_dragonFieldOffsets.ContainsKey(n)) _dragonFieldOffsets[n] = o; }
                        }

                        if (!_dragonFieldOffsets.TryGetValue("guid", out int guidOff)) continue;
                        int curGuid = ReadIl2CppInt(compPtr, guidOff);
                        if (curGuid != guid) continue;

                        guidMatched = true;
                        if (!_dragonFieldOffsets.TryGetValue(fieldName, out int offset))
                        {
                            // 该战斗组件没有此字段：记录并继续找同 GO 的其他组件（勿提前放弃）
                            unknownField = fieldName;
                            continue;
                        }

                        unsafe
                        {
                            if (_dragonFloatFields.Contains(fieldName))
                                *(float*)(compPtr + offset) = value;
                            else
                                *(int*)(compPtr + offset) = (int)value;
                        }

                        // 记录持久化：实体读档后重建、指针会变，按 stuff_id 匹配（与面板关联方式一致）
                        try
                        {
                            int stuffId = _dragonFieldOffsets.TryGetValue("stuff_id", out int sidOff)
                                ? ReadIl2CppInt(compPtr, sidOff) : 0;
                            if (stuffId > 0)
                                ModificationStore.RecordRaw($"dragone:{stuffId}:{fieldName}", value);
                        }
                        catch { }
                        return "ok";
                    }
                }
                catch { }
            }
            return unknownField != null ? $"unknown field: {unknownField}" : (guidMatched ? $"unknown field: {fieldName}" : "dragon not found");
        }
        catch (Exception ex) { return ex.Message; }
    }


    internal static void SearchMapDragonEntities()
    {
        try
        {

            var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
            Plugin.LogInfo($"[DragonEntity] 扫描 {allGOs.Length} 个 GameObject...");

            int found = 0;
            foreach (var go in allGOs)
            {
                try
                {
                    string goName = go.name;
                    // 匹配包含 dragon (不区分大小写) 的 GO 名称
                    if (!goName.ToLower().Contains("dragon")) continue;

                    found++;

                    // 找到所有龙GO，读取战斗属性
                    {
                        var components = go.GetComponents<UnityEngine.Component>();
                        foreach (var comp in components)
                        {
                            if (comp == null) continue;
                            IntPtr compPtr = GetIl2CppPtr(comp);
                            if (compPtr == IntPtr.Zero) continue;
                            IntPtr compClass = IntPtr.Zero;
                            try { compClass = Il2CppApi.GetClass(compPtr); } catch { }
                            if (compClass == IntPtr.Zero) continue;

                            var allFields = Il2CppApi.CollectFieldsInHierarchy(compClass);

                            // 只输出有 hp_total 字段的组件（战斗组件）
                            if (!allFields.Any(x => x.Name == "hp_total")) continue;

                            int stuffId = 0, guid = 0;
                            float hp = 0, hpTotal = 0, atkMax = 0, mAtkMax = 0, speed = 0, power = 0;
                            foreach (var (name, offset) in allFields)
                            {
                                try
                                {
                                    switch (name)
                                    {
                                        case "stuff_id": stuffId = ReadIl2CppInt(compPtr, offset); break;
                                        case "guid": guid = ReadIl2CppInt(compPtr, offset); break;
                                        case "hp": hp = ReadIl2CppFloat(compPtr, offset); break;
                                        case "hp_total": hpTotal = ReadIl2CppFloat(compPtr, offset); break;
                                        case "atk_max": atkMax = ReadIl2CppFloat(compPtr, offset); break;
                                        case "magic_atk_max": mAtkMax = ReadIl2CppFloat(compPtr, offset); break;
                                        case "speed": speed = ReadIl2CppFloat(compPtr, offset); break;
                                        case "power": power = ReadIl2CppFloat(compPtr, offset); break;
                                    }
                                }
                                catch { }
                            }
                            Plugin.LogInfo($"[DragonEntity] {goName} guid={guid} stuffId={stuffId} HP={hp}/{hpTotal} ATK={atkMax} MATK={mAtkMax} SPD={speed} PWR={power}");
                            break; // 每个GO只取一个战斗组件
                        }
                    }

                    if (found >= 30) break;
                }
                catch { }
            }
            Plugin.LogInfo($"[DragonEntity] 共找到 {found} 个龙 GO");
        }
        catch (Exception ex) { Plugin.LogError($"[DragonEntity] 搜索异常: {ex.Message}"); }
    }
}
