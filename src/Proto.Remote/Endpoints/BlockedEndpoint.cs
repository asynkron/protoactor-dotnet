// -----------------------------------------------------------------------
//   <copyright file="BlockedEndpoint.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class BlockedEndpoint : IEndpoint
    {
        private readonly ActorSystem _system;
        public BlockedEndpoint(ActorSystem system) => _system = system;
        public Channel<RemoteMessage> Outgoing { get; } = Channel.CreateUnbounded<RemoteMessage>();
        public ConcurrentStack<RemoteMessage> OutgoingStash { get; } = new();
        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return default;
        }
        public void RemoteTerminate(PID watcher, Terminated terminated) => watcher.SendSystemMessage(_system, terminated);
        public void RemoteUnwatch(PID pid, Unwatch unwatch) { }
        public void RemoteWatch(PID pid, Watch watch) => _system.Root.Send(watch.Watcher, new Terminated { Who = pid, Why = TerminatedReason.AddressTerminated });
        public void SendMessage(PID pid, object msg)
        {
            var (message, sender, _) = Proto.MessageEnvelope.Unwrap(msg);
            switch (message)
            {
                case PoisonPill or Stop when sender is not null:
                    _system.Root.Send(sender, new Terminated { Who = pid, Why = TerminatedReason.AddressTerminated });
                    break;
                default:
                    if (sender is not null)
                        _system.Root.Send(sender, new DeadLetterResponse { Target = pid });
                    else
                        _system.EventStream.Publish(new DeadLetterEvent(pid, message, sender));
                    break;
            }
        }
    }
}
