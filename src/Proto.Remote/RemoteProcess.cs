// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using Proto.Mailbox;

namespace Proto.Remote;

public class RemoteProcess : Process
{
    private readonly EndpointManager _endpointManager;
    private readonly string? _systemId;
    private IEndpoint? _endpoint;

    public RemoteProcess(ActorSystem system, EndpointManager endpointManager, PID pid) : base(system)
    {
        _endpointManager = endpointManager;
        pid.TryGetSystemId(system, out _systemId);
        LastUsedTick = Stopwatch.GetTimestamp();
    }

    internal long LastUsedTick { get; private set; }

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

        LastUsedTick = Stopwatch.GetTimestamp();
    }

    private IEndpoint GetEndpoint(PID pid)
    {
        if (_endpoint?.IsActive == true)
        {
            return _endpoint;
        }

        if (_systemId != null)
        {
            _endpoint = null;

            return _endpointManager.GetClientEndpoint(_systemId);
        }

        _endpoint = _endpointManager.GetOrAddServerEndpoint(pid.Address);

        return _endpoint;
    }
}