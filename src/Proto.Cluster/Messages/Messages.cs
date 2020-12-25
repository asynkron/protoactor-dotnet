// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
using System;
using Google.Protobuf;

namespace Proto.Cluster
{
    public sealed partial class ClusterIdentity : ICustomDiagnosticMessage
    {
        [Obsolete("Do not use")]
        public string ToShortString() => $"{Kind}/{Identity}";

#pragma warning disable 618
        public string ToDiagnosticString() => ToShortString();
#pragma warning restore 618
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
}