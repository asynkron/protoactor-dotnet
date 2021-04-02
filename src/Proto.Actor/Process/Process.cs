// -----------------------------------------------------------------------
// <copyright file="Process.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
using Proto.Context;

namespace Proto
{
    public abstract class Process
    {
        protected Process(ActorSystem system) => System = system;

        protected ActorSystem System { get; }

        protected internal abstract void SendUserMessage(PID pid, object message, IExecutionContext? ec=null);

        protected internal abstract void SendSystemMessage(PID pid, object message, IExecutionContext? ec=null);
        
        public virtual void Stop(PID pid) => SendSystemMessage(pid, Proto.Stop.Instance);
    }
}