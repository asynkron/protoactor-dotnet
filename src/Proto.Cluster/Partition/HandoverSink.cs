﻿// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityHandover.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Proto.Cluster.Partition
{
    class HandoverSink
    {
        private readonly Action<IdentityHandover> _process;
        private readonly Action<IdentityHandover>? _onDuplicate;
        public ulong TopologyHash { get; }
        private readonly Dictionary<string, MemberHandoverSink> _memberSinks = new();
        private readonly List<MemberHandoverStats> _completedHandovers = new();

        public IEnumerable<MemberHandoverStats> CompletedHandovers => _completedHandovers;
        public bool IsComplete => _memberSinks.Count == _completedHandovers.Count;

        /// <summary>
        /// This class is responsible for keeping track of incoming identity handover messages.
        /// The requests from other members may be chunked, and it is required that all chunks are received before
        /// we can complete the rebalance and return do normal mode.
        /// </summary>
        /// <param name="topology"></param>
        /// <param name="process"></param>
        /// <param name="onDuplicate"></param>
        public HandoverSink(ClusterTopology topology, Action<IdentityHandover> process, Action<IdentityHandover>? onDuplicate = null)
        {
            _process = process;
            _onDuplicate = onDuplicate;
            TopologyHash = topology.TopologyHash;

            foreach (var member in topology.Members)
            {
                _memberSinks.Add(member.Address, new MemberHandoverSink(member.Address, process, onDuplicate));
            }
        }

        public bool Receive(string address, IdentityHandover message)
        {
            if (message.TopologyHash != TopologyHash)
            {
                // This does not belong to the current topology
                return false;
            }

            if (_memberSinks.TryGetValue(address, out var sink))
            {
                if (sink.Receive(message))
                {
                    _completedHandovers.Add(new MemberHandoverStats(sink.Address, sink.SentActivations, sink.SkippedActivations));
                }
            }

            return IsComplete;
        }

        /// <summary>
        /// Reset this members handover state, if we need to retry this member in isolation
        /// </summary>
        /// <param name="address"></param>
        public void ResetMember(string address) => _memberSinks[address] = new MemberHandoverSink(address, _process, _onDuplicate);

        public record MemberHandoverStats(string Address, int SentActivations, int SkippedActivations)
        {
            public int TotalActivations => SentActivations + SkippedActivations;
        }
    }

    class MemberHandoverSink
    {
        private readonly IndexSet _receivedChunks = new();
        private readonly Action<IdentityHandover> _process;
        private readonly Action<IdentityHandover>? _onDuplicate;

        private int? _finalChunk;

        public int SentActivations { get; private set; }
        public string Address { get; }
        public int SkippedActivations { get; private set; }
        public bool IsComplete => _finalChunk.HasValue && _receivedChunks.IsCompleteSet;

        public MemberHandoverSink(string address, Action<IdentityHandover> process, Action<IdentityHandover>? onDuplicate = null)
        {
            Address = address;
            _process = process;
            _onDuplicate = onDuplicate;
        }

        /// <summary>
        /// Receive message, process it if it is not seen before or deduplicate is disabled
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if this message completed the handover, false otherwise. If it was previously completed, it will still return false if there was a duplicate message</returns>
        public bool Receive(IdentityHandover message)
        {
            if (message.Final)
            {
                SentActivations = message.Sent;
                SkippedActivations = message.Skipped;
                _finalChunk = message.ChunkId;
            }

            var newChunk = _receivedChunks.TryAddIndex(message.ChunkId);

            if (newChunk)
            {
                _process(message);
                return IsComplete;
            }
            else
            {
                _onDuplicate?.Invoke(message);
                return false;
            }
        }
    }
}