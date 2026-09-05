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

    public static void OnDoLoadPostfix(string __0, bool __result)
    {
        if (__result)
        {
            Plugin.LogInfo($"存档加载成功: {__0}");
            CachedTerritory = null; // 清除旧缓存，等待下次 WalkState 重新获取

            // 延迟重应用 NPC 修改（等实体重建完成）
            ChestEditorComponent.Instance?.ScheduleNpcReapply();
        }
    }

    public static void UpdateSpeedPostfix(object __instance)
    {
        try
        {
            IntPtr ptr = GetIl2CppPtr(__instance);
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
            var areaMap = GetProp(__instance, "area_map");
            if (areaMap == null) return;

            var myTerritory = GetProp(areaMap, "my_territory");
            if (myTerritory == null) return;

            CachedTerritory = myTerritory;
        }
        catch { }
    }

    // ========== 辅助方法 ==========

    private static IntPtr GetIl2CppPtr(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("Pointer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return (IntPtr)prop.GetValue(obj);
        }
        catch { }
        return IntPtr.Zero;
    }

    private static object? GetProp(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (prop != null) return prop.GetValue(obj);
                t = t.BaseType;
            }
        }
        catch { }
        return null;
    }
}
