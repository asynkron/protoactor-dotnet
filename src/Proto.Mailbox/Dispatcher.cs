// -----------------------------------------------------------------------
//  <copyright file="Dispatcher.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
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
    }

    public sealed class ThreadPoolDispatcher : IDispatcher
    {
        public ThreadPoolDispatcher()
        {
            Throughput = 300;
        }

        public void Schedule(Func<Task> runner)
        {
            Task.Factory.StartNew(runner, TaskCreationOptions.None);
        }

        public int Throughput { get; set; }
    }
}