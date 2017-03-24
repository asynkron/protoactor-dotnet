// -----------------------------------------------------------------------
//   <copyright file="DeadLetter.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster
{
    public class MemberListActor : IActor
    {
        private readonly Dictionary<string, MemberStatus> _members = new Dictionary<string, MemberStatus>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterTopologyEvent msg:

                    var tmp = new Dictionary<string, MemberStatus>();
                    foreach (var status in msg.Statuses)
                    {
                        var address = status.Address;
                        tmp[address] = status;
                    }

                    foreach ((var address,var old) in _members)
                    {
                        if (!tmp.TryGetValue(address, out var _))
                        {
                            Notify(address, null, old);
                        }
                    }

                    foreach ((var address, var @new) in tmp)
                    {
                        if (!_members.TryGetValue(address, out var _))
                        {
                            _members[address] = @new;
                            Notify(address, @new, null);
                        }
                    }

                    break;
                default:
                    break;
            }
            return Actor.Done;
        }

        private void Notify(string address, MemberStatus @new, MemberStatus old)
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
                var joined = new MemberRejoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(joined);
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