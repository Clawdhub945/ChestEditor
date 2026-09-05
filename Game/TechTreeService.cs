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
            var w = GameContext.GetGame();
            if (w == null) return "{\"error\":\"Game.w is null\"}";

            var unlockList = GetProp(w, "unlock_tech_list");
            if (unlockList == null) return "{\"error\":\"unlock_tech_list is null\"}";

            var listType = unlockList.GetType();
            var containsMethod = listType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var addMethod = listType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var removeMethod = listType.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (containsMethod == null || addMethod == null || removeMethod == null)
                return "{\"error\":\"List methods not found\"}";

            bool contains = Convert.ToBoolean(containsMethod.Invoke(unlockList, new object[] { techId }));

            if (unlock && !contains)
            {
                addMethod.Invoke(unlockList, new object[] { techId });
                return "{\"ok\":true,\"action\":\"added\"}";
            }
            else if (!unlock && contains)
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


    internal static string UnlockAllTechs()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "{\"error\":\"Game.w is null\"}";

            var unlockList = GetProp(w, "unlock_tech_list");
            if (unlockList == null) return "{\"error\":\"unlock_tech_list is null\"}";

            var listType = unlockList.GetType();
            var containsMethod = listType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var addMethod = listType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (containsMethod == null || addMethod == null) return "{\"error\":\"List methods not found\"}";

            // All tech IDs from TECH_TREE data
            int[] allTechIds = {
                902022,902035,902024,902034,902023,902037,902038,902084,902036,902160,
                902017,902020,902018,902109,902019,902021,902012,902039,902116,902013,
                902005,902004,902007,902156,902113,902154,902155,902153,902130,902131,
                902010,902011,902157,902158,902159,902147,902003,902099,902140,902065,
                902066,902144,902061,902152,902075,902104,902122,902135,902136,902137,
                902138,902139,902002,902091,902014,902015,902102,902063,902064,902009,
                902161,902008,902112,902114,902016,902106,902134,902141,902032,902033,
                902060,902120,902150,902059,902151,902111,902082,902083,902125,902146,
                902126,902124,902077,902117,902078,902080,902110,902149,902081,902025,
                902090,902143,902074,902115,902142,902133,902068,902069,902070,902071,
                902072,902073,902052,902053,902057,902127,902055,902056,902128,902108,
                902103,902129,902092,902097,902123,902098,902100,902094,902093,902095,
                902049,902050,902051,902026,902027,902028,902029,902030,902031,902040,
                902041,902042,902043,902044,902045,902046,902047,902048,902132,902096,
                902101,902105,902118,902119,902148,902085,902086,902087,902088,902089,
                902162,902145
            };

            int added = 0;
            foreach (var techId in allTechIds)
            {
                bool contains = Convert.ToBoolean(containsMethod.Invoke(unlockList, new object[] { techId }));
                if (!contains)
                {
                    addMethod.Invoke(unlockList, new object[] { techId });
                    added++;
                }
            }

            return $"{{\"ok\":true,\"added\":{added},\"total\":{allTechIds.Length}}}";
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
