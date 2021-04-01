// -----------------------------------------------------------------------
// <copyright file="Dispatcher.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    public interface IMessageInvoker
    {
        CancellationTokenSource? CancellationTokenSource { get; }

        Task InvokeSystemMessageAsync(object msg);

        Task InvokeUserMessageAsync(object msg);

        void EscalateFailure(Exception reason, object? message);
    }

    public interface IDispatcher
    {
        int Throughput { get; }

        void Schedule(Func<Task> runner);
    }

    public static class Dispatchers
    {
        public static ThreadPoolDispatcher DefaultDispatcher { get; } = new();
        public static SynchronousDispatcher SynchronousDispatcher { get; } = new();
    }

    public sealed class SynchronousDispatcher : IDispatcher
    {
        private const int DefaultThroughput = 300;

        public SynchronousDispatcher(int throughput = DefaultThroughput) => Throughput = throughput;

        public int Throughput { get; }

        public void Schedule(Func<Task> runner) => runner().Wait();
    }

    public sealed class ThreadPoolDispatcher : IDispatcher
    {
        private const int DefaultThroughput = 300;

        public ThreadPoolDispatcher(int throughput = DefaultThroughput) => Throughput = throughput;

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

    class NoopDispatcher : IDispatcher
    {
        internal static readonly IDispatcher Instance = new NoopDispatcher();
        public int Throughput => 0;

        public void Schedule(Func<Task> runner) => throw new NotImplementedException();
    }

    class NoopInvoker : IMessageInvoker
    {
        internal static readonly IMessageInvoker Instance = new NoopInvoker();

        public CancellationTokenSource CancellationTokenSource => throw new NotImplementedException();

        public Task InvokeSystemMessageAsync(object msg) => throw new NotImplementedException();

        public Task InvokeUserMessageAsync(object msg) => throw new NotImplementedException();

        public void EscalateFailure(Exception reason, object? message) => throw new NotImplementedException();
    }
}