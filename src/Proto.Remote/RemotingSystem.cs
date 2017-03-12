// -----------------------------------------------------------------------
//  <copyright file="RemotingSystem.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Grpc.Core;

namespace Proto.Remote
{
    public static class RemotingSystem
    {
        private static Server _server;
        public static PID EndpointManagerPid { get; private set; }

        public static void Start(string host, int port)
        {
            Start(host, port, new RemoteConfig());
        }

        public static void Start(string host, int port, RemoteConfig config)
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            _server = new Server
            {
                Services = {Remoting.BindService(new EndpointReader())},
                Ports = {new ServerPort(host, port, ServerCredentials.Insecure)},

            };
            _server.Start();

            var boundPort = _server.Ports.Single().BoundPort;
            var addr = host + ":" + boundPort;
            ProcessRegistry.Instance.Address = addr;
            
            var props = Actor.FromProducer(() => new EndpointManager(config));
            EndpointManagerPid = Actor.Spawn(props);

            Console.WriteLine($"[REMOTING] Starting Proto.Actor server on {addr}");
        }
    }
}