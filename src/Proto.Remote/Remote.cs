// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
// Modified file in context of repo fork : https://github.com/Optis-World/protoactor-dotnet
// Copyright 2019 ANSYS, Inc.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class Remote
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Remote).FullName);

        private Server server;
        private readonly Dictionary<string, Props> Kinds = new Dictionary<string, Props>();
        public RemoteConfig RemoteConfig { get; private set; }
        public PID ActivatorPid { get; private set; }

        private EndpointReader _endpointReader;
        private EndpointManager _endpointManager;
        private readonly ActorSystem _system;
        public Serialization Serialization
        {
            get;
        }

        public string[] GetKnownKinds() => Kinds.Keys.ToArray();

        public void RegisterKnownKind(string kind, Props props) => Kinds.Add(kind, props);

        // Modified class in context of repo fork : https://github.com/Optis-World/protoactor-dotnet
        public void UnregisterKnownKind(string kind) => Kinds.Remove(kind);

        public Props GetKnownKind(string kind)
        {
            if (Kinds.TryGetValue(kind, out var props))
            {
                return props;
            }

            throw new ArgumentException($"No Props found for kind '{kind}'");
        }

        public Remote(ActorSystem system, Serialization serialization)
        {
            _system = system;
            Serialization = serialization;

        }

        public void Start(string hostname, int port) => Start(hostname, port, new RemoteConfig());

        public void Start(string hostname, int port, RemoteConfig config)
        {
            RemoteConfig = config;
            _endpointManager = new EndpointManager(this, _system);
            _endpointReader = new EndpointReader(_system, _endpointManager, Serialization);
            _system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(this, _system, _endpointManager, pid));

            server = new Server
            {
                Services = { Remoting.BindService(_endpointReader) },
                Ports = { new ServerPort(hostname, port, config.ServerCredentials) }
            };
            server.Start();

            var boundPort = server.Ports.Single().BoundPort;
            _system.ProcessRegistry.SetAddress(config.AdvertisedHostname ?? hostname, config.AdvertisedPort ?? boundPort);
            _endpointManager.Start();
            SpawnActivator();

            Logger.LogDebug("Starting Proto.Actor server on {Host}:{Port} ({Address})", hostname, boundPort, _system.ProcessRegistry.Address);
        }

        public async Task Shutdown(bool graceful = true)
        {
            try
            {
                if (graceful)
                {
                    _endpointManager.Stop();
                    _endpointReader.Suspend(true);
                    StopActivator();
                    await server.ShutdownAsync();
                }
                else
                {
                    await server.KillAsync();
                }

                Logger.LogDebug(
                    "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                    _system.ProcessRegistry.Address, graceful
                );
            }
            catch (Exception ex)
            {
                await server.KillAsync();

                Logger.LogError(
                    ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                    _system.ProcessRegistry.Address, ex.Message
                );
            }
        }

        private void SpawnActivator()
        {
            var props = Props.FromProducer(() => new Activator(this, _system)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            ActivatorPid = _system.Root.SpawnNamed(props, "activator");
        }

        private void StopActivator() => _system.Root.Stop(ActivatorPid);

        public PID ActivatorForAddress(string address) => new PID(address, "activator");

        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout) => SpawnNamedAsync(address, "", kind, timeout);

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
        }

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

            var env = new RemoteDeliver(header, message, pid, sender, serializerId);
            _endpointManager.RemoteDeliver(env);
        }
    }
}