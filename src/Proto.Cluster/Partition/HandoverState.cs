// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityHandover.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Proto.Cluster.Partition
{
    internal class HandoverState
    {
        public ulong TopologyHash { get; }
        private readonly Dictionary<string, MemberState> _incompleteHandovers = new();

        public HandoverState(ClusterTopology topology)
        {
            TopologyHash = topology.TopologyHash;

            foreach (var member in topology.Members)
            {
                _incompleteHandovers.Add(member.Address, new MemberState());
            }
        }

        public bool IsFinalHandoverMessage(PID sender, IdentityHandover message)
        {
            if (message.TopologyHash != TopologyHash)
            {
                // This does not belong to the current topology
                return false;
            }

            if (_incompleteHandovers.TryGetValue(sender.Address, out var incompleteMember))
            {
                if (incompleteMember.HasAllChunks(message.ChunkId, message.Final))
                {
                    _incompleteHandovers.Remove(sender.Address);
                }
            }

            return _incompleteHandovers.Count == 0;
        }

        private class MemberState
        {
            private uint? _finalChunk;
            private HashSet<uint>? _receivedChunks;

            public bool HasAllChunks(uint chunkId, bool isFinalChunk)
            {
                if (isFinalChunk)
                {
                    if (chunkId == 1) return true;

                    _finalChunk = chunkId;
                }

                if (_receivedChunks is null)
                {
                    _receivedChunks = new HashSet<uint> {chunkId};
                }
                else
                {
                    _receivedChunks.Add(chunkId);
                }

                return _finalChunk?.Equals((uint) _receivedChunks.Count) == true;
            }
        }
    }
}