// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Proto.Mailbox;

namespace Proto.Remote;

public class RemoteProcess : Process
{
    private readonly EndpointManager _endpointManager;
    private readonly string? _systemId;
    private long _lastUsedTick;

    public RemoteProcess(ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
    {
        _endpointManager = endpointManager;
        pid.TryGetSystemId(system, out _systemId);
        _lastUsedTick = Stopwatch.GetTimestamp();
    }

    protected internal override void SendUserMessage(PID pid, object message) => Send(pid, message);

    protected internal override void SendSystemMessage(PID pid, SystemMessage message) => Send(pid, message);

    private void Send(PID pid, object msg)
    {
        var endpoint = GetEndpoint(pid);
        // If the target endpoint is down or blocked, we get a BlockedEndpoint instance
        switch (msg)
        {
            case Watch w:
                endpoint.RemoteWatch(pid, w);
                break;
            case Unwatch uw:
                endpoint.RemoteUnwatch(pid, uw);
                break;
            default:
                endpoint.SendMessage(pid, msg);
                break;
        }
            
        _lastUsedTick = Stopwatch.GetTimestamp();
    }

    private IEndpoint GetEndpoint(PID pid)
    {
        if (_systemId != null)
        {
            return _endpointManager.GetClientEndpoint(_systemId);
        }

        return _endpointManager.GetOrAddServerEndpoint(pid.Address);
    }

    internal long LastUsedTick => _lastUsedTick;
}