using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 字段修改记录：内存记录 + 磁盘持久化 + 读档/扫描后重应用
/// </summary>
internal static class ModificationStore
{

    // ====== 修改记录 + 读档重应用 (方案3) ======

    // key: "guid:fieldName", value: float value
    // 前缀约定: "npc:{npcId}:{field}"=NPC按npcId | "{guid}:{field}"=NPC按guid
    //           "dragon:{soulGuid}:{field}"=龙魂强化(跨读档稳定的字符串GUID)
    //           "dragone:{stuffId}:{field}"=地图龙实体战斗属性(按stuff_id匹配)
    private static readonly Dictionary<string, float> _pendingModifications = new();


    /// <summary>按前缀约定记录任意修改，立即落盘</summary>
    internal static void RecordRaw(string key, float value)
    {
        if (string.IsNullOrEmpty(key)) return;
        _pendingModifications[key] = value;
        SaveToDisk();
    }

    /// <summary>是否有 NPC 类待恢复记录（npc: 前缀 或 裸 guid: 键）</summary>
    internal static bool HasNpcRecords()
    {
        foreach (var kv in _pendingModifications)
            if (!kv.Key.StartsWith("dragon:", StringComparison.Ordinal) && !kv.Key.StartsWith("dragone:", StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>取某前缀下的全部修改（返回完整 key，前缀后的部分自行解析）</summary>
    internal static List<KeyValuePair<string, float>> GetByPrefix(string prefix)
    {
        var list = new List<KeyValuePair<string, float>>();
        if (_pendingModifications.Count == 0) return list;
        foreach (var kv in _pendingModifications)
            if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                list.Add(kv);
        return list;
    }


    internal static void RecordModification(int guid, string field, float value)
    {
        if (guid <= 0 || string.IsNullOrEmpty(field)) return;
        string key = $"{guid}:{field}";
        _pendingModifications[key] = value;
        SaveToDisk();
    }


    internal static void RecordModificationById(int npcId, string field, float value)
    {
        if (npcId <= 0 || string.IsNullOrEmpty(field)) return;
        string key = $"npc:{npcId}:{field}";
        _pendingModifications[key] = value;
        SaveToDisk();
    }


    /// <summary>
    /// 将 _pendingModifications 应用到当前 _entities（不扫描）
    /// </summary>
    internal static void ApplyPendingModifications()
    {
        if (_pendingModifications.Count == 0) return;

        int applied = 0, missed = 0;
        foreach (var entry in _pendingModifications.ToList())
        {
            string key = entry.Key;
            float value = entry.Value;
            EntityScan.EditorEntity? match = null;
            string fieldName, logId;

            if (key.StartsWith("npc:"))
            {
                int firstColon = key.IndexOf(':', 4);
                if (firstColon <= 4) { missed++; continue; }
                if (!int.TryParse(key.Substring(4, firstColon - 4), out int npcId)) { missed++; continue; }
                fieldName = key.Substring(firstColon + 1);
                logId = $"npcId={npcId}";
                foreach (var e in EntityScan.Entities) { if (e.NpcId == npcId) { match = e; break; } }
            }
            else
            {
                int colonIdx = key.IndexOf(':');
                if (colonIdx <= 0) { missed++; continue; }
                if (!int.TryParse(key.Substring(0, colonIdx), out int guid)) { missed++; continue; }
                fieldName = key.Substring(colonIdx + 1);
                logId = $"guid={guid}";
                foreach (var e in EntityScan.Entities) { if (e.Guid == guid) { match = e; break; } }
            }

            if (match == null) { missed++; continue; }
            if (!match.FieldMeta.TryGetValue(fieldName, out var fe) || fe.IsPointer) { missed++; continue; }

            try
            {
                if (fe.IsFloat) WriteIl2CppFloat(match.Ptr, fe.Offset, value);
                else WriteIl2CppInt(match.Ptr, fe.Offset, (int)value);
                NpcEditor.ApplyNpcFieldChange(match, fieldName, value);
                applied++;
            }
            catch { missed++; }
        }
    }


    /// <summary>
    /// 读档后重新应用所有记录的修改（主线程调用）
    /// </summary>
    internal static string ReapplyModifications()
    {
        if (_pendingModifications.Count == 0)
            return JsonBuilder.Object(w => { w.WriteBoolean("ok", true); w.WriteNumber("applied", 0); });
        EntityScan.ScanAll();
        ApplyPendingModifications();
        return JsonBuilder.Ok();
    }

    private static string GetSavePath()
    {
        return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "ChestEditor_modifications.json");
    }

    /// <summary>落盘（System.Text.Json；与旧版手拼格式完全兼容）</summary>
    internal static void SaveToDisk()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_pendingModifications,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            System.IO.File.WriteAllText(GetSavePath(), json);
        }
        catch (Exception ex) { Plugin.LogError($"[ModificationStore] 保存修改失败: {ex.Message}"); }
    }

    /// <summary>启动时加载（兼容旧手写解析器生成的文件），并恢复 speed/hp/hp_total 覆盖</summary>
    internal static void LoadFromDisk()
    {
        try
        {
            string path = GetSavePath();
            if (!System.IO.File.Exists(path)) return;
            string json = System.IO.File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json) || json == "{}") return;

            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, float>>(json);
            if (loaded == null) return;
            _pendingModifications.Clear();
            foreach (var kv in loaded)
                _pendingModifications[kv.Key] = kv.Value;

            // 提取 npc: 覆盖
            foreach (var kv in _pendingModifications)
            {
                if (!kv.Key.StartsWith("npc:")) continue;
                int firstColon = kv.Key.IndexOf(':', 4);
                if (firstColon <= 4) continue;
                if (!int.TryParse(kv.Key.Substring(4, firstColon - 4), out int npcId) || npcId <= 0) continue;
                string field = kv.Key.Substring(firstColon + 1);
                if (field == "speed") NpcEditor.SetSpeedOverride(npcId, kv.Value);
                else if (field == "hp") NpcEditor.SetHpOverride(npcId, kv.Value);
                else if (field == "hp_total") NpcEditor.SetHpTotalOverride(npcId, kv.Value);
            }
            Plugin.LogInfo($"[ModificationStore] 已加载 {_pendingModifications.Count} 条修改记录");
        }
        catch (Exception ex) { Plugin.LogError($"[ModificationStore] 加载修改失败: {ex.Message}"); }
    }

}
