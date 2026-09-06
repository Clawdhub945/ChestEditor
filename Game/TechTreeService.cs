using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChestEditor.Core.JsonUtil;
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
            if (w == null) return "{\"error\":\"Game.w is null\"}";

            // 科技树存储在 Game.w 的多个字段中
            var sb = new System.Text.StringBuilder();
            sb.Append('{');

            // 读取 unlock_tech_list
            var unlockList = GetProp(w, "unlock_tech_list");
            sb.Append("\"unlock_tech_list\":");
            sb.Append(SerializeList(unlockList));

            // 读取 tech_has_paid
            var paidList = GetProp(w, "tech_has_paid");
            sb.Append(",\"tech_has_paid\":");
            sb.Append(SerializeList(paidList));

            // 读取 research_queue_list
            var queueList = GetProp(w, "research_queue_list");
            sb.Append(",\"research_queue_list\":");
            sb.Append(SerializeList(queueList));

            // 读取简单字段
            sb.Append(",\"cur_research_tech\":").Append(GetInt(w, "cur_research_tech"));
            sb.Append(",\"cur_research_progress\":").Append(GetFloat(w, "cur_research_progress").ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"is_unlock_tech_inspiration\":").Append(GetBool(w, "is_unlock_tech_inspiration") ? "true" : "false");

            // 读取 unlock_facility_list
            var facilityList = GetProp(w, "unlock_facility_list");
            sb.Append(",\"unlock_facility_list\":");
            sb.Append(SerializeList(facilityList));

            sb.Append('}');
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{Escape(ex.Message)}\"}}";
        }
    }



    private static string SerializeList(object? list)
    {
        if (list == null) return "[]";
        var sb = new System.Text.StringBuilder();
        sb.Append('[');

        // 先尝试直接 IList cast
        if (list is System.Collections.IList ilist)
        {
            for (int i = 0; i < ilist.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendValue(sb, ilist[i]);
            }
        }
        else
        {
            // IL2CPP List<T> 不能直接 cast，用反射读取 Count + get_Item
            var listType = list.GetType();
            var countProp = listType.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var getItem = listType.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (countProp != null && getItem != null)
            {
                int count = Convert.ToInt32(countProp.GetValue(list) ?? 0);
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(',');
                    try
                    {
                        var item = getItem.Invoke(list, new object[] { i });
                        AppendValue(sb, item);
                    }
                    catch { sb.Append("null"); }
                }
            }
        }

        sb.Append(']');
        return sb.ToString();
    }


    private static void AppendValue(System.Text.StringBuilder sb, object? val)
    {
        if (val == null) { sb.Append("null"); return; }
        if (val is int || val is long || val is short || val is byte)
            sb.Append(val);
        else if (val is bool b)
            sb.Append(b ? "true" : "false");
        else if (val is float f)
            sb.Append(f.ToString(System.Globalization.CultureInfo.InvariantCulture));
        else if (val is double d)
            sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
        else
            sb.Append('"').Append(Escape(val.ToString() ?? "")).Append('"');
    }


    internal static string GetTechTreeJson()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "{}";

            var sb = new System.Text.StringBuilder();
            sb.Append('{');

            // 已解锁科技列表
            var unlockList = GetProp(w, "unlock_tech_list");
            sb.Append("\"unlockTechList\":");
            sb.Append(SerializeList(unlockList));

            // 已付费科技列表
            var paidList = GetProp(w, "tech_has_paid");
            sb.Append(",\"techHasPaid\":");
            sb.Append(SerializeList(paidList));

            // 研究队列
            var queueList = GetProp(w, "research_queue_list");
            sb.Append(",\"researchQueue\":");
            sb.Append(SerializeList(queueList));

            // 当前研究
            sb.Append(",\"curResearchTech\":").Append(GetInt(w, "cur_research_tech"));
            sb.Append(",\"curResearchProgress\":").Append(GetFloat(w, "cur_research_progress").ToString(System.Globalization.CultureInfo.InvariantCulture));

            // 灵感解锁
            sb.Append(",\"isUnlockTechInspiration\":").Append(GetBool(w, "is_unlock_tech_inspiration") ? "true" : "false");

            // 其他相关解锁状态
            sb.Append(",\"isUnlockFreeLove\":").Append(GetBool(w, "is_unlock_free_love") ? "true" : "false");
            sb.Append(",\"isUnlockCourage\":").Append(GetBool(w, "is_unlock_courage") ? "true" : "false");
            sb.Append(",\"isUnlockPenaltySystem\":").Append(GetBool(w, "is_unlock_penalty_system") ? "true" : "false");
            sb.Append(",\"isUnlockRewardsSystem\":").Append(GetBool(w, "is_unlock_rewards_system") ? "true" : "false");
            sb.Append(",\"isUnlockPersonalAwareness\":").Append(GetBool(w, "is_unlock_personal_awareness") ? "true" : "false");
            sb.Append(",\"isUnlockHearken\":").Append(GetBool(w, "is_unlock_hearken") ? "true" : "false");
            sb.Append(",\"isUnlockSocialSupport\":").Append(GetBool(w, "is_unlock_social_support") ? "true" : "false");
            sb.Append(",\"isUnlockEfficientStorage\":").Append(GetBool(w, "is_unlock_efficient_storage") ? "true" : "false");
            sb.Append(",\"isUnlockEncyclopedia\":").Append(GetBool(w, "is_unlock_encyclopedia") ? "true" : "false");

            sb.Append('}');
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{Escape(ex.Message)}\"}}";
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
                if (helper == null) return "{\"error\":\"tech_helper is null\"}";
                IntPtr helperPtr = GetIl2CppPtr(helper);
                IntPtr cls = helperPtr != IntPtr.Zero ? GetClass(helperPtr) : IntPtr.Zero;
                IntPtr m = cls != IntPtr.Zero ? FindMethodInHierarchy(cls, "UnlockNewTech", 2) : IntPtr.Zero;
                if (m == IntPtr.Zero) return "{\"error\":\"UnlockNewTech method not found\"}";
                Invoke(m, helperPtr, techId, false);
                return "{\"ok\":true,\"action\":\"added\"}";
            }

            // 锁定：游戏没有对应入口，手动从 unlock_tech_list 移除
            var w = GameContext.GetGame();
            if (w == null) return "{\"error\":\"Game.w is null\"}";

            var unlockList = GetProp(w, "unlock_tech_list");
            if (unlockList == null) return "{\"error\":\"unlock_tech_list is null\"}";

            var listType = unlockList.GetType();
            var containsMethod = listType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var removeMethod = listType.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (containsMethod == null || removeMethod == null)
                return "{\"error\":\"List methods not found\"}";

            bool contains = Convert.ToBoolean(containsMethod.Invoke(unlockList, new object[] { techId }));
            if (contains)
            {
                removeMethod.Invoke(unlockList, new object[] { techId });
                return "{\"ok\":true,\"action\":\"removed\"}";
            }
            return "{\"ok\":true,\"action\":\"unchanged\"}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{Escape(ex.Message)}\"}}";
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
            if (helper == null) return "{\"error\":\"tech_helper is null (需先读档进入游戏)\"}";

            IntPtr helperPtr = GetIl2CppPtr(helper);
            if (helperPtr == IntPtr.Zero) return "{\"error\":\"tech_helper pointer invalid\"}";

            IntPtr cls = GetClass(helperPtr);
            IntPtr m = FindMethodInHierarchy(cls, "UnlockAllTech", 1);
            if (m == IntPtr.Zero) return "{\"error\":\"UnlockAllTech method not found\"}";

            Invoke(m, helperPtr, false); // except_equip=false：连装备类科技一起解锁
            return "{\"ok\":true}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{Escape(ex.Message)}\"}}";
        }
    }


    internal static string SetResearchTech(int techId)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "{\"error\":\"Game.w is null\"}";

            var wType = w.GetType();
            var prop = wType.GetProperty("cur_research_tech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(w, techId);
                return "{\"ok\":true}";
            }
            var field = wType.GetField("cur_research_tech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (field != null)
            {
                field.SetValue(w, techId);
                return "{\"ok\":true}";
            }
            return "{\"error\":\"cur_research_tech not writable\"}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"{Escape(ex.Message)}\"}}";
        }
    }
}
