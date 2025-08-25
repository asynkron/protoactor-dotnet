using System.Collections.Immutable;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Remote;

namespace Proto.Cluster.Gossip;

public partial class GossipRequest : IRemotePriorityMessage
{
    
}

public partial class GossipResponse : IRemotePriorityMessage
{

}

public record GossipUpdate(string MemberId, string Key, Any Value, long SequenceNumber);

public record GetGossipStateRequest(string Key);

public record GetGossipStateResponse(ImmutableDictionary<string, Any> State);

public record GetGossipStateEntryRequest(string Key);

public record GetGossipStateEntryResponse(ImmutableDictionary<string, GossipKeyValue> State);

public record SetGossipStateKey(string Key, IMessage Value);

public record SetGossipStateResponse;

public record SendGossipStateRequest;

public record SendGossipStateResponse;

public record AddConsensusCheck(ConsensusCheck Check, CancellationToken Token);

public record GetGossipStateSnapshot;

