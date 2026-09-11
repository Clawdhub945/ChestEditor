using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ChestEditor.Interop;

/// <summary>
/// 【诊断·临时】在"鼠标悬停到单位"的 UI 路径关键函数上挂 Harmony 日志钩子。
/// 用途：用户复现悬停崩溃时，LogOutput.log 会留下 "&gt;&gt;&gt; 进入 X" / "&lt;&lt;&lt; 返回 X"，
/// 若某函数只有进入没有返回，即崩溃点。定位完成后连同路由一并删除。
/// 只读参数、不改行为（前缀/后缀只打日志）。
/// </summary>
internal static class HoverProbe
{
    private static Harmony? _harmony;

    /// <summary>Plugin.Load 时调用。</summary>
    public static void Apply(Harmony harmony)
    {
        _harmony = harmony;
        int hooked = 0;
        var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (csharpAsm == null) { Plugin.LogError("[HoverProbe] 找不到 Assembly-CSharp"); return; }

        hooked += Patch(harmony, csharpAsm, "NpcInfoPanel", "UpdateSoldierInfo", 0);
        hooked += Patch(harmony, csharpAsm, "NpcInfoPanel", "UpdateInfo", 0);
        hooked += Patch(harmony, csharpAsm, "Npc", "GetSoldierAttrDesc", 4);
        hooked += Patch(harmony, csharpAsm, "IconText", "SetImgAndDesc", 3);
        hooked += Patch(harmony, csharpAsm, "IconText", "SetInfo", 3);

        Plugin.LogInfo($"[HoverProbe] 已挂 {hooked} 个悬停路径钩子");

        // UnityExplorer（用户装的游戏内对象浏览器）—— 鼠标悬停检查器就在这里
        try { ApplyUnityExplorer(); } catch (Exception ex) { Plugin.LogInfo($"[HoverProbe] UE 挂钩异常: {ex.Message}"); }
    }

    /// <summary>挂 UnityExplorer 的鼠标悬停检查器（显示"一长串 JSON"的就是它）。可运行中重复调用。</summary>
    public static string ApplyUnityExplorer()
    {
        if (_harmony == null) return "harmony 未初始化";
        System.Reflection.Assembly? ue = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if ((a.GetName().Name ?? "").IndexOf("UnityExplorer", StringComparison.OrdinalIgnoreCase) >= 0) { ue = a; break; } }
            catch { }
        }
        if (ue == null) return "未找到 UnityExplorer 程序集";

        int hooked = 0;
        // ⚠ 不钩 TryUpdate / UpdateInspect（每帧调用，会刷爆日志、拖慢游戏）。
        //   只钩"命中对象之后"的路径 —— 崩点几乎必在这里（用户描述是"显示出来后崩"）。
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspector", "StartInspect", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspector", "StopInspect", 0);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspectors.MouseInspectorBase", "OnBeginMouseInspect", 0);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspectors.MouseInspectorBase", "UpdateMouseInspect", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspectors.WorldInspector", "UpdateMouseInspect", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspectors.UiInspector", "UpdateMouseInspect", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspector", "UpdateObjectNameLabel", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.Inspectors.MouseInspector", "UpdateObjectPathLabel", 1);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.UI.Panels.MouseInspectorResultsPanel", "ShowResults", 0);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.UI.Panels.MouseInspectorResultsPanel", "GetEntries", 0);
        hooked += PatchByFullName(_harmony, ue, "UnityExplorer.UI.Panels.MouseInspectorResultsPanel", "SetCell", 2);
        Plugin.LogInfo($"[HoverProbe] UnityExplorer 钩子: {hooked} 个");
        return $"hooked={hooked}";
    }

    private static int PatchByFullName(Harmony harmony, System.Reflection.Assembly asm, string fullName, string methodName, int paramCount)
    {
        try
        {
            Type? t = null;
            foreach (var x in asm.GetTypes())
                if (x.FullName == fullName || x.Name == fullName) { t = x; break; }
            if (t == null) { Plugin.LogInfo($"[HoverProbe] UE 未找到类型 {fullName}"); return 0; }
            MethodInfo? m = null;
            const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            foreach (var x in t.GetMethods(BF))
                if (x.Name == methodName && x.GetParameters().Length == paramCount) { m = x; break; }
            if (m == null) { Plugin.LogInfo($"[HoverProbe] UE 未找到 {fullName}.{methodName}/{paramCount}"); return 0; }
            harmony.Patch(m,
                prefix: new HarmonyMethod(typeof(HoverProbe).GetMethod(nameof(Enter), BindingFlags.Public | BindingFlags.Static)),
                postfix: new HarmonyMethod(typeof(HoverProbe).GetMethod(nameof(Exit), BindingFlags.Public | BindingFlags.Static)));
            Plugin.LogInfo($"[HoverProbe] UE 钩住 {t.Name}.{methodName}/{paramCount}");
            return 1;
        }
        catch (Exception ex) { Plugin.LogInfo($"[HoverProbe] UE 挂 {fullName}.{methodName} 失败: {ex.Message}"); return 0; }
    }

    private static int Patch(Harmony harmony, Assembly asm, string typeName, string methodName, int paramCount)
    {
        try
        {
            Type? t = null;
            foreach (var x in asm.GetTypes())
                if (x.Name == typeName) { t = x; break; }
            if (t == null) { Plugin.LogInfo($"[HoverProbe] 未找到类型 {typeName}（跳过 {methodName}）"); return 0; }

            MethodInfo? m = null;
            foreach (var x in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                if (x.Name == methodName && x.GetParameters().Length == paramCount) { m = x; break; }
            if (m == null) { Plugin.LogInfo($"[HoverProbe] 未找到 {typeName}.{methodName}/{paramCount}（跳过）"); return 0; }

            harmony.Patch(m,
                prefix: new HarmonyMethod(typeof(HoverProbe).GetMethod(nameof(Enter), BindingFlags.Public | BindingFlags.Static)),
                postfix: new HarmonyMethod(typeof(HoverProbe).GetMethod(nameof(Exit), BindingFlags.Public | BindingFlags.Static)));
            Plugin.LogInfo($"[HoverProbe] 钩住 {typeName}.{methodName}/{paramCount}");
            return 1;
        }
        catch (Exception ex) { Plugin.LogInfo($"[HoverProbe] 挂 {typeName}.{methodName} 失败: {ex.Message}"); return 0; }
    }

    /// <summary>Harmony 前缀：__originalMethod 给出被钩方法名。</summary>
    public static void Enter(MethodBase __originalMethod)
    {
        try { Plugin.LogInfo($"[HoverProbe] >>> 进入 {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}"); }
        catch { }
    }

    /// <summary>Harmony 后缀：能走到这里 = 该函数正常返回。</summary>
    public static void Exit(MethodBase __originalMethod)
    {
        try { Plugin.LogInfo($"[HoverProbe] <<< 返回 {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}"); }
        catch { }
    }
}
