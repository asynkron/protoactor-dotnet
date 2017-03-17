// -----------------------------------------------------------------------
//  <copyright file="RemotingSystem.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Grpc.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public static class RemotingSystem
    {
        private static Server _server;
        public static PID EndpointManagerPid { get; private set; }
        public static PID ActivatorPID { get; private set; }

        public static string[] GetKnownKinds()
        {
            return Kinds.Keys.ToArray();
        }

        public static void Start(string host, int port)
        {
            Start(host, port, new RemoteConfig());
        }

        public static void Start(string host, int port, RemoteConfig config)
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            _server = new Server
            {
                Services = { Remoting.BindService(new EndpointReader()) },
                Ports = { new ServerPort(host, port, config.ServerCredentials) },
            };
            _server.Start();

            var boundPort = _server.Ports.Single().BoundPort;
            var addr = host + ":" + boundPort;
            ProcessRegistry.Instance.Address = addr;

            SpawnEndpointManager(config);
            SpawnActivator();

            Console.WriteLine($"[REMOTING] Starting Proto.Actor server on {addr}");
        }

        private static void SpawnActivator()
        {
            var props = Actor.FromProducer(() => new Activator());
            ActivatorPID = Actor.Spawn(props);
        }

        private static void SpawnEndpointManager(RemoteConfig config)
        {
            var props = Actor.FromProducer(() => new EndpointManager(config));
            EndpointManagerPid = Actor.Spawn(props);
        }

        private static Dictionary<string, Props> Kinds { get; } = new Dictionary<string, Props>();
        public static void Register(string kind,Props props)
        {
            Kinds.Add(kind, props);
        }
        public static Props GetKindProps(string kind)
        {
            return Kinds[kind];
        }

        public static PID ActivatorForAddress(string address)
        {
            return new PID(address, "activator");
        }

        public static Task<PID> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return SpawnNamedAsync(address, "", kind, timeout);
        }

        private static async Task<PID> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            var activator = ActivatorForAddress(address);

            var res = await activator.RequestAsync<ActorPidResponse>(new ActorPidRequest
            {
                Kind = kind,
                Name = name,
            }, timeout);

            return res.Pid;
        }
    }
}