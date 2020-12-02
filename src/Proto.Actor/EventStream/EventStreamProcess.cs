// -----------------------------------------------------------------------
// <copyright file="EventStreamProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Proto
{
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

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            //pass
        }
    }
}