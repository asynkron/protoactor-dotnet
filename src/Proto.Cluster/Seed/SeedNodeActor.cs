// -----------------------------------------------------------------------
// <copyright file = "SeedNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Seed
{
    public class SeedNodeActor : IActor
    {
        private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    Console.WriteLine("Started seed node actor " + context.Self);
                    var (host, port) = context.System.GetAddress();
                    var selfMember = new Member()
                    {
                        Id = context.System.Id,
                        Host = host,
                        Port = port,
                        Kinds = { context.Cluster().GetClusterKinds() }
                    };
                    SetMember(selfMember);
                    context.System.EventStream.Subscribe<GossipUpdate>(context.System.Root, context.Self);
                    UpdateMemberList(context);
                    break;
                case GossipUpdate {Key: "topology"} update: {
                    var topology = update.Value.Unpack<ClusterTopology>();

                    foreach (var m in topology.Members)
                    {
                        SetMember(m);
                    }

                    UpdateMemberList(context);
                    break;
                }
                case JoinRequest join: {
                    SetMember(join.Joiner);
                    
                    UpdateMemberList(context);
                    context.Respond(new JoinResponse());
                    break;
                }
            }

            return Task.CompletedTask;
        }

        private void SetMember(Member member)
        {
            if (!_members.ContainsKey(member.Id))
            {
                _members = _members.SetItem(member.Id, member);
            }
        }

        private void UpdateMemberList(IContext context)
        {
            var members = _members.Values.ToList();
            context.Cluster().MemberList.UpdateClusterTopology(members);
        }

        public static Props Props() => Proto.Props.FromProducer(() => new SeedNodeActor());
    }
}