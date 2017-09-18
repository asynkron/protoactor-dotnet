// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public static class Remote
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Remote).FullName);

        private static Server _server;
        private static readonly Dictionary<string, Props> Kinds = new Dictionary<string, Props>();
        public static PID EndpointManagerPid { get; private set; }
        public static PID ActivatorPid { get; private set; }

        private static EndpointReader _endpointReader;
        private static Subscription<object> _endpointTermEvnSub;
        private static Subscription<object> _endpointConnEvnSub;

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
            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            _endpointReader = new EndpointReader();
            _server = new Server
            {
                Services = { Remoting.BindService(_endpointReader) },
                Ports = { new ServerPort(hostname, port, config.ServerCredentials) }
            };
            _server.Start();

            var boundPort = _server.Ports.Single().BoundPort;
            var boundAddr = $"{hostname}:{boundPort}";
            var addr = $"{config.AdvertisedHostname??hostname}:{config.AdvertisedPort?? boundPort}";
            ProcessRegistry.Instance.Address = addr;

            SpawnEndpointManager(config);
            SpawnActivator();

            Logger.LogDebug($"Starting Proto.Actor server on {boundAddr} ({addr})");
        }

        public static void Stop(bool gracefull = true)
        {
            try
            {
                if (gracefull)
                {
                    _endpointReader.Suspend(true);
                    StopEndPointManager();
                    StopActivator();
                    _server.ShutdownAsync().Wait(10000);
                    _server.KillAsync().Wait(5000);
                }
                else
                {
                    _server.KillAsync().Wait(10000);
                }
            }
            catch(Exception ex)
            {
                Logger.LogError("Proto.Actor server shut down with error:\n" + ex.Message);
            }

            Logger.LogDebug($"Proto.Actor server stopped on {ProcessRegistry.Instance.Address}. Graceful:{gracefull}");
        }

        private static void SpawnActivator()
        {
            var props = Actor.FromProducer(() => new Activator());
            ActivatorPid = Actor.SpawnNamed(props,"activator");
        }

        private static void StopActivator()
        {
            ActivatorPid.Stop();
        }

        private static void SpawnEndpointManager(RemoteConfig config)
        {
            var props = Actor.FromProducer(() => new EndpointManager(config));
            EndpointManagerPid = Actor.Spawn(props);
            _endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(EndpointManagerPid.Tell);
            _endpointConnEvnSub = EventStream.Instance.Subscribe<EndpointConnectedEvent>(EndpointManagerPid.Tell);
        }

        private static void StopEndPointManager()
        {
            EndpointManagerPid.Tell(new StopEndpointManager());
            EventStream.Instance.Unsubscribe(_endpointTermEvnSub.Id);
            EventStream.Instance.Unsubscribe(_endpointConnEvnSub.Id);
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
                Name = name
            }, timeout);

            return res.Pid;
        }

        public static void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, _) = Proto.MessageEnvelope.Unwrap(msg);

            var env = new RemoteDeliver(message, pid, sender, serializerId);
            EndpointManagerPid.Tell(env);
        }
    }
}