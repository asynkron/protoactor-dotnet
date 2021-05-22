// -----------------------------------------------------------------------
// <copyright file="Process.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

namespace Proto
{
    public abstract class Process
    {
        protected Process(ActorSystem system) => System = system;

        protected ActorSystem System { get; }

        protected internal abstract void SendUserMessage(PID pid, object message);

        protected internal abstract void SendSystemMessage(PID pid, object message);

        public virtual void Stop(PID pid) => SendSystemMessage(pid, Proto.Stop.Instance);
    }
}