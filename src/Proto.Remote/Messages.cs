// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Remote
{
    public sealed class EndpointTerminatedEvent
    {
        public string Address { get; set; } = null!;

        public override string ToString() => $"EndpointTerminatedEvent: {Address}";
    }

    public sealed class EndpointConnectedEvent
    {
        public string Address { get; set; } = null!;
    }

    public sealed class EndpointErrorEvent
    {
        public string Address { get; set; } = null!;
        public Exception Exception { get; set; } = null!;
    }

    public sealed record RemoteTerminate(PID Watcher, PID Watchee);

    public sealed record RemoteWatch(PID Watcher, PID Watchee);

    public sealed record RemoteUnwatch(PID Watcher, PID Watchee);

    public sealed record RemoteDeliver (Proto.MessageHeader Header, object Message, PID Target, PID Sender, int SerializerId);

    public class JsonMessage
    {
        //NOTE: typename should not be checked against available typenames on send
        //as the message might only exist on the receiveing side
        public JsonMessage(string typeName, string json)
        {
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public string Json { get; set; }
        public string TypeName { get; set; }
    }
    
}