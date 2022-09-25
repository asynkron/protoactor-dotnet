// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proto.Cluster.PartitionActivator;

/// <summary>
///     Optimized version of the Rendezvous algorithm.
///     Instead of calculating Hash each time for Key+Node combination,
///     it simply precalculates Node hash, then calculates Key hash and
///     does a basic hash combination to determine a combined hash. Uses
///     combined hash to determine the owner.
///     Identity distribution over nodes is just as good as Rendezvous,
///     while being 50x faster at 1000 nodes, 25x faster at 100 nodes, and 6x faster at 10 nodes.
///     Also, it does minimal allocations in comparison to Rendezvous, identical to MemberRing.
///     WARNING: Any modifications in this class could cause old version cluster
///     members to be incompatible with the new version cluster members, and it
///     would result in duplicate parallel activations.
/// </summary>
public class RendezvousFast
{
    private readonly MemberData[] _members;

    public RendezvousFast(IEnumerable<Member> members)
    {
        _members = members
            .OrderBy(m => m.Address)
            .Select(x => new MemberData(x))
            .ToArray();
    }

    public string GetOwnerMemberByIdentity(string identity)
    {
        switch (_members.Length)
        {
            case 0:
                return "";
            case 1:
                return _members[0].Info.Address;
        }

        var keyBytes = Encoding.UTF8.GetBytes(identity);
        var keyHash = MurmurHash2.Hash(keyBytes);

        uint maxScore = 0;
        Member? maxNode = null;

        foreach (var member in _members)
        {
            var score = CombineHashes(member.Hash, keyHash);

            if (score <= maxScore)
            {
                continue;
            }

            maxScore = score;
            maxNode = member.Info;
        }

        return maxNode?.Address ?? "";
    }

    /// <summary>
    ///     Combines 2 hashes, with a basic XOR.
    ///     Any more complicated hash combination is simply pointless here.
    /// </summary>
    private uint CombineHashes(uint hash1, uint hash2) => hash1 ^ hash2;

    private readonly struct MemberData
    {
        public MemberData(Member member)
        {
            Info = member;
            Hash = MurmurHash2.Hash(Encoding.UTF8.GetBytes(member.Address));
        }

        public Member Info { get; }
        public uint Hash { get; }
    }
}