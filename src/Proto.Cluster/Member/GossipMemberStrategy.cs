// -----------------------------------------------------------------------
// <copyright file = "GossipMemberStrategy.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Proto.Cluster.Gossip;

namespace Proto.Cluster;

//TODO clear _actorCounts for members not in memberlist
public class GossipMemberStrategy : IMemberStrategy
{
    private readonly Cluster _cluster;
    private readonly string _kind;
    private readonly ConcurrentDictionary<string, long> _actorCounts = new();
    private readonly RoundRobinMemberSelector _rr;
    private readonly ConcurrentDictionary<string, Member> _members = new();

    public GossipMemberStrategy(Cluster cluster, string kind)
    {
        _rr = new RoundRobinMemberSelector(this);
        _cluster = cluster;
        _kind = kind;
        SubscribeToGossipEvents();
    }

    private void SubscribeToGossipEvents() => _cluster.System.EventStream.Subscribe<GossipUpdate>(x => x.Key == GossipKeys.Heartbeat, x => {
            var heartbeat = x.Value.Unpack<MemberHeartbeat>();
            var actorCount = heartbeat.ActorStatistics.ActorCount.Where(m => m.Key == _kind).Select(m => m.Value).FirstOrDefault();
            _actorCounts[x.MemberId] = actorCount;
        }
    );

    private string GetLeastActorsMember() => _actorCounts
        .Where(kvp => _members.ContainsKey(kvp.Key))
        .OrderBy(kvp => kvp.Value)
        .FirstOrDefault().Key;

    public ImmutableList<Member> GetAllMembers() => _members.Values.ToImmutableList();

    public void AddMember(Member member) => _members.TryAdd(member.Id, member);

    public void RemoveMember(Member member)
    {
        _actorCounts.TryRemove(member.Id, out _);
        _members.TryRemove(member.Id, out _);
    }

    public Member? GetActivator(string senderAddress)
    {
        var leastActorsMember = GetLeastActorsMember();
        return _members.TryGetValue(leastActorsMember, out var m) ? m : _rr.GetMember();
    }
}