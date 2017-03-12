// -----------------------------------------------------------------------
//  <copyright file="RemotingSystem.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Grpc.Core;
using System.Collections.Generic;

namespace Proto.Remote
{
    public static class RemotingSystem
    {
        private static Server _server;
        public static PID EndpointManagerPid { get; private set; }

        public static string[] GetKnownKinds()
        {
            return kinds.Keys.ToArray();
        }

        public static void Start(string host, int port)
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            _server = new Server
            {
                Services = {Remoting.BindService(new EndpointReader())},
                Ports = {new ServerPort(host, port, ServerCredentials.Insecure)}
            };
            _server.Start();

            var boundPort = _server.Ports.Single().BoundPort;
            var addr = host + ":" + boundPort;
            ProcessRegistry.Instance.Address = addr;
            
            var props = Actor.FromProducer(() => new EndpointManager());
            EndpointManagerPid = Actor.Spawn(props);

            Console.WriteLine($"[REMOTING] Starting Proto.Actor server on {addr}");
        }

        private static Dictionary<string, Props> kinds = new Dictionary<string, Props>();
        public static void Register(string kind,Props props)
        {
            kinds.Add(kind, props);
        }
    }
}