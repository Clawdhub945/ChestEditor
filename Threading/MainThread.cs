using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ChestEditor.Threading;

/// <summary>
/// 主线程任务调度：HTTP 线程通过 Run 提交闭包并同步等待结果，
/// 游戏主线程（ChestEditorComponent.Update）每帧 Pump 执行队列。
/// 所有触碰游戏对象/IL2CPP 内存的操作必须经由本调度器。
/// </summary>
public static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    internal static void Enqueue(Action job) => Queue.Enqueue(job);

    /// <summary>由 MonoBehaviour 每帧调用，执行所有排队任务</summary>
    internal static void Pump()
    {
        while (Queue.TryDequeue(out var job))
        {
            try { job(); }
            catch (Exception ex) { Plugin.LogError($"[MainThread] 任务异常: {ex.Message}"); }
        }
    }

    /// <summary>
    /// 同步等待主线程执行并返回结果（HTTP 线程调用）。
    /// 组件未就绪 → InvalidOperationException("mod not ready")；
    /// 超时 → TimeoutException；任务内异常 → 原样抛出。
    /// </summary>
    public static T Run<T>(Func<T> func, int timeoutMs = 10000)
    {
        if (ChestEditorComponent.Instance == null)
            throw new InvalidOperationException("mod not ready");

        T result = default!;
        Exception? error = null;
        var signal = new ManualResetEventSlim(false);

        Enqueue(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
            finally { signal.Set(); }
        });

        if (!signal.Wait(timeoutMs))
            throw new TimeoutException($"主线程任务超时 ({timeoutMs}ms)");
        if (error != null)
            throw error;
        return result;
    }

    public static void Run(Action action, int timeoutMs = 10000)
        => Run<object?>(() => { action(); return null; }, timeoutMs);
}
