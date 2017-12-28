// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public static class Remote
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Remote).FullName);

        private static Server server;
        private static readonly Dictionary<string, Props> Kinds = new Dictionary<string, Props>();
        public static RemoteConfig RemoteConfig { get; private set; }
        public static PID ActivatorPid { get; private set; }

        private static EndpointReader endpointReader;

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

        public static void Start(string hostname, int port)
        {
            Start(hostname, port, new RemoteConfig());
        }

        public static void Start(string hostname, int port, RemoteConfig config)
        {
            RemoteConfig = config;

            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            EndpointManager.Start();
            endpointReader = new EndpointReader();
            server = new Server
            {
                Services = { Remoting.BindService(endpointReader) },
                Ports = { new ServerPort(hostname, port, config.ServerCredentials) }
            };
            server.Start();

            var boundPort = server.Ports.Single().BoundPort;
            var boundAddr = $"{hostname}:{boundPort}";
            var addr = $"{config.AdvertisedHostname??hostname}:{config.AdvertisedPort?? boundPort}";
            ProcessRegistry.Instance.Address = addr;

            SpawnActivator();

            Logger.LogDebug($"Starting Proto.Actor server on {boundAddr} ({addr})");
        }

        public static void Shutdown(bool gracefull = true)
        {
            try
            {
                if (gracefull)
                {
                    EndpointManager.Stop();
                    endpointReader.Suspend(true);
                    StopActivator();
                    server.ShutdownAsync().Wait(10000);
                }
                else
                {
                    server.KillAsync().Wait(10000);
                }
                
                Logger.LogDebug($"Proto.Actor server stopped on {ProcessRegistry.Instance.Address}. Graceful:{gracefull}");
            }
            catch(Exception ex)
            {
                server.KillAsync().Wait(1000);
                Logger.LogError($"Proto.Actor server stopped on {ProcessRegistry.Instance.Address} with error:\n{ex.Message}");
            }
        }

        private static void SpawnActivator()
        {
            var props = Actor.FromProducer(() => new Activator()).WithGuardianSupervisorStrategy(Supervision.KeepAliveStrategy);
            ActivatorPid = Actor.SpawnNamed(props, "activator");
        }

        private static void StopActivator()
        {
            ActivatorPid.Stop();
        }

        public static PID ActivatorForAddress(string address)
        {
            return new PID(address, "activator");
        }

        public static Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return SpawnNamedAsync(address, "", kind, timeout);
        }

        public static async Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            var activator = ActivatorForAddress(address);

            var res = await activator.RequestAsync<ActorPidResponse>(new ActorPidRequest
            {
                Kind = kind,
                Name = name
            }, timeout);

            return res;
        }

        public static void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

            var env = new RemoteDeliver(header, message, pid, sender, serializerId);
            EndpointManager.RemoteDeliver(env);
        }
    }
}