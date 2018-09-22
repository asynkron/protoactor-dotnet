// -----------------------------------------------------------------------
//   <copyright file="EndpointWatcher.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointWatcher : IActor
    {
        private readonly Behavior _behavior;
        private readonly ILogger _logger = Log.CreateLogger<EndpointWatcher>();
        private readonly Dictionary<string, HashSet<PID>> _watched = new Dictionary<string, HashSet<PID>>();
        private readonly string _address; //for logging

        public EndpointWatcher(string address)
        {
            _address = address;
            _behavior = new Behavior(ConnectedAsync);
        }

        public Task ReceiveAsync(IContext context) => _behavior.ReceiveAsync(context);

        public Task ConnectedAsync(IContext context)
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
                    var t = new Terminated
                    {
                        Who = msg.Watchee
                    };
                    //send the address Terminated event to the Watcher
                    msg.Watcher.SendSystemMessage(t);
                    break;
                }
                case EndpointTerminatedEvent _:
                {
                    _logger.LogDebug($"Handle terminated address {_address}");

                    foreach (var (id, pidSet) in _watched)
                    {
                        var watcherPid = new PID(ProcessRegistry.Instance.Address, id);
                        var watcherRef = ProcessRegistry.Instance.Get(watcherPid);
                        if (watcherRef != DeadLetterProcess.Instance)
                        {
                            foreach (var pid in pidSet)
                            {
                                //create a terminated event for the Watched actor
                                var t = new Terminated
                                {
                                    Who = pid,
                                    AddressTerminated = true
                                };

                                //send the address Terminated event to the Watcher
                                watcherPid.SendSystemMessage(t);
                            }
                        }
                    }

                    _watched.Clear();
                    _behavior.Become(TerminatedAsync);
                    context.Self.Stop();
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
                    Remote.SendMessage(msg.Watchee, w, -1);
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
                        _watched[msg.Watcher.Id] = new HashSet<PID> {msg.Watchee};
                    }

                    var w = new Watch(msg.Watcher);
                    Remote.SendMessage(msg.Watchee, w, -1);
                    break;
                }
                case Stopped _:
                {
                    _logger.LogDebug($"Stopped EndpointWatcher at {_address}");
                    break;
                }
            }

            return Actor.Done;
        }

        public Task TerminatedAsync(IContext context)
        {
            switch (context.Message)
            {
                case RemoteWatch msg:
                {
                    msg.Watcher.SendSystemMessage(new Terminated
                    {
                        AddressTerminated = true,
                        Who = msg.Watchee
                    });
                    break;
                }
                case EndpointConnectedEvent _:
                {
                    _logger.LogDebug($"Handle restart address {_address}");
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