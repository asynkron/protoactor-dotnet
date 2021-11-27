// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Remote
{
    public sealed record EndpointTerminatedEvent(bool OnError, string? Address, string? ActorSystemId)
    {
        public override string ToString() => $"EndpointTerminatedEvent: {Address ?? ActorSystemId}";
    }

    public sealed class RemoteDeliver
    {
        public RemoteDeliver()
        {
            Message = new();
            Target = new PID();
        }
        public Proto.MessageHeader? Header { get; set; }
        public object Message { get; set; }
        public PID Target { get; set; }
        public PID? Sender { get; set; }
    }
}