// -----------------------------------------------------------------------
// <copyright file="Process.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

using System;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto;

public abstract class Process
{
    protected Process(ActorSystem system)
    {
        System = system;
    }

    protected internal ActorSystem System { get; }

    protected internal abstract void SendUserMessage(PID pid, object message);

    protected internal abstract void SendSystemMessage(PID pid, SystemMessage message);

    public virtual void Stop(PID pid) => SendSystemMessage(pid, Proto.Stop.Instance);
}