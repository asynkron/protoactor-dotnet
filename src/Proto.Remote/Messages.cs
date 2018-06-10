// -----------------------------------------------------------------------
//   <copyright file="Messages.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Remote
{
    public sealed class EndpointTerminatedEvent
    {
        public string Address { get; set; }
    }
    public sealed class EndpointConnectedEvent
    {
        public string Address { get; set; }
    }

    public class RemoteTerminate
    {
        public RemoteTerminate(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }

    public class RemoteWatch
    {
        public RemoteWatch(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }

    public class RemoteUnwatch
    {
        public RemoteUnwatch(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }

    public class RemoteDeliver
    {
        public RemoteDeliver(Proto.MessageHeader header, object message, PID target, PID sender, int serializerId)
        {
            Header = header;
            Message = message;
            Target = target;
            Sender = sender;
            SerializerId = serializerId;
        }

        public Proto.MessageHeader Header { get; }
        public object Message { get; }
        public PID Target { get; }
        public PID Sender { get; }

        public int SerializerId { get; }
    }

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

    public sealed partial class ActorPidResponse
    {
        public static ActorPidResponse TimeOut = new ActorPidResponse() { StatusCode = (int)ResponseStatusCode.Timeout };
        public static ActorPidResponse Unavailable = new ActorPidResponse() { StatusCode = (int)ResponseStatusCode.Unavailable };
        public static ActorPidResponse Err = new ActorPidResponse() { StatusCode = (int)ResponseStatusCode.Error };
    }

}