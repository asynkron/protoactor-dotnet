// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;

// ReSharper disable once CheckNamespace
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

        internal PID? CachedPid { get; set; }
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
        public uint GetMembershipHashCode() => Member.TopologyHash(Members);
        
        /// <summary>
        /// Topology based logic (IE partition based) can use this token to cancel any work when this topology is no longer valid
        /// </summary>
        public CancellationToken? TopologyValidityToken { get; init; }
    }

    public partial class Member
    {
        public static uint TopologyHash(IEnumerable<Member> members)
        {
            var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
            var key = string.Concat(x);
            var hash = MurmurHash2.Hash(key);
            return hash;
        }
    }
}