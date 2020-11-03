// ReSharper disable once CheckNamespace
namespace Proto.Cluster
{
    using System;

    public sealed partial class ClusterIdentity
    {
        public string ToShortString() => $"{Kind}/{Identity}";
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
}