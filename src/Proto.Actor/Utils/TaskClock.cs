// -----------------------------------------------------------------------
// <copyright file="TaskClock.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils;

/// <summary>
///     Provides a clock that "ticks" at a certain interval and the next tick can be awaited.
/// </summary>
public class TaskClock
{
    private readonly TimeSpan _bucketSize;
    private readonly CancellationToken _ct;
    private readonly TimeSpan _updateInterval;
    private Task _currentBucket = Task.CompletedTask;

    /// <summary>
    ///     Creates a new TaskClock. Each bucket completes after <see cref="timeout" /> + <see cref="updateInterval" />. A new
    ///     bucket is created on each <see cref="updateInterval" />
    /// </summary>
    /// <param name="timeout"></param>
    /// <param name="updateInterval"></param>
    /// <param name="ct">Used to stop the clock</param>
    public TaskClock(TimeSpan timeout, TimeSpan updateInterval, CancellationToken ct)
    {
        _bucketSize = timeout + updateInterval;
        _updateInterval = updateInterval;
        _ct = ct;
    }

    /// <summary>
    ///     This task will complete when the clock "ticks" and will be replaced with a new task that will complete on the next
    ///     "tick"
    /// </summary>
    public Task CurrentBucket
    {
        get => Volatile.Read(ref _currentBucket);
        private set => Volatile.Write(ref _currentBucket, value);
    }

    /// <summary>
    ///     Starts the clock
    /// </summary>
    public void Start()
    {
        CurrentBucket = Task.Delay(_bucketSize, _ct);

        _ = SafeTask.Run(async () =>
        {
            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    CurrentBucket = Task.Delay(_bucketSize, _ct);
                    await Task.Delay(_updateInterval, _ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //pass, expected
                }
            }
        });
    }
}