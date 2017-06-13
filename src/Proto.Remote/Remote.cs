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
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public static class Remote
    {
        private static ILogger _logger = Log.CreateLogger(typeof(Remote).FullName);

        private static Server _server;
        private static readonly Dictionary<string, Props> Kinds = new Dictionary<string, Props>();
        public static PID EndpointManagerPid { get; private set; }
        public static PID ActivatorPID { get; private set; }

        public static string[] GetKnownKinds()
        {
            return Kinds.Keys.ToArray();
        }

        public static void RegisterKnownKind(string kind, Props props)
        {
            Kinds.Add(kind, props);
        }
        public static Props GetKnownKind(string kind)
        {
            if (Kinds.TryGetValue(kind, out var props)){
                return props;
            }
            throw new ArgumentException($"No Props found for kind '{kind}'");
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

            _logger.LogDebug($"Starting Proto.Actor server on {addr}");;
        }

        private static void SpawnActivator()
        {
            var props = Actor.FromProducer(() => new Activator());
            ActivatorPID = Actor.SpawnNamed(props,"activator");
        }

        private static void SpawnEndpointManager(RemoteConfig config)
        {
            var props = Actor.FromProducer(() => new EndpointManager(config));
            EndpointManagerPid = Actor.Spawn(props);
            EventStream.Instance.Subscribe<EndpointTerminatedEvent>(x => EndpointManagerPid.SendAsync(x));
        }

        public static PID ActivatorForAddress(string address)
        {
            return new PID(address, "activator");
        }

        public static Task<PID> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return SpawnNamedAsync(address, "", kind, timeout);
        }

        public static async Task<PID> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
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