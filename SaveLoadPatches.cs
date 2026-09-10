using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ChestEditor;

public static class SaveLoadPatches
{
    internal static object? CachedTerritory;
    internal static Type? TerritoryType;
    internal static Type? ArchiveManagerType;

    public static void Apply(Harmony harmony)
    {
        // 从 Assembly-CSharp 查找类型
        var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (csharpAsm == null)
        {
            Plugin.LogError("找不到 Assembly-CSharp！");
            return;
        }

        Type[] types;
        try { types = csharpAsm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var t in types)
        {
            if (t.Name == "Territory") TerritoryType = t;
            if (t.Name == "ArchiveManager") ArchiveManagerType = t;
        }

        PatchDoLoad(harmony);
        PatchWalkStates(harmony);
        PatchUpdateSpeed(harmony);
    }


    private static void PatchDoLoad(Harmony harmony)
    {
        if (ArchiveManagerType == null) return;

        var method = ArchiveManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "DoLoad" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

        if (method != null)
        {
            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(OnDoLoadPostfix)));
            }
            catch (Exception ex) { Plugin.LogError($"钩住 DoLoad 失败: {ex.Message}"); }
        }
    }

    /// <summary>
    /// 钩住 NpcStateWalk.StartPathFinding 以获取 area_map.my_territory
    /// </summary>
    private static void PatchWalkStates(Harmony harmony)
    {
        string[] walkTypes = { "NpcStateWalk", "BattleUnitStateWalk", "AnimalStateWalk" };

        foreach (var typeName in walkTypes)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null) continue;

            var method = AccessTools.Method(type, "StartPathFinding");
            if (method == null) continue;

            try
            {
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(WalkStatePrefix)));
                return; // 只需要钩住一个
            }
            catch (Exception ex)
            {
                Plugin.LogError($"钩住 {typeName}.StartPathFinding 失败: {ex.Message}");
            }
        }

        Plugin.LogError("未能钩住任何行走状态方法");
    }

    /// <summary>
    /// 钩住 UpdateAgentMoveSpeed 以持续应用速度覆盖
    /// </summary>
    private static void PatchUpdateSpeed(Harmony harmony)
    {
        var type = AccessTools.TypeByName("Npc") ?? AccessTools.TypeByName("BattleUnit");
        if (type == null) { Plugin.LogError("找不到 Npc/BattleUnit 类型"); return; }

        var method = AccessTools.Method(type, "UpdateAgentMoveSpeed");
        if (method == null) { Plugin.LogError("找不到 UpdateAgentMoveSpeed 方法"); return; }

        try
        {
            harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(UpdateSpeedPostfix)));
            Plugin.LogInfo("钩住 UpdateAgentMoveSpeed 成功");
        }
        catch (Exception ex) { Plugin.LogError($"钩住 UpdateAgentMoveSpeed 失败: {ex.Message}"); }
    }

    // ========== 补丁回调 ==========

    /// <summary>
    /// NPC 修改读档重放开关。
    /// 旧实现：60帧后每120帧重扫全场景共5次 → 大存档读档后卡5次。
    /// 现实现：延迟12秒只做一次全场景扫描（等场景加载完），加上
    /// NPC面板/实体编辑器手动扫描时也会顺带恢复（懒扫描）。
    /// </summary>
    public static bool NpcReapplyOnLoad = true;

    public static void OnDoLoadPostfix(string __0, bool __result)
    {
        if (__result)
        {
            Plugin.LogInfo($"存档加载成功: {__0}");
            CachedTerritory = null; // 清除旧缓存，等待下次 WalkState 重新获取

            // 读档后恢复：延迟一次全场景扫描（NPC+龙实体），龙魂强化每3秒轻量重试
            if (NpcReapplyOnLoad)
                ChestEditorComponent.Instance?.SchedulePostLoadRestore();
        }
    }

    public static void UpdateSpeedPostfix(object __instance)
    {
        try
        {
            IntPtr ptr = Il2CppApi.GetIl2CppPtr(__instance);
            if (ptr != IntPtr.Zero)
                NpcEditor.OnPostUpdate(ptr);
        }
        catch { }
    }

    public static void WalkStatePrefix(object __instance)
    {
        if (CachedTerritory != null) return;

        try
        {
            var areaMap = ManagedReflect.GetProp(__instance, "area_map");
            if (areaMap == null) return;

            var myTerritory = ManagedReflect.GetProp(areaMap, "my_territory");
            if (myTerritory == null) return;

            CachedTerritory = myTerritory;
        }
        catch { }
    }
}
