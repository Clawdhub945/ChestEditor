using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    // 分片任务：需要跨多帧推进的工作（如全量场景扫描），每帧只推进一步，避免单帧长时间冻结
    private static readonly ConcurrentQueue<Func<bool>> PendingTickers = new();
    private static readonly List<Func<bool>> Tickers = new();

    internal static void Enqueue(Action job) => Queue.Enqueue(job);

    /// <summary>由 MonoBehaviour 每帧调用，执行所有排队任务 + 推进一步分片任务</summary>
    internal static void Pump()
    {
        while (Queue.TryDequeue(out var job))
        {
            try { job(); }
            catch (Exception ex) { Plugin.LogError($"[MainThread] 任务异常: {ex.Message}"); }
        }

        while (PendingTickers.TryDequeue(out var ticker)) Tickers.Add(ticker);
        for (int i = Tickers.Count - 1; i >= 0; i--)
        {
            bool alive;
            try { alive = Tickers[i](); }
            catch (Exception ex)
            {
                Plugin.LogError($"[MainThread] 分片任务异常: {ex.Message}");
                alive = false;
            }
            if (!alive) Tickers.RemoveAt(i);
        }
    }

    /// <summary>
    /// 把一段能"分片推进"的工作摊到多帧执行：每帧调用一次 <paramref name="step"/>，
    /// 返回 <c>false</c> 表示做完（自动摘除）。HTTP 线程阻塞等待全部完成。
    /// <para>用途：全量扫描/批量销毁这类一次做几秒的操作 —— 单帧做完就是几秒的卡死，
    /// 摊到多帧只是整体帧率略降，不会出现冻结。</para>
    /// </summary>
    public static void RunPaced(Func<bool> step, int timeoutMs = 60000)
    {
        if (ChestEditorComponent.Instance == null)
            throw new InvalidOperationException("mod not ready");

        using var signal = new ManualResetEventSlim(false);
        Exception? error = null;
        PendingTickers.Enqueue(() =>
        {
            try { if (step()) return true; }
            catch (Exception ex) { error = ex; }
            signal.Set();
            return false;
        });

        if (!signal.Wait(timeoutMs))
            throw new TimeoutException($"主线程分片任务超时 ({timeoutMs}ms)");
        if (error != null) throw error;
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
