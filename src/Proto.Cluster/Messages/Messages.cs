// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;

namespace Proto.Cluster
{
    public sealed partial class ClusterIdentity : ICustomDiagnosticMessage
    {
        public string ToDiagnosticString() => $"{Kind}/{Identity}";

        public static ClusterIdentity Create(string identity, string kind) => new()
        {
            Identity = identity,
            Kind = kind
        };
    }

    public sealed partial class ActivationRequest
    {
        public string Kind => ClusterIdentity.Kind;
        public string Identity => ClusterIdentity.Identity;
    }

    public sealed partial class ActivationTerminated
    {
        public string Kind => ClusterIdentity.Kind;
        public string Identity => ClusterIdentity.Identity;
    }

    public sealed partial class Activation
    {
        public string Kind => ClusterIdentity.Kind;
        public string Identity => ClusterIdentity.Identity;
    }

    public record Tick;

    public partial class ClusterTopology
    {
        //this ignores joined and left members, only the actual members are relevant
        public uint GetMembershipHashCode() => Member.GetMembershipHashCode(Members);
    }

    public partial class Member
    {
        public static uint GetMembershipHashCode(IEnumerable<Member> members)
        {
            var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
            var key = string.Join("", x);
            var hash = MurmurHash2.Hash(key);
            return hash;
        }
    }

    public partial class GossipKeyValue
    {
        public string GlobalKey => $"{MemberId}.{Key}";
    }

    public partial class GossipState
    {
        public (bool dirty, GossipState state) MergeWith(GossipState other)
        {
            var state =
                Entries
                    .ToDictionary(
                        kvp => kvp.GlobalKey, 
                        kvp => kvp);

            var dirty = false;
            foreach (var kvp in other.Entries)
            {
                if (state.TryAdd(kvp.GlobalKey, kvp))
                {
                    dirty = true;
                    continue;
                }

                var existing = state[kvp.GlobalKey];

                if (kvp.Version <= existing.Version) continue;

                dirty = true;
                state[kvp.GlobalKey] = kvp;
            }

            var newState = new GossipState
            {
                Entries = {state.Values}
            };

            return (dirty,newState);
        }
    }
}