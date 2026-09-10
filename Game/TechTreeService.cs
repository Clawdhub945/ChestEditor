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

/// <summary>科技树：诊断、查询、解锁/锁定、研究</summary>
internal static class TechTreeService
{

    // ====== 科技树 ======

    internal static string DiagnoseTechTree()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return JsonBuilder.Error("Game.w is null");

            // 科技树存储在 Game.w 的多个字段中
            return JsonBuilder.Object(jw =>
            {
                jw.WritePropertyName("unlock_tech_list");
                WriteList(jw, GetProp(w, "unlock_tech_list"));

                jw.WritePropertyName("tech_has_paid");
                WriteList(jw, GetProp(w, "tech_has_paid"));

                jw.WritePropertyName("research_queue_list");
                WriteList(jw, GetProp(w, "research_queue_list"));

                jw.WriteNumber("cur_research_tech", GetInt(w, "cur_research_tech"));
                jw.WriteNumber("cur_research_progress", JsonBuilder.Safe(GetFloat(w, "cur_research_progress")));
                jw.WriteBoolean("is_unlock_tech_inspiration", GetBool(w, "is_unlock_tech_inspiration"));

                jw.WritePropertyName("unlock_facility_list");
                WriteList(jw, GetProp(w, "unlock_facility_list"));
            });
        }
        catch (Exception ex)
        {
            return JsonBuilder.Error(ex);
        }
    }



    /// <summary>
    /// 把游戏侧的 List 写成 JSON 数组。
    /// 先尝试 IList 直接访问；IL2CPP List&lt;T&gt; 无法直接转换，退回反射读 Count + get_Item。
    /// </summary>
    private static void WriteList(Utf8JsonWriter jw, object? list)
    {
        jw.WriteStartArray();
        if (list == null)
        {
            jw.WriteEndArray();
            return;
        }

        if (list is System.Collections.IList ilist)
        {
            for (int i = 0; i < ilist.Count; i++)
                WriteValue(jw, ilist[i]);
        }
        else
        {
            var listType = list.GetType();
            var countProp = listType.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var getItem = listType.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (countProp != null && getItem != null)
            {
                int count = Convert.ToInt32(countProp.GetValue(list) ?? 0);
                for (int i = 0; i < count; i++)
                {
                    object? item;
                    try { item = getItem.Invoke(list, new object[] { i }); }
                    catch { item = null; }
                    WriteValue(jw, item);
                }
            }
        }

        jw.WriteEndArray();
    }


    /// <summary>把反射读到的任意值写成 JSON 值（数字/布尔原样，其余转字符串）</summary>
    private static void WriteValue(Utf8JsonWriter jw, object? val)
    {
        switch (val)
        {
            case null: jw.WriteNullValue(); break;
            case int i: jw.WriteNumberValue(i); break;
            case long l: jw.WriteNumberValue(l); break;
            case short s: jw.WriteNumberValue(s); break;
            case byte b: jw.WriteNumberValue(b); break;
            case bool bo: jw.WriteBooleanValue(bo); break;
            case float f: jw.WriteNumberValue(JsonBuilder.Safe(f)); break;
            case double d: jw.WriteNumberValue(double.IsFinite(d) ? d : 0d); break;
            default: jw.WriteStringValue(val.ToString() ?? ""); break;
        }
    }


    internal static string GetTechTreeJson()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "{}";

            return JsonBuilder.Object(jw =>
            {
                // 已解锁科技列表
                jw.WritePropertyName("unlockTechList");
                WriteList(jw, GetProp(w, "unlock_tech_list"));

                // 已付费科技列表
                jw.WritePropertyName("techHasPaid");
                WriteList(jw, GetProp(w, "tech_has_paid"));

                // 研究队列
                jw.WritePropertyName("researchQueue");
                WriteList(jw, GetProp(w, "research_queue_list"));

                // 当前研究
                jw.WriteNumber("curResearchTech", GetInt(w, "cur_research_tech"));
                jw.WriteNumber("curResearchProgress", JsonBuilder.Safe(GetFloat(w, "cur_research_progress")));

                // 灵感解锁 + 其他相关解锁状态
                jw.WriteBoolean("isUnlockTechInspiration", GetBool(w, "is_unlock_tech_inspiration"));
                jw.WriteBoolean("isUnlockFreeLove", GetBool(w, "is_unlock_free_love"));
                jw.WriteBoolean("isUnlockCourage", GetBool(w, "is_unlock_courage"));
                jw.WriteBoolean("isUnlockPenaltySystem", GetBool(w, "is_unlock_penalty_system"));
                jw.WriteBoolean("isUnlockRewardsSystem", GetBool(w, "is_unlock_rewards_system"));
                jw.WriteBoolean("isUnlockPersonalAwareness", GetBool(w, "is_unlock_personal_awareness"));
                jw.WriteBoolean("isUnlockHearken", GetBool(w, "is_unlock_hearken"));
                jw.WriteBoolean("isUnlockSocialSupport", GetBool(w, "is_unlock_social_support"));
                jw.WriteBoolean("isUnlockEfficientStorage", GetBool(w, "is_unlock_efficient_storage"));
                jw.WriteBoolean("isUnlockEncyclopedia", GetBool(w, "is_unlock_encyclopedia"));
            });
        }
        catch (Exception ex)
        {
            return JsonBuilder.Error(ex);
        }
    }


    internal static string ToggleTechUnlock(int techId, bool unlock)
    {
        try
        {
            // 解锁走游戏自带的 TechHelper.UnlockNewTech（会同步处理设施解锁、通知等全部副作用，
            // 反编译 TechHelper__UnlockNewTech 确认；直接往 unlock_tech_list 塞 id 会漏掉这些联动）
            if (unlock)
            {
                var helper = GameContext.GetTechHelper();
                if (helper == null) return JsonBuilder.Error("tech_helper is null");
                IntPtr helperPtr = GetIl2CppPtr(helper);
                IntPtr cls = helperPtr != IntPtr.Zero ? GetClass(helperPtr) : IntPtr.Zero;
                IntPtr m = cls != IntPtr.Zero ? FindMethodInHierarchy(cls, "UnlockNewTech", 2) : IntPtr.Zero;
                if (m == IntPtr.Zero) return JsonBuilder.Error("UnlockNewTech method not found");
                Invoke(m, helperPtr, techId, false);
                return JsonBuilder.Ok("action", "added");
            }

            // 锁定：游戏没有对应入口，手动从 unlock_tech_list 移除
            var w = GameContext.GetGame();
            if (w == null) return JsonBuilder.Error("Game.w is null");

            var unlockList = GetProp(w, "unlock_tech_list");
            if (unlockList == null) return JsonBuilder.Error("unlock_tech_list is null");

            var listType = unlockList.GetType();
            var containsMethod = listType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var removeMethod = listType.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (containsMethod == null || removeMethod == null)
                return JsonBuilder.Error("List methods not found");

            bool contains = Convert.ToBoolean(containsMethod.Invoke(unlockList, new object[] { techId }));
            if (contains)
            {
                removeMethod.Invoke(unlockList, new object[] { techId });
                return JsonBuilder.Ok("action", "removed");
            }
            return JsonBuilder.Ok("action", "unchanged");
        }
        catch (Exception ex)
        {
            return JsonBuilder.Error(ex);
        }
    }


    /// <summary>
    /// 调用游戏自带 TechHelper.UnlockAllTech(except_equip)。
    /// 游戏内该方法会遍历 D.Ins.tech_tree_dic / tech_list_dic 并处理全部解锁副作用
    /// （新设施列表、通知、事件科技），替代旧的 130 个硬编码 techId 手动塞表。
    /// </summary>
    internal static string UnlockAllTechs()
    {
        try
        {
            var helper = GameContext.GetTechHelper();
            if (helper == null) return JsonBuilder.Error("tech_helper is null (需先读档进入游戏)");

            IntPtr helperPtr = GetIl2CppPtr(helper);
            if (helperPtr == IntPtr.Zero) return JsonBuilder.Error("tech_helper pointer invalid");

            IntPtr cls = GetClass(helperPtr);
            IntPtr m = FindMethodInHierarchy(cls, "UnlockAllTech", 1);
            if (m == IntPtr.Zero) return JsonBuilder.Error("UnlockAllTech method not found");

            Invoke(m, helperPtr, false); // except_equip=false：连装备类科技一起解锁
            return JsonBuilder.Ok();
        }
        catch (Exception ex)
        {
            return JsonBuilder.Error(ex);
        }
    }


    internal static string SetResearchTech(int techId)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return JsonBuilder.Error("Game.w is null");

            var wType = w.GetType();
            var prop = wType.GetProperty("cur_research_tech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(w, techId);
                return JsonBuilder.Ok();
            }
            var field = wType.GetField("cur_research_tech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (field != null)
            {
                field.SetValue(w, techId);
                return JsonBuilder.Ok();
            }
            return JsonBuilder.Error("cur_research_tech not writable");
        }
        catch (Exception ex)
        {
            return JsonBuilder.Error(ex);
        }
    }
}
