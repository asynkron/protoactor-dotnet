// -----------------------------------------------------------------------
//  <copyright file="RemotingSystem.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Grpc.Core;

namespace Proto.Remote
{
    public static class RemotingSystem
    {
        private static Server _server;
        public static PID EndpointManagerPid { get; private set; }

        public static void Start(string host, int port)
        {
            var addr = host + ":" + port;
            ProcessRegistry.Instance.Address = addr;
            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            _server = new Server
            {
                Services = {Remoting.BindService(new EndpointReader())},
                Ports = {new ServerPort(host, port, ServerCredentials.Insecure)}
            };
            _server.Start();
            var emProps =
                Actor.FromProducer(() => new EndpointManager());
            EndpointManagerPid = Actor.Spawn(emProps);

            Console.WriteLine($"[REMOTING] Starting Proto.Actor server on {addr}");
        }
    }
}