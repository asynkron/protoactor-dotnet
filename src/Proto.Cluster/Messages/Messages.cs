// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
using System.Linq;
using Google.Protobuf;

namespace Proto.Cluster
{
    public sealed partial class ClusterIdentity : ICustomDiagnosticMessage
    {
        public string ToDiagnosticString() => $"{Kind}/{Identity}";
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
        public uint GetMembershipHashCode()
        {
            var members = Members;
            var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
            var key = string.Join("", x);
            var hash = MurmurHash2.Hash(key);
            return hash;
        }
    }
}