using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static ChestEditor.Core.JsonUtil;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// NPC 编辑：NPC 列表、字段修改、speed/hp/hp_total 持续覆盖（每帧重应用）
/// </summary>
internal static class NpcEditor
{

    // ========== 持续覆盖（速度、血量、血量上限） ==========

    private static readonly Dictionary<int, float> _speedOverrides = new(); // key: npcId
    private static readonly Dictionary<int, float> _hpOverrides = new();
    private static readonly Dictionary<int, float> _hpTotalOverrides = new();


    internal static void SetSpeedOverride(int npcId, float speed) { if (npcId > 0) _speedOverrides[npcId] = speed; }
    internal static void SetHpOverride(int npcId, float hp) { if (npcId > 0) _hpOverrides[npcId] = hp; }
    internal static void SetHpTotalOverride(int npcId, float hpTotal) { if (npcId > 0) _hpTotalOverrides[npcId] = hpTotal; }


    /// <summary>
    /// 在 UpdateAgentMoveSpeed 后被调用，重新应用覆盖值
    /// </summary>
    internal static void OnPostUpdate(IntPtr instancePtr)
    {
        try
        {
            var entity = EntityScan.FindEntityByPtr(instancePtr);
            if (entity == null || entity.NpcId <= 0) return;
            int npcId = entity.NpcId;

            // 速度覆盖
            if (_speedOverrides.TryGetValue(npcId, out float sp) && entity.FieldMeta.TryGetValue("move_agent", out var maFe) && maFe.IsPointer)
            {
                IntPtr moveAgentPtr;
                unsafe { moveAgentPtr = *(IntPtr*)(instancePtr + maFe.Offset); }
                if (moveAgentPtr != IntPtr.Zero)
                {
                    IntPtr maClass = Il2CppApi.GetClass(moveAgentPtr);
                    IntPtr mth = Il2CppInvoke.FindMethodInHierarchy(maClass, "SetSpeed", 1);
                    if (mth != IntPtr.Zero) Il2CppInvoke.Invoke(mth, moveAgentPtr, sp);
                }
            }

            // 血量覆盖
            if (_hpOverrides.TryGetValue(npcId, out float hpVal))
            {
                if (entity.FieldMeta.TryGetValue("hp", out var hpFe) && !hpFe.IsPointer)
                {
                    WriteIl2CppFloat(instancePtr, hpFe.Offset, hpVal);
                }
            }

            // 血量上限覆盖
            if (_hpTotalOverrides.TryGetValue(npcId, out float hpTotalVal))
            {
                if (entity.FieldMeta.TryGetValue("hp_total", out var htFe) && !htFe.IsPointer)
                {
                    WriteIl2CppFloat(instancePtr, htFe.Offset, hpTotalVal);
                }
            }
        }
        catch { }
    }


    /// <summary>
    /// 返回我方 NPC 列表 JSON（className=Npc, hometownKingdomId=1）
    /// </summary>
    internal static string CachedListJson = "[]";

