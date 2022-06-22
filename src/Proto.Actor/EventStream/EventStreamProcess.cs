// -----------------------------------------------------------------------
// <copyright file="EventStreamProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

using JetBrains.Annotations;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto;

[PublicAPI]
public class EventStreamProcess : Process
{
    public EventStreamProcess(ActorSystem system) : base(system)
    {
    }

    protected internal override void SendUserMessage(PID pid, object message)
    {
        var (msg, _, _) = MessageEnvelope.Unwrap(message);
        System.EventStream.Publish(msg);
    }

    protected internal override void SendSystemMessage(PID pid, SystemMessage message)
    {
        //pass
    }
}