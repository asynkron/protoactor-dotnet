// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Remote
{
    public sealed record EndpointTerminatedEvent(bool OnError, string? Address, string? ActorSystemId)
    {
        public override string ToString() => $"EndpointTerminatedEvent: {Address ?? ActorSystemId}";
    }

    public sealed record RemoteDeliver(Proto.MessageHeader Header, object Message, PID Target, PID? Sender);
}