﻿// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Remote
{
    public sealed record EndpointTerminatedEvent(string Address)
    {
        public override string ToString() => $"EndpointTerminatedEvent: {Address}";
    }

    public sealed record EndpointConnectedEvent(string Address);

    public sealed record EndpointErrorEvent
    {
        public string Address { get; init; } = null!;
        public Exception Exception { get; init; } = null!;
    }

    public sealed record RemoteTerminate(PID Watcher, PID Watchee);

    public sealed record RemoteWatch(PID Watcher, PID Watchee);

    public sealed record RemoteUnwatch(PID Watcher, PID Watchee);

    public sealed record RemoteDeliver(Proto.MessageHeader Header, object Message, PID Target, PID? Sender);
}