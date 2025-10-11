// -----------------------------------------------------------------------
//   <copyright file="IRemoteEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Proto.Remote;

public interface IRemoteEndpoint : IAsyncDisposable
{
    Channel<RemoteDeliver[]> Outgoing { get; }
    ConcurrentStack<RemoteDeliver[]> OutgoingStash { get; }
    bool IsActive { get; }

    void SendMessage(PID pid, object message);
    void RemoteTerminate(PID target, Terminated terminated);
    void RemoteWatch(PID pid, Watch watch);
    void RemoteUnwatch(PID pid, Unwatch unwatch);
}