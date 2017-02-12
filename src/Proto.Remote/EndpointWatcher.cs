// -----------------------------------------------------------------------
//  <copyright file="EndpointWatcher.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class EndpointWatcher : IActor
    {
        private readonly Dictionary<string, PID> _watched = new Dictionary<string, PID>();
        private readonly Dictionary<string, PID> _watcher = new Dictionary<string, PID>();
        private string _address; //for logging

        public EndpointWatcher(string address)
        {
            _address = address;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case RemoteTerminate msg:
                {
                    _watched.Remove(msg.Watcher.Id);
                    _watcher.Remove(msg.Watchee.Id);
                    //create a terminated event for the Watched actor
                    var t = new Terminated
                    {
                        Who = msg.Watchee,
                        AddressTerminated = true
                    };
                    //send the address Terminated event to the Watcher
                    msg.Watcher.SendSystemMessage(t);
                    break;
                }
                case EndpointTerminatedEvent _:
                {
                    foreach (var kvp in _watched)
                    {
                        var id = kvp.Key;
                        var pid = kvp.Value;

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
                    break;
                }
                case RemoteUnwatch msg:
                {
                    _watched[msg.Watcher.Id] = null;
                    _watcher[msg.Watchee.Id] = null;

                    var w = new Unwatch(msg.Watcher);
                    msg.Watchee.SendSystemMessage(w);

                    break;
                }
                case RemoteWatch msg:
                {
                    _watched[msg.Watcher.Id] = msg.Watchee;
                    _watcher[msg.Watchee.Id] = msg.Watcher;

                    var w = new Watch(msg.Watcher);
                    msg.Watchee.SendSystemMessage(w);

                    break;
                }

                default:
                    break;
            }
            return Actor.Done;
        }
    }
}