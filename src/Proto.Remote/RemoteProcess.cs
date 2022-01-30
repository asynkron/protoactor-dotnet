// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;


namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private readonly EndpointManager _endpointManager;
        private readonly PID _pid;
        private readonly string? _systemId;
        private long _lastUsedTick;

        public RemoteProcess(ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
        {
            _endpointManager = endpointManager;
            _pid = pid;
            pid.TryGetSystemId(system, out _systemId);
            _lastUsedTick = Stopwatch.GetTimestamp();
        }

        protected internal override void SendUserMessage(PID _, object message) => Send(message);

        protected internal override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            // If the target endpoint is down or blocked, we get a BlockedEndpoint instance
            var endpoint = _systemId is not null ? _endpointManager.GetClientEndpoint(_systemId) : _endpointManager.GetOrAddServerEndpoint(_pid.Address);
            switch (msg)
            {
                case Watch w:
                    endpoint.RemoteWatch(_pid, w);
                    break;
                case Unwatch uw:
                    endpoint.RemoteUnwatch(_pid, uw);
                    break;
                default:
                    endpoint.SendMessage(_pid, msg);
                    break;
            }
            
            _lastUsedTick = Stopwatch.GetTimestamp();
        }

        internal long LastUsedTick => _lastUsedTick;
    }
}