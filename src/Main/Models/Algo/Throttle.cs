using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace FMO.Models;

/// <summary>
/// 基于完成时间的高性能节流器。
/// 确保一个操作执行完成后，必须经过指定时间窗口才能执行下一个操作。
/// 第一次调用总是立即执行，且执行完成后才开始计时冷却。
/// </summary>
public sealed class Throttle
{
    private const long ExecutingFlag = long.MinValue; // 特殊标记：有线程正在执行操作

    private readonly long _intervalTicks;               // 冷却窗口（Stopwatch 周期数）
    private long _lastCompletionTimestamp;              // 上次操作完成的时间戳（或 ExecutingFlag）

    /// <param name="interval">冷却时间窗口，从每次操作完成时开始计算。</param>
    public Throttle(TimeSpan interval)
    {
        _intervalTicks = (long)(interval.TotalSeconds * Stopwatch.Frequency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <summary>
    /// 尝试获取执行权。
    /// </summary>
    /// <returns>true 表示调用者获得了执行权，必须执行操作并在完成后调用 <see cref="Release"/>；否则表示被节流。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAcquire()
    {
        long now = GetTimestamp();
        long last = Interlocked.Read(ref _lastCompletionTimestamp);

        // 有操作正在执行 → 拒绝其他所有调用
        if (last == ExecutingFlag)
            return false;

        // 检查冷却期是否已过
        if (now - last >= _intervalTicks)
        {
            // 原子更新：将上次完成时间替换为“执行中”标志，只有成功的线程获得执行权
            if (Interlocked.CompareExchange(ref _lastCompletionTimestamp, ExecutingFlag, last) == last)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 释放执行权，并将完成时间记录为当前时刻。
    /// 必须与 <see cref="TryAcquire"/> 配对使用，且只在获得执行权后调用一次。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Release()
    {
        // 将完成时间写入（冷却起点）
        Interlocked.Exchange(ref _lastCompletionTimestamp, GetTimestamp());
    }

    // ---------- 公开 API ----------

    /// <summary>
    /// 同步执行操作。若节流器处于冷却期或正在执行，则忽略本次调用。
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>true 表示操作已执行；false 表示被节流</returns>
    public bool Execute(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (!TryAcquire())
            return false;

        try
        {
            action();
            return true;
        }
        finally
        {
            Release();
        }
    }

    /// <summary>
    /// 异步执行操作。若节流器处于冷却期或正在执行，则忽略本次调用。
    /// </summary>
    /// <param name="asyncAction">异步操作（无参数，返回 Task）</param>
    /// <returns>ValueTask&lt;bool&gt; – true 表示操作已执行；false 表示被节流</returns>
    public async Task<bool> ExecuteAsync(Func<Task> asyncAction)
    {
        if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

        if (!TryAcquire())
            return false;

        try
        {
            await asyncAction().ConfigureAwait(false);
            return true;
        }
        finally
        {
            Release();
        }
    }

    /// <summary>
    /// 异步执行操作（支持 CancellationToken）。若节流器处于冷却期或正在执行，则忽略本次调用。
    /// </summary>
    /// <param name="asyncAction">异步操作（接受 CancellationToken，返回 Task）</param>
    /// <param name="cancellationToken">传递给操作的取消标记</param>
    /// <returns>ValueTask&lt;bool&gt; – true 表示操作已执行；false 表示被节流</returns>
    public async Task<bool> ExecuteAsync(Func<CancellationToken, Task> asyncAction, CancellationToken cancellationToken = default)
    {
        if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

        if (!TryAcquire())
            return false;

        try
        {
            await asyncAction(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            Release();
        }
    }
}