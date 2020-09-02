// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
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
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class Remote
    {
        private PID? _activatorPid;
        public bool IsStarted { get; private set; }
        private readonly ActorSystem _system;
        private readonly EndpointManager _endpointManager;
        private readonly RemoteKindRegistry _remoteKindRegistry;

        public Remote(ActorSystem system, RemoteKindRegistry remoteKindRegistry, EndpointManager endpointManager)
        {
            _system = system;
            _remoteKindRegistry = remoteKindRegistry;
            _endpointManager = endpointManager;
            system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(system, _endpointManager, pid));
        }

        public virtual void Start()
        {
            if (IsStarted) return;
            IsStarted = true;
            _endpointManager.Start();
            SpawnActivator();
        }

        public virtual Task ShutdownAsync(bool graceful = true)
        {
            if (!IsStarted) return Task.CompletedTask;
            else IsStarted = false;
            if (graceful)
            {
                _endpointManager.Stop();
                StopActivator();
            }
            return Task.CompletedTask;
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
        }

        private PID ActivatorForAddress(string address) => new PID(address, "activator");
        private void SpawnActivator()
        {
            var props = Props.FromProducer(() => new Activator(_remoteKindRegistry, _system))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _activatorPid = _system.Root.SpawnNamed(props, "activator");
        }

        private void StopActivator() => _system.Root.Stop(_activatorPid);

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            _endpointManager.SendMessage(pid, msg, serializerId);
        }
    }
}