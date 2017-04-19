// -----------------------------------------------------------------------
//   <copyright file="MemberListActor.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public class MemberListActor : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<MemberListActor>();
        private readonly Dictionary<string, MemberStatus> _members = new Dictionary<string, MemberStatus>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started MemberListActor");
                    break;
                case MemberByKindRequest msg:
                {
                    var members = (from kvp in _members
                                   let address = kvp.Key
                                   let member = kvp.Value
                                   where member.Kinds.Contains(msg.Kind)
                                   select address).ToArray();

                    context.Respond(new MemberByKindResponse(members));
                    break;
                }
                case ClusterTopologyEvent msg:
                {
                    var tmp = new Dictionary<string, MemberStatus>();
                    foreach (var status in msg.Statuses)
                    {
                        tmp[status.Address] = status;
                    }

                    foreach (var (address, old) in _members)
                    {
                        if (!tmp.TryGetValue(address, out var _))
                        {
                            Notify(null, old);
                        }
                    }

                    foreach (var ( address, @new) in tmp)
                    {
                        if (!_members.TryGetValue(address, out var _))
                        {
                            _members[address] = @new;
                            Notify(@new, null);
                        }
                    }
                    break;
                }
            }
            return Actor.Done;
        }

        private void Notify(MemberStatus @new, MemberStatus old)
        {
            if (@new == null && old == null)
            {
                return; //ignore
            }

            if (@new == null)
            {
                //notify left
                var left = new MemberLeftEvent(old.Host, old.Port, old.Kinds);
                Actor.EventStream.Publish(left);
                _members.Remove(old.Address);
                var endpointTerminated = new EndpointTerminatedEvent
                                         {
                                             Address = old.Address
                                         };
                Actor.EventStream.Publish(endpointTerminated);

                return;
            }

            if (old == null)
            {
                //notify joined
                var joined = new MemberJoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(joined);
                return;
            }

            if (@new.MemberId != old.MemberId)
            {
                var rejoined = new MemberRejoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(rejoined);
                return;
            }

            if (old.Alive && !@new.Alive)
            {
                var unavailable = new MemberUnavailableEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(unavailable);
                return;
            }

            if (@new.Alive && !old.Alive)
            {
                var available = new MemberAvailableEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(available);
            }
        }
    }
}