// -----------------------------------------------------------------------
//  <copyright file="EndpointManager.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class Endpoint
    {
        public Endpoint(PID writer, PID watcher)
        {
            Writer = writer;
            Watcher = watcher;
        }

        public PID Writer { get;  }
        public PID Watcher { get;  }
    }
    public class EndpointManager : IActor
    {
        private readonly Dictionary<string, Endpoint> _connections = new Dictionary<string, Endpoint>();

        public Task ReceiveAsync(IContext context)
        {
           
            switch (context.Message)
            {
                case Started _:
                {
                    Console.WriteLine("[REMOTING] Started EndpointManager");
                    return Actor.Done;
                }
                case EndpointTerminatedEvent msg:
                {
                    var endpoint = EnsureConnected(msg.Address, context);
                    endpoint.Watcher.Tell(msg);
                    return Actor.Done;
                }
                case RemoteTerminate msg:
                {
                    var endpoint = EnsureConnected(msg.Watchee.Address, context);
                    endpoint.Watcher.Tell(msg);
                    return Actor.Done;
                }
                case RemoteWatch msg:
                {
                    var endpoint = EnsureConnected(msg.Watchee.Address, context);
                    endpoint.Watcher.Tell(msg);
                    return Actor.Done;
                }
                case RemoteUnwatch msg:
                {
                    var endpoint = EnsureConnected(msg.Watchee.Address, context);
                    endpoint.Watcher.Tell(msg);
                    return Actor.Done;
                }
                case MessageEnvelope msg:
                {
                    var endpoint = EnsureConnected(msg.Target.Address, context);
                    endpoint.Writer.Tell(msg);
                    return Actor.Done;
                }
                default:
                    return Actor.Done;
            }
        }
    

        private Endpoint EnsureConnected(string address, IContext context)
        {
            var ok = _connections.TryGetValue(address, out var endpoint);
            if (!ok)
            {
                var writerProps =
                    Actor.FromProducer(() => new EndpointWriter(address))
                        .WithMailbox(() => new EndpointWriterMailbox());
                var writer = context.Spawn(writerProps);

                var watcherProps = Actor.FromProducer(() => new EndpointWatcher(address));
                var watcher = context.Spawn(watcherProps);


                _connections.Add(address, new Endpoint(writer, watcher));
            }

            return endpoint;
        }
    }
}