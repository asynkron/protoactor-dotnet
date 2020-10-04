// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    [PublicAPI]
    public class Remote
    {
        private static readonly ILogger Logger = Log.CreateLogger<Remote>();
        
        private readonly ActorSystem _system;
        private EndpointManager _endpointManager = null!;
        private EndpointReader _endpointReader = null!;
        private HealthServiceImpl _healthCheck = null!;

        private Server _server = null!;

        public Remote(ActorSystem system,string host, int port, RemoteConfig config) : this(system,config.WithHost(host).WithPort(port))
        {
        }
        
        public Remote(ActorSystem system,RemoteConfig config)
        {
            _system = system;
            Config = config;
        }

        public RemoteConfig Config { get; private set; }
        public PID? ActivatorPid { get; private set; }

        public string[] GetKnownKinds() => Config.KnownKinds.Keys.ToArray();

        // public void RegisterKnownKind(string kind, Props props) => Config.KnownKinds.Add(kind, props);
        //
        // public void UnregisterKnownKind(string kind) => Config.KnownKinds.Remove(kind);
        public Props GetKnownKind(string kind)
        {
            if (!Config.KnownKinds.TryGetValue(kind, out var props))
            {
                throw new ArgumentException($"No Props found for kind '{kind}'");
            }

            return props;
        }

        public Task StartAsync()
        {
            var config = Config;
            _endpointManager = new EndpointManager(this, _system);
            _endpointReader = new EndpointReader(_system, _endpointManager, config.Serialization);
            _healthCheck = new HealthServiceImpl();
            _system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(this, _system, _endpointManager, pid)
            );

            _server = new Server
            {
                Services =
                {
                    Remoting.BindService(_endpointReader),
                    Health.BindService(_healthCheck)
                },
                Ports = {new ServerPort(config.Host, config.Port, config.ServerCredentials)}
            };
            _server.Start();

            var boundPort = _server.Ports.Single().BoundPort;
            _system.SetAddress(config.AdvertisedHostname ?? config.Host, config.AdvertisedPort ?? boundPort
            );
            _endpointManager.Start();
            SpawnActivator();

            Logger.LogDebug("Starting Proto.Actor server on {Host}:{Port} ({Address})", config.Host, boundPort,
                _system.Address
            );

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            try
            {
                if (graceful)
                {
                    _endpointManager.Stop();
                    _endpointReader.Suspend(true);
                    StopActivator();
                    await _server.KillAsync(); //TODO: was ShutdownAsync but that never returns?
                }
                else
                {
                    await _server.KillAsync();
                }

                Logger.LogDebug(
                    "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                    _system.Address, graceful
                );
            }
            catch (Exception ex)
            {
                await _server.KillAsync();

                Logger.LogError(
                    ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                    _system.Address, ex.Message
                );
            }
        }

        /// <summary>
        ///     Span a remote actor with auto-generated name
        /// </summary>
        /// <param name="address">Remote node address</param>
        /// <param name="kind">Actor kind, must be known on the remote node</param>
        /// <param name="timeout">Timeout for the confirmation to be received from the remote node</param>
        /// <returns></returns>
        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout) =>
            SpawnNamedAsync(address, "", kind, timeout);

        public async Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            var activator = ActivatorForAddress(address);

            var res = await _system.Root.RequestAsync<ActorPidResponse>(
                activator, new ActorPidRequest
                {
                    Kind = kind,
                    Name = name
                }, timeout
            );

            return res;

            static PID ActivatorForAddress(string address) => new PID(address, "activator");
        }

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

            var env = new RemoteDeliver(header!, message, pid, sender!, serializerId);
            _endpointManager.RemoteDeliver(env);
        }

        private void SpawnActivator()
        {
            var props = Props.FromProducer(() => new Activator(this, _system))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            ActivatorPid = _system.Root.SpawnNamed(props, "activator");
        }

        private void StopActivator() => _system.Root.Stop(ActivatorPid);
    }
}