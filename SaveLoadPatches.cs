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
        PatchUI(harmony);
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
                // prefix 标记"正在读档"，postfix 记"读档成功次数"（网页端据此自动关闭安全发展模式）
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(OnDoLoadPrefix)),
                    postfix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(OnDoLoadPostfix)));
            }
            catch (Exception ex) { Plugin.LogError($"钩住 DoLoad 失败: {ex.Message}"); }
        }
    }

    /// <summary>DoLoad 开始：标记正在读档（读档期间网页端的长任务应当停手）。</summary>
    public static void OnDoLoadPrefix() => Core.AppState.OnSaveLoadBegin();

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
        Core.AppState.OnSaveLoadEnd(__result);
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

    // ==================== UI 读档（远程“进入存档”） ====================
    // 移植自 GameMCP（已验证可用的机制）：核心是游戏自己的高层入口
    // UI.ExitAndLoadGame(archive_folder_name, exit_msg)，但它在标题画面调用时
    // ExitGame 会走进 OnGameExit 的空引用——必须配合两个 Harmony 补丁：
    //   · UI.ExitGame 前缀：标题画面（Game.main_scene 为空）跳过 OnGameExit，
    //     改为“记录待载存档 + 重载 UIScene”，让 UI.Start 重走启动流程
    //   · UI.Start 前缀：有待载存档（PendingSaveFolder）时设置 MainScene 的
    //     auto_load_archive_folder_name 静态字段，并延迟 3 秒调用 UI.StartGame(folder)
    //     （UI.Start 阶段游戏尚未初始化完成，必须等它跑完；延迟用后台线程 + MainThread.Enqueue）
    // 注意：不移植 GameMCP 的“启动自动加载最新存档”逻辑——UI.Start 无待载存档时照常放行。

    public static string? PendingSaveFolder { get; set; }
    private static IntPtr _autoLoadFieldPtr;
    private static MethodInfo? _setFieldMethod;
    private static MethodInfo? _managedStringToIl2Cpp;

    private static void PatchUI(Harmony harmony)
    {
        var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (csharpAsm == null) { Plugin.LogError("[UI读档] Assembly-CSharp 不可用"); return; }

        Type? uiType = null;
        try { uiType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "UI"); }
        catch (ReflectionTypeLoadException ex) { uiType = ex.Types?.FirstOrDefault(t => t?.Name == "UI"); }
        if (uiType == null) { Plugin.LogError("[UI读档] UI 类型不存在"); return; }

        InitIl2CppApi();

        var exitGame = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ExitGame");
        if (exitGame != null)
        {
            harmony.Patch(exitGame, prefix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(ExitGamePrefix)));
            Plugin.LogInfo("[UI读档] 已补丁 UI.ExitGame");
        }

        var startMethod = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Start");
        if (startMethod != null)
        {
            harmony.Patch(startMethod, prefix: new HarmonyMethod(typeof(SaveLoadPatches), nameof(UIStartPrefix)));
            Plugin.LogInfo("[UI读档] 已补丁 UI.Start");
        }
    }

    private static void InitIl2CppApi()
    {
        try
        {
            var mainSceneType = HarmonyLib.AccessTools.TypeByName("MainScene");
            if (mainSceneType == null) { Plugin.LogError("[UI读档] MainScene 类型不存在"); return; }

            // 触发 MainScene 的 il2cpp_runtime_class_init（访问任一静态成员即可）
            try
            {
                var prop = mainSceneType.GetProperty("auto_load_archive_folder_name",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (prop != null) { var _ = prop.GetValue(null); }
                else
                {
                    var anyField = mainSceneType.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(f => !f.Name.StartsWith("Native"));
                    if (anyField != null) { var _ = anyField.GetValue(null); }
                }
            }
            catch { }

            var autoLoadField = mainSceneType.GetField("NativeFieldInfoPtr_auto_load_archive_folder_name",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (autoLoadField != null)
                _autoLoadFieldPtr = (IntPtr)autoLoadField.GetValue(null)!;

            var il2cppType = typeof(Il2CppInterop.Runtime.IL2CPP);
            _setFieldMethod = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "il2cpp_field_static_set_value" && m.GetParameters().Length == 2);
            _managedStringToIl2Cpp = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ManagedStringToIl2Cpp");

            Plugin.LogInfo($"[UI读档] InitIl2CppApi: autoLoadPtr={_autoLoadFieldPtr}, setField={_setFieldMethod != null}, strToIl2Cpp={_managedStringToIl2Cpp != null}");
        }
        catch (Exception ex) { Plugin.LogError($"[UI读档] InitIl2CppApi 失败: {ex.Message}"); }
    }

    /// <summary>UI.Start 前缀：仅当有 PendingSaveFolder（远程读档指令）时设置自动加载并延迟 StartGame</summary>
    public static void UIStartPrefix(object __instance)
    {
        try
        {
            var folderName = PendingSaveFolder;
            PendingSaveFolder = null;
            if (string.IsNullOrEmpty(folderName)) return;   // 正常启动：不做任何事（不自动进档）

            Plugin.LogInfo($"[UI读档] UIStart: 使用待加载存档: {folderName}");
            SetIl2CppStringField(_autoLoadFieldPtr, folderName!);

            // UI.Start 阶段游戏还没完全初始化，必须等它完成后再调 StartGame：
            // 后台线程等 3 秒，然后经 MainThread.Enqueue 回主线程执行
            var captured = folderName!;
            new System.Threading.Thread(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(3000);
                    Threading.MainThread.Enqueue(() =>
                    {
                        try
                        {
                            var uiType = __instance.GetType();
                            var startGame = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                .FirstOrDefault(m => m.Name == "StartGame");
                            if (startGame == null) { Plugin.LogError("[UI读档] UI.StartGame 不存在"); return; }
                            var parms = startGame.GetParameters();
                            var insProp = uiType.GetProperty("Ins", BindingFlags.Public | BindingFlags.Static);
                            var uiIns = insProp?.GetValue(null);
                            if (uiIns == null)
                            {
                                var insField = uiType.GetField("Ins", BindingFlags.Public | BindingFlags.Static);
                                uiIns = insField?.GetValue(null);
                            }
                            if (uiIns == null) { Plugin.LogError("[UI读档] UI.Ins 不可用"); return; }

                            if (parms.Length == 2) startGame.Invoke(uiIns, new object[] { captured, null! });
                            else if (parms.Length == 1) startGame.Invoke(uiIns, new object[] { captured });
                            else startGame.Invoke(uiIns, null);
                            Plugin.LogInfo($"[UI读档] UI.StartGame({captured}) 调用完成");
                        }
                        catch (Exception ex) { Plugin.LogError($"[UI读档] StartGame 调用失败: {ex.Message} | {ex.InnerException?.Message}"); }
                    });
                }
                catch (Exception ex) { Plugin.LogError($"[UI读档] 延迟线程异常: {ex.Message}"); }
            }) { IsBackground = true, Name = "ChestEditor_DelayedStart" }.Start();
        }
        catch (Exception ex) { Plugin.LogError($"[UI读档] UIStartPrefix 失败: {ex.Message}"); }
    }

    /// <summary>UI.ExitGame 前缀：标题画面（main_scene 为空）跳过 OnGameExit，重载 UIScene 走启动流程</summary>
    public static bool ExitGamePrefix(object __instance)
    {
        try
        {
            var gameType = HarmonyLib.AccessTools.TypeByName("Game");
            if (gameType == null) return true;
            var getMainScene = gameType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "get_main_scene");
            if (getMainScene == null) return true;
            if (getMainScene.Invoke(null, null) != null) return true;   // 在游戏中：走原流程（正常退档）

            // 标题画面：读取 ExitAndLoadGame 留下的待载信息
            var infoField = __instance.GetType().GetField("exit_and_load_game_info",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var info = infoField?.GetValue(__instance);
            if (info != null)
            {
                var folderField = info.GetType().GetField("load_game_archive_folder_name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var folderName = folderField?.GetValue(info)?.ToString();
                if (!string.IsNullOrEmpty(folderName))
                {
                    Plugin.LogInfo($"[UI读档] ExitGame: 标题画面, 待加载存档: {folderName}");
                    PendingSaveFolder = folderName;
                    SetIl2CppStringField(_autoLoadFieldPtr, folderName);
                }
            }
            ReloadUIScene();
            return false;   // 跳过原 ExitGame（避开 OnGameExit 空引用）
        }
        catch (Exception ex) { Plugin.LogError($"[UI读档] ExitGamePrefix 失败: {ex.Message}"); }
        return true;
    }

    private static void SetIl2CppStringField(IntPtr fieldPtr, string value)
    {
        if (fieldPtr == IntPtr.Zero || _setFieldMethod == null || _managedStringToIl2Cpp == null) return;
        try
        {
            var il2cppStr = (IntPtr)_managedStringToIl2Cpp.Invoke(null, new object[] { value })!;
            _setFieldMethod.Invoke(null, new object[] { fieldPtr, il2cppStr });
            Plugin.LogInfo($"[UI读档] 已设置 auto_load_archive_folder_name = {value}");
        }
        catch (Exception ex) { Plugin.LogError($"[UI读档] SetIl2CppStringField 失败: {ex.Message}"); }
    }

    private static void ReloadUIScene()
    {
        try
        {
            Type? sceneManagerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { sceneManagerType = asm.GetTypes().FirstOrDefault(t => t.Name == "SceneManager" && t.Namespace?.Contains("SceneManagement") == true); }
                catch (ReflectionTypeLoadException ex) { sceneManagerType = ex.Types?.FirstOrDefault(t => t?.Name == "SceneManager" && t.Namespace?.Contains("SceneManagement") == true); }
                if (sceneManagerType != null) break;
            }
            var loadScene = sceneManagerType?.GetMethod("LoadScene", new[] { typeof(string) });
            if (loadScene != null)
            {
                Plugin.LogInfo("[UI读档] 重载 UIScene");
                loadScene.Invoke(null, new object[] { "UIScene" });
            }
        }
        catch (Exception ex) { Plugin.LogError($"[UI读档] ReloadUIScene 失败: {ex.Message}"); }
    }

    /// <summary>供 debug/load 端点调用（必须在主线程）：UI.ExitAndLoadGame(folderName, "")</summary>
    public static bool LoadSaveViaUI(string folderName)
    {
        var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (csharpAsm == null) { Plugin.LogError("[UI读档] Assembly-CSharp 不可用"); return false; }
        Type? uiType = null;
        try { uiType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "UI"); }
        catch (ReflectionTypeLoadException ex) { uiType = ex.Types?.FirstOrDefault(t => t?.Name == "UI"); }
        if (uiType == null) { Plugin.LogError("[UI读档] UI 类型不存在"); return false; }

        var insProp = uiType.GetProperty("Ins", BindingFlags.Public | BindingFlags.Static);
        var uiIns = insProp?.GetValue(null);
        if (uiIns == null)
        {
            var insField = uiType.GetField("Ins", BindingFlags.Public | BindingFlags.Static);
            uiIns = insField?.GetValue(null);
        }
        if (uiIns == null) { Plugin.LogError("[UI读档] UI.Ins 不可用"); return false; }

        var exitAndLoad = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ExitAndLoadGame");
        if (exitAndLoad == null) { Plugin.LogError("[UI读档] UI.ExitAndLoadGame 不存在"); return false; }

        exitAndLoad.Invoke(uiIns, new object[] { folderName, "" });
        Plugin.LogInfo($"[UI读档] UI.ExitAndLoadGame({folderName}) 调用成功");
        return true;
    }
}