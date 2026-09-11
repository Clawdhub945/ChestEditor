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

    // 读档后龙魂强化恢复（游戏侧自动，不依赖网页是否打开；每3秒重试直到游戏世界就绪）
    private bool _dragonSoulRestorePending;
    private float _dragonSoulRestoreNextTry;
    private int _dragonRestoreTries;
    private const int DragonRestoreMaxTries = 40; // 40次x3秒=最多等2分钟

    public ChestEditorComponent(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    /// <summary>读档后调度：12秒后一次全场景扫描（NPC+龙实体），龙魂强化则每3秒轻量重试</summary>
    internal void SchedulePostLoadRestore()
    {
        ScheduleDeferredNpcReapply();
        _dragonSoulRestorePending = true;
        _dragonRestoreTries = 0;
        _dragonSoulRestoreNextTry = Time.time + 3f;
    }

    internal void ScheduleDeferredNpcReapply(float delaySeconds = 12f)
    {
        _deferredReapplyPending = true;
        _deferredReapplyAt = Time.time + delaySeconds;
        Plugin.LogInfo($"[ChestEditor] 已调度 NPC 修改延迟重应用（{delaySeconds:0}秒后一次）");
    }

    private void Update()
    {
        // 记录主线程 id：给 MainThread.IsMainThread 用（防止有调用点在 HTTP 线程上碰 Unity 对象）
        MainThread.MarkMainThread();

        // ⚠ 不要再在 Update 里遍历实体快照的裸指针（Ptr）——快照是扫描瞬间的缓存，
        //   NPC 阵亡后指针悬垂，裸读绕过 Unity 销毁保护 → 随机时刻原生 AV。见 NpcSpawnService 注释。

        if (Input.GetKeyDown(KeyCode.F11))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:8765/") { UseShellExecute = true }); }
            catch (Exception ex) { Plugin.LogError($"打开浏览器失败: {ex.Message}"); }
        }

        // 读档后延迟重应用（只扫有记录的部分；NPC 与龙实体共享同一次 GO 枚举）
        if (_deferredReapplyPending && Time.time >= _deferredReapplyAt)
        {
            _deferredReapplyPending = false;
            try
            {
                bool needNpc = ModificationStore.HasNpcRecords();
                bool needDragonEnt = ModificationStore.GetByPrefix("dragone:").Count > 0;
                if (needNpc || needDragonEnt)
                {
                    // 一次 FindObjectsOfTypeAll 喂给两个扫描器，省一半枚举开销
                    var allGOs = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
                    if (needNpc)
                    {
                        EntityScan.ScanAll(allGOs);
                        ModificationStore.ApplyPendingModifications();
                    }
                    if (needDragonEnt)
                        DragonService.RestoreEntityModificationsViaScan(allGOs);
                    Plugin.LogInfo($"[ChestEditor] 延迟恢复完成 (npc={needNpc}, dragonEntity={needDragonEnt})");
                }
                else
                {
                    Plugin.LogInfo("[ChestEditor] 无待恢复记录，跳过延迟扫描");
                }
            }
            catch (Exception ex) { Plugin.LogError($"[ChestEditor] 读档后重应用失败: {ex.Message}"); }
        }

        // 龙魂强化恢复：轻量（无场景扫描），每3秒重试直到游戏世界就绪
        if (_dragonSoulRestorePending && Time.time >= _dragonSoulRestoreNextTry)
        {
            _dragonRestoreTries++;
            if (DragonService.TryRestoreSoulModifications())
            {
                _dragonSoulRestorePending = false;
                Plugin.LogInfo("[ChestEditor] 龙魂强化已恢复");
            }
            else if (_dragonRestoreTries >= DragonRestoreMaxTries)
            {
                _dragonSoulRestorePending = false;
                Plugin.LogInfo("[ChestEditor] 龙魂强化恢复超时放弃");
            }
            else
            {
                _dragonSoulRestoreNextTry = Time.time + 3f;
            }
        }

        // 执行 HTTP 线程投递的主线程任务
        MainThread.Pump();
    }
}
