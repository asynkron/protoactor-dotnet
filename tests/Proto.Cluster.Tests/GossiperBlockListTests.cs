using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;
using static Proto.TestKit.TestKit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossiperBlockListTests
{
    private sealed class TestMemberList : IMemberList
    {
        private readonly ImmutableDictionary<string, Member> _members;

        public TestMemberList(Member self, params Member[] members)
        {
            Self = self;
            _members = members.ToImmutableDictionary(m => m.Id);
        }

        public Member Self { get; }

        public bool ContainsMemberId(string memberId) => _members.ContainsKey(memberId);

        public bool TryGetMember(string memberId, out Member? value) => _members.TryGetValue(memberId, out value);

        public ImmutableHashSet<string> GetMembers() => _members.Keys.ToImmutableHashSet();
    }

    private static RemoteConfig ConfigureRemote() =>
        RemoteConfig.BindToLocalhost().WithProtoMessages(GossipContractsReflection.Descriptor);

    private static Gossiper CreateGossiper(
        ActorSystem system,
        IMemberList memberList
    )
    {
        var options = new GossiperOptions(
            system.Root,
            memberList,
            system.Remote().BlockList,
            system.EventStream,
            system.Id,
            Task.CompletedTask,
            CancellationToken.None,
            () => new ActorStatistics(),
            GossipFanout: 1,
            GossipMaxSend: 1,
            GossipInterval: TimeSpan.FromMilliseconds(200),
            GossipRequestTimeout: TimeSpan.FromSeconds(2),
            GossipDebugLogging: false,
            HeartbeatExpiration: TimeSpan.Zero,
            HeartbeatExpirationHandler: () => Task.CompletedTask
        );

        return new Gossiper(options);
    }

    [Fact]
    public async Task GossipLoopBlocksGracefullyLeftMembers()
    {
        var remote1 = new GrpcNetRemote(new ActorSystem(), ConfigureRemote());
        var remote2 = new GrpcNetRemote(new ActorSystem(), ConfigureRemote());
        await remote1.StartAsync();
        await remote2.StartAsync();

        var system1 = remote1.System;
        var system2 = remote2.System;
        var (host1, port1) = system1.GetAddress();
        var (host2, port2) = system2.GetAddress();
        var member1 = new Member { Id = system1.Id, Host = host1, Port = port1 };
        var member2 = new Member { Id = system2.Id, Host = host2, Port = port2 };

        var memberList1 = new TestMemberList(member1, member1, member2);
        var memberList2 = new TestMemberList(member2, member1, member2);

        var gossiper1 = CreateGossiper(system1, memberList1);
        var gossiper2 = CreateGossiper(system2, memberList2);
        await gossiper1.StartGossipActorAsync();
        await gossiper2.StartGossipActorAsync();
        await gossiper1.StartgossipLoopAsync();

        var topology = new ClusterTopology();
        topology.Members.Add(member1);
        topology.Members.Add(member2);
        system1.EventStream.Publish(topology);
        system2.EventStream.Publish(topology);

        await gossiper2.SetStateAsync(GossipKeys.GracefullyLeft, new Empty());
        var pid2 = PID.FromAddress(system2.Address, Gossiper.GossipActorName);
        await system2.Root.RequestAsync<SendGossipStateResponse>(pid2, new SendGossipStateRequest(),
            CancellationTokens.FromSeconds(5));

        await AwaitConditionAsync(
            () => system1.Remote().BlockList.BlockedMembers.Contains(member2.Id),
            TimeSpan.FromSeconds(5));

        await gossiper1.ShutdownAsync();
        await gossiper2.ShutdownAsync();
        await Task.WhenAll(remote1.ShutdownAsync(), remote2.ShutdownAsync());
    }

}
