using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 统一实体编辑器 - 扫描(含stuff_id)和NPC查找(类名含Npc)，实体列表与字段读写
/// </summary>
internal static class EntityScan
{
    private static readonly List<EditorEntity> _entities = new();

    // ptrHash -> 实体索引（ScanAll 时重建；OnPostUpdate 每帧查询用，替代 O(n) 线性查找）
    private static readonly Dictionary<int, EditorEntity> _byPtrHash = new();


    // NPC 类名关键词
    private static readonly string[] NpcClassKeywords = { "Npc" };


    /// <summary>士兵类型名（数据见 Data/soldier_types.json，由 _tools/generate_data_tables.py 生成）</summary>
    internal static string GetSoldierTypeName(int id) => DataTables.SoldierTypeName(id);


    internal class EditorEntity
    {
        public string GoName = "";
        public string ClassName = "";
        public string NpcName = "";
        public int HometownKingdomId;
        public int TerritoryKingdomId;
        public int KingdomId; // 实体自身的 kingdom_id（Ship 直接持有；Territory.IsMyTerritory 即用此字段判敌我）
        public string StuffNameWithIdIndex = "";
        public int SoldierTypeId;
        public IntPtr Ptr;
        public int PtrHash;
        public int Guid;
        public int NpcId; // Soldier.npc_id，用于 NPC 持久化标识
        public int StuffId;
        public GameObject? GoRef;
        public Component? CompRef;
        public Dictionary<string, Il2CppField> FieldMeta = new();
    }


