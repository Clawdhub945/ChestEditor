using System;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ChestEditor;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public const string PLUGIN_GUID = "claude.chesteditor";
    public const string PLUGIN_NAME = "ChestEditor";
    public const string PLUGIN_VERSION = "1.1.0";

    private Harmony? _harmony;
    private static HttpServer? _httpServer;
    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Logger = Log;

        _harmony = new Harmony(PLUGIN_GUID);
        SaveLoadPatches.Apply(_harmony);
        try
        {
            ModificationStore.LoadFromDisk();
            LogInfo("[Plugin] ModificationStore.LoadFromDisk 完成");
        }
        catch (Exception ex) { LogError($"[Plugin] LoadModificationsFromDisk 失败: {ex}"); }

        // 注册 IL2CPP 组件并创建 GameObject
        ClassInjector.RegisterTypeInIl2Cpp<ChestEditorComponent>();
        var go = new GameObject("ChestEditorUI");
        GameObject.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<ChestEditorComponent>();

        // 启动 HTTP 服务器
        _httpServer = new HttpServer(8765);
        _httpServer.Start();

        LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} 已加载！按 F11 打开/关闭箱子编辑器 | 浏览器打开 http://localhost:8765/");
    }

    public override bool Unload()
    {
        _httpServer?.Stop();
        _harmony?.UnpatchSelf();
        LogInfo($"{PLUGIN_NAME} 已卸载");
        return true;
    }

    internal static void LogInfo(string msg) => Logger.LogInfo(msg);
    internal static void LogError(string msg) => Logger.LogError(msg);

    /// <summary>
    /// 逐实体的调试日志开关（默认关，可用环境变量 CHESTEDITOR_VERBOSE=1 打开）。
    /// <para>批量销毁/扫描这类热路径上，每次操作会打出一堆"逐条"日志；一次清几百个就是几千行，
    /// 既拖慢速度又把 LogOutput.log 撑爆。默认只保留"每批一行"的汇总。</para>
    /// </summary>
    internal static bool Verbose { get; set; } =
        System.Environment.GetEnvironmentVariable("CHESTEDITOR_VERBOSE") == "1";

    internal static void LogVerbose(string msg)
    {
        if (Verbose) Logger.LogInfo(msg);
    }
}
