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
/// Provides a clock that "ticks" at a certain interval and the next tick can be awaited.
/// </summary>
public class TaskClock
{
    private readonly TimeSpan _bucketSize;
    private readonly TimeSpan _updateInterval;
    private readonly CancellationToken _ct;
    private Task _currentBucket = Task.CompletedTask;

    /// <summary>
    /// This task will complete when the clock "ticks" and will be replaced with a new task that will complete on the next "tick"
    /// </summary>
    public Task CurrentBucket {
        get => Volatile.Read(ref _currentBucket);
        private set => Volatile.Write(ref _currentBucket, value);
    }
    
    /// <summary>
    /// Creates a new TaskClock
    /// </summary>
    /// <param name="timeout">Initial delay</param>
    /// <param name="updateInterval">Tick interval</param>
    /// <param name="ct">Used to stop the clock</param>
    public TaskClock(TimeSpan timeout, TimeSpan updateInterval, CancellationToken ct)
    {
        _bucketSize = timeout + updateInterval;
        _updateInterval = updateInterval;
        _ct = ct;
    }

    /// <summary>
    /// Starts the clock
    /// </summary>
    public void Start()
    {
        CurrentBucket = Task.Delay(_bucketSize, _ct);
        _ = SafeTask.Run(async () => {
            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    CurrentBucket = Task.Delay(_bucketSize, _ct);
                    await Task.Delay(_updateInterval, _ct);
                }
                catch (OperationCanceledException)
                {
                    //pass, expected
                }
            }
        });
    }
}