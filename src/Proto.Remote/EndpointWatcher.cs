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
        private string _address; //for logging

        public EndpointWatcher(string address)
        {
            _address = address;
        }

        public async Task ReceiveAsync(IContext context)
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
                    await msg.Watcher.SendSystemMessageAsync(t);
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
                        await watcher.SendSystemMessageAsync(t);
                    }
                    break;
                }
                case RemoteUnwatch msg:
                {
                    _watched[msg.Watcher.Id] = null;

                    var w = new Unwatch(msg.Watcher);
                    await RemoteProcess.SendRemoteMessageAsync(msg.Watchee, w);
                    break;
                }
                case RemoteWatch msg:
                {
                    _watched[msg.Watcher.Id] = msg.Watchee;

                    var w = new Watch(msg.Watcher);
                    await RemoteProcess.SendRemoteMessageAsync(msg.Watchee, w);
                    break;
                }

                default:
                    break;
            }
        }
    }
}