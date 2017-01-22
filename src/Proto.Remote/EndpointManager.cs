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
            if (msg is MessageEnvelope)
            {
                var env = (MessageEnvelope) msg;
                PID pid;
                if (!_connections.TryGetValue(env.Target.Address, out pid))
                {
                    var props =
                        Actor.FromProducer(() => new EndpointWriter(env.Target.Address))
                            .WithMailbox(() => new EndpointWriterMailbox());
                    pid = context.Spawn(props);
                    _connections.Add(env.Target.Address, pid);
                }
                pid.Tell(msg);
                return Actor.Done;
            }
            return Actor.Done;


            //    switch (context.Message)
            //    {
            //        case null:
            //            Console.WriteLine("null");
            //            return Actor.Done;
            //        case Started _:
            //            Console.WriteLine("[REMOTING] Started EndpointManager");
            //            return Actor.Done;
            //        case MessageEnvelope env:
            //            PID pid;
            //            if (!_connections.TryGetValue(env.Target.Address, out pid))
            //            {
            //                Console.WriteLine("Resolving EndpointWriter for {0}", env.Target.Address);
            //                var props =
            //                    Actor.FromProducer(() => new EndpointWriter(env.Target.Address))
            //                        .WithMailbox(() => new EndpointWriterMailbox());
            //                pid = context.Spawn(props);
            //                _connections.Add(env.Target.Address, pid);
            //            }
            //            pid.Tell(env);
            //            return Actor.Done;
            //        default:
            //            return Actor.Done;
            //    }
            //}
        }
    }
}