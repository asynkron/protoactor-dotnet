// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Proto.Cluster.Gossip
{
    public record GossipUpdate(string MemberId, string Key, Any Value, long SequenceNumber);

    public record Gossip(GossipState State);
    public record GetGossipStateRequest(string Key);

    public record GetGossipStateResponse(ImmutableDictionary<string,Any> State);

    public record SetGossipStateKey(string Key, IMessage Value);

    public record SendGossipStateRequest;
    public record SendGossipStateResponse;
}