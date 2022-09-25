// -----------------------------------------------------------------------
// <copyright file="Dispatcher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox;

public interface IMessageInvoker
{
    CancellationTokenSource? CancellationTokenSource { get; }

    ValueTask InvokeSystemMessageAsync(SystemMessage msg);

    ValueTask InvokeUserMessageAsync(object msg);

    void EscalateFailure(Exception reason, object? message);
}

/// <summary>
///     An abstraction for running tasks
/// </summary>
public interface IDispatcher
{
    /// <summary>
    ///     Used by mailbox to determine how many messages to process before yielding
    /// </summary>
    int Throughput { get; }

    /// <summary>
    ///     Runs the task
    /// </summary>
    /// <param name="runner"></param>
    void Schedule(Func<Task> runner);
}

public static class Dispatchers
{
    /// <summary>
    ///     Schedules the task on the <see cref="ThreadPool" />
    /// </summary>
    public static ThreadPoolDispatcher DefaultDispatcher { get; } = new();

    /// <summary>
    ///     Runs and awaits the task
    /// </summary>
    public static SynchronousDispatcher SynchronousDispatcher { get; } = new();
}

/// <summary>
///     Runs and awaits the task
/// </summary>
public sealed class SynchronousDispatcher : IDispatcher
{
    private const int DefaultThroughput = 300;

    public SynchronousDispatcher(int throughput = DefaultThroughput)
    {
        Throughput = throughput;
    }

    public int Throughput { get; }

    public void Schedule(Func<Task> runner) => runner().Wait();
}

/// <summary>
///     Schedules the task on the <see cref="ThreadPool" />
/// </summary>
public sealed class ThreadPoolDispatcher : IDispatcher
{
    private const int DefaultThroughput = 300;

    public ThreadPoolDispatcher(int throughput = DefaultThroughput)
    {
        Throughput = throughput;
    }

    public void Schedule(Func<Task> runner) => Task.Factory.StartNew(runner, TaskCreationOptions.None);

    public int Throughput { get; set; }
}

/// <summary>
///     This must be created on the UI thread after a SynchronizationContext has been created.  Otherwise, an error will
///     occur.
/// </summary>
public sealed class CurrentSynchronizationContextDispatcher : IDispatcher
{
    private const int DefaultThroughput = 300;
    private readonly TaskScheduler _scheduler;

    public CurrentSynchronizationContextDispatcher(int throughput = DefaultThroughput)
    {
        _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        Throughput = throughput;
    }

    public void Schedule(Func<Task> runner) =>
        Task.Factory.StartNew(runner, CancellationToken.None, TaskCreationOptions.None, _scheduler);

    public int Throughput { get; }
}

/// <summary>
///     Throws <see cref="NotImplementedException" /> instead of running the task
/// </summary>
internal class NoopDispatcher : IDispatcher
{
    internal static readonly IDispatcher Instance = new NoopDispatcher();
    public int Throughput => 0;

    public void Schedule(Func<Task> runner) => throw new NotImplementedException();
}

internal class NoopInvoker : IMessageInvoker
{
    internal static readonly IMessageInvoker Instance = new NoopInvoker();

    public CancellationTokenSource CancellationTokenSource => throw new NotImplementedException();

    public ValueTask InvokeSystemMessageAsync(SystemMessage msg) => throw new NotImplementedException();

    public ValueTask InvokeUserMessageAsync(object msg) => throw new NotImplementedException();

    public void EscalateFailure(Exception reason, object? message) => throw new NotImplementedException();
}