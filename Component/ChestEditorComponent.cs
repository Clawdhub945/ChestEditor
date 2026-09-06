using System;
using UnityEngine;

namespace ChestEditor;

/// <summary>
/// IL2CPP 注入的 MonoBehaviour：快捷键、读档后延迟重应用调度、主线程任务泵。
/// 游戏数据操作全部在 Game/ 下的服务类中，由 MainThread 调度到这里执行。
/// </summary>
public class ChestEditorComponent : MonoBehaviour
{
    internal static ChestEditorComponent? Instance;

    // 读档后延迟重应用 NPC 修改（只做一次，等场景完全加载，替代旧的 5 次重扫）
    private bool _deferredReapplyPending;
    private float _deferredReapplyAt;

    public ChestEditorComponent(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    internal void ScheduleDeferredNpcReapply(float delaySeconds = 12f)
    {
        _deferredReapplyPending = true;
        _deferredReapplyAt = Time.time + delaySeconds;
        Plugin.LogInfo($"[ChestEditor] 已调度 NPC 修改延迟重应用（{delaySeconds:0}秒后一次）");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:8765/") { UseShellExecute = true }); }
            catch (Exception ex) { Plugin.LogError($"打开浏览器失败: {ex.Message}"); }
        }

        // 读档后延迟重应用（一次全场景扫描；用户在面板里手动扫描也会触发恢复）
        if (_deferredReapplyPending && Time.time >= _deferredReapplyAt)
        {
            _deferredReapplyPending = false;
            try
            {
                ModificationStore.ReapplyModifications();
            }
            catch (Exception ex) { Plugin.LogError($"[ChestEditor] NPC 修改重应用失败: {ex.Message}"); }
        }

        // 执行 HTTP 线程投递的主线程任务
        MainThread.Pump();
    }
}