    /// <summary>
    /// 统一扫描：实体扫描(stuff_id) + NPC查找(类名含Npc)，按ptrHash去重
    /// </summary>
    internal static void ScanAll(GameObject[]? source = null)
    {
        _byPtrHash.Clear();
        try { _entities.Clear(); } catch (Exception ex) { Plugin.LogError($"[EntityEditor] Clear error: {ex.Message}"); return; }
        Il2CppApi.ClearClassFieldCache();
        try
        {
            GameObject[] allGOs;
            try { allGOs = source ?? Resources.FindObjectsOfTypeAll<GameObject>(); }
            catch (Exception ex) { Plugin.LogError($"[EntityEditor] FindObjectsOfTypeAll error: {ex.Message}"); return; }

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
                        try { compClass = Il2CppApi.GetClass(compPtr); }
                        catch { continue; }
                        if (compClass == IntPtr.Zero) continue;

                        string className = comp.GetIl2CppType().Name;
                        var fieldMap = Il2CppApi.GetClassFieldsCached(compClass, className);
                        if (fieldMap.Count == 0)
                            fieldMap = Il2CppApi.GetClassFieldsCached(compClass, className, force: true);

                        // 判断是否匹配：有 stuff_id 字段 且 stuffId>0,guid>0  OR  类名含 Npc/Soldier/BattleUnit
                        bool hasStuffId = fieldMap.ContainsKey("stuff_id");
                        bool isNpc = (className.Contains("Npc") || className.Contains("Soldier") || className.Contains("BattleUnit"))
                            && !className.Contains("NpcHelper") && !className.Contains("NpcTask") && !className.Contains("NpcFinder");

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

                        // 读取 stuff_name_with_id_index（仅 Facility/Ship 类型，避免对 NPC 调用导致 native crash）
                        string stuffNameWithIdIndex = "";
                        if (className.Contains("Facility") || className.Contains("Ship"))
                        {
                            try
                            {
                                // 先尝试直接读字段（可能已被缓存）
                                if (fieldMap.TryGetValue("stuff_name_with_id_index", out var snFe) && snFe.IsString)
                                    stuffNameWithIdIndex = ReadIl2CppString(compPtr, snFe.Offset) ?? "";
                                // 字段为空则调用 GetFacilityNameWithIdIndex() 触发计算
                                if (string.IsNullOrEmpty(stuffNameWithIdIndex))
                                    stuffNameWithIdIndex = InvokeStringByName(compPtr, compClass, "GetFacilityNameWithIdIndex") ?? "";
                            }
                            catch { }
                        }

                        // 读取 soldier_type_id
                        int soldierTypeId = 0;
                        if (fieldMap.TryGetValue("soldier_type_id", out var stFe))
                            try { soldierTypeId = ReadIl2CppInt(compPtr, stFe.Offset); } catch { }

                        // 读取 npc_id（Soldier 类字段，用于 NPC 持久化标识）
                        int npcId = 0;
                        if (fieldMap.TryGetValue("npc_id", out var npcIdFe))
                            try { npcId = ReadIl2CppInt(compPtr, npcIdFe.Offset); } catch { }

                        // 读取实体自身的 kingdom_id（Ship 有该字段：Ship.SetInfo 里
                        // kingdom_id = territory.kingdom_id，Territory.IsMyTerritory 也用它判敌我）。
                        // Npc 用的是 hometown_kingdom_id，这里读不到就保持 0。
                        int kingdomId = 0;
                        if (fieldMap.TryGetValue("kingdom_id", out var kFe) && !kFe.IsString && !kFe.IsPointer)
                            try { kingdomId = ReadIl2CppInt(compPtr, kFe.Offset); } catch { }

                        seenPtrHash.Add(ptrHash);

                        var entity = new EditorEntity
                        {
                            GoName = go.name,
                            ClassName = className,
                            NpcName = npcName,
                            HometownKingdomId = hometownKingdomId,
                            KingdomId = kingdomId,
                            StuffNameWithIdIndex = stuffNameWithIdIndex,
                            SoldierTypeId = soldierTypeId,
                            Ptr = compPtr,
                            PtrHash = ptrHash,
                            Guid = guid,
                            NpcId = npcId,
                            StuffId = stuffId,
                            GoRef = go,
                            CompRef = comp,
                            FieldMeta = fieldMap
                        };

                        // 读取实体所属 territory 的 kingdom_id（territory / _territory 两个候选名）
                        try
                        {
                            IntPtr territoryPtr = Il2CppApi.ReadPointerFieldAny(compPtr, compClass, "territory", "_territory");
                            if (territoryPtr != IntPtr.Zero)
                            {
                                IntPtr tClass = Il2CppApi.GetClass(territoryPtr);
                                entity.TerritoryKingdomId = ReadIntFieldSafe(territoryPtr, tClass, "kingdom_id", 0);
                            }
                        }
                        catch { }

                        // NPC 的 hometownKingdomId 优先，否则用 territory 的 kingdom_id
                        if (entity.HometownKingdomId == 0 && entity.TerritoryKingdomId != 0)
                            entity.HometownKingdomId = entity.TerritoryKingdomId;

                        _entities.Add(entity);
                        _byPtrHash[entity.PtrHash] = entity;
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
        }
        catch (Exception ex) { Plugin.LogError($"[EntityEditor] 异常: {ex.Message}\n{ex.StackTrace}"); }
    }


    /// <summary>
    /// 返回实体列表 JSON（精简字段数）
    /// </summary>
    internal static string GetAllJson() => JsonBuilder.Build(jw =>
    {
        jw.WriteStartArray();
        foreach (var e in _entities)
        {
            // 计算精简字段数（排除pointer）
            int slimCount = 0;
            foreach (var kv in e.FieldMeta)
                if (!kv.Value.IsPointer) slimCount++;

            jw.WriteStartObject();
            jw.WriteString("goName", e.GoName);
            jw.WriteString("className", e.ClassName);
            jw.WriteString("npcName", e.NpcName);
            jw.WriteString("stuffNameWithIdIndex", e.StuffNameWithIdIndex);
            jw.WriteNumber("soldierTypeId", e.SoldierTypeId);
            jw.WriteString("soldierTypeName", GetSoldierTypeName(e.SoldierTypeId));
            jw.WriteNumber("hometownKingdomId", e.HometownKingdomId);
            jw.WriteNumber("territoryKingdomId", e.TerritoryKingdomId);
            jw.WriteNumber("kingdomId", e.KingdomId);
            jw.WriteNumber("ptrHash", e.PtrHash);
            jw.WriteNumber("guid", e.Guid);
            jw.WriteNumber("stuffId", e.StuffId);
            jw.WriteString("name", DataTables.ItemName(e.StuffId));
            jw.WriteNumber("fieldCount", slimCount);
            jw.WriteEndObject();
        }
        jw.WriteEndArray();
    });


