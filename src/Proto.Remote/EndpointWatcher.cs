// -----------------------------------------------------------------------
//   <copyright file="EndpointWatcher.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointWatcher : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointWatcher>();
        private readonly string _address; //for logging

        private readonly Behavior _behavior;
        private readonly EndpointManager _endpointManager;
        private readonly ActorSystem _system;
        private readonly Dictionary<string, HashSet<PID>> _watched = new Dictionary<string, HashSet<PID>>();

        public EndpointWatcher(EndpointManager endpointManager, ActorSystem system, string address)
        {
            _endpointManager = endpointManager;
            _system = system;
            _address = address;
            _behavior = new Behavior(ConnectedAsync);
        }

        private static Task Ignore => Actor.Done;

        public Task ReceiveAsync(IContext context) => _behavior.ReceiveAsync(context);

        private Task ConnectedAsync(IContext context) =>
            context.Message switch
            {
                RemoteTerminate msg       => RemoteTerminate(msg),
                EndpointTerminatedEvent _ => EndpointTerminated(context),
                RemoteUnwatch msg         => RemoteUnwatch(msg),
                RemoteWatch msg           => RemoteWatch(msg),
                Stopped _                 => Stopped(),
                _                         => Ignore
            };

        private Task TerminatedAsync(IContext context) =>
            context.Message switch
            {
                RemoteWatch msg           => RemoteWatchWhenTerminated(msg),
                EndpointConnectedEvent _  => EndpointConnectedEvent(),
                RemoteUnwatch _           => Ignore,
                EndpointTerminatedEvent _ => Ignore,
                RemoteTerminate _         => Ignore,
                _                         => Ignore
            };

        private Task Stopped()
        {
            Logger.LogDebug("[EndpointWatcher] Stopped at {Address}", _address);
            return Actor.Done;
        }

        private Task RemoteWatch(RemoteWatch msg)
        {
            if (_watched.TryGetValue(msg.Watcher.Id, out var pidSet))
            {
                pidSet.Add(msg.Watchee);
            }
            else
            {
                _watched[msg.Watcher.Id] = new HashSet<PID> {msg.Watchee};
            }

            var w = new Watch(msg.Watcher);
            _endpointManager.SendMessage(msg.Watchee, w, -1);
            return Actor.Done;
        }

        private Task RemoteUnwatch(RemoteUnwatch msg)
        {
            if (_watched.TryGetValue(msg.Watcher.Id, out var pidSet))
            {
                pidSet.Remove(msg.Watchee);

                if (pidSet.Count == 0)
                {
                    _watched.Remove(msg.Watcher.Id);
                }
            }

            var w = new Unwatch(msg.Watcher);
            _endpointManager.SendMessage(msg.Watchee, w, -1);
            return Actor.Done;
        }

        private Task EndpointTerminated(IContext context)
        {
            Logger.LogDebug("[EndpointWatcher] Handle terminated address {Address}", _address);

            foreach (var (id, pidSet) in _watched)
            {
                var watcherPid = new PID(_system.ProcessRegistry.Address, id);
                var watcherRef = _system.ProcessRegistry.Get(watcherPid);

                if (watcherRef == _system.DeadLetter)
                {
                    continue;
                }

                foreach (var t in pidSet.Select(
                    pid => new Terminated
                    {
                        Who = pid,
                        AddressTerminated = true
                    }
                ))
                {
                    //send the address Terminated event to the Watcher
                    watcherPid.SendSystemMessage(_system, t);
                }
            }

            _watched.Clear();
            _behavior.Become(TerminatedAsync);
            if (context.Self != null)
            {
                context.Stop(context.Self);
            }

            return Actor.Done;
        }

        private Task RemoteTerminate(RemoteTerminate msg)
        {
            if (_watched.TryGetValue(msg.Watcher.Id, out var pidSet))
            {
                pidSet.Remove(msg.Watchee);

                if (pidSet.Count == 0)
                {
                    _watched.Remove(msg.Watcher.Id);
                }
            }

            //create a terminated event for the Watched actor
            var t = new Terminated {Who = msg.Watchee};

            //send the address Terminated event to the Watcher
            msg.Watcher.SendSystemMessage(_system, t);
            return Actor.Done;
        }

        private Task RemoteWatchWhenTerminated(RemoteWatch msg)
        {
            msg.Watcher.SendSystemMessage(
                _system,
                new Terminated
                {
                    AddressTerminated = true,
                    Who = msg.Watchee
                }
            );
            return Actor.Done;
        }

        private Task EndpointConnectedEvent()
        {
            Logger.LogDebug("[EndpointWatcher] Handle restart address {Address}", _address);
            _behavior.Become(ConnectedAsync);
            return Actor.Done;
        }
    }
}