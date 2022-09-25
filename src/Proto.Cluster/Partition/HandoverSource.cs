// -----------------------------------------------------------------------
// <copyright file="HandoverSource.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Proto.Cluster.Partition;

internal static class HandoverSource
{
    /// <summary>
    ///     Single member handover
    /// </summary>
    /// <param name="topologyHash"></param>
    /// <param name="chunkSize"></param>
    /// <param name="activations"></param>
    /// <param name="ownedNow"></param>
    /// <param name="ownedBefore"></param>
    /// <returns></returns>
    public static IEnumerable<IdentityHandover> CreateHandovers(
        ulong topologyHash,
        int chunkSize,
        IEnumerable<KeyValuePair<ClusterIdentity, PID>> activations,
        Func<ClusterIdentity, bool> ownedNow,
        Func<ClusterIdentity, bool>? ownedBefore = null
    )
    {
        var memberHandover = new MemberHandover(topologyHash, chunkSize);

        foreach (var (clusterIdentity, pid) in activations)
        {
            if (ownedNow(clusterIdentity))
            {
                if (ownedBefore?.Invoke(clusterIdentity) == true)
                {
                    // Member should already have this activation
                    memberHandover.AddSkipped();
                }
                else if (memberHandover.Add(clusterIdentity, pid, out var message))
                {
                    yield return message;
                }
            }
        }

        yield return memberHandover.GetFinal();
    }

    /// <summary>
    ///     All members handover
    /// </summary>
    /// <param name="topology"></param>
    /// <param name="chunkSize"></param>
    /// <param name="activations"></param>
    /// <param name="getCurrentOwner"></param>
    /// <param name="getPreviousOwner"></param>
    /// <returns></returns>
    public static IEnumerable<(string address, IdentityHandover message)> CreateHandovers(
        ClusterTopology topology,
        int chunkSize,
        IEnumerable<KeyValuePair<ClusterIdentity, PID>> activations,
        Func<ClusterIdentity, string> getCurrentOwner,
        Func<ClusterIdentity, string>? getPreviousOwner = null
    )
    {
        var members = topology.Members.ToDictionary(member => member.Address,
            _ => new MemberHandover(topology.TopologyHash, chunkSize));

        foreach (var (clusterIdentity, pid) in activations)
        {
            var currentOwner = getCurrentOwner(clusterIdentity);
            var memberHandover = members[currentOwner];

            {
                if (getPreviousOwner?.Invoke(clusterIdentity).Equals(currentOwner, StringComparison.Ordinal) == true)
                {
                    // Member should already have this activation
                    memberHandover.AddSkipped();
                }
                else if (memberHandover.Add(clusterIdentity, pid, out var message))
                {
                    yield return (currentOwner, message);
                }
            }
        }

        foreach (var memberHandover in members)
        {
            yield return (memberHandover.Key, memberHandover.Value.GetFinal());
        }
    }

    private class MemberHandover
    {
        private readonly int _chunkSize;
        private readonly ulong _topologyHash;
        private int _chunkId;

        private IdentityHandover _currentMessage;
        private int _sent;
        private int _skipped;

        public MemberHandover(ulong topologyHash, int chunkSize)
        {
            _chunkSize = chunkSize;
            _topologyHash = topologyHash;

            _currentMessage = new IdentityHandover
            {
                ChunkId = ++_chunkId,
                TopologyHash = topologyHash
            };
        }

        public bool Add(ClusterIdentity id, PID activation, [NotNullWhen(true)] out IdentityHandover? message)
        {
            _sent++;
            var flush = _currentMessage.Actors.Count == _chunkSize;

            if (flush)
            {
                message = _currentMessage;

                _currentMessage = new IdentityHandover
                {
                    ChunkId = ++_chunkId,
                    TopologyHash = _topologyHash
                };
            }
            else
            {
                message = default;
            }

            _currentMessage.Actors.Add(new Activation
                {
                    ClusterIdentity = id,
                    Pid = activation
                }
            );

            return flush;
        }

        /// <summary>
        ///     When sending delta only, includes the number of skipped messages (already present on the owner from last rebalance)
        /// </summary>
        public void AddSkipped() => _skipped++;

        public IdentityHandover GetFinal()
        {
            _currentMessage.Final = true;
            _currentMessage.Skipped = _skipped;
            _currentMessage.Sent = _sent;

            return _currentMessage;
        }
    }
}