    /// <summary>
    /// 返回单个实体的精简字段值（排除pointer字段）
    /// </summary>
    internal static string GetFieldsJson(int ptrHash)
    {
        foreach (var e in _entities)
        {
            if (e.PtrHash != ptrHash) continue;

            return JsonBuilder.Object(jw =>
            {
                foreach (var kv in e.FieldMeta)
                {
                    if (kv.Value.IsPointer) continue; // 跳过pointer字段

                    jw.WritePropertyName(kv.Key);
                    jw.WriteStartObject();
                    jw.WriteBoolean("isFloat", kv.Value.IsFloat);
                    jw.WriteBoolean("isString", kv.Value.IsString);
                    jw.WriteString("typeName", kv.Value.TypeName);
                    jw.WritePropertyName("value");
                    try
                    {
                        if (kv.Value.IsString)
                            jw.WriteStringValue(ReadIl2CppString(e.Ptr, kv.Value.Offset) ?? "");
                        else if (kv.Value.IsFloat)
                            jw.WriteNumberValue(JsonBuilder.Safe(ReadIl2CppFloat(e.Ptr, kv.Value.Offset)));
                        else
                            jw.WriteNumberValue(ReadIl2CppInt(e.Ptr, kv.Value.Offset));
                    }
                    catch
                    {
                        // 保持旧实现的降级行为：字符串写 ""，数字写 0
                        if (kv.Value.IsString) jw.WriteStringValue("");
                        else jw.WriteNumberValue(0);
                    }
                    jw.WriteEndObject();
                }
            });
        }
        return JsonBuilder.Error("not found");
    }


    /// <summary>
    /// 获取实体的世界坐标 JSON
    /// </summary>
    internal static string GetEntityPositionJson(int ptrHash)
    {
        foreach (var e in _entities)
        {
            if (e.PtrHash != ptrHash) continue;
            if (e.GoRef == null) return JsonBuilder.Error("no GameObject");
            try
            {
                var pos = e.GoRef.transform.position;
                return JsonBuilder.Object(jw =>
                {
                    jw.WriteNumber("x", JsonBuilder.Safe(pos.x));
                    jw.WriteNumber("y", JsonBuilder.Safe(pos.y));
                });
            }
            catch (Exception ex) { return JsonBuilder.Error(ex); }
        }
        return JsonBuilder.Error("entity not found");
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
                IntPtr cls = Il2CppApi.GetClass(e.Ptr);
                sb.AppendLine($"ptr={e.Ptr} classPtr={cls}");
                if (cls == IntPtr.Zero) return "classPtr is null";
                sb.AppendLine($"className={Il2CppApi.GetClassName(cls)}");
                int depth = 0;
                while (cls != IntPtr.Zero && depth < 10)
                {
                    sb.AppendLine($"=== {Il2CppApi.GetClassName(cls)} (depth={depth}) ===");
                    var methods = Il2CppApi.EnumerateMethods(cls);
                    foreach (var m in methods) sb.AppendLine($"  {m.Name}");
                    sb.AppendLine($"  (total: {methods.Count})");
                    cls = Il2CppApi.GetParent(cls);
                    depth++;
                }
                return sb.ToString();
            }
            catch (Exception ex) { return ex.Message; }
        }
        return "not found";
    }


    internal static EditorEntity? FindEntityByPtr(IntPtr ptr)
        => _byPtrHash.TryGetValue(ptr.GetHashCode(), out var e) ? e : null;


    internal static List<EditorEntity> Entities => _entities;

    /// <summary>按 ptrHash 查找已扫描实体</summary>
    internal static EditorEntity? FindByPtrHash(int ptrHash)
        => _byPtrHash.TryGetValue(ptrHash, out var e) ? e : null;

}
