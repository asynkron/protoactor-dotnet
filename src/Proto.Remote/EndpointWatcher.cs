// -----------------------------------------------------------------------
//   <copyright file="EndpointWatcher.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class EndpointWatcher : IActor
    {
        private readonly Behavior _behavior;
        private readonly Dictionary<string, PID> _watched = new Dictionary<string, PID>();
        private string _address; //for logging

        public EndpointWatcher(string address)
        {
            _address = address;
            _behavior = new Behavior(ConnectedAsync);
        }

        public Task ReceiveAsync(IContext context)
        {
            return _behavior.ReceiveAsync(context);
        }

        public Task ConnectedAsync(IContext context)
        {
            switch (context.Message)
            {
                case RemoteTerminate msg:
                {
                    _watched.Remove(msg.Watcher.Id);
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
                    foreach (var (id, pid) in _watched)
                    {
                        //create a terminated event for the Watched actor
                        var t = new Terminated
                        {
                            Who = pid,
                            AddressTerminated = true
                        };
                        var watcher = new PID(ProcessRegistry.Instance.Address, id);
                        //send the address Terminated event to the Watcher
                        watcher.SendSystemMessage(t);
                    }

                    _behavior.Become(TerminatedAsync);
                    break;
                }
                case RemoteUnwatch msg:
                {
                    _watched[msg.Watcher.Id] = null;

                    var w = new Unwatch(msg.Watcher);
                    Remote.SendMessage(msg.Watchee, w,-1);
                    break;
                }
                case RemoteWatch msg:
                {
                    _watched[msg.Watcher.Id] = msg.Watchee;

                    var w = new Watch(msg.Watcher);
                    Remote.SendMessage(msg.Watchee, w, -1);
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
                case RemoteUnwatch _:
                case EndpointTerminatedEvent _:
                case RemoteTerminate _:
                {
                    //pass 
                    break;
                }
                default:
                {
                    //TODO: log error
                    break;
                }
            }
            return Actor.Done;
        }
    }
}