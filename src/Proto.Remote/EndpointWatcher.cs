// -----------------------------------------------------------------------
//   <copyright file="EndpointWatcher.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
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

        private readonly Behavior _behavior;
        private readonly Dictionary<string, HashSet<PID>> _watched = new Dictionary<string, HashSet<PID>>();
        private readonly string _address; //for logging
        private readonly ActorSystem _system;
        private readonly Remote _remote;

        public EndpointWatcher(Remote remote, ActorSystem system, string address)
        {
            _remote = remote;
            _system = system;
            _address = address;
            _behavior = new Behavior(ConnectedAsync);
        }

        public Task ReceiveAsync(IContext context) => _behavior.ReceiveAsync(context);

        private Task ConnectedAsync(IContext context)
        {
            switch (context.Message)
            {
                case RemoteTerminate msg:
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
                        var t = new Terminated { Who = msg.Watchee };

                        //send the address Terminated event to the Watcher
                        msg.Watcher.SendSystemMessage(_system, t);
                        break;
                    }
                case EndpointTerminatedEvent _:
                    {
                        Logger.LogDebug("Handle terminated address {Address}", _address);

                        foreach (var (id, pidSet) in _watched)
                        {
                            var watcherPid = new PID(_system.ProcessRegistry.Address, id);
                            var watcherRef = _system.ProcessRegistry.Get(watcherPid);

                            if (watcherRef == _system.DeadLetter) continue;

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
                        context.Stop(context.Self);
                        break;
                    }
                case RemoteUnwatch msg:
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
                        _remote.SendMessage(msg.Watchee, w, -1);
                        break;
                    }
                case RemoteWatch msg:
                    {
                        if (_watched.TryGetValue(msg.Watcher.Id, out var pidSet))
                        {
                            pidSet.Add(msg.Watchee);
                        }
                        else
                        {
                            _watched[msg.Watcher.Id] = new HashSet<PID> { msg.Watchee };
                        }

                        var w = new Watch(msg.Watcher);
                        _remote.SendMessage(msg.Watchee, w, -1);
                        break;
                    }
                case Stopped _:
                    {
                        Logger.LogDebug("Stopped EndpointWatcher at {Address}", _address);
                        break;
                    }
            }

            return Actor.Done;
        }

        private Task TerminatedAsync(IContext context)
        {
            switch (context.Message)
            {
                case RemoteWatch msg:
                    {
                        msg.Watcher.SendSystemMessage(
                            _system,
                            new Terminated
                            {
                                AddressTerminated = true,
                                Who = msg.Watchee
                            }
                        );
                        break;
                    }
                case EndpointConnectedEvent _:
                    {
                        Logger.LogDebug("Handle restart address {Address}", _address);
                        _behavior.Become(ConnectedAsync);
                        break;
                    }
                case RemoteUnwatch _:
                case EndpointTerminatedEvent _:
                case RemoteTerminate _:
                    {
                        //pass 
                        break;
                    }
            }

            return Actor.Done;
        }
    }
}