    internal static string GetNpcListJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var e in EntityScan.Entities)
        {
            // 只显示 NPC 类型（含 Soldier、BattleUnit 等敌兵类）
            bool isNpcType = e.ClassName == "Npc" || e.ClassName.Contains("Soldier") || e.ClassName.Contains("BattleUnit");
            if (!isNpcType) continue;

            if (!first) sb.Append(',');
            first = false;

            sb.Append('{');
            sb.Append($"\"ptrHash\":{e.PtrHash},");
            sb.Append($"\"guid\":{e.Guid},");
            sb.Append($"\"npcId\":{e.NpcId},");
            sb.Append($"\"stuffId\":{e.StuffId},");
            sb.Append($"\"npcName\":\"{Escape(e.NpcName)}\",");
            sb.Append($"\"soldierTypeId\":{e.SoldierTypeId},");
            sb.Append($"\"soldierTypeName\":\"{Escape(EntityScan.GetSoldierTypeName(e.SoldierTypeId))}\",");
            sb.Append($"\"name\":\"{Escape(ItemCatalog.GetName(e.StuffId))}\",");

            // 读取重点字段
            int slimCount = 0;
            int pointerCount = 0;
            foreach (var kv in e.FieldMeta)
                if (!kv.Value.IsPointer) slimCount++; else pointerCount++;
            sb.Append($"\"fieldCount\":{slimCount},");

            // speed, hp, hp_total
            if (e.FieldMeta.TryGetValue("speed", out var speedFe) && speedFe.IsFloat)
                sb.Append($"\"speed\":{ReadIl2CppFloat(e.Ptr, speedFe.Offset).ToString("G")},");
            if (e.FieldMeta.TryGetValue("hp", out var hpFe) && hpFe.IsFloat)
                sb.Append($"\"hp\":{ReadIl2CppFloat(e.Ptr, hpFe.Offset).ToString("G")},");
            if (e.FieldMeta.TryGetValue("hp_total", out var hpTotalFe) && hpTotalFe.IsFloat)
                sb.Append($"\"hpTotal\":{ReadIl2CppFloat(e.Ptr, hpTotalFe.Offset).ToString("G")},");

            // 尝试读取 age（可能是 int 或 float）
            if (e.FieldMeta.TryGetValue("age", out var ageFe))
            {
                if (ageFe.IsFloat)
                    sb.Append($"\"age\":{ReadIl2CppFloat(e.Ptr, ageFe.Offset).ToString("G")},");
                else if (!ageFe.IsString && !ageFe.IsPointer)
                    sb.Append($"\"age\":{ReadIl2CppInt(e.Ptr, ageFe.Offset)},");
            }

            // 读取 _npc_type
            if (e.FieldMeta.TryGetValue("_npc_type", out var npcTypeFe) && !npcTypeFe.IsString && !npcTypeFe.IsPointer)
                sb.Append($"\"npcType\":{ReadIl2CppInt(e.Ptr, npcTypeFe.Offset)},");

            // 阵营判断辅助字段
            sb.Append($"\"hometownKingdomId\":{e.HometownKingdomId},");
            sb.Append($"\"soldierTypeId\":{e.SoldierTypeId},");

            // 去掉末尾多余逗号
            if (sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append('}');
        }
        sb.Append(']');
        CachedListJson = sb.ToString();
        return CachedListJson;
    }


    /// <summary>
    /// 设置 NPC 字段并记录修改（用于读档重应用）
    /// </summary>
    internal static string SetNpcField(int ptrHash, string fieldName, float value)
    {
        string result = EntityScan.SetField(ptrHash, fieldName, value);
        if (result == "ok")
        {
            foreach (var e in EntityScan.Entities)
            {
                if (e.PtrHash == ptrHash)
                {
                    // 记录修改
                    if (e.Guid > 0)
                        ModificationStore.RecordModification(e.Guid, fieldName, value);
                    else if (e.NpcId > 0)
                        ModificationStore.RecordModificationById(e.NpcId, fieldName, value);
                    // 应用到游戏
                    ApplyNpcFieldChange(e, fieldName, value);
                    break;
                }
            }
        }
        return result;
    }


    internal static void ApplyNpcFieldChange(EntityScan.EditorEntity e, string fieldName, float value)
    {
        try
        {
            if (fieldName == "speed")
            {
                if (e.NpcId > 0) SetSpeedOverride(e.NpcId, value);
                // 读取 move_agent 字段并调用 SetSpeed
                if (e.FieldMeta.TryGetValue("move_agent", out var maFe) && maFe.IsPointer)
                {
                    IntPtr moveAgentPtr;
                    unsafe { moveAgentPtr = *(IntPtr*)(e.Ptr + maFe.Offset); }
                    if (moveAgentPtr != IntPtr.Zero)
                    {
                        IntPtr maClass = Il2CppApi.GetClass(moveAgentPtr);
                        IntPtr setSpeedMth = Il2CppInvoke.FindMethodInHierarchy(maClass, "SetSpeed", 1);
                        if (setSpeedMth != IntPtr.Zero)
                        {
                            Il2CppInvoke.Invoke(setSpeedMth, moveAgentPtr, value);
                            Plugin.LogInfo($"[ApplyNpcFieldChange] speed={value} -> move_agent.SetSpeed OK");
                        }
                    }
                }
            }
            else if (fieldName == "hp")
            {
                if (e.NpcId > 0) SetHpOverride(e.NpcId, value);
                // 调用 SetHp(float) 方法
                IntPtr compClass = Il2CppApi.GetClass(e.Ptr);
                IntPtr setHpMth = Il2CppInvoke.FindMethodInHierarchy(compClass, "SetHp", 1);
                if (setHpMth != IntPtr.Zero)
                {
                    Il2CppInvoke.Invoke(setHpMth, e.Ptr, value);
                    Plugin.LogInfo($"[ApplyNpcFieldChange] hp={value} -> SetHp OK");
                }
            }
            else if (fieldName == "hp_total")
            {
                if (e.NpcId > 0) SetHpTotalOverride(e.NpcId, value);
                // 调用 UpdateHpProgressBarTotal()
                IntPtr compClass = Il2CppApi.GetClass(e.Ptr);
                IntPtr mth = Il2CppInvoke.FindMethodInHierarchy(compClass, "UpdateHpProgressBarTotal", 0);
                if (mth != IntPtr.Zero)
                {
                    Il2CppInvoke.Invoke(mth, e.Ptr);
                    Plugin.LogInfo($"[ApplyNpcFieldChange] hp_total={value} -> UpdateHpProgressBarTotal OK");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[ApplyNpcFieldChange] {fieldName}={value} 失败: {ex.Message}");
        }
    }

}
