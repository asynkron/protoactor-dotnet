// -----------------------------------------------------------------------
// <copyright file = "SeedNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Seed
{
    public class SeedNodeActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<SeedNodeActor>(); 
        private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started                                        => OnStarted(context),
            GossipUpdate {Key: GossipKeys.Topology} update => OnTopologyUpdate(context, update),
            JoinRequest request                            => OnJoinRequest(context, request),
            _                                              => Task.CompletedTask
        };

        private Task OnStarted(IContext context)
        {
            Logger.LogInformation("Started SeedNodeActor");
            SetMember(context.Cluster().MemberList.Self);
            context.System.EventStream.Subscribe<GossipUpdate>(context.System.Root, context.Self);
            UpdateMemberList(context);
            return Task.CompletedTask;
        }

        private Task OnJoinRequest(IContext context, JoinRequest request)
        {
            Logger.LogInformation("Got JoinRequest from Member {Member}", request.Joiner);
            SetMember(request.Joiner);
            UpdateMemberList(context);
            context.Respond(new JoinResponse());
            return Task.CompletedTask;
        }

        private Task OnTopologyUpdate(IContext context, GossipUpdate update)
        {
            var topology = update.Value.Unpack<ClusterTopology>();

            foreach (var m in topology.Members)
            {
                SetMember(m);
            }

            UpdateMemberList(context);
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