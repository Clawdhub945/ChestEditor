using System;

namespace ChestEditor.Core;

/// <summary>
/// 游戏/存档状态（给网页端做"现在能不能操作"的判断）。
/// <para>
/// ⚠ 读写只发生在主线程：读档钩子（SaveLoadPatches）与 /api/editor/state 都经 MainThread 调度。
/// </para>
/// </summary>
internal static class AppState
{
    /// <summary>存档加载成功的次数（每次 ArchiveManager.DoLoad 成功 +1）。网页端用它识别"读过一次档了"。</summary>
    internal static int SaveLoads;

    /// <summary>是否正在读档（DoLoad 调用期间为 true）。</summary>
    internal static bool Loading;

    /// <summary>DoLoad 开始（Harmony prefix 调用）。</summary>
    internal static void OnSaveLoadBegin() => Loading = true;

    /// <summary>DoLoad 结束（Harmony postfix 调用）。</summary>
    internal static void OnSaveLoadEnd(bool ok)
    {
        Loading = false;
        if (ok) SaveLoads++;
    }
}
