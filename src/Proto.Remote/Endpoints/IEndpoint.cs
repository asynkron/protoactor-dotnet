// -----------------------------------------------------------------------
//   <copyright file="IEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;

namespace Proto.Remote
{
    public interface IEndpoint : IAsyncDisposable
    {
        Channel<RemoteMessage> Outgoing { get; }
        ConcurrentStack<RemoteMessage> OutgoingStash { get; }
        void SendMessage(PID pid, object message);
        void RemoteTerminate(PID target, Terminated terminated);
        void RemoteWatch(PID pid, Watch watch);
        void RemoteUnwatch(PID pid, Unwatch unwatch);
    }
}