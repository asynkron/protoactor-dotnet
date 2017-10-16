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
    internal class MemberListActor : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<MemberListActor>();

        private readonly Dictionary<string, MemberStatus> _members = new Dictionary<string, MemberStatus>();
        private readonly Dictionary<string, IMemberStrategy> _memberStrategyByKind = new Dictionary<string, IMemberStrategy>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                {
                    _logger.LogDebug("Started MemberListActor");
                    break;
                }
                case MembersByKindRequest msg:
                {
                    context.Respond(_memberStrategyByKind.TryGetValue(msg.Kind, out var memberStrategy)
                                        ? new MembersResponse(memberStrategy.GetAllMembers().FindAll(m => !msg.OnlyAlive || (msg.OnlyAlive && m.Alive)).Select(m => m.Address).ToArray())
                                        : new MembersResponse(new string[0]));
                    break;
                }
                case PartitionMemberRequest msg:
                {
                    context.Respond(_memberStrategyByKind.TryGetValue(msg.Kind, out var memberStrategy)
                                        ? new MemberResponse(memberStrategy.GetPartition(msg.Name))
                                        : new MemberResponse(""));
                    break;
                }
                case ActivatorMemberRequest msg:
                {
                    context.Respond(_memberStrategyByKind.TryGetValue(msg.Kind, out var memberStrategy)
                                        ? new MemberResponse(memberStrategy.GetActivator())
                                        : new MemberResponse(""));
                    break;
                }
                case ClusterTopologyEvent msg:
                {
                    //get all new members address sets
                    var newMembersAddress = new HashSet<string>();
                    foreach (var status in msg.Statuses)
                    {
                        newMembersAddress.Add(status.Address);
                    }

                    //remove old ones whose address not exist in new address sets
                    //_members.ToList() duplicates _members, allow _members to be modified in Notify
                    foreach (var (address, old) in _members.ToList())
                    {
                        if (!newMembersAddress.Contains(address))
                        {
                            UpdateAndNotify(null, old);
                        }
                    }

                    //find all the entries that exist in the new set
                    foreach (var @new in msg.Statuses)
                    {
                        _members.TryGetValue(@new.Address, out var old);
                        _members[@new.Address] = @new;
                        UpdateAndNotify(@new, old);
                    }
                    break;
                }
            }
            return Actor.Done;
        }

        private void UpdateAndNotify(MemberStatus @new, MemberStatus old)
        {
            if (@new == null && old == null)
            {
                return; //ignore
            }

            if (@new == null)
            {
                //update MemberStrategy
                foreach (var k in old.Kinds)
                {
                    if (_memberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.RemoveMember(old);
                        if (ms.GetAllMembers().Count == 0)
                            _memberStrategyByKind.Remove(k);
                    }
                }
                
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
                //update MemberStrategy
                foreach (var k in @new.Kinds)
                {
                    if (!_memberStrategyByKind.ContainsKey(k))
                        _memberStrategyByKind[k] = Cluster.cfg.MemberStrategyBuilder(k);
                    _memberStrategyByKind[k].AddMember(@new);
                }

                //notify joined
                var joined = new MemberJoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(joined);
                return;
            }

            //update MemberStrategy
            if (@new.Alive != old.Alive || @new.MemberId != old.MemberId || @new.StatusValue != null && !@new.StatusValue.IsSame(old.StatusValue))
            {
                foreach (var k in @new.Kinds)
                {
                    if (_memberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.UpdateMember(@new);
                    }
                }
            }

            //notify changes
            if (@new.MemberId != old.MemberId)
            {
                var rejoined = new MemberRejoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(rejoined);
                return;
            }
        }
    }
}
