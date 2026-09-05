using System;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// IL2CPP 注入的 MonoBehaviour：快捷键、读档后重应用调度、主线程任务泵。
/// 游戏数据操作全部在 Game/ 下的服务类中，由 MainThread 调度到这里执行。
/// </summary>
public class ChestEditorComponent : MonoBehaviour
{
    internal static ChestEditorComponent? Instance;

    // 读档后延迟重应用 NPC 修改（多次，防止被游戏覆盖）
    private int _reapplyCountdown;
    private int _reapplyRemaining;
    private bool _reapplyPending;

    public ChestEditorComponent(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    internal void ScheduleNpcReapply()
    {
        _reapplyCountdown = 60;
        _reapplyRemaining = 5; // 重应用5次，确保不被游戏覆盖
        _reapplyPending = true;
        Plugin.LogInfo("[ChestEditor] 已调度 NPC 修改重应用 x5...");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:8765/") { UseShellExecute = true }); }
            catch (Exception ex) { Plugin.LogError($"打开浏览器失败: {ex.Message}"); }
        }

        // 读档后延迟重应用 NPC 修改（多次）
        if (_reapplyPending)
        {
            _reapplyCountdown--;
            if (_reapplyCountdown <= 0)
            {
                try
                {
                    ModificationStore.ReapplyModifications();
                }
                catch (Exception ex) { Plugin.LogError($"[ChestEditor] NPC 修改重应用失败: {ex.Message}"); }
                _reapplyRemaining--;
                if (_reapplyRemaining <= 0)
                    _reapplyPending = false;
                else
                    _reapplyCountdown = 120; // 下一次等120帧
            }
        }

        // 执行 HTTP 线程投递的主线程任务
        MainThread.Pump();
    }
}
