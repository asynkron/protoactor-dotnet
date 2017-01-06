// -----------------------------------------------------------------------
//  <copyright file="EndpointManager.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Remoting
{
    public class EndpointManager : IActor
    {
        private readonly Dictionary<string, PID> _connections = new Dictionary<string, PID>();

        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;
            //TODO: convert to switch later, currently doesnt work, switching on type throws null ref error
            if (msg is Started)
            {
                Console.WriteLine("[REMOTING] Started EndpointManager");
                return Actor.Done;
            }
            else if (msg is MessageEnvelope)
            {
                var env = (MessageEnvelope) msg;
                PID pid;
                if (!_connections.TryGetValue(env.Target.Host, out pid))
                {
                    Console.WriteLine("Resolving EndpointWriter for {0}", env.Target.Host);
                    var props =
                        Actor.FromProducer(() => new EndpointWriter(env.Target.Host))
                            .WithMailbox(() => new EndpointWriterMailbox());
                    pid = context.Spawn(props);
                    _connections.Add(env.Target.Host, pid);
                }
                pid.Tell(msg);
                return Actor.Done;
            }
            else
            {
                return Actor.Done;
            }
        }
    }
}