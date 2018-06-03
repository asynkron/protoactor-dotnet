// -----------------------------------------------------------------------
//  <copyright file="Dispatcher.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    public interface IMessageInvoker
    {
        Task InvokeSystemMessageAsync(object msg);
        Task InvokeUserMessageAsync(object msg);
        void EscalateFailure(Exception reason, object message);
    }


    public interface IDispatcher
    {
        int Throughput { get; }
        void Schedule(Func<Task> runner);
    }

    public static class Dispatchers
    {
        public static ThreadPoolDispatcher DefaultDispatcher { get; } = new ThreadPoolDispatcher();
        public static SynchronousDispatcher SynchronousDispatcher { get; } = new SynchronousDispatcher();
    }

    public sealed class SynchronousDispatcher : IDispatcher
    {
        public int Throughput => 300;

        public void Schedule(Func<Task> runner)
        {
            runner().Wait();
        }
    }

    public sealed class ThreadPoolDispatcher : IDispatcher
    {
        public ThreadPoolDispatcher()
        {
            Throughput = 300;
        }

        public void Schedule(Func<Task> runner) => Task.Factory.StartNew(runner, TaskCreationOptions.None);

        public int Throughput { get; set; }
    }

    /// <summary>
    /// This must be created on the UI thread after a SynhronizationContext has been created.  Otherwise, an error will occur.
    /// </summary>
    public sealed class CurrentSynchronizationContextDispatcher : IDispatcher
    {
        private readonly TaskScheduler _scheduler;

        public CurrentSynchronizationContextDispatcher()
        {
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Throughput = 300;
        }

        public void Schedule(Func<Task> runner) => Task.Factory.StartNew(runner, CancellationToken.None, TaskCreationOptions.None, _scheduler);

        public int Throughput { get; set; }
    }
}